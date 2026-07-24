using Android.App;
using Android.Views;
using VaultProspector.Application;
using VaultProspector.Mobile.Core;
using VaultProspector.Mobile.Identity;
using VaultProspector.Providers.Azure;

namespace VaultProspector.Mobile.Android;

public sealed class AndroidPlatformServices : IMobilePlatformServices
{
    private const string ProductClientId = "221af888-1c16-4637-9d45-b6dd2e1e7634";
    private const string RedirectUri = $"msal{ProductClientId}://auth";
    private readonly Activity _activity;
    private readonly MobileMsalIdentityProvider _identityProvider;

    public AndroidPlatformServices(Activity activity)
    {
        _activity = activity;
        DataDirectory = activity.FilesDir?.CanonicalPath
            ?? throw new PlatformNotSupportedException(
                "The Android private files directory is unavailable.");
        KeyMaterialProvider = new AndroidKeyMaterialProvider(
            Path.Combine(DataDirectory, "protected-keys"));
        UserVerificationService = new AndroidUserVerificationService(activity);
        ClipboardService = new AndroidClipboardService(activity);
        _identityProvider = new MobileMsalIdentityProvider(
            () => _activity,
            RedirectUri);
    }

    public MobilePlatformCapabilities Capabilities { get; } = new(
        MobilePlatformKind.Android,
        HasDeviceBoundKeyStorage: true,
        HasLocalUserVerification: true,
        LocksOnBackground: true,
        CancelsValueOperationsOnBackground: true,
        ExcludesLocalDataFromBackup: true,
        MarksClipboardContentSensitive: true,
        ClearsOwnedClipboardContent: true,
        PreventsScreenCapture: true,
        CoversBackgroundSnapshots: true,
        ObservesScreenCapture: false);

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
        if (_activity.Window is null)
            throw new PlatformNotSupportedException(
                "The Android secure window is unavailable.");
        _activity.Window.AddFlags(WindowManagerFlags.Secure);
        Directory.CreateDirectory(DataDirectory);
        return Task.CompletedTask;
    }

    public Task SetSensitiveUiCoveredAsync(
        bool covered,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_activity.Window is null)
            throw new PlatformNotSupportedException(
                "The Android secure window is unavailable.");
        _activity.Window.AddFlags(WindowManagerFlags.Secure);
        return Task.CompletedTask;
    }
}
