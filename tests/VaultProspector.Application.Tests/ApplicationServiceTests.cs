using VaultProspector.Application;
using VaultProspector.Domain;

namespace VaultProspector.Application.Tests;

public sealed class ApplicationServiceTests
{
    [Fact]
    public async Task IdentityAdditionRejectsInvalidClientIdBeforeAuthentication()
    {
        var provider = new FakeIdentityProvider();
        var service = new IdentityService(provider, new FakeRepository(Identity()));

        await Assert.ThrowsAsync<ArgumentException>(() => service.AddAsync(
            "../../outside-cache",
            "Test",
            TestContext.Current.CancellationToken));

        Assert.Equal(0, provider.SignInCalls);
    }

    [Fact]
    public async Task IdentityAdditionCanonicalizesClientIdBeforeAuthentication()
    {
        var provider = new FakeIdentityProvider();
        var repository = new FakeRepository(Identity());
        var service = new IdentityService(provider, repository);

        await service.AddAsync(
            "{11111111-1111-1111-1111-111111111111}",
            " Test ",
            TestContext.Current.CancellationToken);

        Assert.Equal("11111111-1111-1111-1111-111111111111", provider.ClientId);
        Assert.Equal("Test", provider.DisplayName);
        Assert.NotNull(repository.UpsertedIdentity);
    }

    [Fact]
    public async Task SynchronizationPersistsPartialSuccessAndSafeErrors()
    {
        var identity = Identity();
        var repository = new FakeRepository(identity);
        var provider = new FakeProvider { Snapshot = new DiscoverySnapshot([], [], [], [], [], [new ProviderError("scope", "Forbidden", "Azure request failed with status 403.")]) };
        var service = new SynchronizationService(provider, repository, new FixedClock(), new FakeDiagnostics());

        var run = await service.SynchronizeAsync(identity, TestContext.Current.CancellationToken);

        Assert.Equal(SyncStatus.CompletedWithErrors, run.Status);
        Assert.NotNull(repository.AppliedSnapshot);
        Assert.Single(run.NonSensitiveErrors);
    }

