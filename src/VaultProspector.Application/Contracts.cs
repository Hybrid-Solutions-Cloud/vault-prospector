using System.Security.Cryptography;
using VaultProspector.BrowserProtocol;
using VaultProspector.Domain;

namespace VaultProspector.Application;

public interface IClock { DateTimeOffset UtcNow { get; } }

public interface IIdentityProvider
{
    Task<ConnectedIdentity> SignInAsync(string clientId, string displayName, CancellationToken cancellationToken);
    Task<ConnectedIdentity> ReauthenticateAsync(ConnectedIdentity identity, CancellationToken cancellationToken);
    Task<ConnectedIdentity> AuthorizeDirectoryReadAsync(ConnectedIdentity identity, CancellationToken cancellationToken);
    Task RemoveAsync(ConnectedIdentity identity, CancellationToken cancellationToken);
}

public enum WorkloadPermissionEvidenceState
{
    Confirmed,
    Denied,
    Conditional,
    NotGranted,
    Incomplete,
    NotApplicable,
}

public sealed record WorkloadPermissionEvidence(
    string Capability,
    string Subject,
    WorkloadPermissionEvidenceState State,
    string Scope,
    string Basis,
    DateTimeOffset ObservedAt);

public sealed record WorkloadPermissionAssessment(
    string DirectoryVisibility,
    string AttachOrUse,
    string IdentityManagement,
    string KeyVaultDataAccess,
    string RoleAssignmentManagement)
{
    public IReadOnlyList<WorkloadPermissionEvidence> Evidence { get; init; } = [];
}

public sealed record WorkloadIdentityCandidate(
    string IdentityType,
    string TenantId,
    string SubscriptionId,
    string ResourceGroup,
    string DisplayName,
    string ResourceId,
    string ClientId,
    string PrincipalId,
    string Location,
    bool IsEnabled,
    WorkloadPermissionAssessment Permissions);

public sealed record PlannedAzureOperation(
    string Operation,
    string ResourceType,
    string Scope,
    string ExpectedEffect);

public sealed record WorkloadIdentityProvisioningPlan(
    Guid PlanId,
    DateTimeOffset CreatedAt,
    string IdentityType,
    string TenantId,
    string SubscriptionId,
    string ResourceGroup,
    string IdentityName,
    IReadOnlyList<PlannedAzureOperation> Operations,
    bool PerformsMutations = false);

public interface IWorkloadIdentityAdministrationService
{
    Task<IReadOnlyList<WorkloadIdentityCandidate>> ListManagedIdentitiesAsync(
        ConnectedIdentity administrator,
        string subscriptionId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<WorkloadIdentityCandidate>> ListServicePrincipalsAsync(
        ConnectedIdentity administrator,
        CancellationToken cancellationToken);

    Task<WorkloadIdentityCandidate> AssessPermissionsAsync(
        ConnectedIdentity administrator,
        WorkloadIdentityCandidate candidate,
        string keyVaultResourceId,
        CancellationToken cancellationToken);

    WorkloadIdentityProvisioningPlan BuildManagedIdentityDryRun(
        string tenantId,
        string subscriptionId,
        string resourceGroupName,
        string identityName,
        string? keyVaultResourceId = null,
        string? keyVaultRoleDefinitionId = null);

    WorkloadIdentityProvisioningPlan BuildServicePrincipalDryRun(
        string tenantId,
        string identityName,
        string? keyVaultResourceId = null,
        string? keyVaultRoleDefinitionId = null);
}

public interface IVaultProvider
{
    Task<DiscoverySnapshot> DiscoverAsync(
        ConnectedIdentity identity,
        IReadOnlyList<string> excludedSubscriptions,
        IReadOnlyList<string> excludedVaultResourceIds,
        CancellationToken cancellationToken);

    Task<DiscoverySnapshot> DiscoverAsync(
        ConnectedIdentity identity,
        IReadOnlyList<string> excludedSubscriptions,
        IReadOnlyList<string> excludedVaultResourceIds,
        VaultDiscoveryConstraints constraints,
        CancellationToken cancellationToken) =>
        DiscoverAsync(
            identity,
            excludedSubscriptions,
            excludedVaultResourceIds,
            cancellationToken);

