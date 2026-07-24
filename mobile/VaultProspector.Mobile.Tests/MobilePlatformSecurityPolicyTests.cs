using VaultProspector.Mobile.Core;

namespace VaultProspector.Mobile.Tests;

public sealed class MobilePlatformSecurityPolicyTests
{
    [Fact]
    public void AcceptsCompleteAndroidBoundary()
    {
        MobilePlatformSecurityPolicy.EnsureSupported(Android());
    }

    [Fact]
    public void RejectsAndroidWithoutSecureWindow()
    {
        var exception = Assert.Throws<PlatformNotSupportedException>(
            () => MobilePlatformSecurityPolicy.EnsureSupported(
                Android() with { PreventsScreenCapture = false }));

        Assert.Contains("screen-capture prevention", exception.Message);
    }

    [Fact]
    public void AcceptsHonestIosBoundary()
    {
        MobilePlatformSecurityPolicy.EnsureSupported(iOS());
    }

    [Fact]
    public void RejectsIosScreenshotPreventionClaim()
    {
        var exception = Assert.Throws<InvalidOperationException>(
            () => MobilePlatformSecurityPolicy.EnsureSupported(
                iOS() with { PreventsScreenCapture = true }));

        Assert.Contains("must not claim screenshot prevention", exception.Message);
    }

    [Fact]
    public void RejectsMissingBackupExclusionOnEitherPlatform()
    {
        Assert.Throws<PlatformNotSupportedException>(
            () => MobilePlatformSecurityPolicy.EnsureSupported(
                Android() with { ExcludesLocalDataFromBackup = false }));
        Assert.Throws<PlatformNotSupportedException>(
            () => MobilePlatformSecurityPolicy.EnsureSupported(
                iOS() with { ExcludesLocalDataFromBackup = false }));
    }

    private static MobilePlatformCapabilities Android() => new(
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

    private static MobilePlatformCapabilities iOS() => new(
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
}
