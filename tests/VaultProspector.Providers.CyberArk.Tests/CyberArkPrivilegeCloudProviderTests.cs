using System.Net;
using System.Text;
using VaultProspector.Application;
using VaultProspector.Domain;
using VaultProspector.Providers.CyberArk;

namespace VaultProspector.Providers.CyberArk.Tests;

public sealed class CyberArkPrivilegeCloudProviderTests
{
    [Fact]
    public async Task ValidateUsesServiceUserFlowAndPrivilegeCloudApi()
    {
        var handler = new RecordingHandler(
            Token(),
            Authorized(),
            Json(HttpStatusCode.OK, """{"value":[]}"""));
        var provider = Provider(handler);
        using var credential = new SensitiveValue("synthetic-client-credential");

        await provider.ValidateAsync(Profile(), credential, CancellationToken.None);

        Assert.Equal(3, handler.Requests.Count);
        Assert.Equal(
            "https://tenant.id.cyberark.cloud/Oauth2/Token/VaultProspector",
            handler.Requests[0].Uri);
        Assert.StartsWith("Basic ", handler.Requests[0].Authorization);
        Assert.Equal(
            "https://tenant.id.cyberark.cloud/OAuth2/Authorize/VaultProspector?client_id=VaultProspector&response_type=id_token&scope=openid%20profile%20api&redirect_uri=https%3A%2F%2Fcyberark.cloud%2Fredirect",
            handler.Requests[1].Uri);
        Assert.Equal("Bearer identity-access-token", handler.Requests[1].Authorization);
        Assert.Equal(
            "https://tenant.privilegecloud.cyberark.cloud/PasswordVault/API/Safes?limit=1",
            handler.Requests[2].Uri);
        Assert.Equal("Bearer platform-token", handler.Requests[2].Authorization);
        Assert.DoesNotContain(
            "synthetic-client-credential",
            string.Join('|', handler.Requests.Select(request => request.Body)));
    }

    [Fact]
    public async Task DiscoverMapsSafesAccountsVersionsAndPermissions()
    {
        var handler = new RecordingHandler(
            Token(),
            Authorized(),
            Json(HttpStatusCode.OK, """
                {"value":[{"safeUrlId":"safe-1","safeName":"Production","description":"Prod","location":"\\","numberOfDaysRetention":30,"numberOfVersionsRetention":5,"olacEnabled":true,"creationTime":1700000000,"lastModificationTime":1700000100}]}
                """),
            Json(HttpStatusCode.OK, """
                {"memberName":"svc@example.com","memberType":"User","permissions":{"listAccounts":true,"useAccounts":true,"retrieveAccounts":true,"viewAuditLog":true,"accessWithoutConfirmation":false,"requestsAuthorizationLevel1":true,"requestsAuthorizationLevel2":false}}
                """),
            Json(HttpStatusCode.OK, """
                {"value":[{"id":"account-1","name":"database-admin","safeName":"Production","platformId":"WinServerLocal","userName":"administrator","address":"db.example.test","secretType":"password","status":"ready","createdTime":1700000200,"categoryModificationTime":1700000300}],"nextLink":"https://tenant.privilegecloud.cyberark.cloud/PasswordVault/API/Accounts?offset=1&limit=100"}
                """),
            Json(HttpStatusCode.OK, """{"value":[]}"""),
            Json(HttpStatusCode.OK, """{"versions":[{"isTemporary":false,"modificationDate":"2026-07-23T12:00:00Z","modifiedBy":"operator","versionId":3}]}"""));
        var provider = Provider(handler);
        using var credential = new SensitiveValue("synthetic");

        var snapshot = await provider.DiscoverAsync(
            Profile(),
            credential,
            CancellationToken.None);

        var safe = Assert.Single(snapshot.Safes);
        Assert.Equal("Production", safe.Name);
        Assert.True(safe.ObjectLevelAccessControlEnabled);
        var account = Assert.Single(snapshot.Accounts);
        Assert.Equal("database-admin", account.Name);
        Assert.Equal(CyberArkSecretType.Password, account.SecretType);
        Assert.NotEmpty(account.MetadataFingerprint);
        Assert.Equal(3, Assert.Single(snapshot.Versions).VersionId);
        var permission = Assert.Single(snapshot.Permissions);
        Assert.True(permission.RetrieveAccounts);
        Assert.True(permission.ViewAuditLog);
        Assert.Empty(snapshot.Errors);
    }

