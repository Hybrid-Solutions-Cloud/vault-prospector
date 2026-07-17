using VaultProspector.Domain;

namespace VaultProspector.Application;

public interface IClock { DateTimeOffset UtcNow { get; } }

public interface IIdentityProvider
{
    Task<ConnectedIdentity> SignInAsync(string clientId, string displayName, CancellationToken cancellationToken);
    Task RemoveAsync(ConnectedIdentity identity, CancellationToken cancellationToken);
}

public interface IVaultProvider
{
    Task<DiscoverySnapshot> DiscoverAsync(ConnectedIdentity identity, CancellationToken cancellationToken);
    Task<SensitiveValue> RetrieveSecretAsync(ConnectedIdentity identity, VaultResource vault, VaultItem item, CancellationToken cancellationToken);
}

public interface IMetadataRepository
{
    Task InitializeAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<ConnectedIdentity>> GetIdentitiesAsync(CancellationToken cancellationToken);
    Task<ConnectedIdentity?> GetIdentityAsync(Guid id, CancellationToken cancellationToken);
    Task UpsertIdentityAsync(ConnectedIdentity identity, CancellationToken cancellationToken);
    Task RemoveIdentityAsync(Guid id, CancellationToken cancellationToken);
    Task ApplyDiscoveryAsync(Guid identityId, DiscoverySnapshot snapshot, SyncRun run, CancellationToken cancellationToken);
    Task<IReadOnlyList<SearchResult>> SearchAsync(SearchRequest request, DateTimeOffset now, CancellationToken cancellationToken);
    Task<(VaultItem Item, VaultResource Vault, ConnectedIdentity Identity)?> ResolveItemAsync(Guid itemId, CancellationToken cancellationToken);
    Task RecordAccessAsync(Guid itemId, DateTimeOffset accessedAt, CancellationToken cancellationToken);
    Task SetFavoriteAsync(Guid itemId, bool isFavorite, CancellationToken cancellationToken);
    Task<IReadOnlyList<Workspace>> GetWorkspacesAsync(CancellationToken cancellationToken);
    Task UpsertWorkspaceAsync(Workspace workspace, CancellationToken cancellationToken);
    Task RemoveWorkspaceAsync(Guid id, CancellationToken cancellationToken);
    Task AddWorkspaceLinkAsync(WorkspaceResourceLink link, CancellationToken cancellationToken);
    Task RemoveWorkspaceLinkAsync(Guid workspaceId, ResourceLinkType resourceType, string resourceId, CancellationToken cancellationToken);
}

public interface IProtectedValueStore
{
    Task<CachedSecretDescriptor> StoreAsync(Guid vaultItemId, Guid vaultId, Guid? workspaceId, SensitiveValue value, string fingerprint, DateTimeOffset expiresAt, CancellationToken cancellationToken);
    Task<SensitiveValue?> RetrieveAsync(Guid vaultItemId, DateTimeOffset now, string? expectedFingerprint, CancellationToken cancellationToken);
    Task PurgeItemAsync(Guid vaultItemId, CancellationToken cancellationToken);
    Task PurgeVaultAsync(Guid vaultId, CancellationToken cancellationToken);
    Task PurgeWorkspaceAsync(Guid workspaceId, CancellationToken cancellationToken);
    Task PurgeAllAsync(CancellationToken cancellationToken);
}

public interface IClipboardService
{
    Task CopyWithAutoClearAsync(SensitiveValue value, TimeSpan clearAfter, CancellationToken cancellationToken);
    Task ClearIfOwnedAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

public interface IUserVerificationService
{
    bool IsAvailable { get; }
    Task<bool> VerifyAsync(string reason, CancellationToken cancellationToken);
}

public interface IKeyMaterialProvider
{
    bool IsAvailable { get; }
    Task<byte[]> GetOrCreateKeyAsync(string purpose, CancellationToken cancellationToken);
}

public interface IDiagnosticSink
{
    void Information(string eventName, IReadOnlyDictionary<string, object?> fields);
    void WriteError(string eventName, Exception exception, IReadOnlyDictionary<string, object?> fields);
}
