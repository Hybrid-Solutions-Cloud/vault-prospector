using Avalonia.Controls;

namespace VaultProspector.App;

public static class WindowLifecyclePolicy
{
    public static bool ShouldHideOnMinimize(
        bool minimizeToNotificationArea,
        WindowState state) =>
        minimizeToNotificationArea &&
        state == WindowState.Minimized;
}
