using VaultProspector.Application;
using VaultProspector.Providers.Azure;

namespace VaultProspector.Mobile.Core;

public enum MobilePlatformKind
{
    Android,
    iOS,
}

public sealed record MobilePlatformCapabilities(
    MobilePlatformKind Platform,
    bool HasDeviceBoundKeyStorage,
    bool HasLocalUserVerification,
    bool LocksOnBackground,
    bool CancelsValueOperationsOnBackground,
    bool ExcludesLocalDataFromBackup,
    bool MarksClipboardContentSensitive,
    bool ClearsOwnedClipboardContent,
    bool PreventsScreenCapture,
    bool CoversBackgroundSnapshots,
    bool ObservesScreenCapture);

public interface IMobilePlatformServices
{
    MobilePlatformCapabilities Capabilities { get; }
    string DataDirectory { get; }
    IKeyMaterialProvider KeyMaterialProvider { get; }
    IUserVerificationService UserVerificationService { get; }
    IClipboardService ClipboardService { get; }
    IIdentityProvider IdentityProvider { get; }
    IAzureCredentialProvider AzureCredentialProvider { get; }
    Task ApplyPersistentPrivacyControlsAsync(CancellationToken cancellationToken);
    Task SetSensitiveUiCoveredAsync(bool covered, CancellationToken cancellationToken);
}

public static class MobilePlatformSecurityPolicy
{
    public static void EnsureSupported(MobilePlatformCapabilities capabilities)
    {
        ArgumentNullException.ThrowIfNull(capabilities);

        if (!capabilities.HasDeviceBoundKeyStorage)
            throw Unsupported("device-bound protected key storage");
        if (!capabilities.HasLocalUserVerification)
            throw Unsupported("local user verification");
        if (!capabilities.LocksOnBackground)
            throw Unsupported("lock on background");
        if (!capabilities.CancelsValueOperationsOnBackground)
            throw Unsupported("background value-operation cancellation");
        if (!capabilities.ExcludesLocalDataFromBackup)
            throw Unsupported("local-data backup exclusion");
        if (!capabilities.ClearsOwnedClipboardContent)
            throw Unsupported("owned clipboard clearing");
        if (!capabilities.CoversBackgroundSnapshots)
            throw Unsupported("background snapshot covering");

        if (capabilities.Platform == MobilePlatformKind.Android)
        {
            if (!capabilities.PreventsScreenCapture)
                throw Unsupported("Android secure-window screen-capture prevention");
            if (!capabilities.MarksClipboardContentSensitive)
                throw Unsupported("Android sensitive clipboard labeling");
        }
        else
        {
            if (capabilities.PreventsScreenCapture)
            {
                throw new InvalidOperationException(
                    "iOS must not claim screenshot prevention; it can cover background snapshots and observe capture.");
            }

            if (!capabilities.ObservesScreenCapture)
                throw Unsupported("iOS screen-capture observation");
        }
    }

    private static PlatformNotSupportedException Unsupported(string capability) =>
        new($"Vault Prospector mobile requires {capability}; the platform must fail closed.");
}
