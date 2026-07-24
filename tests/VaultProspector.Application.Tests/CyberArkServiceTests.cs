using VaultProspector.Domain;

namespace VaultProspector.Application.Tests;

public sealed class CyberArkServiceTests
{
    [Fact]
    public async Task ConnectValidatesBeforeProtectingAndPersisting()
    {
        var repository = new CyberArkRepository();
        var provider = new FakeProvider();
        var credentialStore = new FakeCredentialStore();
        var service = Service(
            provider,
            credentialStore,
            repository,
            new FakeVerification(UserVerificationResult.Verified));
        using var credential = new SensitiveValue("synthetic");

        var result = await service.ConnectAsync(
            Profile(),
            credential,
            TestContext.Current.CancellationToken);

        Assert.Equal(1, provider.ValidationCount);
        Assert.Equal("synthetic", credentialStore.Value);
        Assert.Equal(CyberArkAuthenticationState.Ready, result.AuthenticationState);
        Assert.Equal(result, repository.Profile);
    }

    [Fact]
    public async Task FailedReplacementPreservesPriorCredentialAndProfile()
    {
        var prior = Profile() with
        {
            AuthenticationState = CyberArkAuthenticationState.Ready,
        };
        var repository = new CyberArkRepository { Profile = prior };
        var provider = new FakeProvider { FailValidation = true };
        var credentialStore = new FakeCredentialStore { Value = "prior" };
        var service = Service(
            provider,
            credentialStore,
            repository,
            new FakeVerification(UserVerificationResult.Verified));
        using var replacement = new SensitiveValue("replacement");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.ConnectAsync(
                prior,
                replacement,
                TestContext.Current.CancellationToken));

