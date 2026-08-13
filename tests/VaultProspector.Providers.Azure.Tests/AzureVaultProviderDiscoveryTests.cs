using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Azure.Core;
using Azure.Core.Pipeline;
using Azure.ResourceManager;
using VaultProspector.Application;
using VaultProspector.Domain;
using VaultProspector.Providers.Azure;

namespace VaultProspector.Providers.Azure.Tests;

public sealed class AzureVaultProviderDiscoveryTests
{
    private const string HomeTenant = "11111111-1111-1111-1111-111111111111";
    private const string GuestTenant = "22222222-2222-2222-2222-222222222222";
    private const string HomeSubscription = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa";
    private const string GuestSubscription = "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb";

    [Fact]
    public async Task DiscoversSubscriptionsUsingEachAccessibleTenantContext()
    {
        var credential = new TenantRecordingCredential();
        var handler = new MultiTenantArmHandler();
        var provider = CreateProvider(credential, handler);

        var snapshot = await provider.DiscoverAsync(
            Identity(),
            [],
            [],
            TestContext.Current.CancellationToken);

        Assert.Equal(2, snapshot.Tenants.Count);
        Assert.Equal(
            [HomeSubscription, GuestSubscription],
            snapshot.Subscriptions
                .Select(subscription => subscription.SubscriptionId)
                .Order(StringComparer.Ordinal)
                .ToArray());
        Assert.Contains(HomeTenant, credential.RequestedTenantIds);
        Assert.Contains(GuestTenant, credential.RequestedTenantIds);
        Assert.Empty(snapshot.Errors);
    }

    [Fact]
    public async Task EnumeratesVaultMetadataUsingItsDiscoveredTenantContext()
    {
        var credential = new TenantRecordingCredential();
        var handler = new MultiTenantArmHandler(includeVaults: true);
        var observedTenantTokens =
            new List<(string VaultTenantId, string Token)>();
        var provider = CreateProvider(
            credential,
            handler,
            async (
                currentCredential,
                vault,
                _,
                _,
                cancellationToken) =>
            {
                var token = await currentCredential.GetTokenAsync(
                    new TokenRequestContext(
                        ["https://vault.azure.net/.default"]),
                    cancellationToken);
                observedTenantTokens.Add((vault.TenantId, token.Token));
                return new AzureVaultProvider.VaultPermissionObservation(
                    "Metadata access allowed.",
                    null);
            });

        var snapshot = await provider.DiscoverAsync(
            Identity(),
            [],
            [],
            TestContext.Current.CancellationToken);

        Assert.Equal(2, snapshot.Vaults.Count);
        Assert.Equal(
            [
                (HomeTenant, $"token-{HomeTenant}"),
                (GuestTenant, $"token-{GuestTenant}"),
            ],
            observedTenantTokens
                .OrderBy(observation => observation.VaultTenantId)
                .ToArray());
        Assert.Empty(snapshot.Errors);
    }

    [Fact]
    public async Task ExcludedTenantRemainsVisibleButItsSubscriptionsAreNotRequested()
    {
        var credential = new TenantRecordingCredential();
        var handler = new MultiTenantArmHandler();
        var provider = CreateProvider(credential, handler);
        var constraints = new VaultDiscoveryConstraints(
            excludedTenantIds: [GuestTenant]);

        var snapshot = await provider.DiscoverAsync(
            Identity(),
            [],
            [],
            constraints,
            TestContext.Current.CancellationToken);

        Assert.Equal(2, snapshot.Tenants.Count);
        Assert.Equal(HomeSubscription, Assert.Single(snapshot.Subscriptions).SubscriptionId);
        Assert.DoesNotContain(GuestTenant, handler.SubscriptionRequestTenantIds);
    }

    [Fact]
    public async Task TenantFailureDoesNotDiscardOtherTenantResults()
    {
        var credential = new TenantRecordingCredential();
        var handler = new MultiTenantArmHandler(failingTenantId: GuestTenant);
        var provider = CreateProvider(credential, handler);

        var snapshot = await provider.DiscoverAsync(
            Identity(),
            [],
            [],
            TestContext.Current.CancellationToken);

        Assert.Equal(2, snapshot.Tenants.Count);
        Assert.Equal(HomeSubscription, Assert.Single(snapshot.Subscriptions).SubscriptionId);
        var error = Assert.Single(snapshot.Errors);
        Assert.Contains("tenant:", error.Scope, StringComparison.Ordinal);
        Assert.DoesNotContain(GuestTenant, error.Scope, StringComparison.Ordinal);
        Assert.Equal(GuestTenant, error.RetryScope?.TenantId);
    }

    private static AzureVaultProvider CreateProvider(
        TenantRecordingCredential credential,
        HttpMessageHandler handler,
        AzureVaultProvider.VaultMetadataEnumerator? vaultMetadataEnumerator =
            null)
    {
        var options = new ArmClientOptions
        {
            Transport = new HttpClientTransport(new HttpClient(handler)),
            Retry =
            {
                MaxRetries = 0,
            },
        };
        return new AzureVaultProvider(
            new StaticCredentialProvider(credential),
            current => new ArmClient(current, null, options),
            vaultMetadataEnumerator);
    }

