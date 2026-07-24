using VaultProspector.Domain;

namespace VaultProspector.Application;

public sealed class CyberArkService(
    ICyberArkProvider provider,
    ICyberArkCredentialStore credentialStore,
    IMetadataRepository repository,
    IUserVerificationService verificationService,
    IClipboardService clipboardService,
    IClock clock)
{
    public async Task<CyberArkProfile> ConnectAsync(
        CyberArkProfile profile,
        SensitiveValue credential,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(credential);
        await provider.ValidateAsync(profile, credential, cancellationToken);

        var existing = await repository.GetCyberArkProfileAsync(
            profile.Id,
            cancellationToken);
        SensitiveValue? priorCredential = null;
        if (existing is not null)
        {
            try
            {
                priorCredential = await credentialStore.RetrieveAsync(
                    profile.Id,
                    cancellationToken);
            }
            catch (ProtectedKeyUnavailableException)
            {
                // A missing prior credential is repaired by this validated replacement.
            }
        }

        var ready = profile with
        {
            AuthenticationState = CyberArkAuthenticationState.Ready,
            IsEnabled = true,
            CredentialUpdatedAt = clock.UtcNow,
            LastValidatedAt = clock.UtcNow,
        };
        try
        {
            await credentialStore.StoreAsync(
                profile.Id,
                credential,
                cancellationToken);
            await repository.UpsertCyberArkProfileAsync(
                ready,
                cancellationToken);
            return ready;
        }
        catch
        {
            if (priorCredential is not null)
            {
                await credentialStore.StoreAsync(
                    profile.Id,
                    priorCredential,
                    CancellationToken.None);
            }
            else
            {
                await credentialStore.RemoveAsync(
                    profile.Id,
                    CancellationToken.None);
            }

            throw;
        }
        finally
        {
            priorCredential?.Dispose();
        }
    }

    public Task<IReadOnlyList<CyberArkProfile>> GetProfilesAsync(
        CancellationToken cancellationToken) =>
        repository.GetCyberArkProfilesAsync(cancellationToken);

    public async Task<CyberArkDiscoverySnapshot> SynchronizeAsync(
        Guid profileId,
        CancellationToken cancellationToken)
    {
        var profile = await RequireEnabledProfileAsync(
            profileId,
            cancellationToken);
        using var credential = await credentialStore.RetrieveAsync(
            profileId,
            cancellationToken);
        var snapshot = await provider.DiscoverAsync(
            profile,
            credential,
            cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        await repository.ApplyCyberArkDiscoveryAsync(
            profileId,
            snapshot,
            cancellationToken);
        await repository.UpsertCyberArkProfileAsync(
            profile with
            {
                AuthenticationState = CyberArkAuthenticationState.Ready,
                LastValidatedAt = clock.UtcNow,
            },
            cancellationToken);
        return snapshot;
    }

    public Task<IReadOnlyList<CyberArkSafe>> GetSafesAsync(
        Guid profileId,
        CancellationToken cancellationToken) =>
        repository.GetCyberArkSafesAsync(profileId, cancellationToken);

    public Task<IReadOnlyList<CyberArkAccount>> SearchAccountsAsync(
        Guid profileId,
        string searchText,
        int limit,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(limit, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(limit, 500);
        return repository.SearchCyberArkAccountsAsync(
            profileId,
            searchText.Trim(),
            limit,
            cancellationToken);
    }

    public Task<IReadOnlyList<CyberArkSecretVersion>> GetVersionsAsync(
        CyberArkAccount account,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(account);
        return repository.GetCyberArkVersionsAsync(
            account.ProfileId,
            account.AccountId,
            cancellationToken);
    }

    public Task<CyberArkSafePermissionEvidence?> GetPermissionAsync(
        Guid profileId,
        string safeId,
        CancellationToken cancellationToken) =>
        repository.GetCyberArkPermissionAsync(
            profileId,
            safeId,
            cancellationToken);

    public async Task<SensitiveValue> RetrieveAsync(
        CyberArkAccount account,
        int? versionId,
        string reason,
        string actionType,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(account);
        if (string.IsNullOrWhiteSpace(reason))
            throw new CyberArkConfigurationException(
                "A non-sensitive business reason is required for CyberArk retrieval.",
                nameof(reason));
        if (reason.Trim().Length > 1_000)
            throw new CyberArkConfigurationException(
                "The retrieval reason cannot exceed 1,000 characters.",
                nameof(reason));
        if (actionType is not ("show" or "copy"))
            throw new CyberArkConfigurationException(
                "The CyberArk action type must be show or copy.",
                nameof(actionType));

        var profile = await RequireReadyProfileAsync(
            account.ProfileId,
            cancellationToken);
        var verification = await verificationService.VerifyAsync(
            $"Verify to {actionType} CyberArk account {account.Name} from safe {account.SafeName}.",
            cancellationToken);
        if (verification != UserVerificationResult.Verified)
        {
            await RecordAuditAsync(
                profile.Id,
                account,
                versionId,
                actionType,
                CyberArkAuditResult.Denied,
                $"Windows verification outcome: {verification}.",
                cancellationToken);
            throw new UnauthorizedAccessException(
                $"Windows verification was not completed ({verification}).");
        }

        await RecordAuditAsync(
            profile.Id,
            account,
            versionId,
            $"{actionType}-authorized",
            CyberArkAuditResult.Succeeded,
            "Fresh Windows verification completed; provider request is authorized.",
            cancellationToken);

        using var credential = await credentialStore.RetrieveAsync(
            profile.Id,
            cancellationToken);
        try
        {
            var value = await provider.RetrieveAsync(
                profile,
                credential,
                account,
                versionId,
                reason.Trim(),
                actionType,
                cancellationToken);
            try
            {
                await RecordAuditAsync(
                    profile.Id,
                    account,
                    versionId,
                    actionType,
                    CyberArkAuditResult.Succeeded,
                    "CyberArk returned the requested value.",
                    cancellationToken);
                return value;
            }
            catch
            {
                value.Dispose();
                throw;
            }
        }
        catch (Exception exception) when (
            exception is not OperationCanceledException)
        {
            await RecordAuditAsync(
                profile.Id,
                account,
                versionId,
                actionType,
                CyberArkAuditResult.Failed,
                "CyberArk did not return the requested value.",
                CancellationToken.None);
            throw;
        }
    }

    public async Task CopyAsync(
        CyberArkAccount account,
        int? versionId,
        string reason,
        TimeSpan clearAfter,
        CancellationToken cancellationToken)
    {
        using var value = await RetrieveAsync(
            account,
            versionId,
            reason,
            "copy",
            cancellationToken);
        await clipboardService.CopyWithAutoClearAsync(
            value,
            clearAfter,
            cancellationToken);
    }

    public async Task SetEnabledAsync(
        Guid profileId,
        bool enabled,
        CancellationToken cancellationToken)
    {
        var profile = await repository.GetCyberArkProfileAsync(
            profileId,
            cancellationToken)
            ?? throw new InvalidOperationException(
                "The CyberArk profile no longer exists.");
        await repository.UpsertCyberArkProfileAsync(
            profile with
            {
                IsEnabled = enabled,
                AuthenticationState = enabled
                    ? CyberArkAuthenticationState.Unknown
                    : CyberArkAuthenticationState.Disabled,
            },
            cancellationToken);
    }

    public async Task RemoveAsync(
        Guid profileId,
        CancellationToken cancellationToken)
    {
        await credentialStore.RemoveAsync(profileId, cancellationToken);
        await repository.RemoveCyberArkProfileAsync(
            profileId,
            cancellationToken);
    }

    public async Task RevokeLocalAccessAsync(
        Guid profileId,
        CancellationToken cancellationToken)
    {
        var profile = await repository.GetCyberArkProfileAsync(
            profileId,
            cancellationToken)
            ?? throw new InvalidOperationException(
                "The CyberArk profile no longer exists.");

        // Persist the fail-closed state before touching the credential. If
        // deletion fails, retrieval remains blocked and the residual can be
        // remediated explicitly instead of silently restoring access.
        await repository.UpsertCyberArkProfileAsync(
            profile with
            {
                IsEnabled = false,
                AuthenticationState = CyberArkAuthenticationState.Revoked,
            },
            cancellationToken);
        await credentialStore.RemoveAsync(profileId, cancellationToken);
    }

    public Task<IReadOnlyList<CyberArkAuditEvent>> GetAuditAsync(
        Guid profileId,
        int limit,
        CancellationToken cancellationToken) =>
        repository.GetCyberArkAuditAsync(
            profileId,
            Math.Clamp(limit, 1, 500),
            cancellationToken);

    private async Task<CyberArkProfile> RequireReadyProfileAsync(
        Guid profileId,
        CancellationToken cancellationToken)
    {
        var profile = await repository.GetCyberArkProfileAsync(
            profileId,
            cancellationToken)
            ?? throw new InvalidOperationException(
                "The selected CyberArk profile no longer exists.");
        if (!profile.IsEnabled ||
            profile.AuthenticationState != CyberArkAuthenticationState.Ready)
            throw new InvalidOperationException(
                "The selected CyberArk profile is not validated and ready.");
        return profile;
    }

    private async Task<CyberArkProfile> RequireEnabledProfileAsync(
        Guid profileId,
        CancellationToken cancellationToken)
    {
        var profile = await repository.GetCyberArkProfileAsync(
            profileId,
            cancellationToken)
            ?? throw new InvalidOperationException(
                "The selected CyberArk profile no longer exists.");
        if (!profile.IsEnabled ||
            profile.AuthenticationState is
                CyberArkAuthenticationState.Disabled or
                CyberArkAuthenticationState.Revoked)
            throw new InvalidOperationException(
                "The selected CyberArk profile is disabled or revoked.");
        return profile;
    }

    private Task RecordAuditAsync(
        Guid profileId,
        CyberArkAccount account,
        int? versionId,
        string operation,
        CyberArkAuditResult result,
        string safeMessage,
        CancellationToken cancellationToken) =>
        repository.RecordCyberArkAuditAsync(
            new CyberArkAuditEvent(
                Guid.NewGuid(),
                profileId,
                account.AccountId,
                account.SafeName,
                versionId,
                operation,
                result,
                safeMessage,
                clock.UtcNow),
            cancellationToken);
}
