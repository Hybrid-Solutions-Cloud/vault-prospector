using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using VaultProspector.Application;

namespace VaultProspector.Application.Tests;

public sealed class ReleaseUpdateServiceTests
{
    private const string Version = "0.2.0-preview.6";
    private const string PackageName =
        "VaultProspector-0.2.0-preview.6-win-x64.msi";
    [Fact]
    public async Task CheckSelectsNewestTrustedRelease()
    {
        var package = Encoding.UTF8.GetBytes("trusted installer");
        var service = CreateService(
            package,
            currentVersion: "0.2.0-preview.5");

        var update = await service.CheckAsync(
            TestContext.Current.CancellationToken);

        Assert.Equal(
            ReleaseUpdateAvailability.Available,
            update.Availability);
        Assert.Equal(Version, update.LatestVersion);
        Assert.Equal(PackageName, update.PackageName);
        Assert.Contains(
            "Security and reliability fixes",
            update.ReleaseNotes,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task CheckDoesNotOfferDowngrade()
    {
        var service = CreateService(
            Encoding.UTF8.GetBytes("trusted installer"),
            currentVersion: "0.2.0-preview.7");

        var update = await service.CheckAsync(
            TestContext.Current.CancellationToken);

        Assert.Equal(
            ReleaseUpdateAvailability.Current,
            update.Availability);
    }

    [Fact]
    public async Task CheckRejectsUntrustedPublisher()
    {
        var package = Encoding.UTF8.GetBytes("trusted installer");
        var handler = new RouteHandler();
        handler.AddJson(
            ReleasesApi,
            CreateReleaseJson(
                package,
                publisher: "someone-else"));
        var service = CreateService(
            handler,
            "0.2.0-preview.5");

        await Assert.ThrowsAsync<InvalidDataException>(
            () => service.CheckAsync(
                TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task MalformedReleaseMetadataFailsClosed()
    {
        var handler = new RouteHandler();
        handler.AddJson(
            ReleasesApi,
            """{"unexpected":"object"}""");
        var service = CreateService(
            handler,
            "0.2.0-preview.5");

        await Assert.ThrowsAsync<InvalidDataException>(
            () => service.CheckAsync(
                TestContext.Current.CancellationToken));
    }

    [Fact]
    public void UpdateContractExposesDiscoveryOnly()
    {
        var methods = typeof(IReleaseUpdateService).GetMethods();

        Assert.Single(methods);
        Assert.Equal(nameof(IReleaseUpdateService.CheckAsync), methods[0].Name);
    }

    private static GitHubReleaseUpdateService CreateService(
        byte[] package,
        string currentVersion) =>
        CreateService(
            CreateHandler(package),
            currentVersion);

    private static GitHubReleaseUpdateService CreateService(
        RouteHandler handler,
        string currentVersion) =>
        new(
            new HttpClient(handler),
            currentVersion);

    private static RouteHandler CreateHandler(
        byte[] trustedPackage)
    {
        var handler = new RouteHandler();
        handler.AddJson(
            ReleasesApi,
            CreateReleaseJson(trustedPackage));
        return handler;
    }

    private static string CreateReleaseJson(
        byte[] package,
        string publisher = "hcs-platform-app[bot]")
    {
        var hash = Convert.ToHexString(
            SHA256.HashData(package));
        var release = new[]
        {
            new
            {
                draft = false,
                author = new { login = publisher },
                tag_name = $"v{Version}",
                name = $"Vault Prospector {Version}",
                body = "Security and reliability fixes.",
                html_url = ReleasePageUri,
                published_at = "2026-07-25T18:00:00Z",
                assets = new object[]
                {
                    new
                    {
                        name = PackageName,
                        browser_download_url = PackageUri,
                        digest = $"sha256:{hash}",
                        size = package.LongLength,
                    },
                    new
                    {
                        name = $"{PackageName}.sha256",
                        browser_download_url = ChecksumUri,
                        digest = $"sha256:{new string('A', 64)}",
                        size = 90,
                    },
                    new
                    {
                        name = $"{PackageName}.sigstore.json",
                        browser_download_url = SigstoreUri,
                        digest = $"sha256:{new string('B', 64)}",
                        size = 100,
                    },
                },
            },
        };
        return JsonSerializer.Serialize(release);
    }

    private const string ReleasesApi =
        "https://api.github.com/repos/Hybrid-Solutions-Cloud/vault-prospector-releases/releases?per_page=20";
    private const string DownloadRoot =
        "https://github.com/Hybrid-Solutions-Cloud/vault-prospector-releases/releases/download/v0.2.0-preview.6/";
    private const string PackageUri =
        DownloadRoot + PackageName;
    private const string ChecksumUri =
        PackageUri + ".sha256";
    private const string SigstoreUri =
        PackageUri + ".sigstore.json";
    private const string ReleasePageUri =
        "https://github.com/Hybrid-Solutions-Cloud/vault-prospector-releases/releases/tag/v0.2.0-preview.6";

    private sealed class RouteHandler : HttpMessageHandler
    {
        private readonly Dictionary<string, Func<HttpResponseMessage>>
            _routes = new(StringComparer.Ordinal);

        public void AddJson(
            string uri,
            string json) =>
            Add(
                uri,
                Encoding.UTF8.GetBytes(json),
                "application/json");

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (request.RequestUri is not null &&
                _routes.TryGetValue(
                    request.RequestUri.AbsoluteUri,
                    out var createResponse))
            {
                return Task.FromResult(createResponse());
            }

            return Task.FromResult(
                new HttpResponseMessage(HttpStatusCode.NotFound));
        }

        private void Add(
            string uri,
            byte[] value,
            string mediaType) =>
            _routes.Add(
                uri,
                () =>
                {
                    var response = new HttpResponseMessage(
                        HttpStatusCode.OK)
                    {
                        Content = new ByteArrayContent(value),
                    };
                    response.Content.Headers.ContentType =
                        new System.Net.Http.Headers.MediaTypeHeaderValue(
                            mediaType);
                    response.Content.Headers.ContentLength =
                        value.LongLength;
                    return response;
                });
    }
}