    [Fact]
    public async Task DiscoverRejectsPaginationOutsideConfiguredOrigin()
    {
        var handler = new RecordingHandler(
            Token(),
            Authorized(),
            Json(HttpStatusCode.OK, """
                {"value":[],"nextLink":"https://attacker.example/PasswordVault/API/Safes?offset=1"}
                """));
        var provider = Provider(handler);
        using var credential = new SensitiveValue("synthetic");

        var exception = await Assert.ThrowsAsync<CyberArkProviderException>(
            () => provider.DiscoverAsync(
                Profile(),
                credential,
                CancellationToken.None));

        Assert.Equal("untrusted_pagination", exception.Category);
    }

    [Fact]
    public async Task ValidateRejectsUnsupportedEndpointBeforeNetwork()
    {
        var handler = new RecordingHandler();
        var provider = Provider(handler);
        var profile = Profile() with
        {
            PrivilegeCloudUrl = new Uri("https://example.com/"),
        };
        using var credential = new SensitiveValue("synthetic");

        await Assert.ThrowsAsync<CyberArkConfigurationException>(
            () => provider.ValidateAsync(
                profile,
                credential,
                CancellationToken.None));

        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task ValidateDoesNotExposeServerBodyOrCredentialInFailure()
    {
        var handler = new RecordingHandler(
            Json(
                HttpStatusCode.Unauthorized,
                """{"error":"synthetic-client-credential"}"""));
        var provider = Provider(handler);
        using var credential = new SensitiveValue("synthetic-client-credential");

        var exception = await Assert.ThrowsAsync<CyberArkProviderException>(
            () => provider.ValidateAsync(
                Profile(),
                credential,
                CancellationToken.None));

        Assert.Equal("authentication_required", exception.Category);
        Assert.DoesNotContain(
            "synthetic-client-credential",
            exception.ToString(),
            StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden, "permission_denied")]
    [InlineData(HttpStatusCode.TooManyRequests, "throttled")]
    [InlineData(HttpStatusCode.ServiceUnavailable, "service_unavailable")]
    public async Task ValidateMapsProviderFailureWithoutReturningBody(
        HttpStatusCode statusCode,
        string expectedCategory)
    {
        var handler = new RecordingHandler(
            Json(statusCode, """{"message":"server-only-detail"}"""));
        var provider = Provider(handler);
        using var credential = new SensitiveValue("synthetic");

        var exception = await Assert.ThrowsAsync<CyberArkProviderException>(
            () => provider.ValidateAsync(
                Profile(),
                credential,
                CancellationToken.None));

        Assert.Equal(expectedCategory, exception.Category);
        Assert.Equal(statusCode, exception.StatusCode);
        Assert.DoesNotContain(
            "server-only-detail",
            exception.ToString(),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task ValidateRejectsUntrustedAuthorizationRedirect()
    {
        var redirect = new HttpResponseMessage(HttpStatusCode.Found);
        redirect.Headers.Location = new Uri(
            "https://attacker.example/redirect#id_token=platform-token");
        var handler = new RecordingHandler(Token(), redirect);
        var provider = Provider(handler);
        using var credential = new SensitiveValue("synthetic");

        var exception = await Assert.ThrowsAsync<CyberArkProviderException>(
            () => provider.ValidateAsync(
                Profile(),
                credential,
                CancellationToken.None));

        Assert.Equal("untrusted_redirect", exception.Category);
        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public async Task ValidateRejectsMalformedMetadataResponse()
    {
        var handler = new RecordingHandler(
            Token(),
            Authorized(),
            Json(HttpStatusCode.OK, "{not-json"));
        var provider = Provider(handler);
        using var credential = new SensitiveValue("synthetic");

        var exception = await Assert.ThrowsAsync<CyberArkProviderException>(
            () => provider.ValidateAsync(
                Profile(),
                credential,
                CancellationToken.None));

        Assert.Equal("invalid_response", exception.Category);
    }

    [Fact]
    public async Task RetrieveSendsReasonVersionAndActionAndReturnsSensitiveValue()
    {
        var handler = new RecordingHandler(
            Token(),
            Authorized(),
            Json(HttpStatusCode.OK, "\"synthetic-secret\""));
        var provider = Provider(handler);
        using var credential = new SensitiveValue("synthetic-client");
        var account = Account();

        using var value = await provider.RetrieveAsync(
            Profile(),
            credential,
            account,
            7,
            "approved test",
            "copy",
            CancellationToken.None);

        Assert.Equal("synthetic-secret", value.Reveal());
        var request = handler.Requests[^1];
        Assert.Equal(
            "https://tenant.privilegecloud.cyberark.cloud/PasswordVault/API/Accounts/account-1/Password/Retrieve",
            request.Uri);
        Assert.Contains("\"reason\":\"approved test\"", request.Body);
        Assert.Contains("\"Version\":\"7\"", request.Body);
        Assert.Contains("\"ActionType\":\"copy\"", request.Body);
    }

    [Fact]
    public async Task ValidateRejectsOversizedResponse()
    {
        var oversized = new StringContent(
            new string('x', (4 * 1024 * 1024) + 1),
            Encoding.UTF8,
            "application/json");
        var handler = new RecordingHandler(
            Token(),
            Authorized(),
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = oversized,
            });
        var provider = Provider(handler);
        using var credential = new SensitiveValue("synthetic");

        var exception = await Assert.ThrowsAsync<CyberArkProviderException>(
            () => provider.ValidateAsync(
                Profile(),
                credential,
                CancellationToken.None));

        Assert.Equal("response_too_large", exception.Category);
    }

    private static CyberArkPrivilegeCloudProvider Provider(
        HttpMessageHandler handler) =>
        new(
            new HttpClient(handler)
            {
                Timeout = TimeSpan.FromSeconds(5),
            },
            new FixedClock(new DateTimeOffset(
                2026,
                7,
                23,
                12,
                30,
                0,
                TimeSpan.Zero)));

    private static CyberArkProfile Profile() =>
        new(
            Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee"),
            "Production CyberArk",
            new Uri("https://tenant.id.cyberark.cloud/"),
            new Uri("https://tenant.privilegecloud.cyberark.cloud/"),
            "svc@example.com",
            "VaultProspector",
            CyberArkAuthenticationState.Unknown,
            true,
            DateTimeOffset.MinValue,
            null);

    private static CyberArkAccount Account() =>
        new(
            Profile().Id,
            "account-1",
            "Production",
            "database-admin",
            "administrator",
            "db.example.test",
            "WinServerLocal",
            CyberArkSecretType.Password,
            "ready",
            null,
            null,
            "fingerprint",
            DateTimeOffset.UtcNow);

    private static HttpResponseMessage Token() =>
        Json(
            HttpStatusCode.OK,
            """{"access_token":"identity-access-token"}""");

    private static HttpResponseMessage Authorized()
    {
        var response = new HttpResponseMessage(HttpStatusCode.Found);
        response.Headers.Location = new Uri(
            "https://cyberark.cloud/redirect#id_token=platform-token");
        return response;
    }

    private static HttpResponseMessage Json(
        HttpStatusCode status,
        string json) =>
        new(status)
        {
            Content = new StringContent(
                json,
                Encoding.UTF8,
                "application/json"),
        };

    private sealed class FixedClock(DateTimeOffset utcNow) : IClock
    {
        public DateTimeOffset UtcNow { get; } = utcNow;
    }

    private sealed class RecordingHandler(
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
                : await request.Content.ReadAsStringAsync(cancellationToken);
            Requests.Add(new RecordedRequest(
                request.RequestUri?.AbsoluteUri ?? string.Empty,
                request.Headers.Authorization?.ToString() ?? string.Empty,
                body));
            if (_responses.Count == 0)
                throw new InvalidOperationException(
                    "No fake CyberArk response remains.");
            var response = _responses.Dequeue();
            response.RequestMessage = request;
            return response;
        }
    }

    private sealed record RecordedRequest(
        string Uri,
        string Authorization,
        string Body);
}
