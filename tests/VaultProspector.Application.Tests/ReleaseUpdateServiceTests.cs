using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using VaultProspector.Application;

namespace VaultProspector.Application.Tests;

public sealed class ReleaseUpdateServiceTests : IDisposable
{
    private const string Version = "0.2.0-preview.6";
    private const string PackageName =
        "VaultProspector-0.2.0-preview.6-win-x64.msi";
    private readonly string _updateDirectory = Path.Combine(
        Path.GetTempPath(),
        "VaultProspector.Tests",
        Guid.NewGuid().ToString("N"));

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
            "0.2.0-preview.5",
            new FakeLauncher());

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
            "0.2.0-preview.5",
            new FakeLauncher());

        await Assert.ThrowsAsync<InvalidDataException>(
            () => service.CheckAsync(
                TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DownloadVerifiesAndLaunchRehashesInstaller()
    {
        var package = Encoding.UTF8.GetBytes("trusted installer");
        var launcher = new FakeLauncher();
        var service = CreateService(
            package,
            currentVersion: "0.2.0-preview.5",
            launcher);
        var release = await service.CheckAsync(
            TestContext.Current.CancellationToken);

        var verified = await service.DownloadAndVerifyAsync(
            release,
            TestContext.Current.CancellationToken);
        await service.LaunchAsync(
            verified,
            TestContext.Current.CancellationToken);

        Assert.True(File.Exists(verified.InstallerPath));
        Assert.Equal(verified.InstallerPath, launcher.InstallerPath);
        Assert.Equal(
            release.ExpectedSha256,
            verified.Sha256,
            ignoreCase: true);
    }

    [Fact]
    public async Task TamperedDownloadIsRejectedWithoutRetainedPackage()
    {
        var trustedPackage = Encoding.UTF8.GetBytes("trusted installer");
        var tamperedPackage = Encoding.UTF8.GetBytes("tampered installe");
        var handler = CreateHandler(
            trustedPackage,
            downloadedPackage: tamperedPackage);
        var service = CreateService(
            handler,
            "0.2.0-preview.5",
            new FakeLauncher());
        var release = await service.CheckAsync(
            TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<InvalidDataException>(
            () => service.DownloadAndVerifyAsync(
                release,
                TestContext.Current.CancellationToken));

        Assert.False(
            File.Exists(
                Path.Combine(
                    _updateDirectory,
                    Version,
                    PackageName)));
        if (Directory.Exists(_updateDirectory))
        {
            Assert.Empty(
                Directory.EnumerateFiles(
                    _updateDirectory,
                    "*.partial",
                    SearchOption.AllDirectories));
        }
    }

    [Fact]
    public async Task ChangedInstallerIsNotLaunched()
    {
        var package = Encoding.UTF8.GetBytes("trusted installer");
        var launcher = new FakeLauncher();
        var service = CreateService(
            package,
            currentVersion: "0.2.0-preview.5",
            launcher);
        var release = await service.CheckAsync(
            TestContext.Current.CancellationToken);
        var verified = await service.DownloadAndVerifyAsync(
            release,
            TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(
            verified.InstallerPath,
            "changed",
            TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<InvalidDataException>(
            () => service.LaunchAsync(
                verified,
                TestContext.Current.CancellationToken));

        Assert.Null(launcher.InstallerPath);
    }

    public void Dispose()
    {
        if (!Directory.Exists(_updateDirectory))
            return;

        var resolvedRoot = Path.GetFullPath(
            Path.Combine(
                Path.GetTempPath(),
                "VaultProspector.Tests"));
        var resolvedTarget = Path.GetFullPath(
            _updateDirectory);
        if (resolvedTarget.StartsWith(
                resolvedRoot +
                Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase))
        {
            Directory.Delete(
                resolvedTarget,
                recursive: true);
        }
    }

    private GitHubReleaseUpdateService CreateService(
        byte[] package,
        string currentVersion,
        FakeLauncher? launcher = null) =>
        CreateService(
            CreateHandler(package),
            currentVersion,
            launcher ?? new FakeLauncher());

    private GitHubReleaseUpdateService CreateService(
        RouteHandler handler,
        string currentVersion,
        FakeLauncher launcher) =>
        new(
            new HttpClient(handler),
            _updateDirectory,
            currentVersion,
            launcher);

    private static RouteHandler CreateHandler(
        byte[] trustedPackage,
        byte[]? downloadedPackage = null)
    {
        var hash = Convert.ToHexString(
            SHA256.HashData(trustedPackage));
        var handler = new RouteHandler();
        handler.AddJson(
            ReleasesApi,
            CreateReleaseJson(trustedPackage));
        handler.AddText(
            ChecksumUri,
            $"{hash}  {PackageName}");
        handler.AddBytes(
            PackageUri,
            downloadedPackage ?? trustedPackage);
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

    private sealed class FakeLauncher : IUpdateInstallerLauncher
    {
        public string? InstallerPath { get; private set; }

        public void Launch(string installerPath) =>
            InstallerPath = installerPath;
    }

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

        public void AddText(
            string uri,
            string value) =>
            Add(
                uri,
                Encoding.UTF8.GetBytes(value),
                "text/plain");

        public void AddBytes(
            string uri,
            byte[] value) =>
            Add(
                uri,
                value,
                "application/octet-stream");

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