    Task<SensitiveValue> RetrieveSecretAsync(ConnectedIdentity identity, VaultResource vault, VaultItem item, CancellationToken cancellationToken);
}

public sealed class VaultDiscoveryConstraints(
    IEnumerable<string>? allowedTenantIds = null)
{
    private readonly HashSet<string> _allowedTenantIds = new(
        allowedTenantIds ?? [],
        StringComparer.OrdinalIgnoreCase);

    public static VaultDiscoveryConstraints Unrestricted { get; } = new();

    public IReadOnlySet<string> AllowedTenantIds => _allowedTenantIds;
    public bool RestrictsTenants => _allowedTenantIds.Count > 0;

    public bool IsTenantAllowed(string tenantId) =>
        !RestrictsTenants ||
        _allowedTenantIds.Contains(tenantId);
}

public interface ICyberArkProvider
{
    Task ValidateAsync(
        CyberArkProfile profile,
        SensitiveValue clientCredential,
        CancellationToken cancellationToken);

    Task<CyberArkDiscoverySnapshot> DiscoverAsync(
        CyberArkProfile profile,
        SensitiveValue clientCredential,
        CancellationToken cancellationToken);

    Task<SensitiveValue> RetrieveAsync(
        CyberArkProfile profile,
        SensitiveValue clientCredential,
        CyberArkAccount account,
        int? versionId,
        string reason,
        string actionType,
        CancellationToken cancellationToken);
}

public interface ICyberArkCredentialStore
{
    Task StoreAsync(Guid profileId, SensitiveValue credential, CancellationToken cancellationToken);
    Task<SensitiveValue> RetrieveAsync(Guid profileId, CancellationToken cancellationToken);
    Task RemoveAsync(Guid profileId, CancellationToken cancellationToken);
}

public sealed class CyberArkConfigurationException(
    string message,
    string? parameterName = null)
    : ArgumentException(message, parameterName);

public interface IMetadataRepository
{
    Task InitializeAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<ConnectedIdentity>> GetIdentitiesAsync(CancellationToken cancellationToken);
    Task<ConnectedIdentity?> GetIdentityAsync(Guid id, CancellationToken cancellationToken);
    Task UpsertIdentityAsync(ConnectedIdentity identity, CancellationToken cancellationToken);
    Task RemoveIdentityAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyList<TenantAccess>> GetTenantsAsync(Guid identityId, CancellationToken cancellationToken);
    Task<IReadOnlyList<SubscriptionAccess>> GetSubscriptionsAsync(Guid identityId, CancellationToken cancellationToken);
    Task SetSubscriptionSelectedAsync(Guid subscriptionAccessId, bool isSelected, CancellationToken cancellationToken);
    Task<IReadOnlyList<VaultAccessSummary>> GetVaultAccessSummariesAsync(Guid identityId, CancellationToken cancellationToken);
    Task<IReadOnlyList<Guid>> GetVaultIdsForIdentityAsync(Guid identityId, CancellationToken cancellationToken);
    Task SetVaultSelectedAsync(Guid vaultAccessId, bool isSelected, CancellationToken cancellationToken);
    Task ApplyDiscoveryAsync(Guid identityId, DiscoverySnapshot snapshot, SyncRun run, CancellationToken cancellationToken);
    Task<IReadOnlyList<SearchResult>> SearchAsync(SearchRequest request, DateTimeOffset now, CancellationToken cancellationToken);
    Task<(VaultItem Item, VaultResource Vault, ConnectedIdentity Identity)?> ResolveItemAsync(Guid itemId, CancellationToken cancellationToken);
    async Task<(VaultItem Item, VaultResource Vault, ConnectedIdentity Identity)?> ResolveItemForIdentityAsync(
        Guid itemId,
        Guid identityId,
        CancellationToken cancellationToken)
    {
        var source = await ResolveItemAsync(itemId, cancellationToken);
        return source?.Identity.Id == identityId ? source : null;
    }
    Task RecordAccessAsync(Guid itemId, DateTimeOffset accessedAt, CancellationToken cancellationToken);
    Task SetFavoriteAsync(Guid itemId, bool isFavorite, CancellationToken cancellationToken);
    Task<IReadOnlyList<Workspace>> GetWorkspacesAsync(CancellationToken cancellationToken);
    Task UpsertWorkspaceAsync(Workspace workspace, CancellationToken cancellationToken);
    Task RemoveWorkspaceAsync(Guid id, CancellationToken cancellationToken);
    Task AddWorkspaceLinkAsync(WorkspaceResourceLink link, CancellationToken cancellationToken);
    Task RemoveWorkspaceLinkAsync(Guid workspaceId, ResourceLinkType resourceType, string resourceId, CancellationToken cancellationToken);
    Task<IReadOnlyList<BrowserFillMapping>> GetBrowserFillMappingsAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<BrowserFillMapping>>([]);
    Task<BrowserFillMapping?> GetBrowserFillMappingAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult<BrowserFillMapping?>(null);
    Task<BrowserFillMapping?> FindBrowserFillMappingAsync(string topOrigin, string frameOrigin, BrowserMappingFieldPurpose fieldPurpose, CancellationToken cancellationToken) =>
        Task.FromResult<BrowserFillMapping?>(null);
    Task UpsertBrowserFillMappingAsync(BrowserFillMapping mapping, CancellationToken cancellationToken) =>
        throw new NotSupportedException("Browser fill mappings are not supported by this repository.");
    Task RemoveBrowserFillMappingAsync(Guid id, CancellationToken cancellationToken) =>
        throw new NotSupportedException("Browser fill mappings are not supported by this repository.");
    Task RecordBrowserFillAuditAsync(BrowserFillAuditEvent auditEvent, CancellationToken cancellationToken) =>
        throw new NotSupportedException("Browser fill audit is not supported by this repository.");
    Task<IReadOnlyList<BrowserFillAuditEvent>> GetBrowserFillAuditAsync(int limit, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<BrowserFillAuditEvent>>([]);
    Task<IReadOnlyList<CyberArkProfile>> GetCyberArkProfilesAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<CyberArkProfile>>([]);
    Task<CyberArkProfile?> GetCyberArkProfileAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult<CyberArkProfile?>(null);
    Task UpsertCyberArkProfileAsync(CyberArkProfile profile, CancellationToken cancellationToken) =>
        throw new NotSupportedException("CyberArk profiles are not supported by this repository.");
    Task RemoveCyberArkProfileAsync(Guid id, CancellationToken cancellationToken) =>
        throw new NotSupportedException("CyberArk profiles are not supported by this repository.");
    Task ApplyCyberArkDiscoveryAsync(Guid profileId, CyberArkDiscoverySnapshot snapshot, CancellationToken cancellationToken) =>
        throw new NotSupportedException("CyberArk discovery is not supported by this repository.");
    Task<IReadOnlyList<CyberArkSafe>> GetCyberArkSafesAsync(Guid profileId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<CyberArkSafe>>([]);
    Task<IReadOnlyList<CyberArkAccount>> SearchCyberArkAccountsAsync(
        Guid profileId,
        string searchText,
        int limit,
        CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<CyberArkAccount>>([]);
    Task<IReadOnlyList<CyberArkSecretVersion>> GetCyberArkVersionsAsync(
        Guid profileId,
        string accountId,
        CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<CyberArkSecretVersion>>([]);
    Task<CyberArkSafePermissionEvidence?> GetCyberArkPermissionAsync(
        Guid profileId,
        string safeId,
        CancellationToken cancellationToken) =>
        Task.FromResult<CyberArkSafePermissionEvidence?>(null);
    Task RecordCyberArkAuditAsync(CyberArkAuditEvent auditEvent, CancellationToken cancellationToken) =>
        throw new NotSupportedException("CyberArk audit is not supported by this repository.");
    Task<IReadOnlyList<CyberArkAuditEvent>> GetCyberArkAuditAsync(
        Guid profileId,
        int limit,
        CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<CyberArkAuditEvent>>([]);
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
    Task<UserVerificationResult> VerifyAsync(string reason, CancellationToken cancellationToken);
}

public enum UserVerificationResult
{
    Verified,
    Canceled,
    Unavailable,
    RemoteSessionUnavailable,
    NotConfigured,
    DisabledByPolicy,
    Failed,
}

public interface ILocalDataResetter
{
    Task<LocalDataArchive> ArchiveForResetAsync(CancellationToken cancellationToken);
}

public sealed record LocalDataArchive(string ArchivePath, bool HadExistingData);

public sealed class LocalDataResetConfirmationException()
    : Exception("The local-data reset confirmation phrase did not match.");

public interface ILocalRecoveryArchiveStore
{
    Task<IReadOnlyList<LocalRecoveryArchive>> ListAsync(CancellationToken cancellationToken);
    Task DeleteAsync(string archiveId, CancellationToken cancellationToken);
}

public enum LocalRecoveryArchiveKind
{
    Reset,
    Rotation,
    FailedRotation,
}

public sealed record LocalRecoveryArchive(
    string Id,
    LocalRecoveryArchiveKind Kind,
    DateTimeOffset CreatedAtUtc,
    long SizeBytes);

public sealed class LocalRecoveryArchiveConfirmationException()
    : Exception("The recovery-archive confirmation did not match the selected archive.");

public sealed class LocalRecoveryArchiveVerificationException()
    : Exception("Fresh Windows verification was not completed for recovery-archive deletion.");

public sealed class LocalRecoveryArchiveValidationException(string message)
    : Exception(message);

public sealed class WorkloadIdentityConfigurationException(string message, string parameterName)
    : ArgumentException(message, parameterName);

public sealed class WorkloadCredentialUnavailableException(string message)
    : Exception(message);

public sealed record LocalIdentityRevocationResult(
    bool ProviderCredentialRemoved,
    int PurgedVaultCount);

public sealed class LocalRevocationCleanupException(
    int failedVaultCount,
    Exception innerException)
    : Exception(
        "Local identity access was revoked, but one or more associated offline-value stores could not be purged.",
        innerException)
{
    public int FailedVaultCount { get; } = failedVaultCount;
}

public sealed class WorkloadAuthorizationEvidenceException(
    int statusCode,
    string operation)
    : Exception($"Azure {operation} failed with HTTP {statusCode}.")
{
    public int StatusCode { get; } = statusCode;
    public string Operation { get; } = operation;
}

public interface IManagedIdentityEnvironmentDetector
{
    Task<ManagedIdentityEnvironmentStatus> DetectAsync(CancellationToken cancellationToken);
}

public sealed record ManagedIdentityEnvironmentStatus(bool IsSupported, string SafeReason);

public interface IKeyMaterialProvider
{
    bool IsAvailable { get; }
    Task<byte[]> GetOrCreateKeyAsync(string purpose, CancellationToken cancellationToken);
    Task<byte[]> GetExistingKeyAsync(string purpose, CancellationToken cancellationToken);
}

public sealed class StagedKeyRotation : IDisposable
{
    private byte[]? _key;

    public StagedKeyRotation(string purpose, Guid rotationId, byte[] key)
    {
        Purpose = purpose;
        RotationId = rotationId;
        _key = key;
    }

    public string Purpose { get; }
    public Guid RotationId { get; }

    public byte[] ExportKey()
    {
        ObjectDisposedException.ThrowIf(_key is null, this);
        return _key.ToArray();
    }

    public void Dispose()
    {
        if (_key is not null)
        {
            CryptographicOperations.ZeroMemory(_key);
            _key = null;
        }

        GC.SuppressFinalize(this);
    }
}

public interface IRotatableKeyMaterialProvider : IKeyMaterialProvider
{
    bool HasKey(string purpose);
    Task<StagedKeyRotation> StageReplacementAsync(
        string purpose,
        Guid rotationId,
        CancellationToken cancellationToken);
    Task PublishReplacementAsync(
        StagedKeyRotation rotation,
        CancellationToken cancellationToken);
    Task CompleteReplacementAsync(
        string purpose,
        Guid rotationId,
        CancellationToken cancellationToken);
    Task AbortReplacementAsync(
        string purpose,
        Guid rotationId,
        CancellationToken cancellationToken);
}

public sealed record LocalEncryptionRotationResult(
    Guid RotationId,
    string RecoveryArchivePath,
    int OfflineValuesRotated);

public sealed record LocalEncryptionRecoveryResult(
    bool Recovered,
    string? FailedStateArchivePath = null);

public interface ILocalEncryptionRotationEngine
{
    Task<LocalEncryptionRotationResult> RotateAsync(CancellationToken cancellationToken);
    Task<LocalEncryptionRecoveryResult> RecoverIfNeededAsync(CancellationToken cancellationToken);
}

public sealed class ProtectedKeyUnavailableException(string message) : Exception(message);

public sealed class IncompatibleLocalDataVersionException(int observedVersion, int supportedVersion)
    : Exception($"Local data schema version {observedVersion} is newer than supported version {supportedVersion}.")
{
    public int ObservedVersion { get; } = observedVersion;
    public int SupportedVersion { get; } = supportedVersion;
}

public sealed class LocalDataIntegrityException(string message, Exception? innerException = null)
    : Exception(message, innerException);

public interface IDiagnosticSink
{
    void Information(string eventName, IReadOnlyDictionary<string, object?> fields);
    void WriteError(string eventName, Exception exception, IReadOnlyDictionary<string, object?> fields);
}

public interface ISupportBundleService
{
    string DiagnosticLogPath { get; }
    Task<string> CreateAsync(CancellationToken cancellationToken);
}

public sealed record BrowserFillApproval(
    Guid ApprovalId,
    ValidatedBrowserFillRequest Request,
    BrowserFillMapping Mapping,
    string ItemName,
    string VaultName,
    string IdentityDisplayName,
    DateTimeOffset ExpiresAt);

public sealed record BrowserFillPolicyDecision(
    bool IsAllowed,
    string SafeReason);

public interface IBrowserFillPolicy
{
    Task<BrowserFillPolicyDecision> EvaluateAsync(
        BrowserFamily browserFamily,
        CanonicalBrowserOrigin topOrigin,
        CanonicalBrowserOrigin frameOrigin,
        BrowserMappingFieldPurpose fieldPurpose,
        CancellationToken cancellationToken);

    Task<string> GetStatusAsync(CancellationToken cancellationToken);
}
