using System.Text.Json;
using System.Xml.Linq;

namespace VaultProspector.Mobile.Tests;

public sealed class MobileNativeAutofillContractTests
{
    private static readonly XNamespace AndroidNamespace =
        "http://schemas.android.com/apk/res/android";

    [Fact]
    public void AndroidPrototypeIsDiscoverableButPackageDisabled()
    {
        var document = Load("AndroidManifest.xml");
        var application =
            document.Root?.Element("application") ??
            throw new InvalidOperationException(
                "Android manifest has no application element.");
        var service = application.Elements("service").Single(element =>
            string.Equals(
                (string?)element.Attribute(AndroidNamespace + "name"),
                "cloud.hybridsolutions.vaultprospector.autofill.VaultProspectorAutofillService",
                StringComparison.Ordinal));

        Assert.Equal(
            "false",
            (string?)service.Attribute(AndroidNamespace + "enabled"));
        Assert.Equal(
            "true",
            (string?)service.Attribute(AndroidNamespace + "exported"));
        Assert.Equal(
            "android.permission.BIND_AUTOFILL_SERVICE",
            (string?)service.Attribute(AndroidNamespace + "permission"));
        Assert.Contains(
            service.Descendants("action"),
            action => string.Equals(
                (string?)action.Attribute(AndroidNamespace + "name"),
                "android.service.autofill.AutofillService",
                StringComparison.Ordinal));
        Assert.Contains(
            service.Elements("meta-data"),
            metadata =>
                string.Equals(
                    (string?)metadata.Attribute(AndroidNamespace + "name"),
                    "android.autofill",
                    StringComparison.Ordinal) &&
                string.Equals(
                    (string?)metadata.Attribute(AndroidNamespace + "resource"),
                    "@xml/autofill_service",
                    StringComparison.Ordinal));

        var configuration = Load("autofill_service.xml");
        Assert.Equal(
            "autofill-service",
            configuration.Root?.Name.LocalName);
    }

    [Fact]
    public void AppleContainerAndExtensionDeclareAutofillEntitlement()
    {
        var appDictionary = GetPlistDictionary(
            Load("iOS-App-Entitlements.plist"));
        var extensionDictionary = GetPlistDictionary(
            Load("iOS-Extension-Entitlements.plist"));

        Assert.Equal(
            "true",
            GetDictionaryValue(
                appDictionary,
                "com.apple.developer.authentication-services.autofill-credential-provider")
                .Name.LocalName);
        Assert.Equal(
            "true",
            GetDictionaryValue(
                extensionDictionary,
                "com.apple.developer.authentication-services.autofill-credential-provider")
                .Name.LocalName);
        Assert.DoesNotContain(
            extensionDictionary.Elements("key"),
            key => key.Value is
                "keychain-access-groups" or
                "com.apple.security.application-groups");
    }

    [Fact]
    public void AppleExtensionUsesOnlyCredentialProviderUiCapability()
    {
        var root = GetPlistDictionary(Load("iOS-Extension-Info.plist"));
        var extension = GetDictionaryValue(root, "NSExtension");
        Assert.Equal("dict", extension.Name.LocalName);

        Assert.Equal(
            "com.apple.authentication-services-credential-provider-ui",
            GetDictionaryValue(
                extension,
                "NSExtensionPointIdentifier").Value);
        Assert.Equal(
            "CredentialProviderViewController",
            GetDictionaryValue(
                extension,
                "NSExtensionPrincipalClass").Value);

        var attributes = GetDictionaryValue(
            extension,
            "NSExtensionAttributes");
        var capabilities = GetDictionaryValue(
            attributes,
            "ASCredentialProviderExtensionCapabilities");
        Assert.Equal(
            "false",
            GetDictionaryValue(
                capabilities,
                "ShowsConfigurationUI").Name.LocalName);
        Assert.DoesNotContain(
            capabilities.Elements("key"),
            key => key.Value is
                "SupportsSavePasswordCredentials" or
                "SupportsConditionalPasskeyRegistration" or
                "ProvidesTextToInsert");
    }

    [Fact]
    public void AppleExtensionDependencyGraphRemainsLeastPrivilege()
    {
        using var document = JsonDocument.Parse(File.ReadAllBytes(
            Path.Combine(
                AppContext.BaseDirectory,
                "NativeContracts",
                "iOS-Extension-packages.lock.json")));
        var target = document.RootElement
            .GetProperty("dependencies")
            .EnumerateObject()
            .Single(property =>
                property.Name.StartsWith(
                    "net10.0-ios",
                    StringComparison.Ordinal) &&
                !property.Name.Contains('/'))
            .Value;
        var allowed = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase)
        {
            "Microsoft.NET.ILLink.Tasks",
            "vaultprospector.browserprotocol",
            "vaultprospector.domain",
            "vaultprospector.mobile.autofill",
        };

        Assert.All(
            target.EnumerateObject(),
            dependency => Assert.Contains(dependency.Name, allowed));
        Assert.Contains(
            target.EnumerateObject(),
            dependency => string.Equals(
                dependency.Name,
                "vaultprospector.mobile.autofill",
                StringComparison.OrdinalIgnoreCase));
    }

    private static XDocument Load(string name) =>
        XDocument.Load(Path.Combine(
            AppContext.BaseDirectory,
            "NativeContracts",
            name));

    private static XElement GetPlistDictionary(XDocument document) =>
        document.Root?.Element("dict") ??
        throw new InvalidOperationException(
            "Property list has no root dictionary.");

    private static XElement GetDictionaryValue(
        XElement dictionary,
        string key)
    {
        var elements = dictionary.Elements().ToArray();
        for (var index = 0; index < elements.Length - 1; index++)
        {
            if (elements[index].Name.LocalName == "key" &&
                string.Equals(
                    elements[index].Value,
                    key,
                    StringComparison.Ordinal))
            {
                return elements[index + 1];
            }
        }

        throw new InvalidOperationException(
            $"Property list key '{key}' is missing.");
    }
}
