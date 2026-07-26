using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using VaultProspector.Domain;

namespace VaultProspector.Application;

public sealed class GovernedAzureMutationService : IDisposable
{
    public const string ReleaseEnablementSwitch =
        "VaultProspector.EnableGovernedAzureMutations";

    private static readonly TimeSpan PreviewLifetime =
        TimeSpan.FromMinutes(5);
    private readonly ConcurrentDictionary<Guid, GovernedMutationPreview>
        _pendingPreviews = new();
    private readonly SemaphoreSlim _auditGate = new(1, 1);
    private readonly IMetadataRepository _repository;
    private readonly IIdentityProvider _identityProvider;
    private readonly IGovernedAzureMutationProvider _mutationProvider;
    private readonly IUserVerificationService _verification;
    private readonly IEnterprisePolicy _enterprisePolicy;
    private readonly IClock _clock;
    private readonly bool _isReleaseApproved;

    public GovernedAzureMutationService(
        IMetadataRepository repository,
        IIdentityProvider identityProvider,
        IGovernedAzureMutationProvider mutationProvider,
        IUserVerificationService verification,
        IEnterprisePolicy enterprisePolicy,
        IClock clock,
        bool isReleaseApproved = false)
    {
        _repository = repository;
        _identityProvider = identityProvider;
        _mutationProvider = mutationProvider;
        _verification = verification;
        _enterprisePolicy = enterprisePolicy;
        _clock = clock;
        _isReleaseApproved = isReleaseApproved;
    }

    public bool IsAvailable =>
        _isReleaseApproved &&
        _verification.IsAvailable &&
        _enterprisePolicy.GetSnapshot().AllowGovernedAzureMutations;

    public async Task<GovernedMutationPreview> PrepareAsync(
        ConnectedIdentity identity,
        VaultResource vault,
        GovernedAzureOperation operation,
        string objectName,
        CancellationToken cancellationToken)
    {
        EnsureReleaseApproved();
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(vault);
        EnsureIdentityAndTarget(identity, vault);
        var normalizedObjectName = NormalizeObjectName(objectName);
        var policy = _enterprisePolicy.GetSnapshot();
        policy.EnsureIdentityAllowed(identity);
        policy.EnsureAzureMutationAllowed(operation, vault.ProviderResourceId);

        var expectedCurrentVersion =
            operation == GovernedAzureOperation.CreateSecretVersion
                ? await _mutationProvider.GetCurrentSecretVersionAsync(
                    identity,
                    vault.VaultUri,
                    normalizedObjectName,
                    cancellationToken)
                : string.Empty;
        if (operation == GovernedAzureOperation.CreateSecretVersion &&
            string.IsNullOrWhiteSpace(expectedCurrentVersion))
        {
            throw new InvalidOperationException(
                "A current secret version could not be established. Create-secret-version fails closed.");
        }

        var now = _clock.UtcNow;
        var id = Guid.NewGuid();
        var preview = new GovernedMutationPreview(
            id,
            operation,
            identity.Id,
            identity.DisplayName,
            vault.TenantId,
            vault.SubscriptionId,
            vault.ResourceGroup,
            vault.ProviderResourceId,
            vault.VaultUri,
            vault.Name,
            normalizedObjectName,
            ExpectedEffect(operation),
            expectedCurrentVersion,
            RecoveryGuidance(operation),
            $"APPLY {id:N}"[..22].ToUpperInvariant(),
            now,
            now.Add(PreviewLifetime));
        if (!_pendingPreviews.TryAdd(preview.Id, preview))
            throw new InvalidOperationException("The one-time mutation preview could not be registered.");

        try
        {
            await RecordAuditAsync(
                preview,
                GovernedMutationAuditResult.Prepared,
                sensitiveValueLength: 0,
                providerVersion: string.Empty,
                safeMessage: "Value-free mutation preview prepared.",
                cancellationToken);
        }
        catch
        {
            _pendingPreviews.TryRemove(preview.Id, out _);
            throw;
        }
        return preview;
    }

