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
    public void SearchSourceFiltersUseDiscoveredChoiceCollections()
    {
        var document = XDocument.Load(FindMainWindowMarkup());
        var expectedBindings = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Tenant filter"] = "{Binding TenantFilterOptions}",
            ["Subscription filter"] = "{Binding SubscriptionFilterOptions}",
            ["Vault filter"] = "{Binding VaultFilterOptions}",
        };

        var filters = document
            .Descendants()
            .Where(element =>
                expectedBindings.ContainsKey(
                    Attribute(element, "AutomationProperties.Name")?.Value ?? string.Empty))
            .ToArray();

        Assert.Equal(expectedBindings.Count, filters.Length);
        Assert.All(filters, filter =>
        {
            Assert.Equal("ComboBox", filter.Name.LocalName);
            var name = Attribute(filter, "AutomationProperties.Name")!.Value;
            Assert.Equal(expectedBindings[name], Attribute(filter, "ItemsSource")?.Value);
        });
    }

    [Fact]
    public void ApplicationStatusIsAnAccessiblePoliteLiveRegion()
    {
        var document = XDocument.Load(FindMainWindowMarkup());
        var status = document
            .Descendants()
            .Single(element => Attribute(element, "Text")?.Value.Contains("StatusText", StringComparison.Ordinal) is true);

        Assert.Equal("TextBlock", status.Name.LocalName);
        Assert.Equal("Polite", Attribute(status, "AutomationProperties.LiveSetting")?.Value);
    }

    [Fact]
    public void BrowserFillConfirmationAndPolicyAreAccessibleLiveRegions()
    {
        var document = XDocument.Load(FindMainWindowMarkup());
        var confirmation = document
            .Descendants()
            .Single(element =>
                Attribute(element, "AutomationProperties.Name")?.Value ==
                "Browser fill confirmation");
        var policy = document
            .Descendants()
            .Single(element =>
                Attribute(element, "AutomationProperties.Name")?.Value ==
                "Browser fill machine policy status");

        Assert.Equal("Assertive", Attribute(
            confirmation,
            "AutomationProperties.LiveSetting")?.Value);
        Assert.Contains(
            policy.Descendants(),
            element =>
                Attribute(element, "Text")?.Value == "{Binding BrowserPolicyStatus}" &&
                Attribute(element, "AutomationProperties.LiveSetting")?.Value == "Polite");
    }

    [Fact]
    public void BrowserMappingUsesCapturedDestinationAndGuidedSourceSelectors()
    {
        var document = XDocument.Load(FindMainWindowMarkup());
        var automationNames = document
            .Descendants()
            .Select(element =>
                Attribute(
                    element,
                    "AutomationProperties.Name")?.Value)
            .Where(value => value is not null)
            .Cast<string>()
            .ToHashSet(StringComparer.Ordinal);

        Assert.Contains(
            "Eligible secret and identity for captured browser destination",
            automationNames);
        Assert.DoesNotContain(
            "Approved browser top-frame HTTPS origin",
            automationNames);
        Assert.DoesNotContain(
            "Approved browser target-frame HTTPS origin",
            automationNames);
        Assert.Contains(
            document.Descendants(),
            element =>
                Attribute(element, "Text")?.Value ==
                "{Binding BrowserCapturedDestination}");
    }

    [Fact]
    public void EnterprisePolicyStatusIsAnAccessiblePoliteLiveRegion()
    {
        var document = XDocument.Load(FindMainWindowMarkup());
        var policy = document
            .Descendants()
            .Single(element =>
                Attribute(element, "AutomationProperties.Name")?.Value ==
                "Machine-managed enterprise policy status");

        Assert.Contains(
            policy.Descendants(),
            element =>
                Attribute(element, "Text")?.Value ==
                    "{Binding EnterprisePolicyStatus}" &&
                Attribute(
                    element,
                    "AutomationProperties.LiveSetting")?.Value ==
                    "Polite");
    }

    [Fact]
    public void RevealGraceControlAndStatusAreAccessible()
    {
        var document = XDocument.Load(FindMainWindowMarkup());
        var selector = document
            .Descendants()
            .Single(element =>
                Attribute(element, "AutomationProperties.Name")?.Value ==
                "Consecutive reveal verification grace period");
        var status = document
            .Descendants()
            .Single(element =>
                Attribute(element, "Text")?.Value ==
                "{Binding RevealVerificationGraceStatus}");

        Assert.Equal("ComboBox", selector.Name.LocalName);
        Assert.Equal(
            "{Binding RevealVerificationGraceOptions}",
            Attribute(selector, "ItemsSource")?.Value);
        Assert.Equal(
            "Polite",
            Attribute(
                status,
                "AutomationProperties.LiveSetting")?.Value);
    }

    [Fact]
    public void CyberArkWorkflowKeepsSourceVerificationAndAuditVisible()
    {
        var document = XDocument.Load(FindMainWindowMarkup());
        var tab = document
            .Descendants()
            .Single(element =>
                element.Name.LocalName == "TabItem" &&
                Attribute(element, "Header")?.Value == "CyberArk");
        var automationNames = tab
            .Descendants()
            .Select(element =>
                Attribute(element, "AutomationProperties.Name")?.Value)
            .Where(value => value is not null)
            .Cast<string>()
            .ToHashSet(StringComparer.Ordinal);

        Assert.Contains("CyberArk profiles", automationNames);
        Assert.Contains("CyberArk accounts", automationNames);
        Assert.Contains("CyberArk retrieval reason", automationNames);
        Assert.Contains("CyberArk protected value preview", automationNames);
        Assert.Contains("CyberArk local audit events", automationNames);
        Assert.Contains(
            tab.Descendants(),
            element =>
                Attribute(element, "Text")?.Value ==
                    "{Binding CyberArkValuePreview}" &&
                Attribute(
                    element,
                    "AutomationProperties.LiveSetting")?.Value ==
                    "Assertive");
    }

    [Fact]
    public void ActionableErrorIsAnAccessibleAssertiveLiveRegion()
    {
        var document = XDocument.Load(FindMainWindowMarkup());
        var banner = document
            .Descendants()
            .Single(element => Attribute(element, "IsVisible")?.Value == "{Binding HasActionableError}");
        var focusTarget = banner
            .Descendants()
            .Single(element => Attribute(element, "Name")?.Value == "ActionableErrorAnnouncementTarget");

        Assert.Equal("{Binding ErrorAnnouncement}", Attribute(banner, "AutomationProperties.Name")?.Value);
        Assert.Equal("Assertive", Attribute(banner, "AutomationProperties.LiveSetting")?.Value);
        Assert.Equal("Button", focusTarget.Name.LocalName);
        Assert.Equal("Return to previous action", Attribute(focusTarget, "Content")?.Value);
        Assert.Equal("{Binding ErrorAnnouncement}", Attribute(focusTarget, "AutomationProperties.Name")?.Value);
        Assert.Equal("#FECACA", Attribute(focusTarget, "Background")?.Value);
        Assert.Equal("#3F1616", Attribute(focusTarget, "Foreground")?.Value);
    }

    [Fact]
    public void IsolatedSynchronizationErrorsAreAccessibleAndActionable()
    {
        var document = XDocument.Load(FindMainWindowMarkup());
        var panel = document
            .Descendants()
            .Single(element =>
                Attribute(element, "AutomationProperties.Name")?.Value ==
                "Actionable isolated synchronization errors");
        var list = panel
            .Descendants()
            .Single(element =>
                Attribute(element, "AutomationProperties.Name")?.Value ==
                "Synchronization error details");

        Assert.Equal("{Binding HasSyncErrors}", Attribute(panel, "IsVisible")?.Value);
        Assert.Equal("Polite", Attribute(panel, "AutomationProperties.LiveSetting")?.Value);
        Assert.Equal("{Binding SyncErrors}", Attribute(list, "ItemsSource")?.Value);
        Assert.Contains(
            panel.Descendants(),
            element => Attribute(element, "Text")?.Value == "{Binding Recovery}");
    }

    [Fact]
    public void ActivitySurfaceExposesExternalLogAndLocalBundleWithoutAutomaticUpload()
    {
        var document = XDocument.Load(FindMainWindowMarkup());
        var activity = document
            .Descendants()
            .Single(element =>
                element.Name.LocalName == "TabItem" &&
                Attribute(element, "Header")?.Value == "Activity & support");

        Assert.Contains(
            activity.Descendants(),
            element =>
                Attribute(element, "AutomationProperties.Name")?.Value ==
                "External privacy-safe diagnostic log path");
        Assert.Contains(
            activity.Descendants(),
            element =>
                Attribute(element, "AutomationProperties.Name")?.Value ==
                "Create a privacy-safe local support bundle");
        Assert.Contains(
            activity.Descendants(),
            element =>
                Attribute(element, "Text")?.Value?.Contains(
                    "Nothing is uploaded automatically",
                    StringComparison.Ordinal) is true);
    }

    [Fact]
    public void NvdaFocusBridgeMatchesPinnedAvaloniaInternals()
    {
        var document = XDocument.Load(FindMainWindowMarkup());
        var tabs = document.Descendants().Single(element => element.Name.LocalName == "TabControl");

        Assert.Equal("MainTabs", Attribute(tabs, "Name")?.Value);
        Assert.True(ReliableWindowAutomationPeer.HasExpectedAvaloniaFocusInternals);
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
    public void AtlasProductionTokensMatchApprovedDesignHandoff()
    {
        var application = XDocument.Load(
            FindMarkup("src/VaultProspector.App/App.axaml"));
        var expectedColors = new Dictionary<string, string>(
            StringComparer.Ordinal)
        {
            ["VaultColorCanvas"] = "#F4F1EA",
            ["VaultColorSurface"] = "#FFFDF8",
            ["VaultColorSurfaceAlt"] = "#EEE9DF",
            ["VaultColorInk"] = "#25231F",
            ["VaultColorMuted"] = "#6F6A61",
            ["VaultColorLine"] = "#D4CDC0",
            ["VaultColorLineStrong"] = "#A99E8D",
            ["VaultColorAccent"] = "#9A412B",
            ["VaultColorAccentStrong"] = "#7B3020",
            ["VaultColorAccentSoft"] = "#F2DFD8",
            ["VaultColorGood"] = "#27715B",
            ["VaultColorGoodSoft"] = "#DEEEE8",
            ["VaultColorWarning"] = "#A26118",
            ["VaultColorNavigation"] = "#EEE8DD",
            ["VaultColorContext"] = "#E7DFD2",
            ["VaultColorHeader"] = "#2C3737",
        };
        var actualColors = application
            .Descendants()
            .Where(element =>
                element.Name.LocalName == "SolidColorBrush")
            .Select(element => (
                Key: Attribute(element, "Key")?.Value,
                Value: element.Value))
            .Where(token => token.Key is not null)
            .ToDictionary(
                token => token.Key!,
                token => token.Value,
                StringComparer.Ordinal);

        Assert.All(
            expectedColors,
            token => Assert.Equal(
                token.Value,
                actualColors[token.Key]));

        var window = XDocument.Load(FindMainWindowMarkup());
        Assert.Contains(
            window.Descendants(),
            element =>
                Attribute(element, "Background")?.Value ==
                "{DynamicResource VaultColorHeader}");
        Assert.Contains(
            window.Descendants(),
            element =>
                Attribute(element, "Background")?.Value ==
                "{DynamicResource VaultColorContext}");
        Assert.Contains(
            window.Descendants(),
            element =>
                Attribute(element, "Text")?.Value ==
                "ACTIVE WORKSPACE");
        Assert.Contains(
            window.Descendants(),
            element =>
                Attribute(element, "Text")?.Value ==
                "Unlock this installation");
        Assert.Contains(
            window.Descendants(),
            element =>
                Attribute(
                    element,
                    "AutomationProperties.Name")?.Value ==
                "Verify and unlock Vault Prospector");
        Assert.Contains(
            window.Descendants(),
            element =>
                Attribute(element, "Text")?.Value ==
                "{Binding ActiveWorkspaceContext}");
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
