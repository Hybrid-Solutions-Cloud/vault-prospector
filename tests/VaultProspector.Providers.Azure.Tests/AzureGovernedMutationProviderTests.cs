using System.Net;
using System.Text;
using Azure.Core;
using VaultProspector.Application;
using VaultProspector.Domain;
using VaultProspector.Providers.Azure;

namespace VaultProspector.Providers.Azure.Tests;

public sealed class AzureGovernedMutationProviderTests
{
    [Fact]
    public async Task CreateSecretFailsClosedWhenNameAlreadyExists()
    {
        var handler = new SequenceHandler(
            Response(
                HttpStatusCode.OK,
                """{"id":"https://example.vault.azure.net/secrets/name/existing"}"""));
        var provider = CreateProvider(handler);
        using var value = new SensitiveValue("sensitive");

        await Assert.ThrowsAsync<GovernedMutationConflictException>(
            () => provider.ExecuteAsync(
                Identity(),
                Preview(GovernedAzureOperation.CreateSecret),
                value,
                TestContext.Current.CancellationToken));

        Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Get, handler.Requests[0].Method);
        Assert.DoesNotContain(
            "sensitive",
            handler.Requests[0].Body,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task SecretVersionChecksExpectedCurrentVersionBeforePut()
    {
        var handler = new SequenceHandler(
            Response(
                HttpStatusCode.OK,
                """{"id":"https://example.vault.azure.net/secrets/name/changed"}"""));
        var provider = CreateProvider(handler);
        using var value = new SensitiveValue("sensitive");

        await Assert.ThrowsAsync<GovernedMutationConflictException>(
            () => provider.ExecuteAsync(
                Identity(),
                Preview(
                    GovernedAzureOperation.CreateSecretVersion,
                    expectedVersion: "reviewed"),
                value,
                TestContext.Current.CancellationToken));

        Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Get, handler.Requests[0].Method);
    }

    [Fact]
    public async Task CreateSecretSubmitsExactlyOnePutAfterAbsenceCheck()
    {
        var handler = new SequenceHandler(
            Response(HttpStatusCode.NotFound, "{}"),
            Response(
                HttpStatusCode.OK,
                """{"id":"https://example.vault.azure.net/secrets/name/created"}"""));
        var provider = CreateProvider(handler);
        using var value = new SensitiveValue("sensitive");

        var result = await provider.ExecuteAsync(
            Identity(),
            Preview(GovernedAzureOperation.CreateSecret),
            value,
            TestContext.Current.CancellationToken);

        Assert.Equal("created", result.ProviderVersion);
        Assert.Equal(2, handler.Requests.Count);
        Assert.Equal(HttpMethod.Get, handler.Requests[0].Method);
        Assert.Equal(HttpMethod.Put, handler.Requests[1].Method);
        Assert.Contains(
            "\"value\":\"sensitive\"",
            handler.Requests[1].Body,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task SoftwareKeyRequestUsesOnlyAllowlistedProperties()
    {
        var handler = new SequenceHandler(
            Response(
                HttpStatusCode.OK,
                """{"id":"https://example.vault.azure.net/keys/name/version"}"""));
        var provider = CreateProvider(handler);

        var result = await provider.ExecuteAsync(
            Identity(),
            Preview(
                GovernedAzureOperation.CreateSoftwareKeyVersion),
            null,
            TestContext.Current.CancellationToken);

        Assert.Equal("version", result.ProviderVersion);
        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Contains("\"kty\":\"RSA\"", request.Body);
        Assert.Contains("\"key_size\":3072", request.Body);
        Assert.DoesNotContain("import", request.Body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("private", request.Body, StringComparison.OrdinalIgnoreCase);
    }

    private static AzureGovernedMutationProvider CreateProvider(
        HttpMessageHandler keyVaultHandler) =>
        new(
            new StaticCredentialProvider(),
            new HttpClient(keyVaultHandler),
            new HttpClient(new SequenceHandler()));

    private static ConnectedIdentity Identity() =>
        new(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            "22222222-2222-2222-2222-222222222222",
            "account",
            "operator@example.invalid",
            "Operator",
            "33333333-3333-3333-3333-333333333333",
            AuthenticationState.Ready,
            DateTimeOffset.UtcNow);

    private static GovernedMutationPreview Preview(
        GovernedAzureOperation operation,
        string expectedVersion = "") =>
        new(
            Guid.Parse("44444444-4444-4444-4444-444444444444"),
            operation,
            Identity().Id,
            "Operator",
            Identity().HomeTenantId,
            "55555555-5555-5555-5555-555555555555",
            "rg",
            "/subscriptions/55555555-5555-5555-5555-555555555555/resourceGroups/rg/providers/Microsoft.KeyVault/vaults/example",
            new Uri("https://example.vault.azure.net/"),
            "example",
            "name",
            "effect",
            expectedVersion,
            "recovery",
            "APPLY",
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow.AddMinutes(5));

    private static HttpResponseMessage Response(
        HttpStatusCode statusCode,
        string json) =>
        new(statusCode)
        {
            Content = new StringContent(
                json,
                Encoding.UTF8,
                "application/json"),
        };

    private sealed class StaticCredentialProvider :
        IAzureCredentialProvider
    {
        public Task<TokenCredential> GetCredentialAsync(
            ConnectedIdentity identity,
            CancellationToken cancellationToken) =>
            Task.FromResult<TokenCredential>(
                new StaticTokenCredential());
    }

    private sealed class StaticTokenCredential : TokenCredential
    {
        public override AccessToken GetToken(
            TokenRequestContext requestContext,
            CancellationToken cancellationToken) =>
            new("token", DateTimeOffset.UtcNow.AddHours(1));

        public override ValueTask<AccessToken> GetTokenAsync(
            TokenRequestContext requestContext,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult(
                new AccessToken(
                    "token",
                    DateTimeOffset.UtcNow.AddHours(1)));
    }

    private sealed record RecordedRequest(
        HttpMethod Method,
        Uri Uri,
        string Body);

    private sealed class SequenceHandler(
        params HttpResponseMessage[] responses) : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses =
            new(responses);
        public List<RecordedRequest> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var body = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(
                    cancellationToken);
            Requests.Add(
                new RecordedRequest(
                    request.Method,
                    request.RequestUri!,
                    body));
            if (_responses.Count == 0)
            {
                throw new InvalidOperationException(
                    "Unexpected HTTP request.");
            }
            return _responses.Dequeue();
        }
    }
}
