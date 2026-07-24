using Microsoft.Win32;
using VaultProspector.Platform;

namespace VaultProspector.Platform.Tests;

public sealed class WindowsSecurityBoundaryMonitorTests
{
    [Theory]
    [InlineData(SessionSwitchReason.ConsoleConnect)]
    [InlineData(SessionSwitchReason.ConsoleDisconnect)]
    [InlineData(SessionSwitchReason.RemoteConnect)]
    [InlineData(SessionSwitchReason.RemoteDisconnect)]
    [InlineData(SessionSwitchReason.SessionLogon)]
    [InlineData(SessionSwitchReason.SessionLogoff)]
    [InlineData(SessionSwitchReason.SessionLock)]
    [InlineData(SessionSwitchReason.SessionUnlock)]
    [InlineData(SessionSwitchReason.SessionRemoteControl)]
    public void EveryWindowsSessionTransitionRequiresLock(
        SessionSwitchReason reason)
    {
        Assert.Equal(
            WindowsSecurityBoundary.SessionTransition,
            WindowsSecurityBoundaryMonitor.FromSessionSwitch(reason));
    }

    [Theory]
    [InlineData(PowerModes.Suspend, WindowsSecurityBoundary.Suspend)]
    [InlineData(PowerModes.Resume, WindowsSecurityBoundary.Resume)]
    public void SuspendAndResumeRequireLock(
        PowerModes mode,
        WindowsSecurityBoundary expected)
    {
        Assert.True(
            WindowsSecurityBoundaryMonitor.TryFromPowerMode(
                mode,
                out var boundary));
        Assert.Equal(expected, boundary);
    }

    [Fact]
    public void OrdinaryPowerStatusChangesDoNotRequireLock()
    {
        Assert.False(
            WindowsSecurityBoundaryMonitor.TryFromPowerMode(
                PowerModes.StatusChange,
                out _));
    }
}
