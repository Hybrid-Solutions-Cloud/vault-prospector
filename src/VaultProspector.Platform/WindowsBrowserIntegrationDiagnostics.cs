using Microsoft.Win32;
using VaultProspector.Application;
using VaultProspector.BrowserProtocol;

namespace VaultProspector.Platform;

public sealed class WindowsBrowserIntegrationDiagnostics(
    string applicationDirectory) : IBrowserIntegrationDiagnostics
{
    private const string NativeHostName =
        "com.hybridsolutionscloud.vaultprospector";

    private readonly string _applicationDirectory =
        Path.TrimEndingDirectorySeparator(
            Path.GetFullPath(applicationDirectory));

    public Task<BrowserIntegrationDiagnosticSnapshot> InspectAsync(
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!OperatingSystem.IsWindows())
        {
            return Task.FromResult(
                new BrowserIntegrationDiagnosticSnapshot(
                    "Unavailable. Browser integration is supported only on Windows.",
                    "Unavailable. Native messaging registration is supported only on Windows."));
        }

        var detectedBrowsers = DetectInstalledExtensions();
        var extensionStatus = detectedBrowsers.Count == 0
            ? "Not detected. Install or enable the Vault Prospector extension in Chrome, Edge, or Firefox, then refresh this check."
            : $"Detected in {string.Join(", ", detectedBrowsers)}. Use an HTTPS sign-in field to confirm the active extension connection.";

        var nativeHostProblems = InspectNativeHostRegistrations();
        var nativeHostStatus = nativeHostProblems.Count == 0
            ? "Ready. Chrome, Edge, and Firefox native-host registrations point to valid installed manifests and the trusted host executable."
            : $"Needs attention. {string.Join(" ", nativeHostProblems)} Repair or reinstall Vault Prospector, then refresh this check.";

        return Task.FromResult(
            new BrowserIntegrationDiagnosticSnapshot(
                extensionStatus,
                nativeHostStatus));
    }

    private static List<string> DetectInstalledExtensions()
    {
        var detected = new List<string>();
        var local = Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData);
        var roaming = Environment.GetFolderPath(
            Environment.SpecialFolder.ApplicationData);

        if (HasChromiumExtension(
                Path.Combine(local, "Google", "Chrome", "User Data")))
        {
            detected.Add("Chrome");
        }

        if (HasChromiumExtension(
                Path.Combine(local, "Microsoft", "Edge", "User Data")))
        {
            detected.Add("Edge");
        }

        if (HasFirefoxExtension(
                Path.Combine(roaming, "Mozilla", "Firefox", "Profiles")))
        {
            detected.Add("Firefox");
        }

        return detected;
    }

    private static bool HasChromiumExtension(string userDataDirectory)
    {
        try
        {
            if (!Directory.Exists(userDataDirectory))
                return false;
            return Directory.EnumerateDirectories(userDataDirectory)
                .Prepend(userDataDirectory)
                .Any(profileDirectory => Directory.Exists(
                    Path.Combine(
                        profileDirectory,
                        "Extensions",
                        BrowserKnownIdentities.ChromiumDevelopment)));
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static bool HasFirefoxExtension(string profilesDirectory)
    {
        try
        {
            if (!Directory.Exists(profilesDirectory))
                return false;
            return Directory.EnumerateDirectories(profilesDirectory)
                .Any(profileDirectory =>
                    File.Exists(
                        Path.Combine(
                            profileDirectory,
                            "extensions",
                            $"{BrowserKnownIdentities.Firefox}.xpi")) ||
                    Directory.Exists(
                        Path.Combine(
                            profileDirectory,
                            "extensions",
                            BrowserKnownIdentities.Firefox)));
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private List<string> InspectNativeHostRegistrations()
    {
        var checks = new[]
        {
            ("Chrome", $@"SOFTWARE\Google\Chrome\NativeMessagingHosts\{NativeHostName}"),
            ("Edge", $@"SOFTWARE\Microsoft\Edge\NativeMessagingHosts\{NativeHostName}"),
            ("Firefox", $@"SOFTWARE\Mozilla\NativeMessagingHosts\{NativeHostName}"),
        };
        var problems = new List<string>();
        foreach (var (browser, keyPath) in checks)
        {
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(
                    keyPath,
                    writable: false);
                var manifestPath = key?.GetValue(null) as string;
                if (!IsTrustedManifest(manifestPath))
                    problems.Add($"{browser} registration is missing or invalid.");
            }
            catch (Exception exception) when (
                exception is UnauthorizedAccessException or
                IOException or
                System.Security.SecurityException)
            {
                problems.Add($"{browser} registration could not be inspected.");
            }
        }

        var hostPath = Path.Combine(
            _applicationDirectory,
            "BrowserHost",
            "VaultProspector.BrowserHost.exe");
        if (!IsTrustedRegularFile(hostPath))
            problems.Add("The native-host executable is missing or invalid.");
        return problems;
    }

    private bool IsTrustedManifest(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;
        try
        {
            var path = Path.GetFullPath(
                Environment.ExpandEnvironmentVariables(value));
            return IsUnderApplicationDirectory(path) &&
                   IsTrustedRegularFile(path) &&
                   string.Equals(
                       Path.GetExtension(path),
                       ".json",
                       StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception exception) when (
            exception is ArgumentException or
            NotSupportedException or
            PathTooLongException)
        {
            return false;
        }
    }

    private bool IsUnderApplicationDirectory(string path) =>
        path.StartsWith(
            $"{_applicationDirectory}{Path.DirectorySeparatorChar}",
            StringComparison.OrdinalIgnoreCase);

    private static bool IsTrustedRegularFile(string path)
    {
        var file = new FileInfo(path);
        file.Refresh();
        return file.Exists &&
               (file.Attributes & FileAttributes.ReparsePoint) == 0;
    }
}
