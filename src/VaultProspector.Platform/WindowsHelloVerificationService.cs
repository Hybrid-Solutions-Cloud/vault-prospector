using VaultProspector.Application;
using Windows.Security.Credentials.UI;

namespace VaultProspector.Platform;

public sealed class WindowsHelloVerificationService : IUserVerificationService
{
    public bool IsAvailable => OperatingSystem.IsWindows();

    public async Task<bool> VerifyAsync(string reason, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var availability = await UserConsentVerifier.CheckAvailabilityAsync();
        if (availability != UserConsentVerifierAvailability.Available) return false;
        var result = await UserConsentVerifier.RequestVerificationAsync(reason);
        cancellationToken.ThrowIfCancellationRequested();
        return result == UserConsentVerificationResult.Verified;
    }
}
