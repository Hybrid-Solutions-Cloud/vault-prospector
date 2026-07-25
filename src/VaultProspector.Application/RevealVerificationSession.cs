namespace VaultProspector.Application;

public interface IRevealVerificationSession
{
    Task<bool> EnsureVerifiedAsync(
        TimeSpan requestedGracePeriod,
        CancellationToken cancellationToken);

    void Invalidate();
}

public sealed class RevealVerificationSession(
    IUserVerificationService verification,
    IEnterprisePolicy enterprisePolicy,
    TimeProvider? timeProvider = null) : IRevealVerificationSession
{
    private readonly IUserVerificationService _verification =
        verification ??
        throw new ArgumentNullException(nameof(verification));
    private readonly IEnterprisePolicy _enterprisePolicy =
        enterprisePolicy ??
        throw new ArgumentNullException(nameof(enterprisePolicy));
    private readonly TimeProvider _timeProvider =
        timeProvider ?? TimeProvider.System;
    private readonly object _stateLock = new();
    private Task<bool>? _activeVerification;
    private long _generation;
    private long? _verifiedAtTimestamp;
    private TimeSpan _effectiveGracePeriod;
    private EnterprisePolicyStamp? _policyStamp;

    public Task<bool> EnsureVerifiedAsync(
        TimeSpan requestedGracePeriod,
        CancellationToken cancellationToken)
    {
        if (requestedGracePeriod < TimeSpan.Zero ||
            requestedGracePeriod > TimeSpan.FromSeconds(120))
        {
            throw new ArgumentOutOfRangeException(
                nameof(requestedGracePeriod));
        }

        cancellationToken.ThrowIfCancellationRequested();
        var policy = _enterprisePolicy.GetSnapshot();
        var effectiveGrace =
            policy.ConstrainRevealVerificationGracePeriod(
                requestedGracePeriod);
        var policyStamp = EnterprisePolicyStamp.From(policy);
        lock (_stateLock)
        {
            if (CanReuseLocked(effectiveGrace, policyStamp))
                return Task.FromResult(true);
            if (_activeVerification is { } activeVerification)
            {
                return AwaitActiveThenRetryAsync(
                    activeVerification,
                    requestedGracePeriod,
                    cancellationToken);
            }

            ClearSession();
            var verificationGeneration = _generation;
            _activeVerification = VerifyAndEstablishAsync(
                requestedGracePeriod,
                verificationGeneration,
                cancellationToken);
            return _activeVerification;
        }
    }

    public void Invalidate()
    {
        lock (_stateLock)
        {
            _generation++;
            ClearSession();
        }
    }

    private async Task<bool> VerifyAndEstablishAsync(
        TimeSpan requestedGracePeriod,
        long verificationGeneration,
        CancellationToken cancellationToken)
    {
        await Task.Yield();
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!_verification.IsAvailable)
                return false;
            var result = await _verification.VerifyAsync(
                "Reveal an Azure Key Vault secret",
                cancellationToken);
            if (result != UserVerificationResult.Verified)
            {
                Invalidate();
                return false;
            }

            var latestPolicy = _enterprisePolicy.GetSnapshot();
            var latestGrace =
                latestPolicy.ConstrainRevealVerificationGracePeriod(
                    requestedGracePeriod);
            var latestStamp = EnterprisePolicyStamp.From(latestPolicy);
            lock (_stateLock)
            {
                if (_generation == verificationGeneration &&
                    latestGrace > TimeSpan.Zero)
                {
                    _verifiedAtTimestamp = _timeProvider.GetTimestamp();
                    _effectiveGracePeriod = latestGrace;
                    _policyStamp = latestStamp;
                }
            }

            return true;
        }
        finally
        {
            lock (_stateLock)
                _activeVerification = null;
        }
    }

    private async Task<bool> AwaitActiveThenRetryAsync(
        Task<bool> activeVerification,
        TimeSpan requestedGracePeriod,
        CancellationToken cancellationToken)
    {
        if (!await activeVerification.WaitAsync(cancellationToken))
            return false;
        return await EnsureVerifiedAsync(
            requestedGracePeriod,
            cancellationToken);
    }

    private bool CanReuseLocked(
        TimeSpan effectiveGrace,
        EnterprisePolicyStamp policyStamp)
    {
        if (_verifiedAtTimestamp is not { } verifiedAt ||
            effectiveGrace <= TimeSpan.Zero ||
            effectiveGrace != _effectiveGracePeriod ||
            _policyStamp != policyStamp)
        {
            ClearSession();
            return false;
        }

        var elapsed = _timeProvider.GetElapsedTime(
            verifiedAt,
            _timeProvider.GetTimestamp());
        if (elapsed < TimeSpan.Zero ||
            elapsed >= _effectiveGracePeriod)
        {
            ClearSession();
            return false;
        }

        return true;
    }

    private void ClearSession()
    {
        _verifiedAtTimestamp = null;
        _effectiveGracePeriod = TimeSpan.Zero;
        _policyStamp = null;
    }

    private sealed record EnterprisePolicyStamp(
        bool IsManaged,
        bool IsValid,
        bool AllowClipboard,
        bool AllowOfflineCache,
        bool AllowRemoteCredentialVerification,
        long? MaximumOfflineCacheTicks,
        long? MaximumRevealGraceTicks,
        string AllowedTenants,
        string AllowedProviders,
        string AllowedIdentityTypes)
    {
        public static EnterprisePolicyStamp From(
            EnterprisePolicySnapshot policy) =>
            new(
                policy.IsManaged,
                policy.IsValid,
                policy.AllowClipboard,
                policy.AllowOfflineCache,
                policy.AllowRemoteCredentialVerification,
                policy.MaximumOfflineCacheLifetime?.Ticks,
                policy.MaximumRevealVerificationGracePeriod?.Ticks,
                string.Join(
                    '\n',
                    policy.AllowedTenantIds.Order(
                        StringComparer.OrdinalIgnoreCase)),
                string.Join(
                    '\n',
                    policy.AllowedProviders
                        .Order()
                        .Select(value => value.ToString())),
                string.Join(
                    '\n',
                    policy.AllowedIdentityTypes
                        .Order()
                        .Select(value => value.ToString())));
    }
}
