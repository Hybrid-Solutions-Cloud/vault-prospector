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

public sealed class AzureVaultProvider(MsalIdentityProvider identityProvider) : IVaultProvider
{
    public async Task<DiscoverySnapshot> DiscoverAsync(ConnectedIdentity identity, CancellationToken cancellationToken)
    {
        var credential = await identityProvider.GetCredentialAsync(identity, cancellationToken);
        var arm = new ArmClient(credential);
        var tenants = new List<TenantAccess>();
        var subscriptions = new List<SubscriptionAccess>();
        var vaults = new List<VaultResource>();
        var accessPaths = new List<VaultAccess>();
        var items = new List<VaultItem>();
        var errors = new List<ProviderError>();
        var now = DateTimeOffset.UtcNow;

        try
        {
            await foreach (var subscription in arm.GetSubscriptions().GetAllAsync(cancellationToken))
            {
                var tenantId = subscription.Data.TenantId?.ToString() ?? identity.HomeTenantId;
                var tenantAccess = tenants.FirstOrDefault(x => x.TenantId == tenantId);
                if (tenantAccess is null)
                {
                    tenantAccess = new TenantAccess(Id(identity.Id, tenantId), identity.Id, tenantId, tenantId, tenantId == identity.HomeTenantId ? "Home" : "Resource", now, "Available");
                    tenants.Add(tenantAccess);
                }

                var subscriptionId = subscription.Data.SubscriptionId ?? subscription.Id.SubscriptionId ?? string.Empty;
                subscriptions.Add(new SubscriptionAccess(Id(identity.Id, tenantId, subscriptionId), tenantAccess.Id, subscriptionId, subscription.Data.DisplayName ?? subscriptionId, subscription.Data.State?.ToString() ?? "Unknown", true, now));
                try
                {
                    await foreach (var resource in subscription.GetGenericResourcesAsync("resourceType eq 'Microsoft.KeyVault/vaults'", cancellationToken: cancellationToken))
                    {
                        var vaultId = Id(resource.Id.ToString());
                        var vault = new VaultResource(vaultId, resource.Id.ToString(), resource.Data.Name, tenantId, subscriptionId, resource.Id.ResourceGroupName ?? string.Empty, resource.Data.Location.Name, ToTags(resource.Data.Tags), new Uri($"https://{resource.Data.Name}.vault.azure.net/"), now);
                        vaults.Add(vault);
                        accessPaths.Add(new VaultAccess(Id(vaultId, identity.Id), vaultId, identity.Id, tenantId, "Discovered", now, null, 0));
                        await EnumerateVaultAsync(credential, vault, items, errors, cancellationToken);
                    }
                }
                catch (Exception ex) when (IsExpectedAzureFailure(ex))
                {
                    errors.Add(SafeError($"subscription:{subscriptionId}", ex));
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
        var credential = await identityProvider.GetCredentialAsync(identity, cancellationToken);
        var client = new SecretClient(vault.VaultUri, credential);
        var response = string.IsNullOrWhiteSpace(item.ProviderVersion)
            ? await client.GetSecretAsync(item.ProviderObjectName, cancellationToken: cancellationToken)
            : await client.GetSecretAsync(item.ProviderObjectName, item.ProviderVersion, cancellationToken);
        return new SensitiveValue(response.Value.Value);
    }

    private static async Task EnumerateVaultAsync(TokenCredential credential, VaultResource vault, List<VaultItem> items, List<ProviderError> errors, CancellationToken cancellationToken)
    {
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
                catch (Exception ex) when (IsExpectedAzureFailure(ex)) { errors.Add(SafeError($"vault:{Pseudonym(vault.Id)}:secret_versions", ex)); }
                if (!foundVersion) items.Add(ToItem(vault.Id, current, DateTimeOffset.UtcNow));
            }
        }
        catch (Exception ex) when (IsExpectedAzureFailure(ex)) { errors.Add(SafeError($"vault:{Pseudonym(vault.Id)}:secrets", ex)); }

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
                catch (Exception ex) when (IsExpectedAzureFailure(ex)) { errors.Add(SafeError($"vault:{Pseudonym(vault.Id)}:key_versions", ex)); }
                if (!foundVersion) items.Add(ToItem(vault.Id, current, DateTimeOffset.UtcNow));
            }
        }
        catch (Exception ex) when (IsExpectedAzureFailure(ex)) { errors.Add(SafeError($"vault:{Pseudonym(vault.Id)}:keys", ex)); }

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
                catch (Exception ex) when (IsExpectedAzureFailure(ex)) { errors.Add(SafeError($"vault:{Pseudonym(vault.Id)}:certificate_versions", ex)); }
                if (!foundVersion) items.Add(ToItem(vault.Id, current, DateTimeOffset.UtcNow));
            }
        }
        catch (Exception ex) when (IsExpectedAzureFailure(ex)) { errors.Add(SafeError($"vault:{Pseudonym(vault.Id)}:certificates", ex)); }
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
    private static ProviderError SafeError(string scope, Exception ex) => new(scope, ex.GetType().Name, ex switch { RequestFailedException r => $"Azure request failed with status {r.Status} ({r.ErrorCode ?? "unknown"}).", MsalUiRequiredException => "Interactive authentication is required.", _ => "Azure operation failed. See the diagnostic event type." });
    private static string Pseudonym(Guid value) => value.ToString("N")[..12];
    private static Guid Id(params object[] values) { var input = string.Join('|', values.Select(x => x.ToString())); var hash = SHA256.HashData(Encoding.UTF8.GetBytes(input)); return new Guid(hash.AsSpan(0, 16)); }
}
