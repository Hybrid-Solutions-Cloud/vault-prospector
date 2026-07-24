using Avalonia;
using Avalonia.iOS;
using Foundation;
using UIKit;

namespace VaultProspector.Mobile.iOS;

[Register("AppDelegate")]
#pragma warning disable CA1711
public sealed class AppDelegate : AvaloniaAppDelegate<global::VaultProspector.Mobile.App>
#pragma warning restore CA1711
{
    private IosPlatformServices? _platform;

    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
    {
        _platform = new IosPlatformServices();
        global::VaultProspector.Mobile.MobileRuntime.Configure(_platform);
        return base.CustomizeAppBuilder(builder).WithInterFont();
    }

}
