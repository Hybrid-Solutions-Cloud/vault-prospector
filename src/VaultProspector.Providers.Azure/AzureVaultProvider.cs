using System.Security.Cryptography;
using System.Text;
using Azure;
using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.Resources;
using Azure.Security.KeyVault.Certificates;
using Azure.Security.KeyVault.Keys;
using Azure.Security.KeyVault.Secrets;
using Microsoft.Identity.Client;
using VaultProspector.Application;
using VaultProspector.Domain;

namespace VaultProspector.Providers.Azure;

public sealed class AzureVaultProvider : IVaultProvider
{
    private readonly IAzureCredentialProvider _identityProvider;
    private readonly Func<TokenCredential, ArmClient> _armClientFactory;
    private readonly VaultMetadataEnumerator _vaultMetadataEnumerator;

    public AzureVaultProvider(IAzureCredentialProvider identityProvider)
        : this(
            identityProvider,
            credential => new ArmClient(credential),
            EnumerateVaultAsync)
    {
    }

    internal AzureVaultProvider(
        IAzureCredentialProvider identityProvider,
        Func<TokenCredential, ArmClient> armClientFactory,
        VaultMetadataEnumerator? vaultMetadataEnumerator = null)
    {
        _identityProvider = identityProvider;
        _armClientFactory = armClientFactory;
        _vaultMetadataEnumerator =
            vaultMetadataEnumerator ?? EnumerateVaultAsync;
    }

    public Task<DiscoverySnapshot> DiscoverAsync(
        ConnectedIdentity identity,
        IReadOnlyList<string> excludedSubscriptions,
        IReadOnlyList<string> excludedVaultResourceIds,
        CancellationToken cancellationToken) =>
        DiscoverAsync(
            identity,
            excludedSubscriptions,
            excludedVaultResourceIds,
            VaultDiscoveryConstraints.Unrestricted,
            cancellationToken);

