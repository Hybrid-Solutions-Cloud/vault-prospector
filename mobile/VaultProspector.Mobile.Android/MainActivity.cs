using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using Avalonia.Android;

namespace VaultProspector.Mobile.Android;

[Activity(
    Label = "Vault Prospector",
    Theme = "@style/VaultProspectorTheme",
    Icon = "@drawable/vault_prospector",
    MainLauncher = true,
    Exported = true,
    ConfigurationChanges =
        ConfigChanges.Orientation |
        ConfigChanges.ScreenSize |
        ConfigChanges.UiMode |
        ConfigChanges.KeyboardHidden)]
public sealed class MainActivity : AvaloniaMainActivity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        Window?.SetFlags(
            WindowManagerFlags.Secure,
            WindowManagerFlags.Secure);
        global::VaultProspector.Mobile.MobileRuntime.Configure(
            new AndroidPlatformServices(this));
        base.OnCreate(savedInstanceState);
    }

    protected override void OnStop()
    {
        global::VaultProspector.Mobile.MobileRuntime.NotifyBackgrounded();
        base.OnStop();
    }

    public override bool DispatchTouchEvent(MotionEvent? ev)
    {
        if (ev is not null &&
            (ev.Flags &
             (MotionEventFlags.WindowIsObscured |
              MotionEventFlags.WindowIsPartiallyObscured)) != 0)
        {
            return false;
        }

        return base.DispatchTouchEvent(ev);
    }
}
