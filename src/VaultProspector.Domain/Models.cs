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
    string Status,
    bool IsSelected = true);

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
    string? CancellationReason = null,
    IReadOnlyList<SyncErrorDetail>? ErrorDetails = null);

public sealed record SyncErrorDetail(
    string Scope,
    string Category,
    string Message,
    string Recovery,
    Guid? RunId = null,
    DateTimeOffset? OccurredAt = null,
    string CorrelationId = "",
    ProviderRetryScope? RetryScope = null);

public sealed record ProviderRetryScope(
    string? SubscriptionId = null,
    string? VaultResourceId = null);

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

public sealed record ProviderError(
    string Scope,
    string Category,
    string SafeMessage,
    ProviderRetryScope? RetryScope = null);

public enum GovernedAzureOperation
{
    CreateSecret,
    CreateSecretVersion,
    CreateSoftwareKeyVersion,
    StartCertificatePolicy,
}

public enum GovernedMutationAuditResult
{
    Prepared,
    Denied,
    Started,
    Succeeded,
    Failed,
    Conflict,
}

public sealed record GovernedMutationPreview(
    Guid Id,
    GovernedAzureOperation Operation,
    Guid IdentityId,
    string IdentityDisplayName,
    string TenantId,
    string SubscriptionId,
    string ResourceGroup,
    string VaultResourceId,
    Uri VaultUri,
    string VaultName,
    string ObjectName,
    string ExpectedEffect,
    string ExpectedCurrentVersion,
    string RecoveryGuidance,
    string ConfirmationPhrase,
    DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt);

public sealed record GovernedMutationResult(
    GovernedAzureOperation Operation,
    string ObjectName,
    string ProviderVersion,
    string SafeStatus,
    string RecoveryGuidance);

public sealed record GovernedMutationAuditEvent(
    Guid Id,
    Guid PreviewId,
    DateTimeOffset OccurredAt,
    GovernedAzureOperation Operation,
    Guid IdentityId,
    string TenantId,
    string SubscriptionId,
    string VaultResourceId,
    string ObjectNameHash,
    int SensitiveValueLength,
    GovernedMutationAuditResult Result,
    string ProviderVersion,
    string SafeMessage,
    string PreviousHash,
    string RecordHash);

public sealed record DiscoverySnapshot(
    IReadOnlyList<TenantAccess> Tenants,
    IReadOnlyList<SubscriptionAccess> Subscriptions,
    IReadOnlyList<VaultResource> Vaults,
    IReadOnlyList<VaultAccess> AccessPaths,
    IReadOnlyList<VaultItem> Items,
    IReadOnlyList<ProviderError> Errors);

public enum CyberArkAuthenticationState
{
    Unknown,
    Ready,
    Disabled,
    Revoked,
    Failed,
}

public enum CyberArkSecretType
{
    Password,
    Key,
    Unknown,
}

public enum CyberArkAuditResult
{
    Succeeded,
    Denied,
    Failed,
}

public sealed record CyberArkProfile(
    Guid Id,
    string DisplayName,
    Uri IdentityUrl,
    Uri PrivilegeCloudUrl,
    string ServiceUserName,
    string ApplicationName,
    CyberArkAuthenticationState AuthenticationState,
    bool IsEnabled,
    DateTimeOffset CredentialUpdatedAt,
    DateTimeOffset? LastValidatedAt);

public sealed record CyberArkSafe(
    Guid ProfileId,
    string SafeId,
    string Name,
    string Description,
    string Location,
    int? RetentionDays,
    int? RetentionVersions,
    bool ObjectLevelAccessControlEnabled,
    DateTimeOffset? CreatedAt,
    DateTimeOffset? UpdatedAt);

public sealed record CyberArkAccount(
    Guid ProfileId,
    string AccountId,
    string SafeName,
    string Name,
    string? UserName,
    string? Address,
    string? PlatformId,
    CyberArkSecretType SecretType,
    string? Status,
    DateTimeOffset? CreatedAt,
    DateTimeOffset? UpdatedAt,
    string MetadataFingerprint,
    DateTimeOffset LastIndexedAt,
    bool IsDeletedOrUnavailable = false);

public sealed record CyberArkSecretVersion(
    Guid ProfileId,
    string AccountId,
    int VersionId,
    bool IsTemporary,
    DateTimeOffset ModifiedAt,
    string ModifiedBy);

public sealed record CyberArkSafePermissionEvidence(
    Guid ProfileId,
    string SafeId,
    string MemberName,
    string MemberType,
    bool ListAccounts,
    bool UseAccounts,
    bool RetrieveAccounts,
    bool ViewAuditLog,
    bool AccessWithoutConfirmation,
    bool RequestsAuthorizationLevel1,
    bool RequestsAuthorizationLevel2,
    DateTimeOffset ObservedAt,
    string EvidenceState);

public sealed record CyberArkDiscoverySnapshot(
    IReadOnlyList<CyberArkSafe> Safes,
    IReadOnlyList<CyberArkAccount> Accounts,
    IReadOnlyList<CyberArkSecretVersion> Versions,
    IReadOnlyList<CyberArkSafePermissionEvidence> Permissions,
    IReadOnlyList<ProviderError> Errors,
    DateTimeOffset CompletedAt);

public sealed record CyberArkAuditEvent(
    Guid Id,
    Guid ProfileId,
    string? AccountId,
    string? SafeName,
    int? VersionId,
    string Operation,
    CyberArkAuditResult Result,
    string SafeMessage,
    DateTimeOffset OccurredAt);
