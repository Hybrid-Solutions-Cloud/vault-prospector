using LocalAuthentication;
using VaultProspector.Application;

namespace VaultProspector.Mobile.iOS;

public sealed class IosUserVerificationService : IUserVerificationService
{
    public bool IsAvailable
    {
        get
        {
            using var context = new LAContext();
            return context.CanEvaluatePolicy(
                LAPolicy.DeviceOwnerAuthentication,
                out _);
        }
    }

    public async Task<UserVerificationResult> VerifyAsync(
        string reason,
        CancellationToken cancellationToken)
    {
        using var context = new LAContext
        {
            LocalizedCancelTitle = "Cancel",
        };
        if (!context.CanEvaluatePolicy(
            LAPolicy.DeviceOwnerAuthentication,
            out _))
        {
            return UserVerificationResult.NotConfigured;
        }

        using var registration = cancellationToken.Register(context.Invalidate);
        try
        {
            var result = await context.EvaluatePolicyAsync(
                LAPolicy.DeviceOwnerAuthentication,
                BoundedReason(reason));
            return result.Item1
                ? UserVerificationResult.Verified
                : MapError(result.Item2);
        }
        catch (OperationCanceledException)
        {
            return UserVerificationResult.Canceled;
        }
    }

    private static UserVerificationResult MapError(
        Foundation.NSError? error) =>
        error is null
            ? UserVerificationResult.Failed
            : (LAStatus)(long)error.Code switch
            {
                LAStatus.UserCancel or
                LAStatus.AppCancel or
                LAStatus.SystemCancel =>
                    UserVerificationResult.Canceled,
                LAStatus.BiometryNotAvailable =>
                    UserVerificationResult.Unavailable,
                LAStatus.BiometryNotEnrolled or
                LAStatus.PasscodeNotSet =>
                    UserVerificationResult.NotConfigured,
                _ => UserVerificationResult.Failed,
            };

    private static string BoundedReason(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            return "Authorize this Vault Prospector action";
        var normalized = reason.Trim();
        return normalized[..Math.Min(normalized.Length, 120)];
    }
}
