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
        Assert.Equal(2, (await repository.SearchAsync(new SearchRequest(TenantId: "nan", SubscriptionId: "script", VaultName: "one"), _clock.UtcNow, TestContext.Current.CancellationToken)).Count);
        await repository.RecordAccessAsync(recentItem.Id, _clock.UtcNow, TestContext.Current.CancellationToken);
        var recentFirst = await repository.SearchAsync(new SearchRequest(RecentlyAccessedFirst: true), _clock.UtcNow, TestContext.Current.CancellationToken);
        Assert.Equal(recentItem.Id, recentFirst[0].Item.Id);
        var bytes = await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken);
        Assert.False(Encoding.ASCII.GetString(bytes.AsSpan(0, Math.Min(16, bytes.Length))).StartsWith("SQLite format 3", StringComparison.Ordinal));
        Assert.DoesNotContain("database-password", Encoding.UTF8.GetString(bytes), StringComparison.Ordinal);
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
        Assert.Equal(2, exception.SupportedVersion);
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
        Assert.Equal(2, await ReadDatabaseScalarAsync<int>(path, "PRAGMA user_version"));
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
        Assert.Equal(2, await ReadDatabaseScalarAsync<int>(path, "PRAGMA user_version"));
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
