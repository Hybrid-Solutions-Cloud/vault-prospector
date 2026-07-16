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
}

public sealed class MsalIdentityProvider(string cacheDirectory) : IIdentityProvider
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

    public async Task RemoveAsync(ConnectedIdentity identity, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var application = await GetApplicationAsync(identity.ClientId);
        var account = (await application.GetAccountsAsync()).FirstOrDefault(x => x.HomeAccountId.Identifier == identity.AccountIdentifier);
        if (account is not null) await application.RemoveAsync(account);
    }

    public async Task<TokenCredential> GetCredentialAsync(ConnectedIdentity identity, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var application = await GetApplicationAsync(identity.ClientId);
        var account = (await application.GetAccountsAsync()).FirstOrDefault(x => x.HomeAccountId.Identifier == identity.AccountIdentifier)
            ?? throw new MsalUiRequiredException("account_missing", "The identity token cache entry is missing. Sign in again.");
        return new MsalTokenCredential(application, account);
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

    private static Guid StableId(string clientId, string accountIdentifier)
    {
        var hash = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes($"{clientId}|{accountIdentifier}"));
        return new Guid(hash.AsSpan(0, 16));
    }

    private sealed class MsalTokenCredential(IPublicClientApplication application, IAccount account) : TokenCredential
    {
        public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken) =>
            GetTokenAsync(requestContext, cancellationToken).AsTask().GetAwaiter().GetResult();

        public override async ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken)
        {
            try
            {
                var builder = application.AcquireTokenSilent(requestContext.Scopes, account);
                if (!string.IsNullOrWhiteSpace(requestContext.TenantId)) builder = builder.WithTenantId(requestContext.TenantId);
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
