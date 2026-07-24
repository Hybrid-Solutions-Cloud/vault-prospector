namespace VaultProspector.Domain;

public enum AuthenticationState { Unknown, Ready, InteractionRequired, Disabled, Revoked }
public enum VaultObjectType { Secret, Key, Certificate }
public enum SyncStatus { Running, Completed, CompletedWithErrors, Cancelled, Failed }
public enum ResourceLinkType { Identity, Tenant, Subscription, Vault }
public enum BrowserMappingFieldPurpose { Username, Password, OneTimeCode }
public enum IdentityType
{
    InteractiveUser = 0,
    ManagedIdentity = 1,
    ServicePrincipal = 2,
    FederatedServicePrincipal = 3,
}

public sealed record ConnectedIdentity(
    Guid Id,
    string ClientId,
    string AccountIdentifier,
    string UsernameHint,
    string DisplayName,
    string HomeTenantId,
    AuthenticationState AuthenticationState,
    DateTimeOffset LastInteractiveAuthentication,
    bool IsEnabled = true,
    IdentityType Type = IdentityType.InteractiveUser,
    string CredentialData = "");

public sealed record TenantAccess(
    Guid Id,
    Guid ConnectedIdentityId,
    string TenantId,
    string DisplayName,
    string TenantType,
    DateTimeOffset LastValidatedAt,
    string Status);

public sealed record SubscriptionAccess(
    Guid Id,
    Guid TenantAccessId,
    string SubscriptionId,
    string DisplayName,
    string State,
    bool IsSelected,
    DateTimeOffset LastDiscoveredAt);

public sealed record VaultResource(
    Guid Id,
    string ProviderResourceId,
    string Name,
    string TenantId,
    string SubscriptionId,
    string ResourceGroup,
    string Location,
    IReadOnlyDictionary<string, string> Tags,
    Uri VaultUri,
    DateTimeOffset LastIndexedAt);

public sealed record VaultAccess(
    Guid Id,
    Guid VaultId,
    Guid ConnectedIdentityId,
    string TenantId,
    string AccessStatus,
    DateTimeOffset LastValidatedAt,
    string? LastFailureCategory,
    int PreferredRank,
    bool IsSelected = true);

public sealed record VaultAccessSummary(
    VaultResource Vault,
    VaultAccess Access,
    string IdentityDisplayName,
    string TenantDisplayName);

public sealed record VaultItem(
    Guid Id,
    Guid VaultId,
    string ProviderObjectName,
    VaultObjectType ObjectType,
    bool Enabled,
    IReadOnlyDictionary<string, string> Tags,
    string? ContentType,
    DateTimeOffset? CreatedAt,
    DateTimeOffset? UpdatedAt,
    DateTimeOffset? ExpiresAt,
    string ProviderVersion,
    string MetadataFingerprint,
    DateTimeOffset LastIndexedAt,
    bool IsDeletedOrUnavailable = false);

public sealed record Workspace(
    Guid Id,
    string Name,
    string Description,
    int SortOrder,
    CachePolicy? CachePolicyOverride = null);

public sealed record WorkspaceResourceLink(
    Guid Id,
    Guid WorkspaceId,
    ResourceLinkType ResourceType,
    string ResourceId);

public sealed record BrowserFillMapping(
    Guid Id,
    Guid VaultItemId,
    Guid ConnectedIdentityId,
    string TopOrigin,
    string FrameOrigin,
    BrowserMappingFieldPurpose FieldPurpose,
    bool IsEnabled,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record BrowserFillAuditEvent(
    Guid Id,
    DateTimeOffset OccurredAt,
    Guid? MappingId,
    Guid? VaultItemId,
    Guid? ConnectedIdentityId,
    string TopOrigin,
    string FrameOrigin,
    BrowserMappingFieldPurpose FieldPurpose,
    string Result);

public sealed record SyncRun(
    Guid Id,
    string Scope,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt,
    SyncStatus Status,
    int VaultCount,
    int ItemCount,
    IReadOnlyList<string> NonSensitiveErrors,
    string? CancellationReason = null);

public sealed record CachePolicy(
    bool IsEnabled,
    TimeSpan MaximumLifetime,
    bool RequireLocalUnlock,
    bool AllowClipboard)
{
    public static CachePolicy SecureDefault { get; } = new(false, TimeSpan.FromHours(8), true, true);

    public DateTimeOffset GetExpiration(DateTimeOffset now, TimeSpan requestedLifetime)
    {
        if (!IsEnabled) throw new InvalidOperationException("Offline caching is disabled by policy.");
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(requestedLifetime, TimeSpan.Zero);
        return now.Add(requestedLifetime <= MaximumLifetime ? requestedLifetime : MaximumLifetime);
    }
}

public sealed record CachedSecretDescriptor(
    Guid Id,
    Guid VaultItemId,
    Guid VaultId,
    Guid? WorkspaceId,
    DateTimeOffset CachedAt,
    DateTimeOffset ExpiresAt,
    DateTimeOffset? LastUnlockedAt,
    string SourceMetadataFingerprint);

public sealed record SearchRequest(
    string Text = "",
    Guid? WorkspaceId = null,
    Guid? IdentityId = null,
    string? TenantId = null,
    string? SubscriptionId = null,
    Guid? VaultId = null,
    string? VaultName = null,
    VaultObjectType? ObjectType = null,
    bool? Enabled = null,
    bool FavoritesOnly = false,
    bool ExpiredOnly = false,
    bool StaleOnly = false,
    bool RecentlyAccessedFirst = false,
    int Limit = 250);

public sealed record SearchResult(
    VaultItem Item,
    VaultResource Vault,
    string IdentityDisplayName,
    string TenantDisplayName,
    bool IsFavorite,
    DateTimeOffset? LastAccessedAt,
    bool IsStale,
    string AccessStatus = "Permission assessment unavailable");

public sealed record ProviderError(string Scope, string Category, string SafeMessage);

public sealed record DiscoverySnapshot(
    IReadOnlyList<TenantAccess> Tenants,
    IReadOnlyList<SubscriptionAccess> Subscriptions,
    IReadOnlyList<VaultResource> Vaults,
    IReadOnlyList<VaultAccess> AccessPaths,
    IReadOnlyList<VaultItem> Items,
    IReadOnlyList<ProviderError> Errors);