    public async Task<DiscoverySnapshot> DiscoverAsync(
        ConnectedIdentity identity,
        IReadOnlyList<string> excludedSubscriptions,
        IReadOnlyList<string> excludedVaultResourceIds,
        VaultDiscoveryConstraints constraints,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(constraints);
        var credential = await _identityProvider.GetCredentialAsync(
            identity,
            cancellationToken);
        var arm = _armClientFactory(credential);
        var tenants = new List<TenantAccess>();
        var subscriptions = new List<SubscriptionAccess>();
        var vaults = new List<VaultResource>();
        var accessPaths = new List<VaultAccess>();
        var items = new List<VaultItem>();
        var errors = new List<ProviderError>();
        var now = DateTimeOffset.UtcNow;

        try
        {
            await foreach (var tenant in arm.GetTenants().GetAllAsync(cancellationToken))
            {
                var tenantId = tenant.Data.TenantId?.ToString() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(tenantId))
                {
                    errors.Add(new ProviderError(
                        "tenant:unknown",
                        "InvalidTenantResponse",
                        "Azure returned a tenant without an identifier."));
                    continue;
                }

                var displayName = tenant.Data.DisplayName;
                if (string.IsNullOrWhiteSpace(displayName))
                    displayName = tenant.Data.DefaultDomain;
                if (string.IsNullOrWhiteSpace(displayName))
                    displayName = tenantId;
                var tenantAccess = new TenantAccess(
                    Id(identity.Id, tenantId),
                    identity.Id,
                    tenantId,
                    displayName,
                    tenant.Data.TenantType?.ToString() ??
                    (string.Equals(tenantId, identity.HomeTenantId, StringComparison.OrdinalIgnoreCase)
                        ? "Home"
                        : "Resource"),
                    now,
                    "Available");
                tenants.Add(tenantAccess);

                if (!constraints.IsTenantAllowed(tenantId))
                    continue;

                try
                {
                    var tenantCredential =
                        new TenantScopedCredential(credential, tenantId);
                    var tenantArm = _armClientFactory(tenantCredential);
                    await foreach (var subscription in tenantArm.GetSubscriptions().GetAllAsync(cancellationToken))
                    {
                        var subscriptionId = subscription.Data.SubscriptionId ?? subscription.Id.SubscriptionId ?? string.Empty;
                        if (!constraints.IsSubscriptionAllowed(subscriptionId))
                            continue;
                        if (excludedSubscriptions.Contains(subscriptionId, StringComparer.OrdinalIgnoreCase))
                            continue;
                        subscriptions.Add(new SubscriptionAccess(Id(identity.Id, tenantId, subscriptionId), tenantAccess.Id, subscriptionId, subscription.Data.DisplayName ?? subscriptionId, subscription.Data.State?.ToString() ?? "Unknown", true, now));
                        try
                        {
                            await foreach (var resource in subscription.GetGenericResourcesAsync("resourceType eq 'Microsoft.KeyVault/vaults'", cancellationToken: cancellationToken))
                            {
                                if (excludedVaultResourceIds.Contains(resource.Id.ToString(), StringComparer.OrdinalIgnoreCase))
                                    continue;
                                if (!constraints.IsVaultAllowed(resource.Id.ToString()))
                                    continue;
                                var vaultId = Id(resource.Id.ToString());
                                var vault = new VaultResource(vaultId, resource.Id.ToString(), resource.Data.Name, tenantId, subscriptionId, resource.Id.ResourceGroupName ?? string.Empty, resource.Data.Location.Name, ToTags(resource.Data.Tags), new Uri($"https://{resource.Data.Name}.vault.azure.net/"), now);
                                vaults.Add(vault);
                                var permissionObservation =
                                    await _vaultMetadataEnumerator(
                                        tenantCredential,
                                        vault,
                                        items,
                                        errors,
                                        cancellationToken);
                                accessPaths.Add(new VaultAccess(
                                    Id(vaultId, identity.Id),
                                    vaultId,
                                    identity.Id,
                                    tenantId,
                                    permissionObservation.Summary,
                                    now,
                                    permissionObservation.FailureCategory,
                                    0));
                            }
                        }
                        catch (Exception ex) when (IsExpectedAzureFailure(ex))
                        {
                            errors.Add(SafeError(
                                $"subscription:{subscriptionId}",
                                ex,
                                new ProviderRetryScope(SubscriptionId: subscriptionId)));
                        }
                    }
                }
                catch (Exception ex) when (IsExpectedAzureFailure(ex))
                {
                    errors.Add(SafeError(
                        $"tenant:{Pseudonym(tenantAccess.Id)}:subscriptions",
                        ex,
                        new ProviderRetryScope(TenantId: tenantId)));
                }
            }
        }
        catch (MsalUiRequiredException)
        {
            throw;
        }
        catch (Exception ex) when (IsExpectedAzureFailure(ex))
        {
            errors.Add(SafeError("subscriptions", ex));
        }

