using VaultProspector.Application;

namespace VaultProspector.Platform;

public sealed class PolicyControlledWindowsVerificationService(
    IUserVerificationService localVerification,
    IUserVerificationService remoteVerification,
    IEnterprisePolicy enterprisePolicy,
    Func<bool> isRemoteSession) : IUserVerificationService
{
    private readonly IUserVerificationService _localVerification =
        localVerification ??
        throw new ArgumentNullException(nameof(localVerification));
    private readonly IUserVerificationService _remoteVerification =
        remoteVerification ??
        throw new ArgumentNullException(nameof(remoteVerification));
    private readonly IEnterprisePolicy _enterprisePolicy =
        enterprisePolicy ??
        throw new ArgumentNullException(nameof(enterprisePolicy));
    private readonly Func<bool> _isRemoteSession =
        isRemoteSession ??
        throw new ArgumentNullException(nameof(isRemoteSession));

    public PolicyControlledWindowsVerificationService(
        IUserVerificationService localVerification,
        IUserVerificationService remoteVerification,
        IEnterprisePolicy enterprisePolicy)
        : this(
            localVerification,
            remoteVerification,
            enterprisePolicy,
            WindowsSession.IsRemote)
    {
    }

    public bool IsAvailable =>
        _localVerification.IsAvailable ||
        (_isRemoteSession() && _remoteVerification.IsAvailable);

    public async Task<UserVerificationResult> VerifyAsync(
        string reason,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var localResult = await _localVerification.VerifyAsync(
            reason,
            cancellationToken);
        if (!_isRemoteSession() ||
            localResult != UserVerificationResult.RemoteSessionUnavailable)
        {
            return localResult;
        }

        var policy = _enterprisePolicy.GetSnapshot();
        if (!policy.IsValid || !policy.AllowRemoteCredentialVerification)
            return UserVerificationResult.DisabledByPolicy;
        if (!_remoteVerification.IsAvailable)
            return UserVerificationResult.RemoteSessionUnavailable;

        return await _remoteVerification.VerifyAsync(
            reason,
            cancellationToken);
    }
}
