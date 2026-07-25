using System.Security.Cryptography;
using System.Text;
using VaultProspector.Application;
using VaultProspector.BrowserProtocol;
using VaultProspector.Domain;

namespace VaultProspector.Application.Tests;

public sealed class BrowserFillServiceTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 7, 23, 16, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task MappingApprovalBindsIdentityOriginPurposeAndFreshVerification()
    {
        var context = CreateContext();
        var mapping = await context.Service.SaveMappingAsync(
            null,
            context.Item.Id,
            context.Identity.Id,
            "https://LOGIN.example.com",
            "https://login.example.com",
            BrowserMappingFieldPurpose.Password,
            true,
            TestContext.Current.CancellationToken);
        var approval = await context.Service.PrepareAsync(
            Request(),
            TestContext.Current.CancellationToken);

        Assert.NotNull(approval);
        var response = await context.Service.ApproveAsync(
            approval,
            TestContext.Current.CancellationToken);
        try
        {
            Assert.Equal(BrowserFillResultCode.Approved, response.Result);
            Assert.Equal(mapping.Id, response.MappingId);
            Assert.Equal("synthetic-value", Encoding.UTF8.GetString(response.ValueUtf8!));
            Assert.Equal(1, context.Verification.Calls);
            Assert.Equal(context.Identity.Id, context.Provider.IdentityId);
            Assert.Equal(["Approved"], context.Repository.Audit.Select(entry => entry.Result));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(response.ValueUtf8!);
        }
    }

    [Fact]
    public async Task UnmappedRequestIsDeniedWithoutValueRetrieval()
    {
        var context = CreateContext();

        var approval = await context.Service.PrepareAsync(
            Request(),
            TestContext.Current.CancellationToken);

        Assert.Null(approval);
        Assert.Equal(0, context.Provider.Calls);
        Assert.Equal("DeniedUnmapped", Assert.Single(context.Repository.Audit).Result);
    }

    [Fact]
    public async Task DestinationAssessmentExposesBrowserDerivedContextWithoutRetrieval()
    {
        var context = CreateContext();

        var assessment = await context.Service.AssessDestinationAsync(
            Request(),
            TestContext.Current.CancellationToken);

        Assert.True(assessment.PolicyDecision.IsAllowed);
        Assert.Equal(
            "https://login.example.com",
            assessment.Request.TopOrigin.SerializedOrigin);
        Assert.Equal(
            "https://login.example.com",
            assessment.Request.FrameOrigin.SerializedOrigin);
        Assert.Equal(
            BrowserMappingFieldPurpose.Password,
            assessment.FieldPurpose);
        Assert.Null(assessment.ExistingMapping);
        Assert.Equal(0, context.Provider.Calls);
        Assert.Empty(context.Repository.Audit);
    }

    [Fact]
    public async Task DestinationAssessmentReportsExactPolicyDenial()
    {
        var context = CreateContext();
        context.Policy!.IsAllowed = false;

        var assessment = await context.Service.AssessDestinationAsync(
            Request(),
            TestContext.Current.CancellationToken);

        Assert.False(assessment.PolicyDecision.IsAllowed);
        Assert.Equal(
            "Denied by test policy.",
            assessment.PolicyDecision.SafeReason);
        Assert.Equal(0, context.Provider.Calls);
    }

    [Fact]
    public async Task MappingChangeAfterConfirmationInvalidatesApproval()
    {
        var context = CreateContext();
        await context.Service.SaveMappingAsync(
            null,
            context.Item.Id,
            context.Identity.Id,
            "https://login.example.com",
            "https://login.example.com",
            BrowserMappingFieldPurpose.Password,
            true,
            TestContext.Current.CancellationToken);
        var approval = await context.Service.PrepareAsync(
            Request(),
            TestContext.Current.CancellationToken);
        Assert.NotNull(approval);
        context.Repository.Mapping = context.Repository.Mapping! with
        {
            UpdatedAt = Now.AddSeconds(1),
        };

        var response = await context.Service.ApproveAsync(
            approval,
            TestContext.Current.CancellationToken);

        Assert.Equal(BrowserFillResultCode.ChangedContext, response.Result);
        Assert.Equal(0, context.Verification.Calls);
        Assert.Equal(0, context.Provider.Calls);
    }

    [Fact]
    public async Task VerificationFailureNeverReturnsAValue()
    {
        var context = CreateContext();
        context.Verification.Result = UserVerificationResult.Canceled;
        await context.Service.SaveMappingAsync(
            null,
            context.Item.Id,
            context.Identity.Id,
            "https://login.example.com",
            "https://login.example.com",
            BrowserMappingFieldPurpose.Password,
            true,
            TestContext.Current.CancellationToken);
        var approval = await context.Service.PrepareAsync(
            Request(),
            TestContext.Current.CancellationToken);
        Assert.NotNull(approval);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => context.Service.ApproveAsync(
                approval,
                TestContext.Current.CancellationToken));

        Assert.Equal(0, context.Provider.Calls);
        Assert.Equal("Denied", Assert.Single(context.Repository.Audit).Result);
    }

    [Fact]
    public async Task ExistingOriginPurposeSlotRequiresExplicitReplacement()
    {
        var context = CreateContext();
        await context.Service.SaveMappingAsync(
            null,
            context.Item.Id,
            context.Identity.Id,
            "https://login.example.com",
            "https://login.example.com",
            BrowserMappingFieldPurpose.Password,
            true,
            TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => context.Service.SaveMappingAsync(
                null,
                context.Item.Id,
                context.Identity.Id,
                "https://login.example.com",
                "https://login.example.com",
                BrowserMappingFieldPurpose.Password,
                true,
                TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task MissingMachinePolicyDeniesBeforeMappingLookupOrValueRetrieval()
    {
        var context = CreateContext(includePolicy: false);

        var approval = await context.Service.PrepareAsync(
            Request(),
            TestContext.Current.CancellationToken);

        Assert.Null(approval);
        Assert.Equal(0, context.Provider.Calls);
        Assert.Equal("DeniedPolicy", Assert.Single(context.Repository.Audit).Result);
    }

    [Fact]
    public async Task PolicyRevocationDuringConfirmationDeniesBeforeValueRetrieval()
    {
        var context = CreateContext();
        await context.Service.SaveMappingAsync(
            null,
            context.Item.Id,
            context.Identity.Id,
            "https://login.example.com",
            "https://login.example.com",
            BrowserMappingFieldPurpose.Password,
            true,
            TestContext.Current.CancellationToken);
        var approval = await context.Service.PrepareAsync(
            Request(),
            TestContext.Current.CancellationToken);
        Assert.NotNull(approval);
        context.Policy!.IsAllowed = false;

        var response = await context.Service.ApproveAsync(
            approval,
            TestContext.Current.CancellationToken);

        Assert.Equal(BrowserFillResultCode.Denied, response.Result);
        Assert.Equal(0, context.Verification.Calls);
        Assert.Equal(0, context.Provider.Calls);
        Assert.Equal("DeniedPolicy", Assert.Single(context.Repository.Audit).Result);
    }

    private static TestContextData CreateContext(bool includePolicy = true)
    {
        var identity = new ConnectedIdentity(
            Guid.NewGuid(),
            "11111111-1111-1111-1111-111111111111",
            "browser-account",
            "user@example.invalid",
            "Browser identity",
            "tenant",
            AuthenticationState.Ready,
            Now);
        var vault = new VaultResource(
            Guid.NewGuid(),
            "/resource",
            "browser-vault",
            "tenant",
            "subscription",
            "rg",
            "eastus",
            new Dictionary<string, string>(),
            new Uri("https://browser-vault.vault.azure.net/"),
            Now);
        var item = new VaultItem(
            Guid.NewGuid(),
            vault.Id,
            "browser-password",
            VaultObjectType.Secret,
            true,
            new Dictionary<string, string>(),
            null,
            Now,
            Now,
            null,
            "v1",
            "fingerprint",
            Now);
        var repository = new BrowserRepository((item, vault, identity));
        var verification = new BrowserVerification();
        var provider = new BrowserProvider();
        var clock = new BrowserClock();
        var policy = includePolicy ? new AllowBrowserFillPolicy() : null;
        var secretAccess = new SecretAccessService(
            provider,
            repository,
            new UnusedValueStore(),
            new UnusedClipboard(),
            verification,
            clock);
        return new TestContextData(
            new BrowserFillService(
                repository,
                secretAccess,
                clock,
                policy),
            repository,
            provider,
            verification,
            identity,
            item,
            policy);
    }

    private static ValidatedBrowserFillRequest Request()
    {
        var request = new BrowserFillRequest(
            BrowserProtocolConstants.CurrentVersion,
            BrowserProtocolConstants.FillOperation,
            Guid.NewGuid(),
            BrowserFamily.Chromium,
            42,
            0,
            "document-token",
            "gesture-token",
            Now,
            "https://login.example.com",
            "https://login.example.com",
            BrowserFieldPurpose.Password,
            "field-token");
        return new ValidatedBrowserFillRequest(
            request,
            CanonicalBrowserOrigin.Parse(request.TopOrigin),
            CanonicalBrowserOrigin.Parse(request.FrameOrigin));
    }

    private sealed record TestContextData(
        BrowserFillService Service,
        BrowserRepository Repository,
        BrowserProvider Provider,
        BrowserVerification Verification,
        ConnectedIdentity Identity,
        VaultItem Item,
        AllowBrowserFillPolicy? Policy);

    private sealed class BrowserClock : IClock
    {
        public DateTimeOffset UtcNow => Now;
    }

    private sealed class AllowBrowserFillPolicy : IBrowserFillPolicy
    {
        public bool IsAllowed { get; set; } = true;

        public Task<BrowserFillPolicyDecision> EvaluateAsync(
            BrowserFamily browserFamily,
            CanonicalBrowserOrigin topOrigin,
            CanonicalBrowserOrigin frameOrigin,
            BrowserMappingFieldPurpose fieldPurpose,
            CancellationToken cancellationToken) =>
            Task.FromResult(new BrowserFillPolicyDecision(
                IsAllowed,
                IsAllowed ? "Allowed by test policy." : "Denied by test policy."));

        public Task<string> GetStatusAsync(CancellationToken cancellationToken) =>
            Task.FromResult("Test policy enabled.");
    }

    private sealed class BrowserVerification : IUserVerificationService
    {
        public bool IsAvailable => true;
        public int Calls { get; private set; }
        public UserVerificationResult Result { get; set; } = UserVerificationResult.Verified;

        public Task<UserVerificationResult> VerifyAsync(
            string reason,
            CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(Result);
        }
    }

    private sealed class BrowserProvider : IVaultProvider
    {
        public int Calls { get; private set; }
        public Guid? IdentityId { get; private set; }

        public Task<DiscoverySnapshot> DiscoverAsync(
            ConnectedIdentity identity,
            IReadOnlyList<string> excludedSubscriptions,
            IReadOnlyList<string> excludedVaultResourceIds,
            CancellationToken cancellationToken) =>
            Task.FromResult(new DiscoverySnapshot([], [], [], [], [], []));

        public Task<SensitiveValue> RetrieveSecretAsync(
            ConnectedIdentity identity,
            VaultResource vault,
            VaultItem item,
            CancellationToken cancellationToken)
        {
            Calls++;
            IdentityId = identity.Id;
            return Task.FromResult(new SensitiveValue("synthetic-value"));
        }
    }

    private sealed class BrowserRepository(
        (VaultItem Item, VaultResource Vault, ConnectedIdentity Identity) source)
        : IMetadataRepository
    {
        public BrowserFillMapping? Mapping { get; set; }
        public List<BrowserFillAuditEvent> Audit { get; } = [];

        public Task InitializeAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<IReadOnlyList<ConnectedIdentity>> GetIdentitiesAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ConnectedIdentity>>([source.Identity]);
        public Task<ConnectedIdentity?> GetIdentityAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult<ConnectedIdentity?>(id == source.Identity.Id ? source.Identity : null);
        public Task UpsertIdentityAsync(ConnectedIdentity identity, CancellationToken cancellationToken) =>
            Task.CompletedTask;
        public Task RemoveIdentityAsync(Guid id, CancellationToken cancellationToken) =>
            Task.CompletedTask;
        public Task<IReadOnlyList<TenantAccess>> GetTenantsAsync(Guid identityId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<TenantAccess>>([]);
        public Task<IReadOnlyList<SubscriptionAccess>> GetSubscriptionsAsync(Guid identityId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<SubscriptionAccess>>([]);
        public Task SetSubscriptionSelectedAsync(Guid subscriptionAccessId, bool isSelected, CancellationToken cancellationToken) =>
            Task.CompletedTask;
        public Task<IReadOnlyList<VaultAccessSummary>> GetVaultAccessSummariesAsync(Guid identityId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<VaultAccessSummary>>([]);
        public Task<IReadOnlyList<Guid>> GetVaultIdsForIdentityAsync(Guid identityId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<Guid>>([]);
        public Task SetVaultSelectedAsync(Guid vaultAccessId, bool isSelected, CancellationToken cancellationToken) =>
            Task.CompletedTask;
        public Task ApplyDiscoveryAsync(Guid identityId, DiscoverySnapshot snapshot, SyncRun run, CancellationToken cancellationToken) =>
            Task.CompletedTask;
        public Task<IReadOnlyList<SearchResult>> SearchAsync(SearchRequest request, DateTimeOffset now, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<SearchResult>>([]);
        public Task<(VaultItem Item, VaultResource Vault, ConnectedIdentity Identity)?> ResolveItemAsync(Guid itemId, CancellationToken cancellationToken) =>
            Task.FromResult<(VaultItem, VaultResource, ConnectedIdentity)?>(itemId == source.Item.Id ? source : null);
        public Task<(VaultItem Item, VaultResource Vault, ConnectedIdentity Identity)?> ResolveItemForIdentityAsync(Guid itemId, Guid identityId, CancellationToken cancellationToken) =>
            Task.FromResult<(VaultItem, VaultResource, ConnectedIdentity)?>(
                itemId == source.Item.Id && identityId == source.Identity.Id ? source : null);
        public Task RecordAccessAsync(Guid itemId, DateTimeOffset accessedAt, CancellationToken cancellationToken) =>
            Task.CompletedTask;
        public Task SetFavoriteAsync(Guid itemId, bool isFavorite, CancellationToken cancellationToken) =>
            Task.CompletedTask;
        public Task<IReadOnlyList<Workspace>> GetWorkspacesAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<Workspace>>([]);
        public Task UpsertWorkspaceAsync(Workspace workspace, CancellationToken cancellationToken) =>
            Task.CompletedTask;
        public Task RemoveWorkspaceAsync(Guid id, CancellationToken cancellationToken) =>
            Task.CompletedTask;
        public Task AddWorkspaceLinkAsync(WorkspaceResourceLink link, CancellationToken cancellationToken) =>
            Task.CompletedTask;
        public Task RemoveWorkspaceLinkAsync(Guid workspaceId, ResourceLinkType resourceType, string resourceId, CancellationToken cancellationToken) =>
            Task.CompletedTask;
        public Task<IReadOnlyList<BrowserFillMapping>> GetBrowserFillMappingsAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<BrowserFillMapping>>(Mapping is null ? [] : [Mapping]);
        public Task<BrowserFillMapping?> GetBrowserFillMappingAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult<BrowserFillMapping?>(Mapping?.Id == id ? Mapping : null);
        public Task<BrowserFillMapping?> FindBrowserFillMappingAsync(string topOrigin, string frameOrigin, BrowserMappingFieldPurpose fieldPurpose, CancellationToken cancellationToken) =>
            Task.FromResult<BrowserFillMapping?>(
                Mapping is not null &&
                Mapping.TopOrigin == topOrigin &&
                Mapping.FrameOrigin == frameOrigin &&
                Mapping.FieldPurpose == fieldPurpose
                    ? Mapping
                    : null);
        public Task UpsertBrowserFillMappingAsync(BrowserFillMapping mapping, CancellationToken cancellationToken)
        {
            Mapping = mapping;
            return Task.CompletedTask;
        }
        public Task RemoveBrowserFillMappingAsync(Guid id, CancellationToken cancellationToken)
        {
            if (Mapping?.Id == id)
                Mapping = null;
            return Task.CompletedTask;
        }
        public Task RecordBrowserFillAuditAsync(BrowserFillAuditEvent auditEvent, CancellationToken cancellationToken)
        {
            Audit.Add(auditEvent);
            return Task.CompletedTask;
        }
        public Task<IReadOnlyList<BrowserFillAuditEvent>> GetBrowserFillAuditAsync(int limit, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<BrowserFillAuditEvent>>(Audit.Take(limit).ToArray());
    }

    private sealed class UnusedValueStore : IProtectedValueStore
    {
        public Task<CachedSecretDescriptor> StoreAsync(Guid vaultItemId, Guid vaultId, Guid? workspaceId, SensitiveValue value, string fingerprint, DateTimeOffset expiresAt, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public Task<SensitiveValue?> RetrieveAsync(Guid vaultItemId, DateTimeOffset now, string? expectedFingerprint, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public Task PurgeItemAsync(Guid vaultItemId, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task PurgeVaultAsync(Guid vaultId, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task PurgeWorkspaceAsync(Guid workspaceId, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task PurgeAllAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class UnusedClipboard : IClipboardService
    {
        public Task CopyWithAutoClearAsync(SensitiveValue value, TimeSpan clearAfter, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
