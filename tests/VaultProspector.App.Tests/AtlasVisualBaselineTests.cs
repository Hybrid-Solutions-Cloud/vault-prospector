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

    [Fact]
    public void ApprovedAtlasPrimaryWorkflowsHaveProductionStructuralParity()
    {
        var productionPath = FindRepoFile(
            "src/VaultProspector.App/Views/MainWindow.axaml");
        var approvedPath = FindRepoFile(
            "docs/design/vault-prospector-ui-redesign-2026-07-25/src/App.tsx");
        var productionText = File.ReadAllText(productionPath);
        var approvedText = File.ReadAllText(approvedPath);
        var production = XDocument.Load(productionPath);

        var approvedWorkflowCopy = new[]
        {
            "Find a vault object",
            "Connect the accounts you use",
            "Workload identities",
            "Workspaces",
            "Fill the field you selected",
            "Understand what happened",
            "PREFERENCES AND LIFECYCLE",
        };
        Assert.All(
            approvedWorkflowCopy,
            copy =>
            {
                Assert.Contains(copy, approvedText, StringComparison.Ordinal);
                Assert.Contains(copy, productionText, StringComparison.Ordinal);
            });

        var window = production.Root;
        Assert.NotNull(window);
        Assert.Equal("1440", Attribute(window, "Width"));
        Assert.Equal("860", Attribute(window, "Height"));

        var titles = production
            .Descendants()
            .Where(element =>
                element.Name.LocalName == "TextBlock" &&
                HasClass(element, "screen-title"))
            .Select(element => Attribute(element, "Text"))
            .Where(value => value is not null)
            .Cast<string>()
            .ToArray();
        Assert.Contains("Find a vault object", titles);
        Assert.Contains("Connect the accounts you use", titles);
        Assert.Contains("Workload identities", titles);
        Assert.Contains("Workspaces", titles);
        Assert.Contains("Fill the field you selected", titles);
        Assert.Contains("Understand what happened", titles);
        Assert.Contains("Settings", titles);

        AssertNamedGridColumns(
            production,
            "SearchFilterGrid",
            "150,*,*,*,*");
        AssertNamedGridColumns(
            production,
            "SearchResultsGrid",
            "2.2*,1*");
        AssertNamedGridColumns(
            production,
            "IdentityGrid",
            "2.2*,1*");
        AssertNamedGridColumns(
            production,
            "SetupStepperGrid",
            "*,*,*,*");

        Assert.Contains(
            production.Descendants(),
            element =>
                element.Name.LocalName == "Expander" &&
                Attribute(element, "Header") ==
                    "Connection details, errors, and discovery scope" &&
                Attribute(element, "IsExpanded") == "False");
        Assert.Contains(
            production.Descendants(),
            element =>
                element.Name.LocalName == "TextBox" &&
                Attribute(element, "Name") == "SearchTextBox" &&
                HasClass(element, "atlas-search"));
        Assert.Contains(
            production.Descendants(),
            element =>
                element.Name.LocalName == "Button" &&
                Attribute(element, "Content") == "Reveal safely" &&
                Attribute(element, "Command") == "{Binding RevealCommand}");
        Assert.Contains(
            production.Descendants(),
            element =>
                element.Name.LocalName == "Button" &&
                Attribute(element, "Content") == "Lock now" &&
                HasClass(element, "header-action"));
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

    private static void AssertNamedGridColumns(
        XDocument document,
        string name,
        string expected)
    {
        var grid = document
            .Descendants()
            .Single(element =>
                element.Name.LocalName == "Grid" &&
                Attribute(element, "Name") == name);
        Assert.Equal(expected, Attribute(grid, "ColumnDefinitions"));
    }

    private static bool HasClass(XElement element, string value) =>
        (Attribute(element, "Classes") ?? string.Empty)
        .Split(' ', StringSplitOptions.RemoveEmptyEntries)
        .Contains(value, StringComparer.Ordinal);

    private static string? Attribute(XElement element, string localName) =>
        element
            .Attributes()
            .SingleOrDefault(attribute =>
                attribute.Name.LocalName == localName)
            ?.Value;
}
