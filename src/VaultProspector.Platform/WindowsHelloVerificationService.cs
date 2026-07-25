using VaultProspector.Application;
using Windows.Security.Credentials.UI;

namespace VaultProspector.Platform;

public sealed class WindowsHelloVerificationService : IUserVerificationService
{
    private readonly Func<nint> _windowHandleProvider;
    private readonly IWindowsUserConsentInterop _interop;
    private readonly Func<bool> _isRemoteSession;

    public WindowsHelloVerificationService(Func<nint> windowHandleProvider)
        : this(
            windowHandleProvider,
            new WindowsUserConsentInterop(),
            WindowsSession.IsRemote)
    {
    }

    internal WindowsHelloVerificationService(
        Func<nint> windowHandleProvider,
        IWindowsUserConsentInterop interop,
        Func<bool> isRemoteSession)
    {
        ArgumentNullException.ThrowIfNull(windowHandleProvider);
        ArgumentNullException.ThrowIfNull(interop);
        ArgumentNullException.ThrowIfNull(isRemoteSession);
        _windowHandleProvider = windowHandleProvider;
        _interop = interop;
        _isRemoteSession = isRemoteSession;
    }

    public bool IsAvailable => OperatingSystem.IsWindows();

    public async Task<UserVerificationResult> VerifyAsync(string reason, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var availability = await _interop.CheckAvailabilityAsync();
        var unavailableResult = availability switch
        {
            UserConsentVerifierAvailability.Available => (UserVerificationResult?)null,
            UserConsentVerifierAvailability.NotConfiguredForUser => UserVerificationResult.NotConfigured,
            UserConsentVerifierAvailability.DisabledByPolicy => UserVerificationResult.DisabledByPolicy,
            UserConsentVerifierAvailability.DeviceNotPresent when _isRemoteSession() =>
                UserVerificationResult.RemoteSessionUnavailable,
            _ => UserVerificationResult.Unavailable,
        };
        if (unavailableResult is not null) return unavailableResult.Value;

        var windowHandle = _windowHandleProvider();
        if (windowHandle == 0)
            return UserVerificationResult.Unavailable;

        var result = await _interop.RequestVerificationForWindowAsync(windowHandle, reason);
        cancellationToken.ThrowIfCancellationRequested();
        return result switch
        {
            UserConsentVerificationResult.Verified => UserVerificationResult.Verified,
            UserConsentVerificationResult.Canceled => UserVerificationResult.Canceled,
            UserConsentVerificationResult.NotConfiguredForUser => UserVerificationResult.NotConfigured,
            UserConsentVerificationResult.DisabledByPolicy => UserVerificationResult.DisabledByPolicy,
            UserConsentVerificationResult.DeviceNotPresent when _isRemoteSession() =>
                UserVerificationResult.RemoteSessionUnavailable,
            UserConsentVerificationResult.DeviceNotPresent or UserConsentVerificationResult.DeviceBusy => UserVerificationResult.Unavailable,
            _ => UserVerificationResult.Failed,
        };
    }
}

internal interface IWindowsUserConsentInterop
{
    Task<UserConsentVerifierAvailability> CheckAvailabilityAsync();
    Task<UserConsentVerificationResult> RequestVerificationForWindowAsync(
        nint windowHandle,
        string reason);
}

internal sealed class WindowsUserConsentInterop : IWindowsUserConsentInterop
{
    public async Task<UserConsentVerifierAvailability> CheckAvailabilityAsync() =>
        await UserConsentVerifier.CheckAvailabilityAsync();

    public async Task<UserConsentVerificationResult> RequestVerificationForWindowAsync(
        nint windowHandle,
        string reason) =>
        await UserConsentVerifierInterop.RequestVerificationForWindowAsync(
            windowHandle,
            reason);
}
