using VaultProspector.Domain;

namespace VaultProspector.Application.Tests;

public sealed class GovernedAzureMutationServiceTests
{
    private const string TenantId =
        "11111111-1111-1111-1111-111111111111";
    private const string SubscriptionId =
        "22222222-2222-2222-2222-222222222222";
    private const string VaultResourceId =
        $"/subscriptions/{SubscriptionId}/resourceGroups/rg/providers/Microsoft.KeyVault/vaults/unit-test";

    [Fact]
    public async Task ReleaseGateDeniesBeforePolicyOrProvider()
    {
        var identity = Identity();
        var repository = new MutationRepository(identity);
        var provider = new MutationProvider();
        var service = CreateService(
            repository,
            provider,
            releaseApproved: false);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.PrepareAsync(
                identity,
                Vault(),
                GovernedAzureOperation.CreateSecret,
                "example",
                TestContext.Current.CancellationToken));

        Assert.Equal(0, provider.CurrentVersionCalls);
        Assert.Empty(repository.Audit);
    }

    [Fact]
    public async Task PolicyMustAllowExactOperationAndVaultBeforeNetwork()
    {
        var identity = Identity();
        var repository = new MutationRepository(identity);
        var provider = new MutationProvider();
        var deniedPolicy = new FixedPolicy(
            new EnterprisePolicySnapshot(
                true,
                allowedTenantIds: [TenantId],
                allowGovernedAzureMutations: true,
                allowedAzureMutations:
                    [GovernedAzureOperation.CreateSecret],
                allowedAzureMutationVaults:
                    [
                        $"/subscriptions/{SubscriptionId}/resourceGroups/rg/providers/Microsoft.KeyVault/vaults/other",
                    ]));
        var service = CreateService(
            repository,
            provider,
            policy: deniedPolicy);

        await Assert.ThrowsAsync<EnterprisePolicyDeniedException>(
            () => service.PrepareAsync(
                identity,
                Vault(),
                GovernedAzureOperation.CreateSecret,
                "example",
                TestContext.Current.CancellationToken));

        Assert.Equal(0, provider.CurrentVersionCalls);
        Assert.Empty(repository.Audit);
    }

    [Fact]
    public async Task SuccessfulSecretMutationUsesEveryGateAndHashChainedAudit()
    {
        var identity = Identity();
        var repository = new MutationRepository(identity);
        var provider = new MutationProvider();
        var identityProvider = new MutationIdentityProvider();
        var verification = new MutationVerification();
        var service = CreateService(
            repository,
            provider,
            identityProvider,
            verification);
        var preview = await service.PrepareAsync(
            identity,
            Vault(),
            GovernedAzureOperation.CreateSecret,
            "example",
            TestContext.Current.CancellationToken);
        using var value = new SensitiveValue("unit-test-secret");

        var result = await service.ExecuteAsync(
            preview,
            preview.ConfirmationPhrase,
            value,
            TestContext.Current.CancellationToken);

        Assert.Equal("created-version", result.ProviderVersion);
        Assert.Equal(1, identityProvider.ReauthenticateCalls);
        Assert.Equal(1, provider.AuthorizationCalls);
        Assert.Equal(1, verification.Calls);
        Assert.Equal(1, provider.ExecuteCalls);
        Assert.True(value.IsDisposed);
        Assert.Equal(
            [
                GovernedMutationAuditResult.Prepared,
                GovernedMutationAuditResult.Started,
                GovernedMutationAuditResult.Succeeded,
            ],
            repository.Audit.Select(item => item.Result));
        Assert.Equal(string.Empty, repository.Audit[0].PreviousHash);
        Assert.Equal(
            repository.Audit[0].RecordHash,
            repository.Audit[1].PreviousHash);
        Assert.Equal(
            repository.Audit[1].RecordHash,
            repository.Audit[2].PreviousHash);
        Assert.All(
            repository.Audit,
            item => Assert.DoesNotContain(
                "unit-test-secret",
                item.SafeMessage,
                StringComparison.Ordinal));
    }

    [Fact]
    public async Task WrongConfirmationConsumesPreviewAndNeverCallsAuthorization()
    {
        var identity = Identity();
        var repository = new MutationRepository(identity);
        var provider = new MutationProvider();
        var service = CreateService(repository, provider);
        var preview = await service.PrepareAsync(
            identity,
            Vault(),
            GovernedAzureOperation.CreateSecret,
            "example",
            TestContext.Current.CancellationToken);
        using var first = new SensitiveValue("first");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.ExecuteAsync(
                preview,
                "WRONG",
                first,
                TestContext.Current.CancellationToken));
        using var second = new SensitiveValue("second");
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.ExecuteAsync(
                preview,
                preview.ConfirmationPhrase,
                second,
                TestContext.Current.CancellationToken));

        Assert.True(first.IsDisposed);
        Assert.False(second.IsDisposed);
        Assert.Equal(0, provider.AuthorizationCalls);
        Assert.Equal(0, provider.ExecuteCalls);
        Assert.Equal(
            GovernedMutationAuditResult.Failed,
            repository.Audit[^1].Result);
    }

    [Fact]
    public async Task SecretVersionPreviewCarriesExactCurrentVersion()
    {
        var identity = Identity();
        var repository = new MutationRepository(identity);
        var provider = new MutationProvider
        {
            CurrentVersion = "current-version",
        };
        var service = CreateService(repository, provider);

        var preview = await service.PrepareAsync(
            identity,
            Vault(),
            GovernedAzureOperation.CreateSecretVersion,
            "example",
            TestContext.Current.CancellationToken);

        Assert.Equal("current-version", preview.ExpectedCurrentVersion);
        Assert.Equal(1, provider.CurrentVersionCalls);
    }

    private static GovernedAzureMutationService CreateService(
        MutationRepository repository,
        MutationProvider provider,
        MutationIdentityProvider? identityProvider = null,
        MutationVerification? verification = null,
        IEnterprisePolicy? policy = null,
        bool releaseApproved = true) =>
        new(
            repository,
            identityProvider ?? new MutationIdentityProvider(),
            provider,
            verification ?? new MutationVerification(),
            policy ?? AllowedPolicy(),
            new FixedClock(),
            releaseApproved);

    private static FixedPolicy AllowedPolicy() =>
        new FixedPolicy(
            new EnterprisePolicySnapshot(
                true,
                allowedTenantIds: [TenantId],
                allowedProviders: [EnterpriseProvider.AzureKeyVault],
                allowedIdentityTypes: [IdentityType.InteractiveUser],
                allowGovernedAzureMutations: true,
                allowedAzureMutations:
                    Enum.GetValues<GovernedAzureOperation>(),
                allowedAzureMutationVaults: [VaultResourceId]));

    private static ConnectedIdentity Identity() =>
        new(
            Guid.Parse("33333333-3333-3333-3333-333333333333"),
            "44444444-4444-4444-4444-444444444444",
            "account",
            "operator@example.invalid",
            "Operator",
            TenantId,
            AuthenticationState.Ready,
            DateTimeOffset.Parse(
                "2026-07-26T12:00:00Z",
                System.Globalization.CultureInfo.InvariantCulture));

    private static VaultResource Vault() =>
        new(
            Guid.Parse("55555555-5555-5555-5555-555555555555"),
            VaultResourceId,
            "unit-test",
            TenantId,
            SubscriptionId,
            "rg",
            "eastus",
            new Dictionary<string, string>(),
            new Uri("https://unit-test.vault.azure.net/"),
            DateTimeOffset.Parse(
                "2026-07-26T12:00:00Z",
                System.Globalization.CultureInfo.InvariantCulture));

    private sealed class FixedClock : IClock
    {
        public DateTimeOffset UtcNow =>
            DateTimeOffset.Parse(
                "2026-07-26T12:00:00Z",
                System.Globalization.CultureInfo.InvariantCulture);
    }

    private sealed class FixedPolicy(
        EnterprisePolicySnapshot snapshot) : IEnterprisePolicy
    {
        public EnterprisePolicySnapshot GetSnapshot() => snapshot;
    }

    private sealed class MutationIdentityProvider : IIdentityProvider
    {
        public int ReauthenticateCalls { get; private set; }
        public Task<ConnectedIdentity> SignInAsync(
            string clientId,
            string displayName,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public Task<ConnectedIdentity> ReauthenticateAsync(
            ConnectedIdentity identity,
            CancellationToken cancellationToken)
        {
            ReauthenticateCalls++;
            return Task.FromResult(
                identity with
                {
                    LastInteractiveAuthentication =
                        DateTimeOffset.Parse(
                            "2026-07-26T12:00:01Z",
                            System.Globalization.CultureInfo.InvariantCulture),
                });
        }
        public Task<ConnectedIdentity> AuthorizeDirectoryReadAsync(
            ConnectedIdentity identity,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public Task RemoveAsync(
            ConnectedIdentity identity,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }

    private sealed class MutationVerification : IUserVerificationService
    {
        public bool IsAvailable => true;
        public int Calls { get; private set; }
        public Task<UserVerificationResult> VerifyAsync(
            string reason,
            CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(UserVerificationResult.Verified);
        }
    }

    private sealed class MutationProvider : IGovernedAzureMutationProvider
    {
        public string CurrentVersion { get; init; } = "version";
        public int CurrentVersionCalls { get; private set; }
        public int AuthorizationCalls { get; private set; }
        public int ExecuteCalls { get; private set; }

        public Task<string> GetCurrentSecretVersionAsync(
            ConnectedIdentity identity,
            Uri vaultUri,
            string objectName,
            CancellationToken cancellationToken)
        {
            CurrentVersionCalls++;
            return Task.FromResult(CurrentVersion);
        }

        public Task EnsureAuthorizedAsync(
            ConnectedIdentity identity,
            string vaultResourceId,
            GovernedAzureOperation operation,
            CancellationToken cancellationToken)
        {
            AuthorizationCalls++;
            return Task.CompletedTask;
        }

        public Task<GovernedMutationResult> ExecuteAsync(
            ConnectedIdentity identity,
            GovernedMutationPreview preview,
            SensitiveValue? sensitiveValue,
            CancellationToken cancellationToken)
        {
            ExecuteCalls++;
            return Task.FromResult(
                new GovernedMutationResult(
                    preview.Operation,
                    preview.ObjectName,
                    "created-version",
                    "Created.",
                    preview.RecoveryGuidance));
        }
    }

    private sealed class MutationRepository(
        ConnectedIdentity identity) : IMetadataRepository
    {
        public List<GovernedMutationAuditEvent> Audit { get; } = [];

        public Task InitializeAsync(CancellationToken c) =>
            Task.CompletedTask;
        public Task<IReadOnlyList<ConnectedIdentity>> GetIdentitiesAsync(
            CancellationToken c) =>
            Task.FromResult<IReadOnlyList<ConnectedIdentity>>([identity]);
        public Task<ConnectedIdentity?> GetIdentityAsync(
            Guid id,
            CancellationToken c) =>
            Task.FromResult<ConnectedIdentity?>(
                id == identity.Id ? identity : null);
        public Task UpsertIdentityAsync(
            ConnectedIdentity value,
            CancellationToken c) =>
            Task.CompletedTask;
        public Task RemoveIdentityAsync(Guid id, CancellationToken c) =>
            Task.CompletedTask;
        public Task<IReadOnlyList<TenantAccess>> GetTenantsAsync(
            Guid id,
            CancellationToken c) =>
            Task.FromResult<IReadOnlyList<TenantAccess>>([]);
        public Task<IReadOnlyList<SubscriptionAccess>> GetSubscriptionsAsync(
            Guid id,
            CancellationToken c) =>
            Task.FromResult<IReadOnlyList<SubscriptionAccess>>([]);
        public Task SetSubscriptionSelectedAsync(
            Guid id,
            bool selected,
            CancellationToken c) =>
            Task.CompletedTask;
        public Task<IReadOnlyList<VaultAccessSummary>>
            GetVaultAccessSummariesAsync(Guid id, CancellationToken c) =>
            Task.FromResult<IReadOnlyList<VaultAccessSummary>>([]);
        public Task<IReadOnlyList<Guid>> GetVaultIdsForIdentityAsync(
            Guid id,
            CancellationToken c) =>
            Task.FromResult<IReadOnlyList<Guid>>([]);
        public Task SetVaultSelectedAsync(
            Guid id,
            bool selected,
            CancellationToken c) =>
            Task.CompletedTask;
        public Task ApplyDiscoveryAsync(
            Guid id,
            DiscoverySnapshot snapshot,
            SyncRun run,
            CancellationToken c) =>
            Task.CompletedTask;
        public Task<IReadOnlyList<SearchResult>> SearchAsync(
            SearchRequest request,
            DateTimeOffset now,
            CancellationToken c) =>
            Task.FromResult<IReadOnlyList<SearchResult>>([]);
        public Task<(VaultItem Item, VaultResource Vault, ConnectedIdentity Identity)?>
            ResolveItemAsync(Guid id, CancellationToken c) =>
            Task.FromResult<(VaultItem, VaultResource, ConnectedIdentity)?>(null);
        public Task RecordAccessAsync(
            Guid id,
            DateTimeOffset at,
            CancellationToken c) =>
            Task.CompletedTask;
        public Task SetFavoriteAsync(
            Guid id,
            bool favorite,
            CancellationToken c) =>
            Task.CompletedTask;
        public Task<IReadOnlyList<Workspace>> GetWorkspacesAsync(
            CancellationToken c) =>
            Task.FromResult<IReadOnlyList<Workspace>>([]);
        public Task UpsertWorkspaceAsync(
            Workspace workspace,
            CancellationToken c) =>
            Task.CompletedTask;
        public Task RemoveWorkspaceAsync(Guid id, CancellationToken c) =>
            Task.CompletedTask;
        public Task AddWorkspaceLinkAsync(
            WorkspaceResourceLink link,
            CancellationToken c) =>
            Task.CompletedTask;
        public Task RemoveWorkspaceLinkAsync(
            Guid workspaceId,
            ResourceLinkType resourceType,
            string resourceId,
            CancellationToken c) =>
            Task.CompletedTask;
        public Task RecordGovernedMutationAuditAsync(
            GovernedMutationAuditEvent auditEvent,
            CancellationToken cancellationToken)
        {
            Audit.Add(auditEvent);
            return Task.CompletedTask;
        }
        public Task<GovernedMutationAuditEvent?>
            GetLatestGovernedMutationAuditAsync(
                CancellationToken cancellationToken) =>
            Task.FromResult(Audit.LastOrDefault());
    }
}