    public async Task<GovernedMutationResult> ExecuteAsync(
        GovernedMutationPreview preview,
        string confirmationPhrase,
        SensitiveValue? sensitiveValue,
        CancellationToken cancellationToken)
    {
        EnsureReleaseApproved();
        ArgumentNullException.ThrowIfNull(preview);
        if (!_pendingPreviews.TryRemove(preview.Id, out var registered) ||
            registered != preview)
        {
            throw new InvalidOperationException(
                "This mutation preview is stale, unknown, or was already submitted.");
        }

        var valueLength = sensitiveValue?.Length ?? 0;
        try
        {
            if (_clock.UtcNow > preview.ExpiresAt)
                throw new InvalidOperationException(
                    "The mutation preview expired. Prepare and review a new preview.");
            EnsureSensitiveInput(preview.Operation, sensitiveValue);
            EnsureConfirmation(preview.ConfirmationPhrase, confirmationPhrase);

            var identity = await _repository.GetIdentityAsync(
                preview.IdentityId,
                cancellationToken) ??
                throw new KeyNotFoundException(
                    "The preview identity no longer exists.");
            EnsurePreviewIdentity(preview, identity);
            var policy = _enterprisePolicy.GetSnapshot();
            policy.EnsureIdentityAllowed(identity);
            policy.EnsureAzureMutationAllowed(
                preview.Operation,
                preview.VaultResourceId);

            var refreshed = await _identityProvider.ReauthenticateAsync(
                identity,
                cancellationToken);
            EnsurePreviewIdentity(preview, refreshed);
            if (!string.Equals(
                    identity.AccountIdentifier,
                    refreshed.AccountIdentifier,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    identity.ClientId,
                    refreshed.ClientId,
                    StringComparison.OrdinalIgnoreCase) ||
                identity.Type != refreshed.Type)
            {
                throw new InvalidOperationException(
                    "Fresh Azure authentication returned a different identity than the reviewed preview.");
            }
            await _repository.UpsertIdentityAsync(
                refreshed,
                cancellationToken);
            await _mutationProvider.EnsureAuthorizedAsync(
                refreshed,
                preview.VaultResourceId,
                preview.Operation,
                cancellationToken);

            var verification = await _verification.VerifyAsync(
                $"Confirm {preview.Operation} for {preview.VaultName}/{preview.ObjectName}",
                cancellationToken);
            if (verification != UserVerificationResult.Verified)
            {
                throw new UnauthorizedAccessException(
                    $"Fresh Windows verification did not succeed ({verification}).");
            }

            await RecordAuditAsync(
                preview,
                GovernedMutationAuditResult.Started,
                valueLength,
                string.Empty,
                "Provider mutation started after all authorization gates.",
                cancellationToken);
            var result = await _mutationProvider.ExecuteAsync(
                refreshed,
                preview,
                sensitiveValue,
                cancellationToken);
            await RecordAuditAsync(
                preview,
                GovernedMutationAuditResult.Succeeded,
                valueLength,
                result.ProviderVersion,
                result.SafeStatus,
                CancellationToken.None);
            return result;
        }
        catch (GovernedMutationConflictException exception)
        {
            await RecordAuditAsync(
                preview,
                GovernedMutationAuditResult.Conflict,
                valueLength,
                string.Empty,
                exception.Message,
                CancellationToken.None);
            throw;
        }
        catch (Exception exception)
        {
            await RecordAuditAsync(
                preview,
                exception is UnauthorizedAccessException or
                    EnterprisePolicyDeniedException
                    ? GovernedMutationAuditResult.Denied
                    : GovernedMutationAuditResult.Failed,
                valueLength,
                string.Empty,
                SafeFailure(exception),
                CancellationToken.None);
            throw;
        }
        finally
        {
            sensitiveValue?.Dispose();
        }
    }

    public void Cancel(Guid previewId) =>
        _pendingPreviews.TryRemove(previewId, out _);

    public void Dispose()
    {
        _pendingPreviews.Clear();
        _auditGate.Dispose();
        GC.SuppressFinalize(this);
    }

    private async Task RecordAuditAsync(
        GovernedMutationPreview preview,
        GovernedMutationAuditResult result,
        int sensitiveValueLength,
        string providerVersion,
        string safeMessage,
        CancellationToken cancellationToken)
    {
        await _auditGate.WaitAsync(cancellationToken);
        try
        {
            var previous = await _repository
                .GetLatestGovernedMutationAuditAsync(cancellationToken);
            var previousHash = previous?.RecordHash ?? string.Empty;
            var occurredAt = _clock.UtcNow;
            var eventId = Guid.NewGuid();
            var objectNameHash = Hash(preview.ObjectName);
            var recordHash = Hash(
                string.Join(
                    "|",
                    eventId.ToString("D"),
                    preview.Id.ToString("D"),
                    occurredAt.ToUniversalTime().ToString("O"),
                    (int)preview.Operation,
                    preview.IdentityId.ToString("D"),
                    preview.TenantId,
                    preview.SubscriptionId,
                    preview.VaultResourceId,
                    objectNameHash,
                    sensitiveValueLength,
                    (int)result,
                    providerVersion,
                    safeMessage,
                    previousHash));
            await _repository.RecordGovernedMutationAuditAsync(
                new GovernedMutationAuditEvent(
                    eventId,
                    preview.Id,
                    occurredAt,
                    preview.Operation,
                    preview.IdentityId,
                    preview.TenantId,
                    preview.SubscriptionId,
                    preview.VaultResourceId,
                    objectNameHash,
                    sensitiveValueLength,
                    result,
                    providerVersion,
                    safeMessage,
                    previousHash,
                    recordHash),
                cancellationToken);
        }
        finally
        {
            _auditGate.Release();
        }
    }

    private void EnsureReleaseApproved()
    {
        if (!_isReleaseApproved)
        {
            throw new InvalidOperationException(
                "Governed Azure mutations remain disabled until independent security review, live Azure evidence, and exact-candidate approval are complete.");
        }
    }

