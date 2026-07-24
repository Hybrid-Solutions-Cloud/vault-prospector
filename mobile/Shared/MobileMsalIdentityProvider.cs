using Azure.Core;
using Azure.Identity;
using Microsoft.Identity.Client;
using VaultProspector.Application;
using VaultProspector.Domain;
using VaultProspector.Providers.Azure;

namespace VaultProspector.Mobile.Identity;

public sealed class MobileMsalIdentityProvider(
    Func<object?> parentWindow,
    string redirectUri) : IIdentityProvider, IAzureCredentialProvider
{
    private readonly Dictionary<string, IPublicClientApplication> _applications =
        new(StringComparer.OrdinalIgnoreCase);

    public async Task<ConnectedIdentity> SignInAsync(
        string clientId,
        string displayName,
        CancellationToken cancellationToken)
    {
        var application = GetApplication(clientId);
        var result = await Interactive(application, AzureAuthenticationScopes.InteractiveSignIn)
            .WithExtraScopesToConsent(AzureAuthenticationScopes.AdditionalConsent)
            .WithPrompt(Prompt.SelectAccount)
            .ExecuteAsync(cancellationToken);

        return ToIdentity(clientId, displayName, result);
    }

    public async Task<ConnectedIdentity> ReauthenticateAsync(
        ConnectedIdentity identity,
        CancellationToken cancellationToken)
    {
        EnsureInteractive(identity);
        var application = GetApplication(identity.ClientId);
        var account = await FindAccountAsync(application, identity);
        var builder = Interactive(application, AzureAuthenticationScopes.InteractiveSignIn)
            .WithExtraScopesToConsent(AzureAuthenticationScopes.AdditionalConsent)
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

    public async Task<ConnectedIdentity> AuthorizeDirectoryReadAsync(
        ConnectedIdentity identity,
        CancellationToken cancellationToken)
    {
        EnsureInteractive(identity);
        var application = GetApplication(identity.ClientId);
        var account = await FindAccountAsync(application, identity);
        var builder = Interactive(application, AzureAuthenticationScopes.GraphDirectoryRead)
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

    public async Task RemoveAsync(
        ConnectedIdentity identity,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (identity.Type != IdentityType.InteractiveUser)
            return;

        var application = GetApplication(identity.ClientId);
        var account = await FindAccountAsync(application, identity);
        if (account is not null)
            await application.RemoveAsync(account);
    }

    public async Task<TokenCredential> GetCredentialAsync(
        ConnectedIdentity identity,
        CancellationToken cancellationToken)
    {
        EnsureInteractive(identity);
        var application = GetApplication(identity.ClientId);
        var account = await FindAccountAsync(application, identity)
            ?? throw new MsalUiRequiredException(
                "account_missing",
                "The mobile identity token-cache entry is missing. Sign in again.");
        return new MobileMsalTokenCredential(application, account);
    }

    private AcquireTokenInteractiveParameterBuilder Interactive(
        IPublicClientApplication application,
        IEnumerable<string> scopes)
    {
        var parent = parentWindow()
            ?? throw new InvalidOperationException(
                "The native mobile authentication parent is unavailable.");
        return application.AcquireTokenInteractive(scopes)
            .WithParentActivityOrWindow(parent);
    }

    private IPublicClientApplication GetApplication(string clientId)
    {
        clientId = NormalizeClientId(clientId);
        if (_applications.TryGetValue(clientId, out var application))
            return application;

        application = PublicClientApplicationBuilder.Create(clientId)
            .WithAuthority(
                AzureCloudInstance.AzurePublic,
                AadAuthorityAudience.AzureAdMultipleOrgs)
            .WithRedirectUri(redirectUri)
            .WithLegacyCacheCompatibility(false)
            .Build();
        _applications.Add(clientId, application);
        return application;
    }

    private static async Task<IAccount?> FindAccountAsync(
        IPublicClientApplication application,
        ConnectedIdentity identity) =>
        (await application.GetAccountsAsync()).FirstOrDefault(
            account => string.Equals(
                account.HomeAccountId.Identifier,
                identity.AccountIdentifier,
                StringComparison.Ordinal));

    private static ConnectedIdentity ToIdentity(
        string clientId,
        string displayName,
        AuthenticationResult result)
    {
        var accountId = result.Account.HomeAccountId.Identifier;
        var tenantId = result.Account.HomeAccountId.TenantId ?? result.TenantId;
        return new ConnectedIdentity(
            StableId(clientId, accountId),
            clientId,
            accountId,
            result.Account.Username,
            string.IsNullOrWhiteSpace(displayName)
                ? result.Account.Username
                : displayName.Trim(),
            tenantId,
            AuthenticationState.Ready,
            DateTimeOffset.UtcNow);
    }

    private static void EnsureInteractive(ConnectedIdentity identity)
    {
        if (identity.Type != IdentityType.InteractiveUser)
        {
            throw new PlatformNotSupportedException(
                "The mobile release supports interactive user identities only.");
        }
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

    private static string NormalizeClientId(string clientId)
    {
        if (!Guid.TryParse(clientId, out var parsed))
            throw new ArgumentException(
                "The Microsoft Entra public-client application ID must be a GUID.",
                nameof(clientId));
        return parsed.ToString("D");
    }

    private static Guid StableId(string clientId, string accountIdentifier)
    {
        var hash = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(
                $"{clientId}|{accountIdentifier}"));
        return new Guid(hash.AsSpan(0, 16));
    }

    private sealed class MobileMsalTokenCredential(
        IPublicClientApplication application,
        IAccount account) : TokenCredential
    {
        public override AccessToken GetToken(
            TokenRequestContext requestContext,
            CancellationToken cancellationToken) =>
            GetTokenAsync(requestContext, cancellationToken)
                .AsTask()
                .GetAwaiter()
                .GetResult();

        public override async ValueTask<AccessToken> GetTokenAsync(
            TokenRequestContext requestContext,
            CancellationToken cancellationToken)
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
            catch (MsalUiRequiredException exception)
            {
                throw new AuthenticationFailedException(
                    "Interactive Microsoft Entra authentication is required.",
                    exception);
            }
        }
    }
}
