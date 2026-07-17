using System.Xml.Linq;

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
