using Azure.Core;
using Azure.Identity;
using Microsoft.Identity.Client;
using Microsoft.Identity.Client.Extensions.Msal;
using VaultProspector.Application;
using VaultProspector.Domain;

namespace VaultProspector.Providers.Azure;

public static class AzureAuthenticationScopes
{
    public const string ArmDelegated = "https://management.azure.com/user_impersonation";
    public const string KeyVaultDelegated = "https://vault.azure.net/user_impersonation";
    public static IReadOnlyList<string> InteractiveSignIn { get; } = Array.AsReadOnly([ArmDelegated]);
    public static IReadOnlyList<string> AdditionalConsent { get; } = Array.AsReadOnly([KeyVaultDelegated]);
    public static IReadOnlyList<string> ArmApplication { get; } = Array.AsReadOnly(["https://management.azure.com/.default"]);
    public static IReadOnlyList<string> GraphDirectoryRead { get; } =
        Array.AsReadOnly(["https://graph.microsoft.com/Application.Read.All"]);
}

public interface IAzureCredentialProvider
{
    Task<TokenCredential> GetCredentialAsync(
        ConnectedIdentity identity,
        CancellationToken cancellationToken);
}

public sealed class MsalIdentityProvider(string cacheDirectory)
    : IIdentityProvider, IAzureCredentialProvider
{
    private readonly Dictionary<string, IPublicClientApplication> _applications = new(StringComparer.OrdinalIgnoreCase);

    public async Task<ConnectedIdentity> SignInAsync(string clientId, string displayName, CancellationToken cancellationToken)
    {
        var application = await GetApplicationAsync(clientId);
        var result = await application.AcquireTokenInteractive(AzureAuthenticationScopes.InteractiveSignIn)
            .WithExtraScopesToConsent(AzureAuthenticationScopes.AdditionalConsent)
            .WithPrompt(Prompt.SelectAccount)
            .ExecuteAsync(cancellationToken);
        var accountId = result.Account.HomeAccountId.Identifier;
        var tenantId = result.Account.HomeAccountId.TenantId ?? result.TenantId;
        return new ConnectedIdentity(
            StableId(clientId, accountId),
            clientId,
            accountId,
            result.Account.Username,
            string.IsNullOrWhiteSpace(displayName) ? result.Account.Username : displayName,
            tenantId,
            AuthenticationState.Ready,
            DateTimeOffset.UtcNow);
    }

    public async Task<ConnectedIdentity> ReauthenticateAsync(ConnectedIdentity identity, CancellationToken cancellationToken)
    {
        if (identity.Type != IdentityType.InteractiveUser)
        {
            var credential = await GetCredentialAsync(identity, cancellationToken);
            await credential.GetTokenAsync(
                new TokenRequestContext(AzureAuthenticationScopes.ArmApplication.ToArray()),
                cancellationToken);
            return identity with
            {
                AuthenticationState = AuthenticationState.Ready,
                LastInteractiveAuthentication = DateTimeOffset.UtcNow
            };
        }

        var application = await GetApplicationAsync(identity.ClientId);
        var accounts = await application.GetAccountsAsync();
        var account = accounts.FirstOrDefault(x => x.HomeAccountId.Identifier == identity.AccountIdentifier);

        var builder = application.AcquireTokenInteractive(AzureAuthenticationScopes.InteractiveSignIn)
            .WithExtraScopesToConsent(AzureAuthenticationScopes.AdditionalConsent)
            .WithPrompt(Prompt.SelectAccount);

        if (account != null)
        {
            builder = builder.WithAccount(account);
        }
        else if (!string.IsNullOrWhiteSpace(identity.UsernameHint))
        {
            builder = builder.WithLoginHint(identity.UsernameHint);
        }

        var result = await builder.ExecuteAsync(cancellationToken);
        EnsureSameAccount(identity, result);

        return identity with
        {
            AuthenticationState = AuthenticationState.Ready,
            LastInteractiveAuthentication = DateTimeOffset.UtcNow
        };
    }

    public async Task<ConnectedIdentity> AuthorizeDirectoryReadAsync(
        ConnectedIdentity identity,
        CancellationToken cancellationToken)
    {
        if (identity.Type != IdentityType.InteractiveUser)
            throw new InvalidOperationException(
                "Microsoft Graph delegated consent requires an interactive user.");

        var application = await GetApplicationAsync(identity.ClientId);
        var account = (await application.GetAccountsAsync())
            .FirstOrDefault(candidate =>
                candidate.HomeAccountId.Identifier == identity.AccountIdentifier);
        var builder = application.AcquireTokenInteractive(AzureAuthenticationScopes.GraphDirectoryRead)
            .WithPrompt(Prompt.SelectAccount);
        if (account is not null)
            builder = builder.WithAccount(account);
        else if (!string.IsNullOrWhiteSpace(identity.UsernameHint))
            builder = builder.WithLoginHint(identity.UsernameHint);

        var result = await builder.ExecuteAsync(cancellationToken);
        EnsureSameAccount(identity, result);
        return identity with
        {
            AuthenticationState = AuthenticationState.Ready,
            LastInteractiveAuthentication = DateTimeOffset.UtcNow,
        };
    }

    public async Task RemoveAsync(ConnectedIdentity identity, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (identity.Type != IdentityType.InteractiveUser) return;

        var application = await GetApplicationAsync(identity.ClientId);
        var account = (await application.GetAccountsAsync()).FirstOrDefault(x => x.HomeAccountId.Identifier == identity.AccountIdentifier);
        if (account is not null) await application.RemoveAsync(account);
    }

    public async Task<TokenCredential> GetCredentialAsync(ConnectedIdentity identity, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (identity.Type == IdentityType.ManagedIdentity)
        {
            var id = string.IsNullOrWhiteSpace(identity.ClientId) ? ManagedIdentityId.SystemAssigned : ManagedIdentityId.FromUserAssignedClientId(identity.ClientId);
            return new ManagedIdentityCredential(id);
        }
        else if (identity.Type == IdentityType.ServicePrincipal)
        {
            var cert = GetCertificate(identity.CredentialData);
            return new ClientCertificateCredential(identity.HomeTenantId, identity.ClientId, cert);
        }
        else if (identity.Type == IdentityType.FederatedServicePrincipal)
        {
            return new WorkloadIdentityCredential(new WorkloadIdentityCredentialOptions
            {
                TenantId = identity.HomeTenantId,
                ClientId = identity.ClientId,
                TokenFilePath = identity.CredentialData,
            });
        }

        var application = await GetApplicationAsync(identity.ClientId);
        var account = (await application.GetAccountsAsync()).FirstOrDefault(x => x.HomeAccountId.Identifier == identity.AccountIdentifier)
            ?? throw new MsalUiRequiredException("account_missing", "The identity token cache entry is missing. Sign in again.");
        return new MsalTokenCredential(application, account);
    }

    private static System.Security.Cryptography.X509Certificates.X509Certificate2 GetCertificate(string thumbprint)
    {
        var locations = new[] { System.Security.Cryptography.X509Certificates.StoreLocation.CurrentUser, System.Security.Cryptography.X509Certificates.StoreLocation.LocalMachine };
        foreach (var location in locations)
        {
            using var store = new System.Security.Cryptography.X509Certificates.X509Store(System.Security.Cryptography.X509Certificates.StoreName.My, location);
            store.Open(System.Security.Cryptography.X509Certificates.OpenFlags.ReadOnly);
            var certs = store.Certificates.Find(System.Security.Cryptography.X509Certificates.X509FindType.FindByThumbprint, thumbprint, false);
            if (certs.Count > 0)
            {
                var certificate = certs[0];
                if (!certificate.HasPrivateKey)
                    throw new WorkloadCredentialUnavailableException("The selected certificate does not have an accessible private key.");
                if (certificate.NotBefore.ToUniversalTime() > DateTime.UtcNow ||
                    certificate.NotAfter.ToUniversalTime() <= DateTime.UtcNow)
                    throw new WorkloadCredentialUnavailableException("The selected certificate is not currently valid.");
                return certificate;
            }
        }
        throw new WorkloadCredentialUnavailableException("The selected certificate was not found in an available Personal certificate store.");
    }

    private async Task<IPublicClientApplication> GetApplicationAsync(string clientId)
    {
        clientId = NormalizeClientId(clientId);
        if (_applications.TryGetValue(clientId, out var existing)) return existing;
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("Vault Prospector interactive identity storage is supported on Windows in this release.");
        }

        Directory.CreateDirectory(cacheDirectory);
        var application = PublicClientApplicationBuilder.Create(clientId)
            .WithAuthority(AzureCloudInstance.AzurePublic, AadAuthorityAudience.AzureAdMultipleOrgs)
            .WithDefaultRedirectUri()
            .Build();
        var storage = new StorageCreationPropertiesBuilder($"msal-{clientId}.cache", cacheDirectory).Build();
        var helper = await MsalCacheHelper.CreateAsync(storage);
        helper.RegisterCache(application.UserTokenCache);
        _applications[clientId] = application;
        return application;
    }

    private static string NormalizeClientId(string clientId)
    {
        if (!Guid.TryParse(clientId, out var parsed))
            throw new ArgumentException("The Microsoft Entra public-client application ID must be a GUID.", nameof(clientId));
        return parsed.ToString("D");
    }

    private static void EnsureSameAccount(
        ConnectedIdentity identity,
        AuthenticationResult result)
    {
        if (!string.Equals(
            result.Account.HomeAccountId.Identifier,
            identity.AccountIdentifier,
            StringComparison.Ordinal))
        {
            throw new AuthenticationFailedException(
                "Microsoft Entra returned a different account than the selected identity.");
        }
    }

    private static Guid StableId(string clientId, string accountIdentifier)
    {
        var hash = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes($"{clientId}|{accountIdentifier}"));
        return new Guid(hash.AsSpan(0, 16));
    }

    private sealed class MsalTokenCredential(
        IPublicClientApplication application,
        IAccount account) : TokenCredential
    {
        public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken) =>
            GetTokenAsync(requestContext, cancellationToken).AsTask().GetAwaiter().GetResult();

        public override async ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken)
        {
            try
            {
                var builder = application.AcquireTokenSilent(
                    requestContext.Scopes,
                    account);
                if (!string.IsNullOrWhiteSpace(requestContext.TenantId))
                    builder = builder.WithTenantId(requestContext.TenantId);
                var result = await builder.ExecuteAsync(cancellationToken);
                return new AccessToken(result.AccessToken, result.ExpiresOn);
            }
            catch (MsalUiRequiredException ex)
            {
                throw new AuthenticationFailedException("Interactive Microsoft Entra authentication is required.", ex);
            }
        }
    }
}