        return new DiscoverySnapshot(tenants, subscriptions, vaults, accessPaths, items, errors);
    }

    public async Task<SensitiveValue> RetrieveSecretAsync(ConnectedIdentity identity, VaultResource vault, VaultItem item, CancellationToken cancellationToken)
    {
        if (item.ObjectType != VaultObjectType.Secret) throw new InvalidOperationException("Only secrets can be retrieved as values.");
        var credential = await _identityProvider.GetCredentialAsync(identity, cancellationToken);
        var client = new SecretClient(vault.VaultUri, credential);
        var response = string.IsNullOrWhiteSpace(item.ProviderVersion)
            ? await client.GetSecretAsync(item.ProviderObjectName, cancellationToken: cancellationToken)
            : await client.GetSecretAsync(item.ProviderObjectName, item.ProviderVersion, cancellationToken);
        return new SensitiveValue(response.Value.Value);
    }

    private static async Task<VaultPermissionObservation> EnumerateVaultAsync(TokenCredential credential, VaultResource vault, List<VaultItem> items, List<ProviderError> errors, CancellationToken cancellationToken)
    {
        var secretList = "Allowed";
        var keyList = "Allowed";
        var certificateList = "Allowed";
        try
        {
            var client = new SecretClient(vault.VaultUri, credential);
            await foreach (var current in client.GetPropertiesOfSecretsAsync(cancellationToken))
            {
                var foundVersion = false;
                try
                {
                    await foreach (var version in client.GetPropertiesOfSecretVersionsAsync(current.Name, cancellationToken))
                    {
                        items.Add(ToItem(vault.Id, version, DateTimeOffset.UtcNow));
                        foundVersion = true;
                    }
                }
                catch (Exception ex) when (IsExpectedAzureFailure(ex)) { errors.Add(SafeError($"vault:{Pseudonym(vault.Id)}:secret_versions", ex, new ProviderRetryScope(VaultResourceId: vault.ProviderResourceId))); }
                if (!foundVersion) items.Add(ToItem(vault.Id, current, DateTimeOffset.UtcNow));
            }
        }
        catch (Exception ex) when (IsExpectedAzureFailure(ex))
        {
            secretList = PermissionState(ex);
            errors.Add(SafeError($"vault:{Pseudonym(vault.Id)}:secrets", ex, new ProviderRetryScope(VaultResourceId: vault.ProviderResourceId)));
        }

        try
        {
            var client = new KeyClient(vault.VaultUri, credential);
            await foreach (var current in client.GetPropertiesOfKeysAsync(cancellationToken))
            {
                var foundVersion = false;
                try
                {
                    await foreach (var version in client.GetPropertiesOfKeyVersionsAsync(current.Name, cancellationToken))
                    {
                        items.Add(ToItem(vault.Id, version, DateTimeOffset.UtcNow));
                        foundVersion = true;
                    }
                }
                catch (Exception ex) when (IsExpectedAzureFailure(ex)) { errors.Add(SafeError($"vault:{Pseudonym(vault.Id)}:key_versions", ex, new ProviderRetryScope(VaultResourceId: vault.ProviderResourceId))); }
                if (!foundVersion) items.Add(ToItem(vault.Id, current, DateTimeOffset.UtcNow));
            }
        }
        catch (Exception ex) when (IsExpectedAzureFailure(ex))
        {
            keyList = PermissionState(ex);
            errors.Add(SafeError($"vault:{Pseudonym(vault.Id)}:keys", ex, new ProviderRetryScope(VaultResourceId: vault.ProviderResourceId)));
        }

        try
        {
            var client = new CertificateClient(vault.VaultUri, credential);
            await foreach (var current in client.GetPropertiesOfCertificatesAsync(cancellationToken: cancellationToken))
            {
                var foundVersion = false;
                try
                {
                    await foreach (var version in client.GetPropertiesOfCertificateVersionsAsync(current.Name, cancellationToken))
                    {
                        items.Add(ToItem(vault.Id, version, DateTimeOffset.UtcNow));
                        foundVersion = true;
                    }
                }
                catch (Exception ex) when (IsExpectedAzureFailure(ex)) { errors.Add(SafeError($"vault:{Pseudonym(vault.Id)}:certificate_versions", ex, new ProviderRetryScope(VaultResourceId: vault.ProviderResourceId))); }
                if (!foundVersion) items.Add(ToItem(vault.Id, current, DateTimeOffset.UtcNow));
            }
        }
        catch (Exception ex) when (IsExpectedAzureFailure(ex))
        {
            certificateList = PermissionState(ex);
            errors.Add(SafeError($"vault:{Pseudonym(vault.Id)}:certificates", ex, new ProviderRetryScope(VaultResourceId: vault.ProviderResourceId)));
        }

        var failedCategories = new[]
        {
            secretList == "Allowed" ? null : $"Secrets:{secretList}",
            keyList == "Allowed" ? null : $"Keys:{keyList}",
            certificateList == "Allowed" ? null : $"Certificates:{certificateList}",
        }.Where(value => value is not null).ToArray();
        var summary =
            $"Management visibility: Visible; metadata list — secrets: {secretList}, keys: {keyList}, certificates: {certificateList}; " +
            "secret value read: Not tested (only on explicit reveal); data-plane writes: Disabled by application policy.";
        return new VaultPermissionObservation(
            summary,
            failedCategories.Length == 0 ? null : string.Join(", ", failedCategories!));
    }

    private static VaultItem ToItem(Guid vaultId, SecretProperties p, DateTimeOffset indexed) => CreateItem(vaultId, p.Name, VaultObjectType.Secret, p.Enabled ?? true, ToTags(p.Tags), p.ContentType, p.CreatedOn, p.UpdatedOn, p.ExpiresOn, VersionFromUri(p.Id), indexed);
    private static VaultItem ToItem(Guid vaultId, KeyProperties p, DateTimeOffset indexed) => CreateItem(vaultId, p.Name, VaultObjectType.Key, p.Enabled ?? true, ToTags(p.Tags), null, p.CreatedOn, p.UpdatedOn, p.ExpiresOn, VersionFromUri(p.Id), indexed);
    private static VaultItem ToItem(Guid vaultId, CertificateProperties p, DateTimeOffset indexed) => CreateItem(vaultId, p.Name, VaultObjectType.Certificate, p.Enabled ?? true, ToTags(p.Tags), null, p.CreatedOn, p.UpdatedOn, p.ExpiresOn, VersionFromUri(p.Id), indexed);

    private static VaultItem CreateItem(Guid vaultId, string name, VaultObjectType type, bool enabled, IReadOnlyDictionary<string, string> tags, string? contentType, DateTimeOffset? created, DateTimeOffset? updated, DateTimeOffset? expires, string version, DateTimeOffset indexed)
    {
        var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{name}|{type}|{enabled}|{version}|{updated:O}|{expires:O}")));
        return new VaultItem(Id(vaultId, type, name, version), vaultId, name, type, enabled, tags, contentType, created, updated, expires, version, fingerprint, indexed);
    }

    private static Dictionary<string, string> ToTags(IDictionary<string, string>? tags) => tags is null ? [] : new Dictionary<string, string>(tags, StringComparer.OrdinalIgnoreCase);
    private static string VersionFromUri(Uri uri) => uri.Segments.Length > 3 ? uri.Segments[^1].Trim('/') : string.Empty;
    private static bool IsExpectedAzureFailure(Exception ex) => ex is RequestFailedException or AuthenticationFailedException or MsalException;
    private static string PermissionState(Exception ex) =>
        ex is RequestFailedException { Status: 401 or 403 } ? "Denied" : "Indeterminate";
    private static ProviderError SafeError(
        string scope,
        Exception ex,
        ProviderRetryScope? retryScope = null) =>
        new(
            scope,
            ex.GetType().Name,
            ex switch
            {
                RequestFailedException r => $"Azure request failed with status {r.Status} ({r.ErrorCode ?? "unknown"}).",
                MsalUiRequiredException => "Interactive authentication is required.",
                _ => "Azure operation failed. See the diagnostic event type.",
            },
            retryScope);
    private static string Pseudonym(Guid value) => value.ToString("N")[..12];
    private static Guid Id(params object[] values) { var input = string.Join('|', values.Select(x => x.ToString())); var hash = SHA256.HashData(Encoding.UTF8.GetBytes(input)); return new Guid(hash.AsSpan(0, 16)); }

    internal delegate Task<VaultPermissionObservation> VaultMetadataEnumerator(
        TokenCredential credential,
        VaultResource vault,
        List<VaultItem> items,
        List<ProviderError> errors,
        CancellationToken cancellationToken);

    internal sealed record VaultPermissionObservation(
        string Summary,
        string? FailureCategory);
}