        Assert.Equal("prior", credentialStore.Value);
        Assert.Equal(prior, repository.Profile);
    }

    [Fact]
    public async Task RetrievalDeniesBeforeProviderWhenVerificationIsCanceled()
    {
        var repository = new CyberArkRepository { Profile = ReadyProfile() };
        var provider = new FakeProvider();
        var service = Service(
            provider,
            new FakeCredentialStore { Value = "synthetic" },
            repository,
            new FakeVerification(UserVerificationResult.Canceled));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => service.RetrieveAsync(
                Account(),
                null,
                "approved test",
                "show",
                TestContext.Current.CancellationToken));

        Assert.Equal(0, provider.RetrievalCount);
        var audit = Assert.Single(repository.Audit);
        Assert.Equal(CyberArkAuditResult.Denied, audit.Result);
        Assert.DoesNotContain(
            "approved test",
            audit.SafeMessage,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task RetrievalRequiresReasonAndRecordsValueFreeAuthorizationAndResult()
    {
        var repository = new CyberArkRepository { Profile = ReadyProfile() };
        var provider = new FakeProvider();
        var service = Service(
            provider,
            new FakeCredentialStore { Value = "synthetic" },
            repository,
            new FakeVerification(UserVerificationResult.Verified));

        await Assert.ThrowsAsync<CyberArkConfigurationException>(
            () => service.RetrieveAsync(
                Account(),
                null,
                " ",
                "show",
                TestContext.Current.CancellationToken));
        using var value = await service.RetrieveAsync(
            Account(),
            4,
            "approved test",
            "show",
            TestContext.Current.CancellationToken);

        Assert.Equal("provider-value", value.Reveal());
        Assert.Equal(1, provider.RetrievalCount);
        Assert.Equal(2, repository.Audit.Count);
        Assert.All(
            repository.Audit,
            audit => Assert.DoesNotContain(
                "provider-value",
                audit.SafeMessage,
                StringComparison.Ordinal));
        Assert.All(
            repository.Audit,
            audit => Assert.DoesNotContain(
                "approved test",
                audit.SafeMessage,
                StringComparison.Ordinal));
    }

    [Fact]
    public async Task RetrievalDisposesReturnedValueWhenSuccessAuditCannotBeWritten()
    {
        var repository = new CyberArkRepository
        {
            Profile = ReadyProfile(),
            FailAuditOnWrite = 2,
        };
        var provider = new FakeProvider();
        var service = Service(
            provider,
            new FakeCredentialStore { Value = "synthetic" },
            repository,
            new FakeVerification(UserVerificationResult.Verified));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.RetrieveAsync(
                Account(),
                null,
                "approved test",
                "show",
                TestContext.Current.CancellationToken));

        Assert.NotNull(provider.LastRetrievedValue);
        Assert.True(provider.LastRetrievedValue.IsDisposed);
    }

    [Fact]
    public async Task LocalRevocationFailsClosedBeforeRemovingCredential()
    {
        var repository = new CyberArkRepository { Profile = ReadyProfile() };
        var credentialStore = new FakeCredentialStore
        {
            Value = "synthetic",
            FailRemoval = true,
        };
        var service = Service(
            new FakeProvider(),
            credentialStore,
            repository,
            new FakeVerification(UserVerificationResult.Verified));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.RevokeLocalAccessAsync(
                ReadyProfile().Id,
                TestContext.Current.CancellationToken));

        Assert.NotNull(repository.Profile);
        Assert.False(repository.Profile.IsEnabled);
        Assert.Equal(
            CyberArkAuthenticationState.Revoked,
            repository.Profile.AuthenticationState);
        Assert.Equal("synthetic", credentialStore.Value);
    }

    [Fact]
    public async Task SuccessfulSynchronizationRevalidatesEnabledProfile()
    {
        var repository = new CyberArkRepository { Profile = Profile() };
        var provider = new FakeProvider();
        var service = Service(
            provider,
            new FakeCredentialStore { Value = "synthetic" },
            repository,
            new FakeVerification(UserVerificationResult.Verified));

        await service.SynchronizeAsync(
            Profile().Id,
            TestContext.Current.CancellationToken);

        Assert.Equal(1, provider.DiscoveryCount);
        Assert.NotNull(repository.Profile);
        Assert.True(repository.Profile.IsEnabled);
        Assert.Equal(
            CyberArkAuthenticationState.Ready,
            repository.Profile.AuthenticationState);
        Assert.Equal(FixedClock.Now, repository.Profile.LastValidatedAt);
    }

    private static CyberArkService Service(
        ICyberArkProvider provider,
        ICyberArkCredentialStore credentialStore,
        IMetadataRepository repository,
        IUserVerificationService verification) =>
        new(
            provider,
            credentialStore,
            repository,
            verification,
            new FakeClipboard(),
            new FixedClock());

    private static CyberArkProfile Profile() =>
        new(
            Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            "CyberArk",
            new Uri("https://tenant.id.cyberark.cloud/"),
            new Uri("https://tenant.privilegecloud.cyberark.cloud/"),
            "svc@example.com",
            "VaultProspector",
            CyberArkAuthenticationState.Unknown,
            true,
            DateTimeOffset.MinValue,
            null);

    private static CyberArkProfile ReadyProfile() =>
        Profile() with
        {
            AuthenticationState = CyberArkAuthenticationState.Ready,
            CredentialUpdatedAt = FixedClock.Now,
            LastValidatedAt = FixedClock.Now,
        };

    private static CyberArkAccount Account() =>
        new(
            Profile().Id,
            "account-1",
            "Production",
            "database-admin",
            "administrator",
            "db.example.test",
            "WinServerLocal",
            CyberArkSecretType.Password,
            "ready",
            null,
            null,
            "fingerprint",
            FixedClock.Now);

    private sealed class FakeProvider : ICyberArkProvider
    {
        public bool FailValidation { get; init; }
        public int ValidationCount { get; private set; }
        public int DiscoveryCount { get; private set; }
        public int RetrievalCount { get; private set; }
        public SensitiveValue? LastRetrievedValue { get; private set; }

        public Task ValidateAsync(
            CyberArkProfile profile,
            SensitiveValue clientCredential,
            CancellationToken cancellationToken)
        {
            ValidationCount++;
            if (FailValidation)
                throw new InvalidOperationException("Synthetic validation failure.");
            return Task.CompletedTask;
        }

        public Task<CyberArkDiscoverySnapshot> DiscoverAsync(
            CyberArkProfile profile,
            SensitiveValue clientCredential,
            CancellationToken cancellationToken)
        {
            DiscoveryCount++;
            return Task.FromResult(new CyberArkDiscoverySnapshot(
                [],
                [],
                [],
                [],
                [],
                FixedClock.Now));
        }

        public Task<SensitiveValue> RetrieveAsync(
            CyberArkProfile profile,
            SensitiveValue clientCredential,
            CyberArkAccount account,
            int? versionId,
            string reason,
            string actionType,
            CancellationToken cancellationToken)
        {
            RetrievalCount++;
            LastRetrievedValue = new SensitiveValue("provider-value");
            return Task.FromResult(LastRetrievedValue);
        }
    }

    private sealed class FakeCredentialStore : ICyberArkCredentialStore
    {
        public string? Value { get; set; }
        public bool FailRemoval { get; init; }

        public Task StoreAsync(
            Guid profileId,
            SensitiveValue credential,
            CancellationToken cancellationToken)
        {
            Value = credential.Reveal();
            return Task.CompletedTask;
        }

        public Task<SensitiveValue> RetrieveAsync(
            Guid profileId,
            CancellationToken cancellationToken) =>
            Value is null
                ? throw new ProtectedKeyUnavailableException("Missing.")
                : Task.FromResult(new SensitiveValue(Value));

        public Task RemoveAsync(
            Guid profileId,
            CancellationToken cancellationToken)
        {
            if (FailRemoval)
                throw new InvalidOperationException(
                    "Synthetic credential removal failure.");
            Value = null;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeVerification(UserVerificationResult result)
        : IUserVerificationService
    {
        public bool IsAvailable => true;
        public Task<UserVerificationResult> VerifyAsync(
            string reason,
            CancellationToken cancellationToken) =>
            Task.FromResult(result);
    }

    private sealed class FakeClipboard : IClipboardService
    {
        public Task CopyWithAutoClearAsync(
            SensitiveValue value,
            TimeSpan clearAfter,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }

    private sealed class FixedClock : IClock
    {
        public static DateTimeOffset Now { get; } =
            DateTimeOffset.Parse(
                "2026-07-24T12:00:00Z",
                System.Globalization.CultureInfo.InvariantCulture);
        public DateTimeOffset UtcNow => Now;
    }

    private sealed class CyberArkRepository : IMetadataRepository
    {
        public CyberArkProfile? Profile { get; set; }
        public List<CyberArkAuditEvent> Audit { get; } = [];
        public int? FailAuditOnWrite { get; init; }
        private int AuditWriteCount { get; set; }

        public Task<CyberArkProfile?> GetCyberArkProfileAsync(
            Guid id,
            CancellationToken cancellationToken) =>
            Task.FromResult(Profile?.Id == id ? Profile : null);

        public Task<IReadOnlyList<CyberArkProfile>> GetCyberArkProfilesAsync(
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<CyberArkProfile>>(
                Profile is null ? [] : [Profile]);

        public Task UpsertCyberArkProfileAsync(
            CyberArkProfile profile,
            CancellationToken cancellationToken)
        {
            Profile = profile;
            return Task.CompletedTask;
        }

        public Task RemoveCyberArkProfileAsync(
            Guid id,
            CancellationToken cancellationToken)
        {
            Profile = null;
            return Task.CompletedTask;
        }

        public Task ApplyCyberArkDiscoveryAsync(
            Guid profileId,
            CyberArkDiscoverySnapshot snapshot,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task RecordCyberArkAuditAsync(
            CyberArkAuditEvent auditEvent,
            CancellationToken cancellationToken)
        {
            AuditWriteCount++;
            if (AuditWriteCount == FailAuditOnWrite)
                throw new InvalidOperationException(
                    "Synthetic audit failure.");
            Audit.Add(auditEvent);
            return Task.CompletedTask;
        }

        public Task InitializeAsync(CancellationToken cancellationToken) =>
            Task.CompletedTask;
        public Task<IReadOnlyList<ConnectedIdentity>> GetIdentitiesAsync(
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ConnectedIdentity>>([]);
        public Task<ConnectedIdentity?> GetIdentityAsync(
            Guid id,
            CancellationToken cancellationToken) =>
            Task.FromResult<ConnectedIdentity?>(null);
        public Task UpsertIdentityAsync(
            ConnectedIdentity identity,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;
        public Task RemoveIdentityAsync(
            Guid id,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;
        public Task<IReadOnlyList<TenantAccess>> GetTenantsAsync(
            Guid identityId,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<TenantAccess>>([]);
        public Task<IReadOnlyList<SubscriptionAccess>> GetSubscriptionsAsync(
            Guid identityId,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<SubscriptionAccess>>([]);
        public Task SetSubscriptionSelectedAsync(
            Guid subscriptionAccessId,
            bool isSelected,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;
        public Task<IReadOnlyList<VaultAccessSummary>> GetVaultAccessSummariesAsync(
            Guid identityId,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<VaultAccessSummary>>([]);
        public Task<IReadOnlyList<Guid>> GetVaultIdsForIdentityAsync(
            Guid identityId,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<Guid>>([]);
        public Task SetVaultSelectedAsync(
            Guid vaultAccessId,
            bool isSelected,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;
        public Task ApplyDiscoveryAsync(
            Guid identityId,
            DiscoverySnapshot snapshot,
            SyncRun run,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;
        public Task<IReadOnlyList<SearchResult>> SearchAsync(
            SearchRequest request,
            DateTimeOffset now,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<SearchResult>>([]);
        public Task<(VaultItem Item, VaultResource Vault, ConnectedIdentity Identity)?>
            ResolveItemAsync(
                Guid itemId,
                CancellationToken cancellationToken) =>
            Task.FromResult<
                (VaultItem Item, VaultResource Vault, ConnectedIdentity Identity)?>(
                null);
        public Task RecordAccessAsync(
            Guid itemId,
            DateTimeOffset accessedAt,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;
        public Task SetFavoriteAsync(
            Guid itemId,
            bool isFavorite,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;
        public Task<IReadOnlyList<Workspace>> GetWorkspacesAsync(
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<Workspace>>([]);
        public Task UpsertWorkspaceAsync(
            Workspace workspace,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;
        public Task RemoveWorkspaceAsync(
            Guid id,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;
        public Task AddWorkspaceLinkAsync(
            WorkspaceResourceLink link,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;
        public Task RemoveWorkspaceLinkAsync(
            Guid workspaceId,
            ResourceLinkType resourceType,
            string resourceId,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }
}