    [Fact]
    public async Task SecretRetrievalRejectsKeysWithoutCallingProvider()
    {
        var identity = Identity();
        var item = Item(VaultObjectType.Key);
        var repository = new FakeRepository(identity) { Resolved = (item, Vault(), identity) };
        var provider = new FakeProvider();
        var service = new SecretAccessService(provider, repository, new FakeValueStore(), new FakeClipboard(), new AlwaysVerify(), new FixedClock());

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.RetrieveAsync(item.Id, TestContext.Current.CancellationToken));
        Assert.Equal(0, provider.RetrieveCalls);
    }

    [Fact]
    public async Task SecretRetrievalRequiresLocalVerification()
    {
        var identity = Identity();
        var item = Item(VaultObjectType.Secret);
        var repository = new FakeRepository(identity) { Resolved = (item, Vault(), identity) };
        var service = new SecretAccessService(new FakeProvider(), repository, new FakeValueStore(), new FakeClipboard(), new NeverVerify(), new FixedClock());
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.RetrieveAsync(item.Id, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SecretRetrievalDisposesValueWhenAccessHistoryCannotBeRecorded()
    {
        var identity = Identity();
        var item = Item(VaultObjectType.Secret);
        var repository = new FakeRepository(identity)
        {
            Resolved = (item, Vault(), identity),
            RecordAccessException = new InvalidOperationException("metadata unavailable"),
        };
        var provider = new FakeProvider();
        var service = new SecretAccessService(provider, repository, new FakeValueStore(), new FakeClipboard(), new AlwaysVerify(), new FixedClock());

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.RetrieveAsync(item.Id, TestContext.Current.CancellationToken));

        Assert.NotNull(provider.LastRetrievedValue);
        Assert.True(provider.LastRetrievedValue.IsDisposed);
    }

    [Fact]
    public async Task CopyRequiresVerificationBeforeAzureOrClipboardAccess()
    {
        var identity = Identity();
        var item = Item(VaultObjectType.Secret);
        var repository = new FakeRepository(identity) { Resolved = (item, Vault(), identity) };
        var provider = new FakeProvider();
        var clipboard = new FakeClipboard();
        var service = new SecretAccessService(provider, repository, new FakeValueStore(), clipboard, new NeverVerify(), new FixedClock());

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.RetrieveAndCopyAsync(
            item.Id,
            TimeSpan.FromSeconds(30),
            new CachePolicy(false, TimeSpan.FromHours(8), true, true),
            TestContext.Current.CancellationToken));

        Assert.Equal(0, provider.RetrieveCalls);
        Assert.Equal(0, clipboard.CopyCalls);
    }

    [Fact]
    public async Task CopyUsesOneVerificationAndCopiesOneRetrievedValue()
    {
        var identity = Identity();
        var item = Item(VaultObjectType.Secret);
        var repository = new FakeRepository(identity) { Resolved = (item, Vault(), identity) };
        var provider = new FakeProvider();
        var clipboard = new FakeClipboard();
        var verification = new CountingVerify();
        var service = new SecretAccessService(provider, repository, new FakeValueStore(), clipboard, verification, new FixedClock());

        await service.RetrieveAndCopyAsync(
            item.Id,
            TimeSpan.FromSeconds(30),
            new CachePolicy(false, TimeSpan.FromHours(8), true, true),
            TestContext.Current.CancellationToken);

        Assert.Equal(1, verification.Calls);
        Assert.Equal(1, provider.RetrieveCalls);
        Assert.Equal(1, clipboard.CopyCalls);
        Assert.Equal("value", clipboard.CopiedValue);
    }

    [Fact]
    public async Task DisabledOfflinePolicyRejectsBeforeVerificationOrAzureRetrieval()
    {
        var identity = Identity();
        var item = Item(VaultObjectType.Secret);
        var repository = new FakeRepository(identity) { Resolved = (item, Vault(), identity) };
        var provider = new FakeProvider();
        var verification = new CountingVerify();
        var store = new FakeValueStore();
        var service = new SecretAccessService(provider, repository, store, new FakeClipboard(), verification, new FixedClock());

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.RetrieveAndCacheAsync(
            item.Id,
            null,
            TimeSpan.FromHours(1),
            CachePolicy.SecureDefault,
            TestContext.Current.CancellationToken));

        Assert.Equal(0, verification.Calls);
        Assert.Equal(0, provider.RetrieveCalls);
        Assert.Equal(0, store.StoreCalls);
    }

    [Fact]
    public async Task OfflineRetrievalRequiresVerificationAndDoesNotCallAzure()
    {
        var identity = Identity();
        var item = Item(VaultObjectType.Secret);
        var repository = new FakeRepository(identity) { Resolved = (item, Vault(), identity) };
        var provider = new FakeProvider();
        var store = new FakeValueStore { Value = "offline-value" };
        var service = new SecretAccessService(provider, repository, store, new FakeClipboard(), new AlwaysVerify(), new FixedClock());

        using var value = await service.RetrieveCachedAsync(item.Id, TestContext.Current.CancellationToken);

        Assert.Equal("offline-value", value.Reveal());
        Assert.Equal(0, provider.RetrieveCalls);
        Assert.Equal(1, store.RetrieveCalls);
    }

    [Fact]
    public async Task OfflineRetrievalFailsClosedWhenVerificationIsRejected()
    {
        var identity = Identity();
        var item = Item(VaultObjectType.Secret);
        var store = new FakeValueStore { Value = "offline-value" };
        var repository = new FakeRepository(identity) { Resolved = (item, Vault(), identity) };
        var service = new SecretAccessService(new FakeProvider(), repository, store, new FakeClipboard(), new NeverVerify(), new FixedClock());

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.RetrieveCachedAsync(item.Id, TestContext.Current.CancellationToken));

        Assert.Equal(0, store.RetrieveCalls);
    }

    [Fact]
    public async Task OfflineRetrievalRejectsNonSecretMetadataBeforeVerificationOrCacheAccess()
    {
        var identity = Identity();
        var item = Item(VaultObjectType.Key);
        var store = new FakeValueStore { Value = "offline-value" };
        var verification = new CountingVerify();
        var repository = new FakeRepository(identity) { Resolved = (item, Vault(), identity) };
        var service = new SecretAccessService(new FakeProvider(), repository, store, new FakeClipboard(), verification, new FixedClock());

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.RetrieveCachedAsync(item.Id, TestContext.Current.CancellationToken));

        Assert.Equal(0, verification.Calls);
        Assert.Equal(0, store.RetrieveCalls);
    }

    [Fact]
    public async Task RetrieveAndCacheUsesOneVerificationAndDisposesTheProviderValue()
    {
        var identity = Identity();
        var item = Item(VaultObjectType.Secret);
        var repository = new FakeRepository(identity) { Resolved = (item, Vault(), identity) };
        var provider = new FakeProvider();
        var store = new FakeValueStore();
        var verification = new CountingVerify();
        var service = new SecretAccessService(provider, repository, store, new FakeClipboard(), verification, new FixedClock());

        await service.RetrieveAndCacheAsync(item.Id, null, TimeSpan.FromHours(1), new CachePolicy(true, TimeSpan.FromHours(2), true, true), TestContext.Current.CancellationToken);

        Assert.Equal(1, verification.Calls);
        Assert.Equal(1, provider.RetrieveCalls);
        Assert.Equal(1, store.StoreCalls);
        Assert.Equal("value", store.StoredValue);
        Assert.Equal("fingerprint", store.StoredFingerprint);
    }

    [Fact]
    public async Task OfflineCachingCannotDisableLocalVerificationThroughPolicy()
    {
        var identity = Identity();
        var item = Item(VaultObjectType.Secret);
        var repository = new FakeRepository(identity) { Resolved = (item, Vault(), identity) };
        var provider = new FakeProvider();
        var store = new FakeValueStore();
        var service = new SecretAccessService(provider, repository, store, new FakeClipboard(), new NeverVerify(), new FixedClock());

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.RetrieveAndCacheAsync(
            item.Id,
            null,
            TimeSpan.FromHours(1),
            new CachePolicy(true, TimeSpan.FromHours(2), false, true),
            TestContext.Current.CancellationToken));

        Assert.Equal(0, provider.RetrieveCalls);
        Assert.Equal(0, store.StoreCalls);
    }

    [Fact]
    public async Task SynchronizationReportsUserCancellationWithoutPersistingPartialState()
    {
        var identity = Identity();
        var repository = new FakeRepository(identity);
        var provider = new FakeProvider { HonorCancellation = true };
        var service = new SynchronizationService(provider, repository, new FixedClock(), new FakeDiagnostics());
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var run = await service.SynchronizeAsync(identity, cancellation.Token);

        Assert.Equal(SyncStatus.Cancelled, run.Status);
        Assert.Null(repository.AppliedSnapshot);
    }

    [Fact]
    public async Task WorkspaceLinksUseExplicitResourceTypeAndIdentifier()
    {
        var repository = new FakeRepository(Identity());
        var service = new WorkspaceService(repository);
        var workspaceId = Guid.NewGuid();
        var vaultId = Guid.NewGuid().ToString("D");

        await service.AddResourceAsync(workspaceId, ResourceLinkType.Vault, vaultId, TestContext.Current.CancellationToken);

        Assert.NotNull(repository.AddedLink);
        Assert.Equal(workspaceId, repository.AddedLink.WorkspaceId);
        Assert.Equal(ResourceLinkType.Vault, repository.AddedLink.ResourceType);
        Assert.Equal(vaultId, repository.AddedLink.ResourceId);
    }

    private static ConnectedIdentity Identity() => new(Guid.NewGuid(), "11111111-1111-1111-1111-111111111111", "account", "user@example.invalid", "Test", "tenant", AuthenticationState.Ready, DateTimeOffset.UtcNow);
    private static VaultResource Vault() => new(Guid.NewGuid(), "/subscriptions/redacted/resourceGroups/rg/providers/Microsoft.KeyVault/vaults/test", "test", "tenant", "subscription", "rg", "eastus", new Dictionary<string, string>(), new Uri("https://test.vault.azure.net/"), DateTimeOffset.UtcNow);
    private static VaultItem Item(VaultObjectType type) { var vault = Vault(); return new(Guid.NewGuid(), vault.Id, "item", type, true, new Dictionary<string, string>(), null, null, null, null, "version", "fingerprint", DateTimeOffset.UtcNow); }

    private sealed class FixedClock : IClock { public DateTimeOffset UtcNow => DateTimeOffset.Parse("2026-07-16T12:00:00Z", System.Globalization.CultureInfo.InvariantCulture); }
    private sealed class FakeProvider : IVaultProvider
    {
        public DiscoverySnapshot Snapshot { get; set; } = new([], [], [], [], [], []);
        public bool HonorCancellation { get; init; }
        public int RetrieveCalls { get; private set; }
        public SensitiveValue? LastRetrievedValue { get; private set; }
        public Task<DiscoverySnapshot> DiscoverAsync(ConnectedIdentity identity, CancellationToken cancellationToken)
        {
            if (HonorCancellation) cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(Snapshot);
        }
        public Task<SensitiveValue> RetrieveSecretAsync(ConnectedIdentity identity, VaultResource vault, VaultItem item, CancellationToken cancellationToken)
        {
            RetrieveCalls++;
            LastRetrievedValue = new SensitiveValue("value");
            return Task.FromResult(LastRetrievedValue);
        }
    }
    private sealed class FakeRepository(ConnectedIdentity identity) : IMetadataRepository
    {
        public DiscoverySnapshot? AppliedSnapshot { get; private set; }
        public WorkspaceResourceLink? AddedLink { get; private set; }
        public ConnectedIdentity? UpsertedIdentity { get; private set; }
        public (VaultItem Item, VaultResource Vault, ConnectedIdentity Identity)? Resolved { get; set; }
        public Exception? RecordAccessException { get; init; }
        public Task InitializeAsync(CancellationToken c) => Task.CompletedTask;
        public Task<IReadOnlyList<ConnectedIdentity>> GetIdentitiesAsync(CancellationToken c) => Task.FromResult<IReadOnlyList<ConnectedIdentity>>([identity]);
        public Task<ConnectedIdentity?> GetIdentityAsync(Guid id, CancellationToken c) => Task.FromResult<ConnectedIdentity?>(identity);
        public Task UpsertIdentityAsync(ConnectedIdentity x, CancellationToken c) { UpsertedIdentity = x; return Task.CompletedTask; }
        public Task RemoveIdentityAsync(Guid id, CancellationToken c) => Task.CompletedTask;
        public Task ApplyDiscoveryAsync(Guid id, DiscoverySnapshot snapshot, SyncRun run, CancellationToken c) { AppliedSnapshot = snapshot; return Task.CompletedTask; }
        public Task<IReadOnlyList<SearchResult>> SearchAsync(SearchRequest r, DateTimeOffset n, CancellationToken c) => Task.FromResult<IReadOnlyList<SearchResult>>([]);
        public Task<(VaultItem Item, VaultResource Vault, ConnectedIdentity Identity)?> ResolveItemAsync(Guid id, CancellationToken c) => Task.FromResult(Resolved);
        public Task RecordAccessAsync(Guid id, DateTimeOffset at, CancellationToken c) => RecordAccessException is null
            ? Task.CompletedTask
            : Task.FromException(RecordAccessException);
        public Task SetFavoriteAsync(Guid id, bool favorite, CancellationToken c) => Task.CompletedTask;
        public Task<IReadOnlyList<Workspace>> GetWorkspacesAsync(CancellationToken c) => Task.FromResult<IReadOnlyList<Workspace>>([]);
        public Task UpsertWorkspaceAsync(Workspace w, CancellationToken c) => Task.CompletedTask;
        public Task RemoveWorkspaceAsync(Guid id, CancellationToken c) => Task.CompletedTask;
        public Task AddWorkspaceLinkAsync(WorkspaceResourceLink link, CancellationToken c) { AddedLink = link; return Task.CompletedTask; }
        public Task RemoveWorkspaceLinkAsync(Guid workspaceId, ResourceLinkType resourceType, string resourceId, CancellationToken c) => Task.CompletedTask;
    }
    private sealed class FakeIdentityProvider : IIdentityProvider
    {
        public int SignInCalls { get; private set; }
        public string? ClientId { get; private set; }
        public string? DisplayName { get; private set; }
        public Task<ConnectedIdentity> SignInAsync(string clientId, string displayName, CancellationToken cancellationToken)
        {
            SignInCalls++;
            ClientId = clientId;
            DisplayName = displayName;
            return Task.FromResult(new ConnectedIdentity(Guid.NewGuid(), clientId, "account", "user@example.invalid", displayName, "tenant", AuthenticationState.Ready, DateTimeOffset.UtcNow));
        }
        public Task RemoveAsync(ConnectedIdentity identity, CancellationToken cancellationToken) => Task.CompletedTask;
    }
    private sealed class FakeDiagnostics : IDiagnosticSink { public void Information(string e, IReadOnlyDictionary<string, object?> f) { } public void WriteError(string e, Exception x, IReadOnlyDictionary<string, object?> f) { } }
    private sealed class AlwaysVerify : IUserVerificationService { public bool IsAvailable => true; public Task<bool> VerifyAsync(string r, CancellationToken c) => Task.FromResult(true); }
    private sealed class NeverVerify : IUserVerificationService { public bool IsAvailable => true; public Task<bool> VerifyAsync(string r, CancellationToken c) => Task.FromResult(false); }
    private sealed class CountingVerify : IUserVerificationService
    {
        public bool IsAvailable => true;
        public int Calls { get; private set; }
        public Task<bool> VerifyAsync(string r, CancellationToken c) { Calls++; return Task.FromResult(true); }
    }
    private sealed class FakeClipboard : IClipboardService
    {
        public int CopyCalls { get; private set; }
        public string? CopiedValue { get; private set; }
        public Task CopyWithAutoClearAsync(SensitiveValue v, TimeSpan d, CancellationToken c)
        {
            CopyCalls++;
            CopiedValue = v.Reveal();
            return Task.CompletedTask;
        }
    }
    private sealed class FakeValueStore : IProtectedValueStore
    {
        public string? Value { get; init; }
        public int RetrieveCalls { get; private set; }
        public int StoreCalls { get; private set; }
        public string? StoredValue { get; private set; }
        public string? StoredFingerprint { get; private set; }
        public Task<CachedSecretDescriptor> StoreAsync(Guid i, Guid v, Guid? w, SensitiveValue s, string f, DateTimeOffset e, CancellationToken c)
        {
            StoreCalls++;
            StoredValue = s.Reveal();
            StoredFingerprint = f;
            return Task.FromResult(new CachedSecretDescriptor(Guid.NewGuid(), i, v, w, DateTimeOffset.MinValue, e, null, f));
        }
        public Task<SensitiveValue?> RetrieveAsync(Guid i, DateTimeOffset n, string? f, CancellationToken c) { RetrieveCalls++; return Task.FromResult(Value is null ? null : new SensitiveValue(Value)); }
        public Task PurgeItemAsync(Guid i, CancellationToken c) => Task.CompletedTask;
        public Task PurgeVaultAsync(Guid i, CancellationToken c) => Task.CompletedTask;
        public Task PurgeWorkspaceAsync(Guid i, CancellationToken c) => Task.CompletedTask;
        public Task PurgeAllAsync(CancellationToken c) => Task.CompletedTask;
    }
}
