using VaultProspector.Application;

namespace VaultProspector.Platform;

public sealed class CurrentWindowsRemoteVerificationService(
    IUserVerificationService entraVerification,
    IUserVerificationService credentialVerification)
    : IUserVerificationService
{
    private readonly IUserVerificationService _entraVerification =
        entraVerification ?? throw new ArgumentNullException(
            nameof(entraVerification));
    private readonly IUserVerificationService _credentialVerification =
        credentialVerification ?? throw new ArgumentNullException(
            nameof(credentialVerification));

    public bool IsAvailable =>
        _entraVerification.IsAvailable ||
        _credentialVerification.IsAvailable;

    public Task<UserVerificationResult> VerifyAsync(
        string reason,
        CancellationToken cancellationToken) =>
        (_entraVerification.IsAvailable
            ? _entraVerification
            : _credentialVerification).VerifyAsync(
                reason,
                cancellationToken);
}
