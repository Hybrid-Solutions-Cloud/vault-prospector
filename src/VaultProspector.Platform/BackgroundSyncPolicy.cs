using System.Runtime.InteropServices;

namespace VaultProspector.Platform;

public static class BackgroundSyncPolicy
{
    public static bool IsEligible(
        bool enabled,
        bool mainWindowHidden,
        bool networkAvailable,
        bool powerStatusKnown,
        bool onExternalPower) =>
        enabled &&
        mainWindowHidden &&
        networkAvailable &&
        powerStatusKnown &&
        onExternalPower;

    public static bool TryIsOnExternalPower(out bool onExternalPower)
    {
        onExternalPower = false;
        if (!OperatingSystem.IsWindows()) return false;
        if (!GetSystemPowerStatus(out var status)) return false;
        if (status.AcLineStatus is not (0 or 1)) return false;
        onExternalPower = status.AcLineStatus == 1;
        return true;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSystemPowerStatus(out SystemPowerStatus status);

    [StructLayout(LayoutKind.Sequential)]
    private struct SystemPowerStatus
    {
        public byte AcLineStatus;
        public byte BatteryFlag;
        public byte BatteryLifePercent;
        public byte SystemStatusFlag;
        public uint BatteryLifeTime;
        public uint BatteryFullLifeTime;
    }
}
