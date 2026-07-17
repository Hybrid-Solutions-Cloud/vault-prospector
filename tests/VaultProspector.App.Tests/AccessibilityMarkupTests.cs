using System.Xml.Linq;
using VaultProspector.App.Views;

namespace VaultProspector.App.Tests;

public sealed class AccessibilityMarkupTests
{
    [Fact]
    public void DataEntryAndSelectionControlsHaveExplicitAutomationNames()
    {
        var document = XDocument.Load(FindMainWindowMarkup());
        var namedControlTypes = new HashSet<string>(StringComparer.Ordinal)
        {
            "ComboBox",
            "ListBox",
            "NumericUpDown",
            "TextBox",
        };

        var unnamed = document
            .Descendants()
            .Where(element => namedControlTypes.Contains(element.Name.LocalName))
            .Where(element => Attribute(element, "AutomationProperties.Name") is null)
            .Select(Describe)
            .ToArray();

        Assert.Empty(unnamed);
    }

    [Fact]
    public void ApplicationStatusIsAnAccessiblePoliteLiveRegion()
    {
        var document = XDocument.Load(FindMainWindowMarkup());
        var status = document
            .Descendants()
            .Single(element => Attribute(element, "Text")?.Value.Contains("StatusText", StringComparison.Ordinal) is true);

        Assert.Equal("Application status", Attribute(status, "AutomationProperties.Name")?.Value);
        Assert.Equal("Polite", Attribute(status, "AutomationProperties.LiveSetting")?.Value);
    }

    [Fact]
    public void FirstRunGuidanceUsesTheVerifiedForegroundOnItsDarkPanel()
    {
        var document = XDocument.Load(FindMainWindowMarkup());
        var panel = document
            .Descendants()
            .Single(element => Attribute(element, "Background")?.Value == "#123F36");

        var guidance = panel.Descendants().Where(element => element.Name.LocalName == "TextBlock").ToArray();
        Assert.NotEmpty(guidance);
        Assert.All(guidance, text => Assert.Equal("White", Attribute(text, "Foreground")?.Value));
    }

    [Theory]
    [InlineData(1024, 720, 1, 960, 640, false)]
    [InlineData(1024, 720, 2, 512, 360, true)]
    [InlineData(1920, 1040, 2, 960, 520, false)]
    public void WindowFitsPhysicalWorkingAreaAndSelectsResponsiveLayout(
        double physicalWidth,
        double physicalHeight,
        double scale,
        double expectedWidth,
        double expectedHeight,
        bool expectedNarrow)
    {
        var metrics = WindowLayoutMetrics.Fit(960, 640, physicalWidth, physicalHeight, scale, 720);

        Assert.Equal(expectedWidth, metrics.Width);
        Assert.Equal(expectedHeight, metrics.Height);
        Assert.Equal(expectedNarrow, metrics.UseNarrowLayout);
    }

    private static XAttribute? Attribute(XElement element, string localName) =>
        element.Attributes().SingleOrDefault(attribute => attribute.Name.LocalName == localName);

    private static string Describe(XElement element)
    {
        var identifyingAttribute = element.Attributes().FirstOrDefault(attribute =>
            attribute.Name.LocalName is "PlaceholderText" or "Text" or "ItemsSource");
        return identifyingAttribute is null
            ? element.Name.LocalName
            : $"{element.Name.LocalName} ({identifyingAttribute.Name.LocalName}={identifyingAttribute.Value})";
    }

    private static string FindMainWindowMarkup()
    {
        const string relativePath = "src/VaultProspector.App/Views/MainWindow.axaml";
        foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            for (var directory = new DirectoryInfo(start); directory is not null; directory = directory.Parent)
            {
                var candidate = Path.Combine(directory.FullName, relativePath);
                if (File.Exists(candidate)) return candidate;
            }
        }

        throw new FileNotFoundException($"Could not locate {relativePath} from the test working directory or output directory.");
    }
}
