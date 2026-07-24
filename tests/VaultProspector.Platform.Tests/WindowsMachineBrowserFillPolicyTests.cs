using System.Text;
using VaultProspector.BrowserProtocol;
using VaultProspector.Domain;

namespace VaultProspector.Platform.Tests;

public sealed class WindowsMachineBrowserFillPolicyTests : IDisposable
{
    private static readonly Encoding Utf8WithoutBom = new UTF8Encoding(false);

    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        $"VaultProspectorPolicyTests-{Guid.NewGuid():N}");
    private readonly string _policyPath;

    public WindowsMachineBrowserFillPolicyTests()
    {
        Directory.CreateDirectory(_directory);
        _policyPath = Path.Combine(_directory, "browser-fill-policy.json");
    }

    [Fact]
    public async Task ExactEnabledRuleAllowsOnlyListedCombination()
    {
        await File.WriteAllTextAsync(
            _policyPath,
            ValidPolicy(),
            Utf8WithoutBom,
            TestContext.Current.CancellationToken);
        var policy = new WindowsMachineBrowserFillPolicy(_policyPath, _directory);

        var allowed = await policy.EvaluateAsync(
            BrowserFamily.Chromium,
            Origin("https://login.example.com"),
            Origin("https://login.example.com"),
            BrowserMappingFieldPurpose.Password,
            TestContext.Current.CancellationToken);
        var wrongPurpose = await policy.EvaluateAsync(
            BrowserFamily.Chromium,
            Origin("https://login.example.com"),
            Origin("https://login.example.com"),
            BrowserMappingFieldPurpose.Username,
            TestContext.Current.CancellationToken);
        var wrongBrowser = await policy.EvaluateAsync(
            BrowserFamily.Firefox,
            Origin("https://login.example.com"),
            Origin("https://login.example.com"),
            BrowserMappingFieldPurpose.Password,
            TestContext.Current.CancellationToken);

        Assert.True(allowed.IsAllowed);
        Assert.False(wrongPurpose.IsAllowed);
        Assert.False(wrongBrowser.IsAllowed);
        Assert.Contains("enabled for 1 exact destination", await policy.GetStatusAsync(
            TestContext.Current.CancellationToken), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("""{"version":1,"enabled":false,"allowedDestinations":[]}""")]
    [InlineData("""{"version":1,"enabled":true,"allowedDestinations":[],"unexpected":true}""")]
    [InlineData("""{"version":1,"version":1,"enabled":true,"allowedDestinations":[]}""")]
    [InlineData("""
        {"version":1,"enabled":true,"allowedDestinations":[{
          "topOrigin":"http://login.example.com",
          "frameOrigin":"http://login.example.com",
          "browserFamilies":["chromium"],
          "fieldPurposes":["password"]
        }]}
        """)]
    public async Task InvalidOrDisabledPolicyFailsClosed(string json)
    {
        await File.WriteAllTextAsync(
            _policyPath,
            json,
            Utf8WithoutBom,
            TestContext.Current.CancellationToken);
        var policy = new WindowsMachineBrowserFillPolicy(_policyPath, _directory);

        var decision = await policy.EvaluateAsync(
            BrowserFamily.Chromium,
            Origin("https://login.example.com"),
            Origin("https://login.example.com"),
            BrowserMappingFieldPurpose.Password,
            TestContext.Current.CancellationToken);

        Assert.False(decision.IsAllowed);
        Assert.Contains("disabled", decision.SafeReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PolicyOutsideTrustedInstallationRootFailsClosed()
    {
        await File.WriteAllTextAsync(
            _policyPath,
            ValidPolicy(),
            Utf8WithoutBom,
            TestContext.Current.CancellationToken);
        var otherRoot = Path.Combine(_directory, "protected");
        Directory.CreateDirectory(otherRoot);
        var policy = new WindowsMachineBrowserFillPolicy(_policyPath, otherRoot);

        var decision = await policy.EvaluateAsync(
            BrowserFamily.Chromium,
            Origin("https://login.example.com"),
            Origin("https://login.example.com"),
            BrowserMappingFieldPurpose.Password,
            TestContext.Current.CancellationToken);

        Assert.False(decision.IsAllowed);
        Assert.Contains("outside", decision.SafeReason, StringComparison.OrdinalIgnoreCase);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, recursive: true);
    }

    private static CanonicalBrowserOrigin Origin(string value) =>
        CanonicalBrowserOrigin.Parse(value);

    private static string ValidPolicy() =>
        """
        {
          "version": 1,
          "enabled": true,
          "allowedDestinations": [
            {
              "topOrigin": "https://login.example.com",
              "frameOrigin": "https://login.example.com",
              "browserFamilies": [ "chromium" ],
              "fieldPurposes": [ "password" ]
            }
          ]
        }
        """;
}
