using Azure.Core;

namespace VaultProspector.Providers.Azure;

internal sealed class TenantScopedCredential(
    TokenCredential inner,
    string tenantId) : TokenCredential
{
    public override AccessToken GetToken(
        TokenRequestContext requestContext,
        CancellationToken cancellationToken) =>
        inner.GetToken(WithTenant(requestContext), cancellationToken);

    public override ValueTask<AccessToken> GetTokenAsync(
        TokenRequestContext requestContext,
        CancellationToken cancellationToken) =>
        inner.GetTokenAsync(WithTenant(requestContext), cancellationToken);

    private TokenRequestContext WithTenant(TokenRequestContext context) =>
        new(
            context.Scopes,
            context.ParentRequestId,
            context.Claims,
            tenantId,
            context.IsCaeEnabled,
            context.IsProofOfPossessionEnabled,
            context.ProofOfPossessionNonce,
            context.ResourceRequestUri,
            context.ResourceRequestMethod);
}
