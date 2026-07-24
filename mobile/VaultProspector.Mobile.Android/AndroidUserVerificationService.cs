using Android.App;
using Android.Hardware.Biometrics;
using Android.OS;
using VaultProspector.Application;

namespace VaultProspector.Mobile.Android;

public sealed class AndroidUserVerificationService(Activity activity)
    : IUserVerificationService
{
    private const int AllowedAuthenticators =
        (int)(BiometricManagerAuthenticators.BiometricStrong |
              BiometricManagerAuthenticators.DeviceCredential);

    public bool IsAvailable
    {
        get
        {
            var manager = activity.GetSystemService(
                global::Android.Content.Context.BiometricService) as
                BiometricManager;
            return manager?.CanAuthenticate(AllowedAuthenticators) ==
                BiometricCode.Success;
        }
    }

    public async Task<UserVerificationResult> VerifyAsync(
        string reason,
        CancellationToken cancellationToken)
    {
        if (!IsAvailable)
            return UserVerificationResult.NotConfigured;

        var completion =
            new TaskCompletionSource<UserVerificationResult>(
                TaskCreationOptions.RunContinuationsAsynchronously);
        using var signal = new CancellationSignal();
        using var registration = cancellationToken.Register(signal.Cancel);
        using var callback = new Callback(completion);
        var prompt = new BiometricPrompt.Builder(activity)
            .SetTitle("Verify it is you")
            .SetSubtitle(BoundedReason(reason))
            .SetAllowedAuthenticators(AllowedAuthenticators)
            .Build();
        var executor = activity.MainExecutor
            ?? throw new PlatformNotSupportedException(
                "The Android main executor is unavailable.");
        prompt.Authenticate(signal, executor, callback);

        try
        {
            return await completion.Task.WaitAsync(cancellationToken);
        }
        catch (System.OperationCanceledException)
        {
            signal.Cancel();
            return UserVerificationResult.Canceled;
        }
    }

    private static string BoundedReason(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            return "Authorize this Vault Prospector action";
        return reason.Trim()[..Math.Min(reason.Trim().Length, 120)];
    }

    private sealed class Callback(
        TaskCompletionSource<UserVerificationResult> completion)
        : BiometricPrompt.AuthenticationCallback
    {
        public override void OnAuthenticationSucceeded(
            BiometricPrompt.AuthenticationResult? result) =>
            completion.TrySetResult(UserVerificationResult.Verified);

        public override void OnAuthenticationError(
            BiometricErrorCode errorCode,
            Java.Lang.ICharSequence? errString) =>
            completion.TrySetResult(errorCode switch
            {
                BiometricErrorCode.UserCanceled or
                BiometricErrorCode.Canceled =>
                    UserVerificationResult.Canceled,
                BiometricErrorCode.NoBiometrics or
                BiometricErrorCode.NoDeviceCredential =>
                    UserVerificationResult.NotConfigured,
                BiometricErrorCode.HwNotPresent or
                BiometricErrorCode.HwUnavailable =>
                    UserVerificationResult.Unavailable,
                _ => UserVerificationResult.Failed,
            });
    }
}
