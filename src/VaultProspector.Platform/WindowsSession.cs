using System.Runtime.InteropServices;

namespace VaultProspector.Platform;

internal static class WindowsSession
{
    private const int SmRemoteSession = 0x1000;

    internal static bool IsRemote() =>
        OperatingSystem.IsWindows() && GetSystemMetrics(SmRemoteSession) != 0;

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int index);
}
