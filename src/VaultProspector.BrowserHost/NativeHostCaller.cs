using VaultProspector.BrowserProtocol;

namespace VaultProspector.BrowserHost;

public sealed record NativeHostCaller(BrowserFamily BrowserFamily, string ExtensionId);

public static class NativeHostCallerParser
{
    public static NativeHostCaller Parse(
        IReadOnlyList<string> arguments,
        string applicationDirectory,
        BrowserHostConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(configuration);
        if (arguments.Count is < 1 or > 2)
            throw new BrowserProtocolException("Native host caller arguments are invalid.");

        if (TryParseChromium(arguments, out var chromiumCaller))
        {
            if (!configuration.IsAllowed(chromiumCaller.BrowserFamily, chromiumCaller.ExtensionId))
                throw new BrowserProtocolException("Native host caller is not allowed.");
            return chromiumCaller;
        }

        var expectedManifest = Path.GetFullPath(
            Path.Combine(applicationDirectory, BrowserHostConfiguration.FirefoxManifestFileName));
        var suppliedManifest = Path.GetFullPath(arguments[0]);
        if (arguments.Count != 2 ||
            !string.Equals(suppliedManifest, expectedManifest, StringComparison.OrdinalIgnoreCase))
        {
            throw new BrowserProtocolException("Firefox native host manifest argument is invalid.");
        }

        var firefoxId = arguments[1];
        BrowserBrokerAuthentication.ValidateExtensionId(BrowserFamily.Firefox, firefoxId);
        if (!configuration.IsAllowed(BrowserFamily.Firefox, firefoxId))
            throw new BrowserProtocolException("Native host caller is not allowed.");

        return new NativeHostCaller(BrowserFamily.Firefox, firefoxId);
    }

    private static bool TryParseChromium(
        IReadOnlyList<string> arguments,
        out NativeHostCaller caller)
    {
        caller = null!;
        if (!Uri.TryCreate(arguments[0], UriKind.Absolute, out var origin) ||
            !string.Equals(origin.Scheme, "chrome-extension", StringComparison.Ordinal) ||
            !string.IsNullOrEmpty(origin.UserInfo) ||
            !string.IsNullOrEmpty(origin.Query) ||
            !string.IsNullOrEmpty(origin.Fragment) ||
            !string.Equals(origin.AbsolutePath, "/", StringComparison.Ordinal))
        {
            return false;
        }

        if (arguments.Count == 2 &&
            (!arguments[1].StartsWith("--parent-window=", StringComparison.Ordinal) ||
             !long.TryParse(
                 arguments[1]["--parent-window=".Length..],
                 System.Globalization.NumberStyles.None,
                 System.Globalization.CultureInfo.InvariantCulture,
                 out var parentHandle) ||
             parentHandle < 0))
        {
            throw new BrowserProtocolException("Chromium parent-window argument is invalid.");
        }

        BrowserBrokerAuthentication.ValidateExtensionId(BrowserFamily.Chromium, origin.Host);
        caller = new NativeHostCaller(BrowserFamily.Chromium, origin.Host);
        return true;
    }
}
