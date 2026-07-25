using VaultProspector.Application;
using VaultProspector.Platform;

namespace VaultProspector.Platform.Tests;

public sealed class RemoteWindowsCredentialVerificationServiceTests
{
    [Fact]
    public async Task MissingWindowHandleFailsClosedWithoutPrompt()
    {
        var interop = new FakeInterop(UserVerificationResult.Verified);
        var service = new RemoteWindowsCredentialVerificationService(
            () => 0,
            interop);

        var result = await service.VerifyAsync(
            "Verify",
            TestContext.Current.CancellationToken);

        Assert.Equal(UserVerificationResult.Unavailable, result);
        Assert.Equal(0, interop.CallCount);
    }

    [Theory]
    [InlineData(UserVerificationResult.Verified)]
    [InlineData(UserVerificationResult.Canceled)]
    [InlineData(UserVerificationResult.RemoteCredentialUnavailable)]
    [InlineData(UserVerificationResult.RemoteCredentialFailed)]
    public async Task PreservesCredentialPromptOutcome(
        UserVerificationResult expected)
    {
        var interop = new FakeInterop(expected);
        var service = new RemoteWindowsCredentialVerificationService(
            () => 42,
            interop);

        var result = await service.VerifyAsync(
            "Unlock Vault Prospector",
            TestContext.Current.CancellationToken);

        Assert.Equal(expected, result);
        Assert.Equal(1, interop.CallCount);
        Assert.Equal(42, interop.WindowHandle);
        Assert.Equal("Unlock Vault Prospector", interop.Reason);
    }

    private sealed class FakeInterop(UserVerificationResult result) :
        IRemoteWindowsCredentialInterop
    {
        public int CallCount { get; private set; }
        public nint WindowHandle { get; private set; }
        public string? Reason { get; private set; }

        public UserVerificationResult VerifyCurrentUser(
            nint windowHandle,
            string reason)
        {
            CallCount++;
            WindowHandle = windowHandle;
            Reason = reason;
            return result;
        }
    }
}
