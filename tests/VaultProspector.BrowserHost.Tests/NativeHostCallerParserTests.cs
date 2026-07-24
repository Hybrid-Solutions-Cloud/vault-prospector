using VaultProspector.BrowserHost;
using VaultProspector.BrowserProtocol;

namespace VaultProspector.BrowserHost.Tests;

public sealed class NativeHostCallerParserTests : IDisposable
{
    private const string ChromiumId = "fmkdaepdbgdbhdhcednhppbhhejeabin";
    private const string FirefoxId = "vault-prospector@hybrid-solutions.cloud";
    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), $"VaultProspector.BrowserHost.Tests-{Guid.NewGuid():N}");

    public NativeHostCallerParserTests()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(
            Path.Combine(_directory, BrowserHostConfiguration.FileName),
            $$"""
            {
              "protocolVersion": 1,
              "pipeName": "VaultProspector.BrowserBroker.v1",
              "chromiumExtensionIds": ["{{ChromiumId}}"],
              "firefoxExtensionIds": ["{{FirefoxId}}"]
            }
            """);
    }

    [Fact]
    public void ChromiumCallerRequiresExactAllowedOrigin()
    {
        var configuration = BrowserHostConfiguration.Load(_directory);

        var caller = NativeHostCallerParser.Parse(
            [$"chrome-extension://{ChromiumId}/", "--parent-window=0"],
            _directory,
            configuration);

        Assert.Equal(BrowserFamily.Chromium, caller.BrowserFamily);
        Assert.Equal(ChromiumId, caller.ExtensionId);
    }

    [Theory]
    [InlineData("chrome-extension://aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa/")]
    [InlineData("chrome-extension://fmkdaepdbgdbhdhcednhppbhhejeabin/path")]
    [InlineData("https://fmkdaepdbgdbhdhcednhppbhhejeabin/")]
    public void ChromiumCallerRejectsUntrustedOrigin(string origin)
    {
        var configuration = BrowserHostConfiguration.Load(_directory);

        Assert.Throws<BrowserProtocolException>(
            () => NativeHostCallerParser.Parse([origin], _directory, configuration));
    }

    [Fact]
    public void FirefoxCallerRequiresExactManifestAndAllowedId()
    {
        var configuration = BrowserHostConfiguration.Load(_directory);
        var manifest = Path.Combine(
            _directory,
            BrowserHostConfiguration.FirefoxManifestFileName);

        var caller = NativeHostCallerParser.Parse(
            [manifest, FirefoxId],
            _directory,
            configuration);

        Assert.Equal(BrowserFamily.Firefox, caller.BrowserFamily);
        Assert.Equal(FirefoxId, caller.ExtensionId);
    }

    [Fact]
    public void FirefoxCallerRejectsDifferentManifest()
    {
        var configuration = BrowserHostConfiguration.Load(_directory);

        Assert.Throws<BrowserProtocolException>(
            () => NativeHostCallerParser.Parse(
                [Path.Combine(_directory, "other.json"), FirefoxId],
                _directory,
                configuration));
    }

    [Fact]
    public void ConfigurationRejectsUnknownProperty()
    {
        File.WriteAllText(
            Path.Combine(_directory, BrowserHostConfiguration.FileName),
            $$"""
            {
              "protocolVersion": 1,
              "pipeName": "VaultProspector.BrowserBroker.v1",
              "chromiumExtensionIds": ["{{ChromiumId}}"],
              "firefoxExtensionIds": ["{{FirefoxId}}"],
              "unexpected": true
            }
            """);

        Assert.Throws<BrowserProtocolException>(
            () => BrowserHostConfiguration.Load(_directory));
    }

    [Fact]
    public void ConfigurationRejectsDuplicateIdentity()
    {
        File.WriteAllText(
            Path.Combine(_directory, BrowserHostConfiguration.FileName),
            $$"""
            {
              "protocolVersion": 1,
              "pipeName": "VaultProspector.BrowserBroker.v1",
              "chromiumExtensionIds": ["{{ChromiumId}}", "{{ChromiumId}}"],
              "firefoxExtensionIds": []
            }
            """);

        Assert.Throws<BrowserProtocolException>(
            () => BrowserHostConfiguration.Load(_directory));
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, true);
    }
}
