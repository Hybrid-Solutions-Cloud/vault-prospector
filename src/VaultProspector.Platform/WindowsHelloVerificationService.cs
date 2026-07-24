using VaultProspector.Application;
using Windows.Security.Credentials.UI;

namespace VaultProspector.Platform;

public sealed class WindowsHelloVerificationService : IUserVerificationService
{
    public bool IsAvailable => OperatingSystem.IsWindows();

    public async Task<UserVerificationResult> VerifyAsync(string reason, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var availability = await UserConsentVerifier.CheckAvailabilityAsync();
        var unavailableResult = availability switch
        {
            UserConsentVerifierAvailability.Available => (UserVerificationResult?)null,
            UserConsentVerifierAvailability.NotConfiguredForUser => UserVerificationResult.NotConfigured,
            UserConsentVerifierAvailability.DisabledByPolicy => UserVerificationResult.DisabledByPolicy,
            _ => UserVerificationResult.Unavailable,
        };
        if (unavailableResult is not null) return unavailableResult.Value;

        var result = await UserConsentVerifier.RequestVerificationAsync(reason);
        cancellationToken.ThrowIfCancellationRequested();
        return result switch
        {
            UserConsentVerificationResult.Verified => UserVerificationResult.Verified,
            UserConsentVerificationResult.Canceled => UserVerificationResult.Canceled,
            UserConsentVerificationResult.NotConfiguredForUser => UserVerificationResult.NotConfigured,
            UserConsentVerificationResult.DisabledByPolicy => UserVerificationResult.DisabledByPolicy,
            UserConsentVerificationResult.DeviceNotPresent or UserConsentVerificationResult.DeviceBusy => UserVerificationResult.Unavailable,
            _ => UserVerificationResult.Failed,
        };
    }
}
