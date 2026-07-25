using VaultProspector.Application;
using VaultProspector.Platform;
using Windows.Security.Credentials.UI;

namespace VaultProspector.Platform.Tests;

public sealed class WindowsHelloVerificationServiceTests
{
    [Fact]
    public async Task UsesWindowBoundDesktopInteropWhenAvailable()
    {
        var interop = new FakeInterop
        {
            Availability = UserConsentVerifierAvailability.Available,
            Result = UserConsentVerificationResult.Verified,
        };
        var service = new WindowsHelloVerificationService(
            () => 42,
            interop,
            () => false);

        var result = await service.VerifyAsync(
            "Verify Vault Prospector",
            CancellationToken.None);

        Assert.Equal(UserVerificationResult.Verified, result);
        Assert.Equal(42, interop.RequestedWindowHandle);
        Assert.Equal("Verify Vault Prospector", interop.RequestedReason);
    }

    [Fact]
    public async Task MissingWindowHandleFailsClosedWithoutRequest()
    {
        var interop = new FakeInterop
        {
            Availability = UserConsentVerifierAvailability.Available,
        };
        var service = new WindowsHelloVerificationService(
            () => 0,
            interop,
            () => false);

        var result = await service.VerifyAsync("Verify", CancellationToken.None);

        Assert.Equal(UserVerificationResult.Unavailable, result);
        Assert.Null(interop.RequestedWindowHandle);
    }

    [Fact]
    public async Task DeviceNotPresentInRemoteSessionHasSpecificResult()
    {
        var interop = new FakeInterop
        {
            Availability = UserConsentVerifierAvailability.DeviceNotPresent,
        };
        var service = new WindowsHelloVerificationService(
            () => 42,
            interop,
            () => true);

        var result = await service.VerifyAsync("Verify", CancellationToken.None);

        Assert.Equal(UserVerificationResult.RemoteSessionUnavailable, result);
        Assert.Null(interop.RequestedWindowHandle);
    }

    private sealed class FakeInterop : IWindowsUserConsentInterop
    {
        public UserConsentVerifierAvailability Availability { get; init; }
        public UserConsentVerificationResult Result { get; init; }
        public nint? RequestedWindowHandle { get; private set; }
        public string? RequestedReason { get; private set; }

        public Task<UserConsentVerifierAvailability> CheckAvailabilityAsync() =>
            Task.FromResult(Availability);

        public Task<UserConsentVerificationResult> RequestVerificationForWindowAsync(
            nint windowHandle,
            string reason)
        {
            RequestedWindowHandle = windowHandle;
            RequestedReason = reason;
            return Task.FromResult(Result);
        }
    }
}