    private static void EnsureIdentityAndTarget(
        ConnectedIdentity identity,
        VaultResource vault)
    {
        if (!identity.IsEnabled ||
            identity.AuthenticationState != AuthenticationState.Ready)
        {
            throw new InvalidOperationException(
                "The selected identity is not ready for a governed Azure operation.");
        }
        if (!string.Equals(
                identity.HomeTenantId,
                vault.TenantId,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "The selected identity and Key Vault target belong to different tenants.");
        }
        if (vault.VaultUri.Scheme != Uri.UriSchemeHttps ||
            !vault.VaultUri.Host.EndsWith(
                ".vault.azure.net",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "The Key Vault endpoint is not a trusted public Azure Key Vault endpoint.");
        }
    }

    private static void EnsurePreviewIdentity(
        GovernedMutationPreview preview,
        ConnectedIdentity identity)
    {
        if (identity.Id != preview.IdentityId ||
            !identity.IsEnabled ||
            identity.AuthenticationState != AuthenticationState.Ready ||
            !string.Equals(
                identity.HomeTenantId,
                preview.TenantId,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "The selected identity changed after the mutation preview was prepared.");
        }
    }

    private static string NormalizeObjectName(string value)
    {
        var normalized = value?.Trim() ?? string.Empty;
        if (normalized.Length is < 1 or > 127 ||
            normalized.Any(character =>
                !char.IsAsciiLetterOrDigit(character) &&
                character != '-'))
        {
            throw new ArgumentException(
                "Azure Key Vault object names must contain 1–127 letters, digits, or hyphens.",
                nameof(value));
        }
        return normalized;
    }

    private static void EnsureSensitiveInput(
        GovernedAzureOperation operation,
        SensitiveValue? sensitiveValue)
    {
        var requiresValue =
            operation is GovernedAzureOperation.CreateSecret or
                GovernedAzureOperation.CreateSecretVersion;
        if (requiresValue &&
            (sensitiveValue is null ||
             sensitiveValue.IsDisposed ||
             sensitiveValue.Length is < 1 or > 25_600))
        {
            throw new ArgumentException(
                "Secret operations require a non-empty value within the Azure Key Vault size limit.",
                nameof(sensitiveValue));
        }
        if (!requiresValue && sensitiveValue is not null)
        {
            throw new ArgumentException(
                "Key and certificate-policy operations do not accept secret or private-key material.",
                nameof(sensitiveValue));
        }
    }

    private static void EnsureConfirmation(
        string expected,
        string supplied)
    {
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        var suppliedBytes = Encoding.UTF8.GetBytes(
            supplied?.Trim() ?? string.Empty);
        try
        {
            if (expectedBytes.Length != suppliedBytes.Length ||
                !CryptographicOperations.FixedTimeEquals(
                    expectedBytes,
                    suppliedBytes))
            {
                throw new InvalidOperationException(
                    "The one-time mutation confirmation phrase did not match.");
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(expectedBytes);
            CryptographicOperations.ZeroMemory(suppliedBytes);
        }
    }

    private static string ExpectedEffect(
        GovernedAzureOperation operation) =>
        operation switch
        {
            GovernedAzureOperation.CreateSecret =>
                "Create the first immutable version of one named secret.",
            GovernedAzureOperation.CreateSecretVersion =>
                "Create one immutable secret version only if the reviewed current version is still current.",
            GovernedAzureOperation.CreateSoftwareKeyVersion =>
                "Create one enabled, software-protected RSA-3072 key version for encrypt, decrypt, wrap, and unwrap operations. Earlier versions remain unchanged.",
            GovernedAzureOperation.StartCertificatePolicy =>
                "Start one certificate operation using a self-signed RSA-3072 policy with a non-exportable private key. No certificate or private-key material is imported.",
            _ => throw new ArgumentOutOfRangeException(nameof(operation)),
        };

    private static string RecoveryGuidance(
        GovernedAzureOperation operation) =>
        operation switch
        {
            GovernedAzureOperation.CreateSecret or
                GovernedAzureOperation.CreateSecretVersion =>
                "If the new version is unsuitable, select an earlier version in Azure and disable the new version after an independent review. Vault Prospector never deletes it automatically.",
            GovernedAzureOperation.CreateSoftwareKeyVersion =>
                "If the new key version is unsuitable, keep clients on the earlier version and disable the new version after review. Vault Prospector never deletes or purges key material.",
            GovernedAzureOperation.StartCertificatePolicy =>
                "Review the certificate operation in Azure. Cancel or disable it through an independently authorized Azure workflow if required; Vault Prospector does not delete or purge certificates.",
            _ => "Review the resulting Azure object before taking any separate recovery action.",
        };

    private static string SafeFailure(Exception exception) =>
        exception switch
        {
            EnterprisePolicyDeniedException =>
                "Machine-managed policy denied the operation.",
            UnauthorizedAccessException =>
                "Fresh authorization or Windows verification denied the operation.",
            GovernedMutationConflictException =>
                "The Azure target changed after preview; no mutation was submitted.",
            OperationCanceledException =>
                "The operation was canceled. Review Azure activity before preparing another mutation.",
            _ =>
                "The governed mutation failed. Review the value-free audit and Azure activity before deciding whether to prepare a new operation.",
        };

    private static string Hash(string value) =>
        Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}

public sealed class GovernedMutationConflictException(string message)
    : InvalidOperationException(message);
