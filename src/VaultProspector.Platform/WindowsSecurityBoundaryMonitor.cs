using Microsoft.Win32;

namespace VaultProspector.Platform;

public enum WindowsSecurityBoundary
{
    SessionTransition,
    Suspend,
    Resume,
}

public sealed class WindowsSecurityBoundaryEventArgs(
    WindowsSecurityBoundary boundary) : EventArgs
{
    public WindowsSecurityBoundary Boundary { get; } = boundary;
}

public sealed class WindowsSecurityBoundaryMonitor : IDisposable
{
    private bool _disposed;

    public WindowsSecurityBoundaryMonitor()
    {
        SystemEvents.SessionSwitch += OnSessionSwitch;
        try
        {
            SystemEvents.PowerModeChanged += OnPowerModeChanged;
        }
        catch
        {
            SystemEvents.SessionSwitch -= OnSessionSwitch;
            throw;
        }
    }

    public event EventHandler<WindowsSecurityBoundaryEventArgs>? LockRequired;

    public static WindowsSecurityBoundary FromSessionSwitch(
        SessionSwitchReason _) =>
        WindowsSecurityBoundary.SessionTransition;

    public static bool TryFromPowerMode(
        PowerModes mode,
        out WindowsSecurityBoundary boundary)
    {
        switch (mode)
        {
            case PowerModes.Suspend:
                boundary = WindowsSecurityBoundary.Suspend;
                return true;
            case PowerModes.Resume:
                boundary = WindowsSecurityBoundary.Resume;
                return true;
            default:
                boundary = default;
                return false;
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        SystemEvents.SessionSwitch -= OnSessionSwitch;
        SystemEvents.PowerModeChanged -= OnPowerModeChanged;
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    private void OnSessionSwitch(
        object sender,
        SessionSwitchEventArgs args) =>
        LockRequired?.Invoke(
            this,
            new WindowsSecurityBoundaryEventArgs(
                FromSessionSwitch(args.Reason)));

    private void OnPowerModeChanged(
        object sender,
        PowerModeChangedEventArgs args)
    {
        if (TryFromPowerMode(args.Mode, out var boundary))
        {
            LockRequired?.Invoke(
                this,
                new WindowsSecurityBoundaryEventArgs(boundary));
        }
    }
}
