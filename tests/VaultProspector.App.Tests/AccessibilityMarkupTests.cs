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

    [Fact]
    public void HighContrastTextBoxesUseReadablePlaceholderForeground()
    {
        var document = XDocument.Load(FindMarkup("src/VaultProspector.App/App.axaml"));
        var style = document
            .Descendants()
            .Single(element =>
                element.Name.LocalName == "Style" &&
                Attribute(element, "Selector")?.Value == "Window.high-contrast TextBox");
        var setter = style
            .Descendants()
            .Single(element => Attribute(element, "Property")?.Value == "PlaceholderForeground");

        Assert.Equal("{DynamicResource TextControlForeground}", Attribute(setter, "Value")?.Value);
    }

    [Fact]
    public void HighContrastFocusedComboBoxesUseReadableSystemColors()
    {
        var document = XDocument.Load(FindMarkup("src/VaultProspector.App/App.axaml"));
        var setters = document
            .Descendants()
            .Where(element => element.Name.LocalName == "Style")
            .Where(element => Attribute(element, "Selector")?.Value.StartsWith(
                "Window.high-contrast ComboBox:focus-visible /template/",
                StringComparison.Ordinal) == true)
            .Select(style => (
                Selector: Attribute(style, "Selector")!.Value,
                Setter: style.Descendants().Single(element => element.Name.LocalName == "Setter")))
            .ToDictionary(item => item.Selector, item => item.Setter, StringComparer.Ordinal);

        Assert.Equal(
            "{DynamicResource TextControlBackground}",
            Attribute(setters["Window.high-contrast ComboBox:focus-visible /template/ Border#HighlightBackground"], "Value")?.Value);
        foreach (var selector in new[]
                 {
                     "Window.high-contrast ComboBox:focus-visible /template/ ContentControl#ContentPresenter",
                     "Window.high-contrast ComboBox:focus-visible /template/ TextBlock#PlaceholderTextBlock",
                     "Window.high-contrast ComboBox:focus-visible /template/ PathIcon#DropDownGlyph",
                 })
        {
            Assert.Equal("Foreground", Attribute(setters[selector], "Property")?.Value);
            Assert.Equal("{DynamicResource TextControlForeground}", Attribute(setters[selector], "Value")?.Value);
        }
    }

    [Fact]
    public void NumericControlsProvideMinimumSizedTemplateButtons()
    {
        var document = XDocument.Load(FindMarkup("src/VaultProspector.App/App.axaml"));
        var style = document
            .Descendants()
            .Single(element =>
                element.Name.LocalName == "Style" &&
                Attribute(element, "Selector")?.Value == "NumericUpDown");
        var setter = style
            .Descendants()
            .Single(element => Attribute(element, "Property")?.Value == "MinHeight");

        Assert.Equal("26", Attribute(setter, "Value")?.Value);
    }

    [Theory]
    [InlineData(null, 1)]
    [InlineData(50, 1)]
    [InlineData(100, 1)]
    [InlineData(150, 1.5)]
    [InlineData(200L, 2)]
    [InlineData("225", 2.25)]
    [InlineData(500, 2.25)]
    [InlineData("invalid", 1)]
    public void WindowsTextScaleIsNormalizedToTheSupportedRange(object? rawValue, double expectedFactor) =>
        Assert.Equal(expectedFactor, WindowsTextScale.FactorFrom(rawValue));

    [Fact]
    public void ExplicitFontSizesUseApplicationScaleResources()
    {
        var application = XDocument.Load(FindMarkup("src/VaultProspector.App/App.axaml"));
        var window = XDocument.Load(FindMainWindowMarkup());
        var resources = application
            .Descendants()
            .Select(element => Attribute(element, "Key")?.Value)
            .Where(value => value?.StartsWith("VaultFontSize", StringComparison.Ordinal) == true)
            .Cast<string>()
            .ToHashSet(StringComparer.Ordinal);
        var fontSizes = window
            .Descendants()
            .Select(element => Attribute(element, "FontSize")?.Value)
            .Where(value => value is not null)
            .Cast<string>()
            .ToArray();

        Assert.NotEmpty(fontSizes);
        Assert.All(fontSizes, value =>
        {
            Assert.StartsWith("{StaticResource VaultFontSize", value, StringComparison.Ordinal);
            Assert.Contains(value[16..^1], resources);
        });
    }

    [Theory]
    [InlineData(1024, 720, 1, 1, 960, 640, false)]
    [InlineData(1024, 720, 2, 1, 512, 360, true)]
    [InlineData(1920, 1040, 2, 1, 960, 520, false)]
    [InlineData(1920, 1040, 2, 2, 960, 520, true)]
    public void WindowFitsPhysicalWorkingAreaAndSelectsResponsiveLayout(
        double physicalWidth,
        double physicalHeight,
        double scale,
        double textScale,
        double expectedWidth,
        double expectedHeight,
        bool expectedNarrow)
    {
        var metrics = WindowLayoutMetrics.Fit(960, 640, physicalWidth, physicalHeight, scale, textScale, 720);

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

    private static string FindMainWindowMarkup() => FindMarkup("src/VaultProspector.App/Views/MainWindow.axaml");

    private static string FindMarkup(string relativePath)
    {
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
