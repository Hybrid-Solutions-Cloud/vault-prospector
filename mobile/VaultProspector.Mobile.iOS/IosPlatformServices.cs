using Foundation;
using UIKit;
using VaultProspector.Application;
using VaultProspector.Mobile.Core;
using VaultProspector.Mobile.Identity;
using VaultProspector.Providers.Azure;

namespace VaultProspector.Mobile.iOS;

public sealed class IosPlatformServices : IMobilePlatformServices, IDisposable
{
    private const string ProductClientId = "221af888-1c16-4637-9d45-b6dd2e1e7634";
    private const string RedirectUri = $"msal{ProductClientId}://auth";
    private const nint PrivacyCoverTag = 0x5650;
    private readonly MobileMsalIdentityProvider _identityProvider;
    private readonly NSObject _captureObserver;
    private readonly NSObject _screenshotObserver;
    private readonly NSObject _resignObserver;
    private readonly NSObject _backgroundObserver;
    private readonly NSObject _activeObserver;
    private readonly NSObject _protectedDataObserver;
    private bool _disposed;

    public IosPlatformServices()
    {
        DataDirectory = Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData);
        KeyMaterialProvider = new IosKeyMaterialProvider();
        UserVerificationService = new IosUserVerificationService();
        ClipboardService = new IosClipboardService();
        _identityProvider = new MobileMsalIdentityProvider(
            () => KeyWindow()?.RootViewController,
            RedirectUri);
        _captureObserver = NSNotificationCenter.DefaultCenter.AddObserver(
            UIScreen.CapturedDidChangeNotification,
            notification =>
            {
                if (KeyWindow()?.TraitCollection.SceneCaptureState ==
                    UISceneCaptureState.Active)
                {
                    global::VaultProspector.Mobile.MobileRuntime.NotifyBackgrounded();
                }
                else
                {
                    global::VaultProspector.Mobile.MobileRuntime.NotifyForegrounded();
                }
            });
        _screenshotObserver = NSNotificationCenter.DefaultCenter.AddObserver(
            UIApplication.UserDidTakeScreenshotNotification,
            _ => global::VaultProspector.Mobile.MobileRuntime.NotifyScreenshotTaken());
        _resignObserver = NSNotificationCenter.DefaultCenter.AddObserver(
            UIApplication.WillResignActiveNotification,
            _ => global::VaultProspector.Mobile.MobileRuntime.NotifyBackgrounded());
        _backgroundObserver = NSNotificationCenter.DefaultCenter.AddObserver(
            UIApplication.DidEnterBackgroundNotification,
            _ => global::VaultProspector.Mobile.MobileRuntime.NotifyBackgrounded());
        _activeObserver = NSNotificationCenter.DefaultCenter.AddObserver(
            UIApplication.DidBecomeActiveNotification,
            _ => global::VaultProspector.Mobile.MobileRuntime.NotifyForegrounded());
        _protectedDataObserver =
            UIApplication.Notifications.ObserveProtectedDataWillBecomeUnavailable(
                (_, _) =>
                    global::VaultProspector.Mobile.MobileRuntime.NotifyBackgrounded());
    }

    public MobilePlatformCapabilities Capabilities { get; } = new(
        MobilePlatformKind.iOS,
        HasDeviceBoundKeyStorage: true,
        HasLocalUserVerification: true,
        LocksOnBackground: true,
        CancelsValueOperationsOnBackground: true,
        ExcludesLocalDataFromBackup: true,
        MarksClipboardContentSensitive: false,
        ClearsOwnedClipboardContent: true,
        PreventsScreenCapture: false,
        CoversBackgroundSnapshots: true,
        ObservesScreenCapture: true);

    public string DataDirectory { get; }
    public IKeyMaterialProvider KeyMaterialProvider { get; }
    public IUserVerificationService UserVerificationService { get; }
    public IClipboardService ClipboardService { get; }
    public IIdentityProvider IdentityProvider => _identityProvider;
    public IAzureCredentialProvider AzureCredentialProvider => _identityProvider;

    public Task ApplyPersistentPrivacyControlsAsync(
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Directory.CreateDirectory(DataDirectory);
        if (!UIApplication.SharedApplication.ProtectedDataAvailable)
        {
            throw new PlatformNotSupportedException(
                "iOS protected data is unavailable.");
        }
        IosKeyMaterialProvider.PrepareForCurrentInstall(DataDirectory);
        var url = NSUrl.FromFilename(DataDirectory);
        if (!url.SetResource(
            NSUrl.IsExcludedFromBackupKey,
            NSNumber.FromBoolean(true),
            out var error))
        {
            throw new InvalidOperationException(
                "iOS could not exclude local encrypted data from backup.",
                error is null ? null : new NSErrorException(error));
        }

        return Task.CompletedTask;
    }

    public Task SetSensitiveUiCoveredAsync(
        bool covered,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var window = KeyWindow()
            ?? throw new PlatformNotSupportedException(
                "The iOS application window is unavailable.");
        if (!covered &&
            window.TraitCollection.SceneCaptureState ==
                UISceneCaptureState.Active)
        {
            throw new PlatformNotSupportedException(
                "Unlock is unavailable while iOS screen capture is active.");
        }

        var existing = window.ViewWithTag(PrivacyCoverTag);
        if (covered)
        {
            if (existing is not null)
                return Task.CompletedTask;
            var cover = new UIView(window.Bounds)
            {
                AutoresizingMask =
                    UIViewAutoresizing.FlexibleWidth |
                    UIViewAutoresizing.FlexibleHeight,
                BackgroundColor = UIColor.FromRGB(8, 17, 30),
                Tag = PrivacyCoverTag,
            };
            var label = new UILabel(cover.Bounds)
            {
                AutoresizingMask =
                    UIViewAutoresizing.FlexibleWidth |
                    UIViewAutoresizing.FlexibleHeight,
                Text = "Vault Prospector locked",
                TextAlignment = UITextAlignment.Center,
                TextColor = UIColor.White,
            };
            cover.AddSubview(label);
            window.AddSubview(cover);
            window.BringSubviewToFront(cover);
        }
        else
        {
            existing?.RemoveFromSuperview();
        }

        return Task.CompletedTask;
    }

    private static UIWindow? KeyWindow() =>
        UIApplication.SharedApplication.ConnectedScenes
            .OfType<UIWindowScene>()
            .SelectMany(scene => scene.Windows)
            .FirstOrDefault(window => window.IsKeyWindow);

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _captureObserver.Dispose();
        _screenshotObserver.Dispose();
        _resignObserver.Dispose();
        _backgroundObserver.Dispose();
        _activeObserver.Dispose();
        _protectedDataObserver.Dispose();
        GC.SuppressFinalize(this);
    }
}