    private static ConnectedIdentity Identity() => new(
        Guid.Parse("33333333-3333-3333-3333-333333333333"),
        "44444444-4444-4444-4444-444444444444",
        "account",
        "user@example.invalid",
        "Multi-tenant account",
        HomeTenant,
        AuthenticationState.Ready,
        DateTimeOffset.UtcNow);

    private sealed class StaticCredentialProvider(TokenCredential credential)
        : IAzureCredentialProvider
    {
        public Task<TokenCredential> GetCredentialAsync(
            ConnectedIdentity identity,
            CancellationToken cancellationToken) =>
            Task.FromResult(credential);
    }

    private sealed class TenantRecordingCredential : TokenCredential
    {
        public List<string> RequestedTenantIds { get; } = [];

        public override AccessToken GetToken(
            TokenRequestContext requestContext,
            CancellationToken cancellationToken) =>
            CreateToken(requestContext);

        public override ValueTask<AccessToken> GetTokenAsync(
            TokenRequestContext requestContext,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult(CreateToken(requestContext));

        private AccessToken CreateToken(TokenRequestContext context)
        {
            var tenantId = context.TenantId ?? "default";
            RequestedTenantIds.Add(tenantId);
            return new AccessToken(
                $"token-{tenantId}",
                DateTimeOffset.UtcNow.AddHours(1));
        }
    }

    private sealed class MultiTenantArmHandler(
        string? failingTenantId = null,
        bool includeVaults = false)
        : HttpMessageHandler
    {
        public List<string> SubscriptionRequestTenantIds { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var path = request.RequestUri?.AbsolutePath ?? string.Empty;
            if (path.Equals("/tenants", StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(Json(HttpStatusCode.OK, $$"""
                    {"value":[
                      {"id":"/tenants/{{HomeTenant}}","tenantId":"{{HomeTenant}}","tenantCategory":"Home","displayName":"Home tenant","defaultDomain":"home.example.invalid","tenantType":"AAD"},
                      {"id":"/tenants/{{GuestTenant}}","tenantId":"{{GuestTenant}}","tenantCategory":"ProjectedBy","displayName":"Guest tenant","defaultDomain":"guest.example.invalid","tenantType":"AAD"}
                    ]}
                    """));
            }

            var tenantId = TenantIdFrom(request.Headers.Authorization);
            if (path.Equals("/subscriptions", StringComparison.OrdinalIgnoreCase))
            {
                SubscriptionRequestTenantIds.Add(tenantId);
                if (string.Equals(tenantId, failingTenantId, StringComparison.OrdinalIgnoreCase))
                    return Task.FromResult(Json(HttpStatusCode.Forbidden, "{\"error\":{\"code\":\"AuthorizationFailed\",\"message\":\"denied\"}}"));

                var subscriptionId = string.Equals(tenantId, HomeTenant, StringComparison.OrdinalIgnoreCase)
                    ? HomeSubscription
                    : GuestSubscription;
                return Task.FromResult(Json(HttpStatusCode.OK, $$"""
                    {"value":[{"id":"/subscriptions/{{subscriptionId}}","subscriptionId":"{{subscriptionId}}","displayName":"Subscription {{tenantId[..4]}}","state":"Enabled","tenantId":"{{tenantId}}"}]}
                    """));
            }

            if (path.EndsWith("/resources", StringComparison.OrdinalIgnoreCase))
            {
                if (!includeVaults)
                    return Task.FromResult(Json(
                        HttpStatusCode.OK,
                        "{\"value\":[]}"));

                var subscriptionId =
                    string.Equals(
                        tenantId,
                        HomeTenant,
                        StringComparison.OrdinalIgnoreCase)
                        ? HomeSubscription
                        : GuestSubscription;
                var vaultName = $"vault-{tenantId[..4]}";
                return Task.FromResult(Json(HttpStatusCode.OK, $$"""
                    {"value":[{
                      "id":"/subscriptions/{{subscriptionId}}/resourceGroups/rg/providers/Microsoft.KeyVault/vaults/{{vaultName}}",
                      "name":"{{vaultName}}",
                      "type":"Microsoft.KeyVault/vaults",
                      "location":"eastus",
                      "tags":{}
                    }]}
                    """));
            }

            return Task.FromResult(Json(HttpStatusCode.NotFound, "{\"error\":{\"code\":\"NotFound\",\"message\":\"unexpected test request\"}}"));
        }

        private static string TenantIdFrom(AuthenticationHeaderValue? authorization)
        {
            const string prefix = "token-";
            var parameter = authorization?.Parameter ?? string.Empty;
            return parameter.StartsWith(prefix, StringComparison.Ordinal)
                ? parameter[prefix.Length..]
                : string.Empty;
        }

        private static HttpResponseMessage Json(HttpStatusCode statusCode, string json) =>
            new(statusCode)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
            };
    }
}
