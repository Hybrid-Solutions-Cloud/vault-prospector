using VaultProspector.Application;
using VaultProspector.Platform;

namespace VaultProspector.Platform.Tests;

public sealed class CurrentWindowsRemoteVerificationServiceTests
{
    [Fact]
    public async Task EntraSessionUsesInteractiveEntraVerification()
    {
        var entra = new FixedVerificationService(
            UserVerificationResult.Verified,
            isAvailable: true);
        var credential = new FixedVerificationService(
            UserVerificationResult.RemoteCredentialFailed,
            isAvailable: true);
        var service = new CurrentWindowsRemoteVerificationService(
            entra,
            credential);

        var result = await service.VerifyAsync(
            "Verify",
            TestContext.Current.CancellationToken);

        Assert.Equal(UserVerificationResult.Verified, result);
        Assert.Equal(1, entra.CallCount);
        Assert.Equal(0, credential.CallCount);
    }

    [Fact]
    public async Task NonEntraSessionUsesWindowsCredentialVerification()
    {
        var entra = new FixedVerificationService(
            UserVerificationResult.Verified,
            isAvailable: false);
        var credential = new FixedVerificationService(
            UserVerificationResult.Verified,
            isAvailable: true);
        var service = new CurrentWindowsRemoteVerificationService(
            entra,
            credential);

        var result = await service.VerifyAsync(
            "Verify",
            TestContext.Current.CancellationToken);

        Assert.Equal(UserVerificationResult.Verified, result);
        Assert.Equal(0, entra.CallCount);
        Assert.Equal(1, credential.CallCount);
    }

    private sealed class FixedVerificationService(
        UserVerificationResult result,
        bool isAvailable) : IUserVerificationService
    {
        public int CallCount { get; private set; }
        public bool IsAvailable { get; } = isAvailable;

        public Task<UserVerificationResult> VerifyAsync(
            string reason,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            return Task.FromResult(result);
        }
    }
}
