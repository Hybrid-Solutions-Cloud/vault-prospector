using System.Diagnostics;

namespace VaultProspector.Platform;

public sealed class WindowsBrowserHostProcessVerifier(string expectedExecutablePath)
{
    private readonly string _expectedExecutablePath =
        ValidateExpectedPath(expectedExecutablePath);

    public bool IsAllowed(int processId)
    {
        if (!OperatingSystem.IsWindows() || processId <= 0)
            return false;

        try
        {
            using var process = Process.GetProcessById(processId);
            var observed = process.MainModule?.FileName;
            if (string.IsNullOrWhiteSpace(observed) ||
                !string.Equals(
                    Path.GetFullPath(observed),
                    _expectedExecutablePath,
                    StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var file = new FileInfo(_expectedExecutablePath);
            return file.Exists &&
                   (file.Attributes & FileAttributes.ReparsePoint) == 0;
        }
        catch (Exception exception) when (
            exception is ArgumentException or
            InvalidOperationException or
            NotSupportedException or
            System.ComponentModel.Win32Exception)
        {
            return false;
        }
    }

    private static string ValidateExpectedPath(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException(
                "Expected browser host path is required.",
                nameof(value));
        return Path.GetFullPath(value);
    }
}
