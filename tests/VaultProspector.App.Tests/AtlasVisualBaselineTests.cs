using System.Security.Cryptography;
using System.Text.Json;
using System.Xml.Linq;

namespace VaultProspector.App.Tests;

public sealed class AtlasVisualBaselineTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    [Fact]
    public void ApprovedAtlasSourcesAndReferenceImagesMatchBaseline()
    {
        var baselinePath = FindRepoFile(
            "tests/VaultProspector.App.Tests/Baselines/atlas.visual-baseline.json");
        var baseline = JsonSerializer.Deserialize<AtlasBaseline>(
            File.ReadAllText(baselinePath),
            JsonOptions);

        Assert.NotNull(baseline);
        Assert.Equal("Atlas", baseline.Direction);
        Assert.NotEmpty(baseline.Files);

        Assert.All(
            baseline.Files,
            item => Assert.Equal(
                item.Sha256,
                Convert.ToHexString(
                    SHA256.HashData(
                        File.ReadAllBytes(
                            FindRepoFile(item.Path)))),
                ignoreCase: true));
    }

    [Fact]
    public void InstallerUsesAtlasBrandingAndAnInteractiveAccessibleFlow()
    {
        var package = XDocument.Load(FindRepoFile("installer/Package.wxs"));
        var wixNamespace = XNamespace.Get("http://wixtoolset.org/schemas/v4/wxs");
        var uiNamespace = XNamespace.Get("http://wixtoolset.org/schemas/v4/wxs/ui");

        var wixUi = package.Descendants(uiNamespace + "WixUI").Single();
        Assert.Equal("WixUI_InstallDir", wixUi.Attribute("Id")?.Value);
        Assert.Equal("INSTALLFOLDER", wixUi.Attribute("InstallDirectory")?.Value);
        Assert.Contains(
            package.Descendants(wixNamespace + "UIRef"),
            element =>
                element.Attribute("Id")?.Value ==
                "WixUI_ErrorProgressText");

        var variables = package
            .Descendants(wixNamespace + "WixVariable")
            .ToDictionary(
                element => element.Attribute("Id")!.Value,
                element => element.Attribute("Value")!.Value,
                StringComparer.Ordinal);
        Assert.Equal(
            "$(sys.SOURCEFILEDIR)Assets\\AtlasDialog.bmp",
            variables["WixUIDialogBmp"]);
        Assert.Equal(
            "$(sys.SOURCEFILEDIR)Assets\\AtlasBanner.bmp",
            variables["WixUIBannerBmp"]);
        Assert.Equal(
            "$(sys.SOURCEFILEDIR)Assets\\License.rtf",
            variables["WixUILicenseRtf"]);

        var properties = package
            .Descendants(wixNamespace + "Property")
            .ToDictionary(
                element => element.Attribute("Id")!.Value,
                element => element.Attribute("Value")!.Value,
                StringComparer.Ordinal);
        Assert.Contains(
            "preserve encrypted local state",
            properties["WIXUI_EXITDIALOGOPTIONALTEXT"],
            StringComparison.OrdinalIgnoreCase);
        Assert.Equal(
            "Launch Vault Prospector",
            properties["WIXUI_EXITDIALOGOPTIONALCHECKBOXTEXT"]);

        var launch = package
            .Descendants(wixNamespace + "CustomAction")
            .Single(element =>
                element.Attribute("Id")?.Value == "LaunchVaultProspector");
        Assert.Equal("MainExecutableFile", launch.Attribute("FileRef")?.Value);
        Assert.Equal("yes", launch.Attribute("Impersonate")?.Value);
        Assert.Equal("asyncNoWait", launch.Attribute("Return")?.Value);

        AssertBitmap(
            FindRepoFile("installer/Assets/AtlasDialog.bmp"),
            expectedWidth: 493,
            expectedHeight: 312);
        AssertBitmap(
            FindRepoFile("installer/Assets/AtlasBanner.bmp"),
            expectedWidth: 493,
            expectedHeight: 58);
    }

    private static void AssertBitmap(
        string path,
        int expectedWidth,
        int expectedHeight)
    {
        using var stream = File.OpenRead(path);
        using var reader = new BinaryReader(stream);

        Assert.Equal((byte)'B', reader.ReadByte());
        Assert.Equal((byte)'M', reader.ReadByte());
        stream.Position = 18;
        Assert.Equal(expectedWidth, reader.ReadInt32());
        Assert.Equal(expectedHeight, reader.ReadInt32());
        stream.Position = 28;
        Assert.Equal(24, reader.ReadInt16());
    }

    private static string FindRepoFile(string relativePath)
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            var candidate = Path.Combine(
                current.FullName,
                relativePath.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(candidate))
            {
                return candidate;
            }

            current = current.Parent;
        }

        throw new FileNotFoundException(
            $"Could not find '{relativePath}' from '{AppContext.BaseDirectory}'.");
    }

    private sealed record AtlasBaseline(
        string Direction,
        IReadOnlyList<AtlasBaselineFile> Files);

    private sealed record AtlasBaselineFile(
        string Path,
        string Sha256);
}
