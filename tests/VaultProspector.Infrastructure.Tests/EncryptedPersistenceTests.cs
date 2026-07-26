using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.Data.Sqlite;
using VaultProspector.Application;
using VaultProspector.Domain;
using VaultProspector.Infrastructure;

namespace VaultProspector.Infrastructure.Tests;

public sealed class EncryptedPersistenceTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"vault-prospector-{Guid.NewGuid():N}");
    private readonly TestKeyProvider _keys = new();
    private readonly FixedClock _clock = new();

    [Fact]
    public async Task SqlCipherDatabaseStoresSearchableMetadataWithoutPlaintextHeader()
    {
        var path = Path.Combine(_directory, "metadata.db");
        var repository = new EncryptedSqliteMetadataRepository(path, _keys);
        await repository.InitializeAsync(TestContext.Current.CancellationToken);
        var identity = new ConnectedIdentity(Guid.NewGuid(), "11111111-1111-1111-1111-111111111111", "account", "user@example.invalid", "Test Identity", "tenant", AuthenticationState.Ready, _clock.UtcNow);
        await repository.UpsertIdentityAsync(identity, TestContext.Current.CancellationToken);
        Assert.Equal(identity.ClientId, (await repository.GetIdentityAsync(identity.Id, TestContext.Current.CancellationToken))?.ClientId);
        var tenant = new TenantAccess(Guid.NewGuid(), identity.Id, "tenant", "Tenant", "Home", _clock.UtcNow, "Available");
        var subscription = new SubscriptionAccess(Guid.NewGuid(), tenant.Id, "subscription", "Subscription", "Enabled", true, _clock.UtcNow);
        var vault = new VaultResource(Guid.NewGuid(), "/resource", "vault-one", "tenant", "subscription", "rg", "eastus", new Dictionary<string, string>(), new Uri("https://vault-one.vault.azure.net/"), _clock.UtcNow);
        var access = new VaultAccess(Guid.NewGuid(), vault.Id, identity.Id, "tenant", "Ready", _clock.UtcNow, null, 0);
        var item = new VaultItem(Guid.NewGuid(), vault.Id, "database-password", VaultObjectType.Secret, true, new Dictionary<string, string> { ["app"] = "orders" }, "text/plain", _clock.UtcNow, _clock.UtcNow, null, "v1", "fingerprint", _clock.UtcNow);
        var recentItem = new VaultItem(Guid.NewGuid(), vault.Id, "recent-token", VaultObjectType.Secret, true, new Dictionary<string, string>(), "text/plain", _clock.UtcNow, _clock.UtcNow, null, "v1", "recent-fingerprint", _clock.UtcNow);
        await repository.ApplyDiscoveryAsync(identity.Id, new DiscoverySnapshot([tenant], [subscription], [vault], [access], [item, recentItem], []), new SyncRun(Guid.NewGuid(), "test", _clock.UtcNow, _clock.UtcNow, SyncStatus.Completed, 1, 2, []), TestContext.Current.CancellationToken);

        var results = await repository.SearchAsync(new SearchRequest("database"), _clock.UtcNow, TestContext.Current.CancellationToken);

        Assert.Single(results);
        Assert.Equal("database-password", results[0].Item.ProviderObjectName);
        Assert.Equal("Ready", results[0].AccessStatus);
        Assert.Equal(tenant.Id, Assert.Single(await repository.GetTenantsAsync(identity.Id, TestContext.Current.CancellationToken)).Id);
        Assert.Equal(2, (await repository.SearchAsync(new SearchRequest(TenantId: "nan", SubscriptionId: "script", VaultName: "one"), _clock.UtcNow, TestContext.Current.CancellationToken)).Count);
        await repository.RecordAccessAsync(recentItem.Id, _clock.UtcNow, TestContext.Current.CancellationToken);
        var recentFirst = await repository.SearchAsync(new SearchRequest(RecentlyAccessedFirst: true), _clock.UtcNow, TestContext.Current.CancellationToken);
        Assert.Equal(recentItem.Id, recentFirst[0].Item.Id);
        var bytes = await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken);
        Assert.False(Encoding.ASCII.GetString(bytes.AsSpan(0, Math.Min(16, bytes.Length))).StartsWith("SQLite format 3", StringComparison.Ordinal));
        Assert.DoesNotContain("database-password", Encoding.UTF8.GetString(bytes), StringComparison.Ordinal);
    }

    [Fact]
    public async Task GovernedMutationAuditIsAppendOnlyAndTamperingFailsStartup()
    {
        var path = Path.Combine(
            _directory,
            "governed-mutation-audit.db");
        using (var repository =
               new EncryptedSqliteMetadataRepository(path, _keys))
        {
            await repository.InitializeAsync(
                TestContext.Current.CancellationToken);
            var audit = CreateGovernedMutationAudit(
                previousHash: string.Empty);
            await repository.RecordGovernedMutationAuditAsync(
                audit,
                TestContext.Current.CancellationToken);

            var latest =
                await repository.GetLatestGovernedMutationAuditAsync(
                    TestContext.Current.CancellationToken);
            Assert.Equal(audit, latest);
        }

        await ExecuteDatabaseCommandAsync(
            path,
            "UPDATE governed_mutation_audit SET safe_message='tampered'");

        using var reopened =
            new EncryptedSqliteMetadataRepository(path, _keys);
        await Assert.ThrowsAsync<LocalDataIntegrityException>(
            () => reopened.InitializeAsync(
                TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SearchSelectsDeterministicPreferredAccessAndHonorsIdentityFilter()
    {
        var path = Path.Combine(_directory, "preferred-access.db");
        using var repository = new EncryptedSqliteMetadataRepository(path, _keys);
        await repository.InitializeAsync(TestContext.Current.CancellationToken);
        var secondary = new ConnectedIdentity(
            Guid.Parse("00000000-0000-0000-0000-000000000001"),
            "11111111-1111-1111-1111-111111111111",
            "secondary-account",
            "secondary@example.invalid",
            "Secondary identity",
            "tenant",
            AuthenticationState.Ready,
            _clock.UtcNow);
        var preferred = new ConnectedIdentity(
            Guid.Parse("00000000-0000-0000-0000-000000000002"),
            "11111111-1111-1111-1111-111111111111",
            "preferred-account",
            "preferred@example.invalid",
            "Preferred identity",
            "tenant",
            AuthenticationState.Ready,
            _clock.UtcNow);
        await repository.UpsertIdentityAsync(
            secondary,
            TestContext.Current.CancellationToken);
        await repository.UpsertIdentityAsync(
            preferred,
            TestContext.Current.CancellationToken);

        var secondaryTenant = new TenantAccess(
            Guid.NewGuid(),
            secondary.Id,
            "tenant",
            "Secondary tenant",
            "Home",
            _clock.UtcNow,
            "Available");
        var preferredTenant = new TenantAccess(
            Guid.NewGuid(),
            preferred.Id,
            "tenant",
            "Preferred tenant",
            "Home",
            _clock.UtcNow,
            "Available");
        var subscription = new SubscriptionAccess(
            Guid.NewGuid(),
            secondaryTenant.Id,
            "subscription",
            "Subscription",
            "Enabled",
            true,
            _clock.UtcNow);
        var vault = new VaultResource(
            Guid.NewGuid(),
            "/resource/preferred-access",
            "preferred-access",
            "tenant",
            "subscription",
            "rg",
            "eastus",
            new Dictionary<string, string>(),
            new Uri("https://preferred-access.vault.azure.net/"),
            _clock.UtcNow);
        var secondaryAccess = new VaultAccess(
            Guid.NewGuid(),
            vault.Id,
            secondary.Id,
            "tenant",
            "Secondary",
            _clock.UtcNow,
            null,
            5);
        var preferredAccess = new VaultAccess(
            Guid.NewGuid(),
            vault.Id,
            preferred.Id,
            "tenant",
            "Preferred",
            _clock.UtcNow,
            null,
            1);
        var item = new VaultItem(
            Guid.NewGuid(),
            vault.Id,
            "preferred-secret",
            VaultObjectType.Secret,
            true,
            new Dictionary<string, string>(),
            null,
            _clock.UtcNow,
            _clock.UtcNow,
            null,
            "v1",
            "preferred-fingerprint",
            _clock.UtcNow);
        await repository.ApplyDiscoveryAsync(
            secondary.Id,
            new DiscoverySnapshot(
                [secondaryTenant, preferredTenant],
                [subscription],
                [vault],
                [secondaryAccess, preferredAccess],
                [item],
                []),
            new SyncRun(
                Guid.NewGuid(),
                "preferred access",
                _clock.UtcNow,
                _clock.UtcNow,
                SyncStatus.Completed,
                1,
                1,
                []),
            TestContext.Current.CancellationToken);

        var defaultResult = Assert.Single(await repository.SearchAsync(
            new SearchRequest("preferred-secret"),
            _clock.UtcNow,
            TestContext.Current.CancellationToken));
        Assert.Equal("Preferred identity", defaultResult.IdentityDisplayName);
        Assert.Equal("Preferred", defaultResult.AccessStatus);

        var filteredResult = Assert.Single(await repository.SearchAsync(
            new SearchRequest("preferred-secret", IdentityId: secondary.Id),
            _clock.UtcNow,
            TestContext.Current.CancellationToken));
        Assert.Equal("Secondary identity", filteredResult.IdentityDisplayName);
        Assert.Equal("Secondary", filteredResult.AccessStatus);

        await repository.UpsertIdentityAsync(
            preferred with { IsEnabled = false },
            TestContext.Current.CancellationToken);
        var enabledFallback = Assert.Single(await repository.SearchAsync(
            new SearchRequest("preferred-secret"),
            _clock.UtcNow,
            TestContext.Current.CancellationToken));
        Assert.Equal("Secondary identity", enabledFallback.IdentityDisplayName);
    }

    [Fact]
    public async Task RawKeyOpenRemainsCompatibleWithLegacyPassphraseDatabase()
    {
        var path = Path.Combine(_directory, "legacy-passphrase.db");
        Directory.CreateDirectory(_directory);
        SQLitePCL.Batteries_V2.Init();
        var key = await _keys.GetOrCreateKeyAsync(
            "metadata-database",
            TestContext.Current.CancellationToken);
        try
        {
            await using var connection = new SqliteConnection(
                new SqliteConnectionStringBuilder
                {
                    DataSource = path,
                    Mode = SqliteOpenMode.ReadWriteCreate,
                    Password = Convert.ToBase64String(key),
                    Pooling = false,
                }.ToString());
            await connection.OpenAsync(TestContext.Current.CancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText =
                "CREATE TABLE legacy_probe(id INTEGER PRIMARY KEY); PRAGMA user_version=0;";
            await command.ExecuteNonQueryAsync(
                TestContext.Current.CancellationToken);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
        }

        using var repository = new EncryptedSqliteMetadataRepository(path, _keys);
        await repository.InitializeAsync(TestContext.Current.CancellationToken);

        Assert.Empty(await repository.GetIdentitiesAsync(
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CyberArkMetadataRemainsProviderSpecificAndAuditSurvivesRemoval()
    {
        var path = Path.Combine(_directory, "cyberark.db");
        var repository = new EncryptedSqliteMetadataRepository(path, _keys);
        await repository.InitializeAsync(TestContext.Current.CancellationToken);
        var profileId = Guid.NewGuid();
        var profile = new CyberArkProfile(
            profileId,
            "Production CyberArk",
            new Uri("https://tenant.id.cyberark.cloud/"),
            new Uri("https://tenant.privilegecloud.cyberark.cloud/"),
            "svc@example.com",
            "VaultProspector",
            CyberArkAuthenticationState.Unknown,
            true,
            _clock.UtcNow,
            null);
        await repository.UpsertCyberArkProfileAsync(
            profile,
            TestContext.Current.CancellationToken);
        var safe = new CyberArkSafe(
            profileId,
            "safe-1",
            "Production",
            "Production systems",
            "\\",
            30,
            5,
            true,
            _clock.UtcNow,
            _clock.UtcNow);
        var account = new CyberArkAccount(
            profileId,
            "account-1",
            safe.Name,
            "database-admin",
            "administrator",
            "db.example.test",
            "WinServerLocal",
            CyberArkSecretType.Password,
            "ready",
            _clock.UtcNow,
            _clock.UtcNow,
            "fingerprint",
            _clock.UtcNow);
        var version = new CyberArkSecretVersion(
            profileId,
            account.AccountId,
            3,
            false,
            _clock.UtcNow,
            "operator");
        var permission = new CyberArkSafePermissionEvidence(
            profileId,
            safe.SafeId,
            profile.ServiceUserName,
            "User",
            true,
            true,
            true,
            true,
            false,
            true,
            false,
            _clock.UtcNow,
            "direct");
        await repository.ApplyCyberArkDiscoveryAsync(
            profileId,
            new CyberArkDiscoverySnapshot(
                [safe],
                [account],
                [version],
                [permission],
                [],
                _clock.UtcNow),
            TestContext.Current.CancellationToken);

        Assert.Equal(
            profile with
            {
                AuthenticationState = CyberArkAuthenticationState.Ready,
                LastValidatedAt = _clock.UtcNow,
            },
            await repository.GetCyberArkProfileAsync(
                profileId,
                TestContext.Current.CancellationToken));
        Assert.Equal(
            safe,
            Assert.Single(await repository.GetCyberArkSafesAsync(
                profileId,
                TestContext.Current.CancellationToken)));
        Assert.Equal(
            account,
            Assert.Single(await repository.SearchCyberArkAccountsAsync(
                profileId,
                "database",
                25,
                TestContext.Current.CancellationToken)));
        Assert.Equal(
            version,
            Assert.Single(await repository.GetCyberArkVersionsAsync(
                profileId,
                account.AccountId,
                TestContext.Current.CancellationToken)));
        Assert.Equal(
            permission,
            await repository.GetCyberArkPermissionAsync(
                profileId,
                safe.SafeId,
                TestContext.Current.CancellationToken));

        var audit = new CyberArkAuditEvent(
            Guid.NewGuid(),
            profileId,
            account.AccountId,
            safe.Name,
            version.VersionId,
            "show",
            CyberArkAuditResult.Succeeded,
            "Synthetic value was returned.",
            _clock.UtcNow);
        await repository.RecordCyberArkAuditAsync(
            audit,
            TestContext.Current.CancellationToken);
        await repository.RemoveCyberArkProfileAsync(
            profileId,
            TestContext.Current.CancellationToken);

        Assert.Empty(await repository.GetCyberArkProfilesAsync(
            TestContext.Current.CancellationToken));
        Assert.Empty(await repository.SearchCyberArkAccountsAsync(
            profileId,
            string.Empty,
            25,
            TestContext.Current.CancellationToken));
        Assert.Equal(
            audit,
            Assert.Single(await repository.GetCyberArkAuditAsync(
                profileId,
                25,
                TestContext.Current.CancellationToken)));
        var bytes = await File.ReadAllBytesAsync(
            path,
            TestContext.Current.CancellationToken);
        Assert.DoesNotContain(
            "database-admin",
            Encoding.UTF8.GetString(bytes),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task CyberArkDiscoveryRejectsCrossProfileDataAtomically()
    {
        var path = Path.Combine(_directory, "cyberark-boundary.db");
        var repository = new EncryptedSqliteMetadataRepository(path, _keys);
        await repository.InitializeAsync(TestContext.Current.CancellationToken);
        var profile = new CyberArkProfile(
            Guid.NewGuid(),
            "Test",
            new Uri("https://tenant.id.cyberark.cloud/"),
            new Uri("https://tenant.privilegecloud.cyberark.cloud/"),
            "svc@example.com",
            "VaultProspector",
            CyberArkAuthenticationState.Ready,
            true,
            _clock.UtcNow,
            _clock.UtcNow);
        await repository.UpsertCyberArkProfileAsync(
            profile,
            TestContext.Current.CancellationToken);
        var mismatchedSafe = new CyberArkSafe(
            Guid.NewGuid(),
            "safe-1",
            "Wrong source",
            string.Empty,
            "\\",
            null,
            null,
            false,
            null,
            null);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => repository.ApplyCyberArkDiscoveryAsync(
                profile.Id,
                new CyberArkDiscoverySnapshot(
                    [mismatchedSafe],
                    [],
                    [],
                    [],
                    [],
                    _clock.UtcNow),
                TestContext.Current.CancellationToken));

        Assert.Empty(await repository.GetCyberArkSafesAsync(
            profile.Id,
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task BrowserMappingsRemainIdentityBoundAndAuditSurvivesMappingRemoval()
    {
        var path = Path.Combine(_directory, "browser-mappings.db");
        var repository = new EncryptedSqliteMetadataRepository(path, _keys);
        await repository.InitializeAsync(TestContext.Current.CancellationToken);
        var identity = TestIdentity("browser-mapping-account");
        await repository.UpsertIdentityAsync(identity, TestContext.Current.CancellationToken);
        var tenant = new TenantAccess(
            Guid.NewGuid(),
            identity.Id,
            "tenant",
            "Tenant",
            "Home",
            _clock.UtcNow,
            "Available");
        var subscription = new SubscriptionAccess(
            Guid.NewGuid(),
            tenant.Id,
            "subscription",
            "Subscription",
            "Enabled",
            true,
            _clock.UtcNow);
        var vault = new VaultResource(
            Guid.NewGuid(),
            "/resource/browser-vault",
            "browser-vault",
            "tenant",
            "subscription",
            "rg",
            "eastus",
            new Dictionary<string, string>(),
            new Uri("https://browser-vault.vault.azure.net/"),
            _clock.UtcNow);
        var access = new VaultAccess(
            Guid.NewGuid(),
            vault.Id,
            identity.Id,
            "tenant",
            "Ready",
            _clock.UtcNow,
            null,
            0);
        var item = new VaultItem(
            Guid.NewGuid(),
            vault.Id,
            "browser-password",
            VaultObjectType.Secret,
            true,
            new Dictionary<string, string>(),
            "text/plain",
            _clock.UtcNow,
            _clock.UtcNow,
            null,
            "v1",
            "fingerprint",
            _clock.UtcNow);
        await repository.ApplyDiscoveryAsync(
            identity.Id,
            new DiscoverySnapshot([tenant], [subscription], [vault], [access], [item], []),
            new SyncRun(
                Guid.NewGuid(),
                "test",
                _clock.UtcNow,
                _clock.UtcNow,
                SyncStatus.Completed,
                1,
                1,
                []),
            TestContext.Current.CancellationToken);

        var mapping = new BrowserFillMapping(
            Guid.NewGuid(),
            item.Id,
            identity.Id,
            "https://login.example.com",
            "https://login.example.com",
            BrowserMappingFieldPurpose.Password,
            true,
            _clock.UtcNow,
            _clock.UtcNow);
        await repository.UpsertBrowserFillMappingAsync(
            mapping,
            TestContext.Current.CancellationToken);
        await repository.RecordBrowserFillAuditAsync(
            new BrowserFillAuditEvent(
                Guid.NewGuid(),
                _clock.UtcNow,
                mapping.Id,
                item.Id,
                identity.Id,
                mapping.TopOrigin,
                mapping.FrameOrigin,
                mapping.FieldPurpose,
                "Approved"),
            TestContext.Current.CancellationToken);

        Assert.Equal(
            mapping,
            await repository.FindBrowserFillMappingAsync(
                mapping.TopOrigin,
                mapping.FrameOrigin,
                mapping.FieldPurpose,
                TestContext.Current.CancellationToken));
        Assert.NotNull(await repository.ResolveItemForIdentityAsync(
            item.Id,
            identity.Id,
            TestContext.Current.CancellationToken));
        Assert.Null(await repository.ResolveItemForIdentityAsync(
            item.Id,
            Guid.NewGuid(),
            TestContext.Current.CancellationToken));
        Assert.Single(await repository.GetBrowserFillAuditAsync(
            10,
            TestContext.Current.CancellationToken));

        await repository.RemoveIdentityAsync(identity.Id, TestContext.Current.CancellationToken);

        Assert.Empty(await repository.GetBrowserFillMappingsAsync(
            TestContext.Current.CancellationToken));
        Assert.Single(await repository.GetBrowserFillAuditAsync(
            10,
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task VersionFourDatabaseAddsBrowserTablesWithoutRebuildingExistingState()
    {
        var path = Path.Combine(_directory, "version-four-browser.db");
        var repository = new EncryptedSqliteMetadataRepository(path, _keys);
        await repository.InitializeAsync(TestContext.Current.CancellationToken);
        var identity = TestIdentity("version-four-browser-account");
        await repository.UpsertIdentityAsync(identity, TestContext.Current.CancellationToken);
        await ExecuteDatabaseCommandAsync(
            path,
            """
            DROP TABLE browser_fill_mappings;
            DROP TABLE browser_fill_audit;
            PRAGMA user_version=4;
            """);

        var migrated = new EncryptedSqliteMetadataRepository(path, _keys);
        await migrated.InitializeAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(await migrated.GetIdentityAsync(
            identity.Id,
            TestContext.Current.CancellationToken));
        Assert.Empty(await migrated.GetBrowserFillMappingsAsync(
            TestContext.Current.CancellationToken));
        Assert.Equal(7, await ReadDatabaseScalarAsync<int>(path, "PRAGMA user_version"));
    }

    [Fact]
    public async Task WorkloadIdentityProfileRoundTripsThroughEncryptedMetadata()
    {
        var path = Path.Combine(_directory, "workload-profile.db");
        var repository = new EncryptedSqliteMetadataRepository(path, _keys);
        await repository.InitializeAsync(TestContext.Current.CancellationToken);
        var identity = new ConnectedIdentity(
            Guid.NewGuid(),
            "11111111-1111-1111-1111-111111111111",
            "workload-account",
            string.Empty,
            "Automation",
            "22222222-2222-2222-2222-222222222222",
            AuthenticationState.Ready,
            _clock.UtcNow,
            true,
            IdentityType.ServicePrincipal,
            "AA11BB22");

        await repository.UpsertIdentityAsync(identity, TestContext.Current.CancellationToken);
        var restored = await repository.GetIdentityAsync(identity.Id, TestContext.Current.CancellationToken);

        Assert.NotNull(restored);
        Assert.Equal(IdentityType.ServicePrincipal, restored.Type);
        Assert.Equal("AA11BB22", restored.CredentialData);
    }

    [Fact]
    public async Task FederatedWorkloadProfileRoundTripsWithoutTokenContent()
    {
        var path = Path.Combine(_directory, "federated-workload-profile.db");
        var repository = new EncryptedSqliteMetadataRepository(path, _keys);
        await repository.InitializeAsync(TestContext.Current.CancellationToken);
        var tokenPath = Path.Combine(_directory, "projected-token");
        var identity = new ConnectedIdentity(
            Guid.NewGuid(),
            "11111111-1111-1111-1111-111111111111",
            "federated-workload",
            string.Empty,
            "Federated automation",
            "22222222-2222-2222-2222-222222222222",
            AuthenticationState.Ready,
            _clock.UtcNow,
            true,
            IdentityType.FederatedServicePrincipal,
            tokenPath);

        await repository.UpsertIdentityAsync(identity, TestContext.Current.CancellationToken);
        var restored = await repository.GetIdentityAsync(
            identity.Id,
            TestContext.Current.CancellationToken);

        Assert.NotNull(restored);
        Assert.Equal(IdentityType.FederatedServicePrincipal, restored.Type);
        Assert.Equal(tokenPath, restored.CredentialData);
    }

    [Fact]
    public async Task VersionTwoDatabaseMigratesIdentityProfileColumns()
    {
        var path = Path.Combine(_directory, "version-two-profile-migration.db");
        var repository = new EncryptedSqliteMetadataRepository(path, _keys);
        await repository.InitializeAsync(TestContext.Current.CancellationToken);
        var identity = TestIdentity("version-two-account");
        await repository.UpsertIdentityAsync(identity, TestContext.Current.CancellationToken);
        await ExecuteDatabaseCommandAsync(path, """
            ALTER TABLE identities DROP COLUMN credential_data;
            ALTER TABLE identities DROP COLUMN identity_type;
            PRAGMA user_version=2;
            """);

        var migrated = new EncryptedSqliteMetadataRepository(path, _keys);
        await migrated.InitializeAsync(TestContext.Current.CancellationToken);
        var restored = await migrated.GetIdentityAsync(identity.Id, TestContext.Current.CancellationToken);

        Assert.NotNull(restored);
        Assert.Equal(IdentityType.InteractiveUser, restored.Type);
        Assert.Equal(string.Empty, restored.CredentialData);
        Assert.Equal(7, await ReadDatabaseScalarAsync<int>(path, "PRAGMA user_version"));
    }

    [Fact]
    public async Task VersionThreeDatabaseMigratesVaultSelectionWithoutDroppingAccessPaths()
    {
        var path = Path.Combine(_directory, "version-three-vault-selection.db");
        var repository = new EncryptedSqliteMetadataRepository(path, _keys);
        await repository.InitializeAsync(TestContext.Current.CancellationToken);
        var identity = TestIdentity("version-three-account");
        await repository.UpsertIdentityAsync(identity, TestContext.Current.CancellationToken);
        var tenant = new TenantAccess(Guid.NewGuid(), identity.Id, "tenant", "Tenant", "Home", _clock.UtcNow, "Available");
        var subscription = new SubscriptionAccess(Guid.NewGuid(), tenant.Id, "subscription", "Subscription", "Enabled", true, _clock.UtcNow);
        var vault = new VaultResource(Guid.NewGuid(), "/vaults/migration", "migration", "tenant", "subscription", "rg", "eastus", new Dictionary<string, string>(), new Uri("https://migration.vault.azure.net/"), _clock.UtcNow);
        var access = new VaultAccess(Guid.NewGuid(), vault.Id, identity.Id, "tenant", "Visible", _clock.UtcNow, null, 0);
        await repository.ApplyDiscoveryAsync(
            identity.Id,
            new DiscoverySnapshot([tenant], [subscription], [vault], [access], [], []),
            new SyncRun(Guid.NewGuid(), "initial", _clock.UtcNow, _clock.UtcNow, SyncStatus.Completed, 1, 0, []),
            TestContext.Current.CancellationToken);
        await ExecuteDatabaseCommandAsync(path, """
            ALTER TABLE vault_access DROP COLUMN is_selected;
            PRAGMA user_version=3;
            """);

        var migrated = new EncryptedSqliteMetadataRepository(path, _keys);
        await migrated.InitializeAsync(TestContext.Current.CancellationToken);
        var summaries = await migrated.GetVaultAccessSummariesAsync(identity.Id, TestContext.Current.CancellationToken);

        Assert.Single(summaries);
        Assert.True(summaries[0].Access.IsSelected);
        Assert.Equal(7, await ReadDatabaseScalarAsync<int>(path, "PRAGMA user_version"));
    }

    [Fact]
    public async Task CompleteDiscoveryTombstonesMissingObjectsButPartialFailurePreservesThem()
    {
        var path = Path.Combine(_directory, "reconciliation.db");
        var repository = new EncryptedSqliteMetadataRepository(path, _keys);
        await repository.InitializeAsync(TestContext.Current.CancellationToken);
        var identity = TestIdentity("reconciliation-account");
        await repository.UpsertIdentityAsync(identity, TestContext.Current.CancellationToken);
        var tenant = new TenantAccess(Guid.NewGuid(), identity.Id, "tenant", "Tenant", "Home", _clock.UtcNow, "Available");
        var subscription = new SubscriptionAccess(Guid.NewGuid(), tenant.Id, "subscription", "Subscription", "Enabled", true, _clock.UtcNow);
        var firstVault = new VaultResource(Guid.NewGuid(), "/vaults/first", "first", "tenant", "subscription", "rg", "eastus", new Dictionary<string, string>(), new Uri("https://first.vault.azure.net/"), _clock.UtcNow);
        var secondVault = new VaultResource(Guid.NewGuid(), "/vaults/second", "second", "tenant", "subscription", "rg", "eastus", new Dictionary<string, string>(), new Uri("https://second.vault.azure.net/"), _clock.UtcNow);
        var firstAccess = new VaultAccess(Guid.NewGuid(), firstVault.Id, identity.Id, "tenant", "Ready", _clock.UtcNow, null, 0);
        var secondAccess = new VaultAccess(Guid.NewGuid(), secondVault.Id, identity.Id, "tenant", "Ready", _clock.UtcNow, null, 0);
        var firstItem = new VaultItem(Guid.NewGuid(), firstVault.Id, "first-item", VaultObjectType.Secret, true, new Dictionary<string, string>(), null, null, null, null, "v1", "first-fingerprint", _clock.UtcNow);
        var removedItem = new VaultItem(Guid.NewGuid(), firstVault.Id, "removed-item", VaultObjectType.Secret, true, new Dictionary<string, string>(), null, null, null, null, "v1", "removed-fingerprint", _clock.UtcNow);
        var secondItem = new VaultItem(Guid.NewGuid(), secondVault.Id, "second-item", VaultObjectType.Secret, true, new Dictionary<string, string>(), null, null, null, null, "v1", "second-fingerprint", _clock.UtcNow);
        await repository.ApplyDiscoveryAsync(
            identity.Id,
            new DiscoverySnapshot([tenant], [subscription], [firstVault, secondVault], [firstAccess, secondAccess], [firstItem, removedItem, secondItem], []),
            new SyncRun(Guid.NewGuid(), "initial", _clock.UtcNow, _clock.UtcNow, SyncStatus.Completed, 2, 3, []),
            TestContext.Current.CancellationToken);

        await repository.ApplyDiscoveryAsync(
            identity.Id,
            new DiscoverySnapshot([tenant], [subscription], [firstVault], [firstAccess], [firstItem], [new ProviderError("subscription", "Unavailable", "One scope was unavailable.")]),
            new SyncRun(Guid.NewGuid(), "partial", _clock.UtcNow, _clock.UtcNow, SyncStatus.CompletedWithErrors, 1, 1, ["One scope was unavailable."]),
            TestContext.Current.CancellationToken);

        Assert.Equal(3, (await repository.SearchAsync(new SearchRequest(), _clock.UtcNow, TestContext.Current.CancellationToken)).Count);
        Assert.Equal("Ready", await ReadDatabaseScalarAsync<string>(path, $"SELECT status FROM vault_access WHERE id='{secondAccess.Id:D}'"));

        await repository.ApplyDiscoveryAsync(
            identity.Id,
            new DiscoverySnapshot([tenant], [subscription], [firstVault], [firstAccess], [firstItem], []),
            new SyncRun(Guid.NewGuid(), "complete", _clock.UtcNow, _clock.UtcNow, SyncStatus.Completed, 1, 1, []),
            TestContext.Current.CancellationToken);

        Assert.Single(await repository.SearchAsync(new SearchRequest(), _clock.UtcNow, TestContext.Current.CancellationToken));
        Assert.Equal("Removed", await ReadDatabaseScalarAsync<string>(path, $"SELECT status FROM vault_access WHERE id='{secondAccess.Id:D}'"));
        Assert.Equal(1, await ReadDatabaseScalarAsync<int>(path, $"SELECT is_deleted FROM items WHERE id='{removedItem.Id:D}'"));
        Assert.Equal(0, await ReadDatabaseScalarAsync<int>(path, $"SELECT is_deleted FROM items WHERE id='{secondItem.Id:D}'"));
        var associatedVaultIds =
            await repository.GetVaultIdsForIdentityAsync(
                identity.Id,
                TestContext.Current.CancellationToken);
        Assert.Equal(2, associatedVaultIds.Count);
        Assert.Contains(firstVault.Id, associatedVaultIds);
        Assert.Contains(secondVault.Id, associatedVaultIds);
    }

    [Fact]
    public async Task SubscriptionSelectionPersistsAcrossRediscoveryAndIsScopedToIdentity()
    {
        var path = Path.Combine(_directory, "subscription-selection.db");
        var repository = new EncryptedSqliteMetadataRepository(path, _keys);
        await repository.InitializeAsync(TestContext.Current.CancellationToken);
        var selectedIdentity = TestIdentity("selected-account");
        var otherIdentity = TestIdentity("other-account");
        await repository.UpsertIdentityAsync(selectedIdentity, TestContext.Current.CancellationToken);
        await repository.UpsertIdentityAsync(otherIdentity, TestContext.Current.CancellationToken);
        var selectedTenant = new TenantAccess(Guid.NewGuid(), selectedIdentity.Id, "selected-tenant", "Selected tenant", "Home", _clock.UtcNow, "Available");
        var otherTenant = new TenantAccess(Guid.NewGuid(), otherIdentity.Id, "other-tenant", "Other tenant", "Home", _clock.UtcNow, "Available");
        var selectedSubscription = new SubscriptionAccess(Guid.NewGuid(), selectedTenant.Id, "selected-subscription", "Selected subscription", "Enabled", true, _clock.UtcNow);
        var otherSubscription = new SubscriptionAccess(Guid.NewGuid(), otherTenant.Id, "other-subscription", "Other subscription", "Enabled", true, _clock.UtcNow);

        await repository.ApplyDiscoveryAsync(
            selectedIdentity.Id,
            new DiscoverySnapshot([selectedTenant], [selectedSubscription], [], [], [], []),
            new SyncRun(Guid.NewGuid(), "selected", _clock.UtcNow, _clock.UtcNow, SyncStatus.Completed, 0, 0, []),
            TestContext.Current.CancellationToken);
        await repository.ApplyDiscoveryAsync(
            otherIdentity.Id,
            new DiscoverySnapshot([otherTenant], [otherSubscription], [], [], [], []),
            new SyncRun(Guid.NewGuid(), "other", _clock.UtcNow, _clock.UtcNow, SyncStatus.Completed, 0, 0, []),
            TestContext.Current.CancellationToken);

        await repository.SetSubscriptionSelectedAsync(selectedSubscription.Id, false, TestContext.Current.CancellationToken);
        await repository.ApplyDiscoveryAsync(
            selectedIdentity.Id,
            new DiscoverySnapshot(
                [selectedTenant],
                [selectedSubscription with { DisplayName = "Renamed", IsSelected = true }],
                [],
                [],
                [],
                []),
            new SyncRun(Guid.NewGuid(), "rediscovery", _clock.UtcNow, _clock.UtcNow, SyncStatus.Completed, 0, 0, []),
            TestContext.Current.CancellationToken);

        var selectedSubscriptions = await repository.GetSubscriptionsAsync(selectedIdentity.Id, TestContext.Current.CancellationToken);
        var otherSubscriptions = await repository.GetSubscriptionsAsync(otherIdentity.Id, TestContext.Current.CancellationToken);
        Assert.Single(selectedSubscriptions);
        Assert.Equal("Renamed", selectedSubscriptions[0].DisplayName);
        Assert.False(selectedSubscriptions[0].IsSelected);
        Assert.Single(otherSubscriptions);
        Assert.True(otherSubscriptions[0].IsSelected);
    }

    [Fact]
    public async Task VaultSelectionAndPermissionAssessmentPersistAcrossRediscovery()
    {
        var repository = new EncryptedSqliteMetadataRepository(
            Path.Combine(_directory, "vault-selection.db"),
            _keys);
        await repository.InitializeAsync(TestContext.Current.CancellationToken);
        var identity = TestIdentity("vault-selection-account");
        await repository.UpsertIdentityAsync(identity, TestContext.Current.CancellationToken);
        var tenant = new TenantAccess(Guid.NewGuid(), identity.Id, "tenant", "Tenant", "Home", _clock.UtcNow, "Available");
        var subscription = new SubscriptionAccess(Guid.NewGuid(), tenant.Id, "subscription", "Subscription", "Enabled", true, _clock.UtcNow);
        var vault = new VaultResource(Guid.NewGuid(), "/subscriptions/sub/resourceGroups/rg/providers/Microsoft.KeyVault/vaults/one", "one", "tenant", "subscription", "rg", "eastus", new Dictionary<string, string>(), new Uri("https://one.vault.azure.net/"), _clock.UtcNow);
        var access = new VaultAccess(Guid.NewGuid(), vault.Id, identity.Id, "tenant", "Initial assessment", _clock.UtcNow, "Secrets:Denied", 0);
        var snapshot = new DiscoverySnapshot([tenant], [subscription], [vault], [access], [], []);
        await repository.ApplyDiscoveryAsync(
            identity.Id,
            snapshot,
            new SyncRun(Guid.NewGuid(), "initial", _clock.UtcNow, _clock.UtcNow, SyncStatus.Completed, 1, 0, []),
            TestContext.Current.CancellationToken);

        await repository.SetVaultSelectedAsync(access.Id, false, TestContext.Current.CancellationToken);
        await repository.ApplyDiscoveryAsync(
            identity.Id,
            snapshot with { AccessPaths = [access with { AccessStatus = "Updated assessment", IsSelected = true }] },
            new SyncRun(Guid.NewGuid(), "rediscovery", _clock.UtcNow, _clock.UtcNow, SyncStatus.Completed, 1, 0, []),
            TestContext.Current.CancellationToken);

        var summaries = await repository.GetVaultAccessSummariesAsync(identity.Id, TestContext.Current.CancellationToken);
        Assert.Single(summaries);
        Assert.False(summaries[0].Access.IsSelected);
        Assert.Equal("Updated assessment", summaries[0].Access.AccessStatus);
        Assert.Equal(identity.DisplayName, summaries[0].IdentityDisplayName);
        Assert.Equal("Tenant", summaries[0].TenantDisplayName);
    }

    [Fact]
    public async Task ProtectedValueRoundTripsEncryptedAndPurgesByVault()
    {
        var vaultId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var store = new EncryptedFileValueStore(Path.Combine(_directory, "values"), _keys, _clock);
        using var original = new SensitiveValue("not-in-plaintext");
        await store.StoreAsync(itemId, vaultId, null, original, "fingerprint", _clock.UtcNow.AddHours(1), TestContext.Current.CancellationToken);

        var fileBytes = await File.ReadAllBytesAsync(Path.Combine(_directory, "values", $"{itemId:D}.vpcache"), TestContext.Current.CancellationToken);
        Assert.DoesNotContain("not-in-plaintext", Encoding.UTF8.GetString(fileBytes), StringComparison.Ordinal);
        Assert.Contains("\"KeyVersion\":2", Encoding.UTF8.GetString(fileBytes), StringComparison.Ordinal);
        using var restored = await store.RetrieveAsync(itemId, _clock.UtcNow, "fingerprint", TestContext.Current.CancellationToken);
        Assert.Equal("not-in-plaintext", restored?.Reveal());

        await store.PurgeVaultAsync(vaultId, TestContext.Current.CancellationToken);
        Assert.Null(await store.RetrieveAsync(itemId, _clock.UtcNow, "fingerprint", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task WorkspaceLinkScopesSearchAndCanBeRemoved()
    {
        var repository = new EncryptedSqliteMetadataRepository(Path.Combine(_directory, "workspace.db"), _keys);
        await repository.InitializeAsync(TestContext.Current.CancellationToken);
        var identity = new ConnectedIdentity(Guid.NewGuid(), "11111111-1111-1111-1111-111111111111", "workspace-account", "user@example.invalid", "Workspace Identity", "tenant", AuthenticationState.Ready, _clock.UtcNow);
        await repository.UpsertIdentityAsync(identity, TestContext.Current.CancellationToken);
        var tenant = new TenantAccess(Guid.NewGuid(), identity.Id, "tenant", "Tenant", "Home", _clock.UtcNow, "Available");
        var subscription = new SubscriptionAccess(Guid.NewGuid(), tenant.Id, "subscription", "Subscription", "Enabled", true, _clock.UtcNow);
        var vault = new VaultResource(Guid.NewGuid(), "/workspace-resource", "workspace-vault", "tenant", "subscription", "rg", "eastus", new Dictionary<string, string>(), new Uri("https://workspace-vault.vault.azure.net/"), _clock.UtcNow);
        var access = new VaultAccess(Guid.NewGuid(), vault.Id, identity.Id, "tenant", "Ready", _clock.UtcNow, null, 0);
        var item = new VaultItem(Guid.NewGuid(), vault.Id, "workspace-secret", VaultObjectType.Secret, true, new Dictionary<string, string>(), null, null, null, null, "v1", "fingerprint", _clock.UtcNow);
        await repository.ApplyDiscoveryAsync(identity.Id, new DiscoverySnapshot([tenant], [subscription], [vault], [access], [item], []), new SyncRun(Guid.NewGuid(), "test", _clock.UtcNow, _clock.UtcNow, SyncStatus.Completed, 1, 1, []), TestContext.Current.CancellationToken);
        var workspace = new Workspace(Guid.NewGuid(), "Production", string.Empty, 0);
        await repository.UpsertWorkspaceAsync(workspace, TestContext.Current.CancellationToken);
        await repository.AddWorkspaceLinkAsync(new WorkspaceResourceLink(Guid.NewGuid(), workspace.Id, ResourceLinkType.Vault, vault.Id.ToString("D")), TestContext.Current.CancellationToken);

        Assert.Single(await repository.SearchAsync(new SearchRequest(WorkspaceId: workspace.Id), _clock.UtcNow, TestContext.Current.CancellationToken));

        await repository.RemoveWorkspaceLinkAsync(workspace.Id, ResourceLinkType.Vault, vault.Id.ToString("D"), TestContext.Current.CancellationToken);
        Assert.Empty(await repository.SearchAsync(new SearchRequest(WorkspaceId: workspace.Id), _clock.UtcNow, TestContext.Current.CancellationToken));

        await repository.AddWorkspaceLinkAsync(new WorkspaceResourceLink(Guid.NewGuid(), workspace.Id, ResourceLinkType.Tenant, tenant.TenantId), TestContext.Current.CancellationToken);
        Assert.Single(await repository.SearchAsync(new SearchRequest(WorkspaceId: workspace.Id), _clock.UtcNow, TestContext.Current.CancellationToken));
        await repository.RemoveWorkspaceLinkAsync(workspace.Id, ResourceLinkType.Tenant, tenant.TenantId, TestContext.Current.CancellationToken);

        await repository.AddWorkspaceLinkAsync(new WorkspaceResourceLink(Guid.NewGuid(), workspace.Id, ResourceLinkType.Subscription, subscription.SubscriptionId), TestContext.Current.CancellationToken);
        Assert.Single(await repository.SearchAsync(new SearchRequest(WorkspaceId: workspace.Id), _clock.UtcNow, TestContext.Current.CancellationToken));

        await repository.RemoveWorkspaceAsync(workspace.Id, TestContext.Current.CancellationToken);
        Assert.Empty(await repository.GetWorkspacesAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ProtectedValueCanBePurgedByWorkspaceWithoutAffectingOtherWorkspaces()
    {
        var store = new EncryptedFileValueStore(Path.Combine(_directory, "workspace-values"), _keys, _clock);
        var firstWorkspace = Guid.NewGuid();
        var retainedWorkspace = Guid.NewGuid();
        var firstItem = Guid.NewGuid();
        var retainedItem = Guid.NewGuid();
        using var firstValue = new SensitiveValue("first");
        using var retainedValue = new SensitiveValue("retained");
        await store.StoreAsync(firstItem, Guid.NewGuid(), firstWorkspace, firstValue, "one", _clock.UtcNow.AddHours(1), TestContext.Current.CancellationToken);
        await store.StoreAsync(retainedItem, Guid.NewGuid(), retainedWorkspace, retainedValue, "two", _clock.UtcNow.AddHours(1), TestContext.Current.CancellationToken);

        await store.PurgeWorkspaceAsync(firstWorkspace, TestContext.Current.CancellationToken);

        Assert.Null(await store.RetrieveAsync(firstItem, _clock.UtcNow, "one", TestContext.Current.CancellationToken));
        using var restored = await store.RetrieveAsync(retainedItem, _clock.UtcNow, "two", TestContext.Current.CancellationToken);
        Assert.Equal("retained", restored?.Reveal());
    }

    [Fact]
    public async Task ExpiredProtectedValueIsDeleted()
    {
        var itemId = Guid.NewGuid();
        var store = new EncryptedFileValueStore(Path.Combine(_directory, "values"), _keys, _clock);
        using var value = new SensitiveValue("expired");
        await store.StoreAsync(itemId, Guid.NewGuid(), null, value, "fingerprint", _clock.UtcNow.AddMinutes(1), TestContext.Current.CancellationToken);
        Assert.Null(await store.RetrieveAsync(itemId, _clock.UtcNow.AddMinutes(2), "fingerprint", TestContext.Current.CancellationToken));
        Assert.False(File.Exists(Path.Combine(_directory, "values", $"{itemId:D}.vpcache")));
    }

    [Fact]
    public async Task ReplacingProtectedValuePublishesOneCompleteEnvelopeWithoutTemporaryFiles()
    {
        var itemId = Guid.NewGuid();
        var valueDirectory = Path.Combine(_directory, "replacement-values");
        var store = new EncryptedFileValueStore(valueDirectory, _keys, _clock);
        using var first = new SensitiveValue("first-value");
        using var replacement = new SensitiveValue("replacement-value");
        await store.StoreAsync(itemId, Guid.NewGuid(), null, first, "first-fingerprint", _clock.UtcNow.AddHours(1), TestContext.Current.CancellationToken);

        await store.StoreAsync(itemId, Guid.NewGuid(), null, replacement, "replacement-fingerprint", _clock.UtcNow.AddHours(1), TestContext.Current.CancellationToken);

        Assert.Single(Directory.GetFiles(valueDirectory, "*.vpcache"));
        Assert.Empty(Directory.GetFiles(valueDirectory, "*.tmp"));
        using var restored = await store.RetrieveAsync(itemId, _clock.UtcNow, "replacement-fingerprint", TestContext.Current.CancellationToken);
        Assert.Equal("replacement-value", restored?.Reveal());
    }

    [Fact]
    public async Task ChangedMetadataFingerprintInvalidatesProtectedValue()
    {
        var itemId = Guid.NewGuid();
        var store = new EncryptedFileValueStore(Path.Combine(_directory, "fingerprint-values"), _keys, _clock);
        using var value = new SensitiveValue("superseded");
        await store.StoreAsync(itemId, Guid.NewGuid(), null, value, "old-fingerprint", _clock.UtcNow.AddHours(1), TestContext.Current.CancellationToken);

        Assert.Null(await store.RetrieveAsync(itemId, _clock.UtcNow, "new-fingerprint", TestContext.Current.CancellationToken));
        Assert.False(File.Exists(Path.Combine(_directory, "fingerprint-values", $"{itemId:D}.vpcache")));
    }

    [Fact]
    public async Task ModifiedFingerprintCannotMakeProtectedValueAppearCurrent()
    {
        var itemId = Guid.NewGuid();
        var valueDirectory = Path.Combine(_directory, "tampered-fingerprint-values");
        var path = Path.Combine(valueDirectory, $"{itemId:D}.vpcache");
        var store = new EncryptedFileValueStore(valueDirectory, _keys, _clock);
        using var value = new SensitiveValue("rotated-value");
        await store.StoreAsync(itemId, Guid.NewGuid(), null, value, "old-fingerprint", _clock.UtcNow.AddHours(1), TestContext.Current.CancellationToken);

        var envelope = JsonNode.Parse(await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken))!.AsObject();
        envelope["Descriptor"]!["SourceMetadataFingerprint"] = "new-fingerprint";
        await File.WriteAllTextAsync(path, envelope.ToJsonString(), TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<AuthenticationTagMismatchException>(() =>
            store.RetrieveAsync(itemId, _clock.UtcNow, "new-fingerprint", TestContext.Current.CancellationToken));
        Assert.False(File.Exists(path));
    }

    [Theory]
    [InlineData("Nonce")]
    [InlineData("Tag")]
    [InlineData("Ciphertext")]
    public async Task ModifiedCryptographicFieldFailsAuthenticationAndIsDeleted(string field)
    {
        var itemId = Guid.NewGuid();
        var valueDirectory = Path.Combine(_directory, $"tampered-{field.ToLowerInvariant()}-values");
        var path = Path.Combine(valueDirectory, $"{itemId:D}.vpcache");
        var store = new EncryptedFileValueStore(valueDirectory, _keys, _clock);
        using var value = new SensitiveValue("protected-value");
        await store.StoreAsync(itemId, Guid.NewGuid(), null, value, "fingerprint", _clock.UtcNow.AddHours(1), TestContext.Current.CancellationToken);

        var envelope = JsonNode.Parse(await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken))!.AsObject();
        var bytes = Convert.FromBase64String(envelope[field]!.GetValue<string>());
        bytes[0] ^= 0x01;
        envelope[field] = Convert.ToBase64String(bytes);
        await File.WriteAllTextAsync(path, envelope.ToJsonString(), TestContext.Current.CancellationToken);

        await Assert.ThrowsAnyAsync<CryptographicException>(() =>
            store.RetrieveAsync(itemId, _clock.UtcNow, "fingerprint", TestContext.Current.CancellationToken));
        Assert.False(File.Exists(path));
    }

    [Fact]
    public async Task ModifiedExpiryCannotTriggerAnUnauthenticatedExpiryDecision()
    {
        var itemId = Guid.NewGuid();
        var valueDirectory = Path.Combine(_directory, "tampered-expiry-values");
        var path = Path.Combine(valueDirectory, $"{itemId:D}.vpcache");
        var store = new EncryptedFileValueStore(valueDirectory, _keys, _clock);
        using var value = new SensitiveValue("protected-value");
        await store.StoreAsync(itemId, Guid.NewGuid(), null, value, "fingerprint", _clock.UtcNow.AddHours(1), TestContext.Current.CancellationToken);

        var envelope = JsonNode.Parse(await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken))!.AsObject();
        envelope["Descriptor"]!["ExpiresAt"] = _clock.UtcNow.AddMinutes(-1).ToString("O");
        await File.WriteAllTextAsync(path, envelope.ToJsonString(), TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<AuthenticationTagMismatchException>(() =>
            store.RetrieveAsync(itemId, _clock.UtcNow, "fingerprint", TestContext.Current.CancellationToken));
        Assert.False(File.Exists(path));
    }

    [Fact]
    public async Task SubstitutedEnvelopeCannotBeOpenedUnderAnotherItemPath()
    {
        var originalItemId = Guid.NewGuid();
        var substitutedItemId = Guid.NewGuid();
        var valueDirectory = Path.Combine(_directory, "substituted-values");
        var originalPath = Path.Combine(valueDirectory, $"{originalItemId:D}.vpcache");
        var substitutedPath = Path.Combine(valueDirectory, $"{substitutedItemId:D}.vpcache");
        var store = new EncryptedFileValueStore(valueDirectory, _keys, _clock);
        using var value = new SensitiveValue("protected-value");
        await store.StoreAsync(originalItemId, Guid.NewGuid(), null, value, "fingerprint", _clock.UtcNow.AddHours(1), TestContext.Current.CancellationToken);
        File.Copy(originalPath, substitutedPath);

        await Assert.ThrowsAsync<AuthenticationTagMismatchException>(() =>
            store.RetrieveAsync(substitutedItemId, _clock.UtcNow, "fingerprint", TestContext.Current.CancellationToken));

        Assert.False(File.Exists(substitutedPath));
        using var original = await store.RetrieveAsync(originalItemId, _clock.UtcNow, "fingerprint", TestContext.Current.CancellationToken);
        Assert.Equal("protected-value", original?.Reveal());
    }

    [Fact]
    public async Task ScopedPurgeDeletesTamperedDescriptorInsteadOfTrustingItsScope()
    {
        var vaultId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var valueDirectory = Path.Combine(_directory, "tampered-purge-values");
        var path = Path.Combine(valueDirectory, $"{itemId:D}.vpcache");
        var store = new EncryptedFileValueStore(valueDirectory, _keys, _clock);
        using var value = new SensitiveValue("protected-value");
        await store.StoreAsync(itemId, vaultId, null, value, "fingerprint", _clock.UtcNow.AddHours(1), TestContext.Current.CancellationToken);

        var envelope = JsonNode.Parse(await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken))!.AsObject();
        envelope["Descriptor"]!["VaultId"] = Guid.NewGuid().ToString("D");
        await File.WriteAllTextAsync(path, envelope.ToJsonString(), TestContext.Current.CancellationToken);

        await store.PurgeVaultAsync(vaultId, TestContext.Current.CancellationToken);

        Assert.False(File.Exists(path));
    }

    [Fact]
    public async Task ScopedPurgeRemovesMalformedEntriesAndContinuesWithValidEntries()
    {
        var targetVaultId = Guid.NewGuid();
        var retainedVaultId = Guid.NewGuid();
        var targetItemId = Guid.NewGuid();
        var retainedItemId = Guid.NewGuid();
        var malformedItemId = Guid.NewGuid();
        var valueDirectory = Path.Combine(_directory, "malformed-purge-values");
        var malformedPath = Path.Combine(valueDirectory, $"{malformedItemId:D}.vpcache");
        var store = new EncryptedFileValueStore(valueDirectory, _keys, _clock);
        using var target = new SensitiveValue("target-value");
        using var retained = new SensitiveValue("retained-value");
        await store.StoreAsync(targetItemId, targetVaultId, null, target, "target", _clock.UtcNow.AddHours(1), TestContext.Current.CancellationToken);
        await store.StoreAsync(retainedItemId, retainedVaultId, null, retained, "retained", _clock.UtcNow.AddHours(1), TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(malformedPath, "{not-json", TestContext.Current.CancellationToken);

        await store.PurgeVaultAsync(targetVaultId, TestContext.Current.CancellationToken);

        Assert.False(File.Exists(malformedPath));
        Assert.Null(await store.RetrieveAsync(targetItemId, _clock.UtcNow, "target", TestContext.Current.CancellationToken));
        using var restored = await store.RetrieveAsync(retainedItemId, _clock.UtcNow, "retained", TestContext.Current.CancellationToken);
        Assert.Equal("retained-value", restored?.Reveal());
    }

    [Theory]
    [InlineData("{not-json")]
    [InlineData("{}")]
    public async Task MalformedEnvelopeIsRejectedAndDeleted(string malformedEnvelope)
    {
        var itemId = Guid.NewGuid();
        var valueDirectory = Path.Combine(_directory, "malformed-values");
        var path = Path.Combine(valueDirectory, $"{itemId:D}.vpcache");
        Directory.CreateDirectory(valueDirectory);
        await File.WriteAllTextAsync(path, malformedEnvelope, TestContext.Current.CancellationToken);
        var store = new EncryptedFileValueStore(valueDirectory, _keys, _clock);

        await Assert.ThrowsAsync<CryptographicException>(() =>
            store.RetrieveAsync(itemId, _clock.UtcNow, "fingerprint", TestContext.Current.CancellationToken));

        Assert.False(File.Exists(path));
    }

    [Fact]
    public async Task OversizedProtectedValueEnvelopeIsRejectedAndDeleted()
    {
        var itemId = Guid.NewGuid();
        var valueDirectory = Path.Combine(
            _directory,
            "oversized-values");
        Directory.CreateDirectory(valueDirectory);
        var path = Path.Combine(
            valueDirectory,
            $"{itemId:D}.vpcache");
        await using (var stream = File.Create(path))
            stream.SetLength((16 * 1024 * 1024) + 1);
        var store = new EncryptedFileValueStore(
            valueDirectory,
            _keys,
            _clock);

        await Assert.ThrowsAsync<CryptographicException>(
            () => store.RetrieveAsync(
                itemId,
                DateTimeOffset.UtcNow,
                null,
                TestContext.Current.CancellationToken));

        Assert.False(File.Exists(path));
    }

    [Theory]
    [InlineData("Nonce", "not-base64")]
    [InlineData("Tag", "AA==")]
    public async Task InvalidEnvelopeEncodingOrLengthIsRejectedAndDeleted(string field, string value)
    {
        var itemId = Guid.NewGuid();
        var valueDirectory = Path.Combine(_directory, $"invalid-{field.ToLowerInvariant()}-values");
        var path = Path.Combine(valueDirectory, $"{itemId:D}.vpcache");
        var store = new EncryptedFileValueStore(valueDirectory, _keys, _clock);
        using var secret = new SensitiveValue("protected-value");
        await store.StoreAsync(itemId, Guid.NewGuid(), null, secret, "fingerprint", _clock.UtcNow.AddHours(1), TestContext.Current.CancellationToken);

        var envelope = JsonNode.Parse(await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken))!.AsObject();
        envelope[field] = value;
        await File.WriteAllTextAsync(path, envelope.ToJsonString(), TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<CryptographicException>(() =>
            store.RetrieveAsync(itemId, _clock.UtcNow, "fingerprint", TestContext.Current.CancellationToken));

        Assert.False(File.Exists(path));
    }

    [Fact]
    public async Task FutureKeyVersionFailsWithoutDestroyingPotentiallyNewerData()
    {
        var itemId = Guid.NewGuid();
        var valueDirectory = Path.Combine(_directory, "future-version-values");
        var path = Path.Combine(valueDirectory, $"{itemId:D}.vpcache");
        var store = new EncryptedFileValueStore(valueDirectory, _keys, _clock);
        using var value = new SensitiveValue("future-value");
        await store.StoreAsync(itemId, Guid.NewGuid(), null, value, "fingerprint", _clock.UtcNow.AddHours(1), TestContext.Current.CancellationToken);

        var envelope = JsonNode.Parse(await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken))!.AsObject();
        envelope["KeyVersion"] = 999;
        await File.WriteAllTextAsync(path, envelope.ToJsonString(), TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<CryptographicException>(() =>
            store.RetrieveAsync(itemId, _clock.UtcNow, "fingerprint", TestContext.Current.CancellationToken));

        Assert.True(File.Exists(path));
    }

    [Fact]
    public async Task LegacyUnauthenticatedDescriptorIsInvalidated()
    {
        var itemId = Guid.NewGuid();
        var valueDirectory = Path.Combine(_directory, "legacy-values");
        var path = Path.Combine(valueDirectory, $"{itemId:D}.vpcache");
        var store = new EncryptedFileValueStore(valueDirectory, _keys, _clock);
        using var value = new SensitiveValue("legacy-value");
        await store.StoreAsync(itemId, Guid.NewGuid(), null, value, "fingerprint", _clock.UtcNow.AddHours(1), TestContext.Current.CancellationToken);

        var envelope = JsonNode.Parse(await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken))!.AsObject();
        envelope["KeyVersion"] = 1;
        await File.WriteAllTextAsync(path, envelope.ToJsonString(), TestContext.Current.CancellationToken);

        Assert.Null(await store.RetrieveAsync(itemId, _clock.UtcNow, "fingerprint", TestContext.Current.CancellationToken));
        Assert.False(File.Exists(path));
    }

    [Fact]
    public async Task VersionOneDatabaseMigratesClientIdColumnBeforeQueries()
    {
        Directory.CreateDirectory(_directory);
        var path = Path.Combine(_directory, "migration.db");
        SQLitePCL.Batteries_V2.Init();
        var key = await _keys.GetOrCreateKeyAsync("metadata-database", TestContext.Current.CancellationToken);
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Password = Convert.ToBase64String(key),
            Pooling = false,
        }.ToString();
        try
        {
            await using var connection = new SqliteConnection(connectionString);
            await connection.OpenAsync(TestContext.Current.CancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = "CREATE TABLE identities(id TEXT PRIMARY KEY,account_identifier TEXT NOT NULL UNIQUE,username_hint TEXT NOT NULL,display_name TEXT NOT NULL,home_tenant_id TEXT NOT NULL,auth_state INTEGER NOT NULL,last_interactive TEXT NOT NULL,is_enabled INTEGER NOT NULL); PRAGMA user_version=1;";
            await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
        }

        var repository = new EncryptedSqliteMetadataRepository(path, _keys);
        await repository.InitializeAsync(TestContext.Current.CancellationToken);
        var identity = new ConnectedIdentity(Guid.NewGuid(), "22222222-2222-2222-2222-222222222222", "migrated-account", "user@example.invalid", "Migrated", "tenant", AuthenticationState.Ready, _clock.UtcNow);
        await repository.UpsertIdentityAsync(identity, TestContext.Current.CancellationToken);

        Assert.Equal(identity.ClientId, (await repository.GetIdentityAsync(identity.Id, TestContext.Current.CancellationToken))?.ClientId);
    }

    [Fact]
    public async Task MissingMetadataKeyPreservesDatabaseUntilMatchedKeyIsRestored()
    {
        var path = Path.Combine(_directory, "missing-key.db");
        var repository = new EncryptedSqliteMetadataRepository(path, _keys);
        await repository.InitializeAsync(TestContext.Current.CancellationToken);
        var identity = TestIdentity("missing-key-account");
        await repository.UpsertIdentityAsync(identity, TestContext.Current.CancellationToken);
        var before = SHA256.HashData(await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken));
        var originalKey = _keys.RemoveKey("metadata-database");

        try
        {
            await Assert.ThrowsAsync<ProtectedKeyUnavailableException>(() =>
                new EncryptedSqliteMetadataRepository(path, _keys).InitializeAsync(TestContext.Current.CancellationToken));

            Assert.False(_keys.ContainsKey("metadata-database"));
            Assert.Equal(before, SHA256.HashData(await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken)));

            _keys.RestoreKey("metadata-database", originalKey);
            var recovered = new EncryptedSqliteMetadataRepository(path, _keys);
            await recovered.InitializeAsync(TestContext.Current.CancellationToken);
            Assert.Equal(identity.Id, (await recovered.GetIdentityAsync(identity.Id, TestContext.Current.CancellationToken))?.Id);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(originalKey);
        }
    }

    [Fact]
    public async Task MissingOfflineValueKeyPreservesEnvelopeUntilMatchedKeyIsRestored()
    {
        var itemId = Guid.NewGuid();
        var valueDirectory = Path.Combine(_directory, "missing-cache-key");
        var path = Path.Combine(valueDirectory, $"{itemId:D}.vpcache");
        var store = new EncryptedFileValueStore(valueDirectory, _keys, _clock);
        using var value = new SensitiveValue("recoverable-value");
        await store.StoreAsync(itemId, Guid.NewGuid(), null, value, "fingerprint", _clock.UtcNow.AddHours(1), TestContext.Current.CancellationToken);
        var before = await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken);
        var originalKey = _keys.RemoveKey("offline-values-v2");

        try
        {
            await Assert.ThrowsAsync<ProtectedKeyUnavailableException>(() =>
                store.RetrieveAsync(itemId, _clock.UtcNow, "fingerprint", TestContext.Current.CancellationToken));

            Assert.False(_keys.ContainsKey("offline-values-v2"));
            Assert.Equal(before, await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken));

            _keys.RestoreKey("offline-values-v2", originalKey);
            using var recovered = await store.RetrieveAsync(itemId, _clock.UtcNow, "fingerprint", TestContext.Current.CancellationToken);
            Assert.Equal("recoverable-value", recovered?.Reveal());
        }
        finally
        {
            CryptographicOperations.ZeroMemory(originalKey);
        }
    }

    [Fact]
    public async Task FutureDatabaseSchemaIsRejectedWithoutModification()
    {
        var path = Path.Combine(_directory, "future-schema.db");
        await new EncryptedSqliteMetadataRepository(path, _keys).InitializeAsync(TestContext.Current.CancellationToken);
        await ExecuteDatabaseCommandAsync(path, "PRAGMA user_version=99;");
        var before = SHA256.HashData(await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken));

        var exception = await Assert.ThrowsAsync<IncompatibleLocalDataVersionException>(() =>
            new EncryptedSqliteMetadataRepository(path, _keys).InitializeAsync(TestContext.Current.CancellationToken));

        Assert.Equal(99, exception.ObservedVersion);
        Assert.Equal(7, exception.SupportedVersion);
        Assert.Equal(before, SHA256.HashData(await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken)));
        Assert.Equal(99, await ReadDatabaseScalarAsync<int>(path, "PRAGMA user_version"));
    }

    [Fact]
    public async Task CorruptDatabaseIsRejectedAndPreserved()
    {
        var path = Path.Combine(_directory, "corrupt.db");
        Directory.CreateDirectory(_directory);
        await File.WriteAllBytesAsync(path, RandomNumberGenerator.GetBytes(4096), TestContext.Current.CancellationToken);
        var before = SHA256.HashData(await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken));
        await _keys.GetOrCreateKeyAsync("metadata-database", TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<LocalDataIntegrityException>(() =>
            new EncryptedSqliteMetadataRepository(path, _keys).InitializeAsync(TestContext.Current.CancellationToken));

        Assert.Equal(before, SHA256.HashData(await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken)));
        Assert.True(_keys.ContainsKey("metadata-database"));
    }

    [Fact]
    public async Task WrongMetadataKeyIsRejectedWithoutReplacingDatabase()
    {
        var path = Path.Combine(_directory, "wrong-key.db");
        await new EncryptedSqliteMetadataRepository(path, _keys).InitializeAsync(TestContext.Current.CancellationToken);
        var before = SHA256.HashData(await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken));
        var originalKey = _keys.ReplaceKey("metadata-database", RandomNumberGenerator.GetBytes(32));

        try
        {
            await Assert.ThrowsAsync<LocalDataIntegrityException>(() =>
                new EncryptedSqliteMetadataRepository(path, _keys).InitializeAsync(TestContext.Current.CancellationToken));
            Assert.Equal(before, SHA256.HashData(await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken)));
        }
        finally
        {
            _keys.RestoreKey("metadata-database", originalKey);
            CryptographicOperations.ZeroMemory(originalKey);
        }

        await new EncryptedSqliteMetadataRepository(path, _keys).InitializeAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task CurrentVersionWithMissingTableFailsInsteadOfSilentlyRecreatingIt()
    {
        var path = Path.Combine(_directory, "missing-table.db");
        await new EncryptedSqliteMetadataRepository(path, _keys).InitializeAsync(TestContext.Current.CancellationToken);
        await ExecuteDatabaseCommandAsync(path, "DROP TABLE sync_runs;");

        await Assert.ThrowsAsync<LocalDataIntegrityException>(() =>
            new EncryptedSqliteMetadataRepository(path, _keys).InitializeAsync(TestContext.Current.CancellationToken));

        Assert.Equal(0, await ReadDatabaseScalarAsync<long>(path, "SELECT COUNT(*) FROM sqlite_schema WHERE type='table' AND name='sync_runs'"));
        Assert.Equal(7, await ReadDatabaseScalarAsync<int>(path, "PRAGMA user_version"));
    }

    [Fact]
    public async Task CurrentVersionWithMissingColumnFailsInsteadOfStartingWithLatentDamage()
    {
        var path = Path.Combine(_directory, "missing-column.db");
        await new EncryptedSqliteMetadataRepository(path, _keys).InitializeAsync(TestContext.Current.CancellationToken);
        await ExecuteDatabaseCommandAsync(path, "ALTER TABLE sync_runs DROP COLUMN error_count;");

        await Assert.ThrowsAsync<LocalDataIntegrityException>(() =>
            new EncryptedSqliteMetadataRepository(path, _keys).InitializeAsync(TestContext.Current.CancellationToken));

        Assert.Equal(0, await ReadDatabaseScalarAsync<long>(path, "SELECT COUNT(*) FROM pragma_table_info('sync_runs') WHERE name='error_count'"));
        Assert.Equal(7, await ReadDatabaseScalarAsync<int>(path, "PRAGMA user_version"));
    }

    private ConnectedIdentity TestIdentity(string accountIdentifier) => new(
        Guid.NewGuid(),
        "11111111-1111-1111-1111-111111111111",
        accountIdentifier,
        "user@example.invalid",
        "Recovery Test",
        "tenant",
        AuthenticationState.Ready,
        _clock.UtcNow);

    private static GovernedMutationAuditEvent
        CreateGovernedMutationAudit(string previousHash)
    {
        var id = Guid.Parse(
            "11111111-1111-1111-1111-111111111111");
        var previewId = Guid.Parse(
            "22222222-2222-2222-2222-222222222222");
        var occurredAt =
            DateTimeOffset.Parse("2026-07-26T12:00:00Z");
        const GovernedAzureOperation operation =
            GovernedAzureOperation.CreateSecret;
        var identityId = Guid.Parse(
            "33333333-3333-3333-3333-333333333333");
        const string tenantId =
            "44444444-4444-4444-4444-444444444444";
        const string subscriptionId =
            "55555555-5555-5555-5555-555555555555";
        const string vaultResourceId =
            "/subscriptions/55555555-5555-5555-5555-555555555555/resourceGroups/rg/providers/Microsoft.KeyVault/vaults/example";
        var objectNameHash = Convert.ToHexString(
            SHA256.HashData(
                Encoding.UTF8.GetBytes("example")));
        const int sensitiveValueLength = 12;
        const GovernedMutationAuditResult result =
            GovernedMutationAuditResult.Succeeded;
        const string providerVersion = "version";
        const string safeMessage = "Created.";
        var recordHash = Convert.ToHexString(
            SHA256.HashData(
                Encoding.UTF8.GetBytes(
                    string.Join(
                        "|",
                        id.ToString("D"),
                        previewId.ToString("D"),
                        occurredAt.ToUniversalTime().ToString("O"),
                        (int)operation,
                        identityId.ToString("D"),
                        tenantId,
                        subscriptionId,
                        vaultResourceId,
                        objectNameHash,
                        sensitiveValueLength,
                        (int)result,
                        providerVersion,
                        safeMessage,
                        previousHash))));
        return new GovernedMutationAuditEvent(
            id,
            previewId,
            occurredAt,
            operation,
            identityId,
            tenantId,
            subscriptionId,
            vaultResourceId,
            objectNameHash,
            sensitiveValueLength,
            result,
            providerVersion,
            safeMessage,
            previousHash,
            recordHash);
    }

    private async Task ExecuteDatabaseCommandAsync(string path, string sql)
    {
        await using var connection = await OpenDatabaseAsync(path);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }

    private async Task<T> ReadDatabaseScalarAsync<T>(string path, string sql)
    {
        await using var connection = await OpenDatabaseAsync(path);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return (T)Convert.ChangeType(
            await command.ExecuteScalarAsync(TestContext.Current.CancellationToken) ?? throw new InvalidOperationException("Expected a database value."),
            typeof(T),
            System.Globalization.CultureInfo.InvariantCulture);
    }

    private async Task<SqliteConnection> OpenDatabaseAsync(string path)
    {
        SQLitePCL.Batteries_V2.Init();
        var key = await _keys.GetExistingKeyAsync("metadata-database", TestContext.Current.CancellationToken);
        try
        {
            var connection = new SqliteConnection(new SqliteConnectionStringBuilder
            {
                DataSource = path,
                Mode = SqliteOpenMode.ReadWrite,
                Password = Convert.ToBase64String(key),
                Pooling = false,
            }.ToString());
            await connection.OpenAsync(TestContext.Current.CancellationToken);
            return connection;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
        }
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
        GC.SuppressFinalize(this);
    }

    private sealed class FixedClock : IClock { public DateTimeOffset UtcNow => DateTimeOffset.Parse("2026-07-16T12:00:00Z", System.Globalization.CultureInfo.InvariantCulture); }
    private sealed class TestKeyProvider : IKeyMaterialProvider
    {
        private readonly Dictionary<string, byte[]> _keys = [];
        public bool IsAvailable => true;
        public Task<byte[]> GetOrCreateKeyAsync(string purpose, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!_keys.TryGetValue(purpose, out var key)) _keys[purpose] = key = System.Security.Cryptography.RandomNumberGenerator.GetBytes(32);
            return Task.FromResult(key.ToArray());
        }

        public Task<byte[]> GetExistingKeyAsync(string purpose, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!_keys.TryGetValue(purpose, out var key))
                throw new ProtectedKeyUnavailableException("The required test key does not exist.");
            return Task.FromResult(key.ToArray());
        }

        public bool ContainsKey(string purpose) => _keys.ContainsKey(purpose);

        public byte[] RemoveKey(string purpose)
        {
            if (!_keys.Remove(purpose, out var key)) throw new KeyNotFoundException(purpose);
            return key;
        }

        public byte[] ReplaceKey(string purpose, byte[] replacement)
        {
            if (!_keys.TryGetValue(purpose, out var original)) throw new KeyNotFoundException(purpose);
            _keys[purpose] = replacement.ToArray();
            CryptographicOperations.ZeroMemory(replacement);
            return original;
        }

        public void RestoreKey(string purpose, byte[] key)
        {
            if (_keys.Remove(purpose, out var replaced)) CryptographicOperations.ZeroMemory(replaced);
            _keys[purpose] = key.ToArray();
        }
    }
}
