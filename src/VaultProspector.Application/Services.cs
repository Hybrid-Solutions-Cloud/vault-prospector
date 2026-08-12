using System.Security.Cryptography;
using VaultProspector.BrowserProtocol;
using VaultProspector.Domain;

namespace VaultProspector.Application;

public sealed class SystemClock : IClock { public DateTimeOffset UtcNow => DateTimeOffset.UtcNow; }

public sealed class IdentityService
{
    private readonly IIdentityProvider _provider;
    private readonly IMetadataRepository _repository;
    private readonly IDiagnosticSink _diagnostics;
    private readonly IProtectedValueStore? _protectedValueStore;
    private readonly IEnterprisePolicy _enterprisePolicy;

    public IdentityService(IIdentityProvider provider, IMetadataRepository repository)
        : this(provider, repository, new NullDiagnosticSink(), null, null)
    {
    }

    public IdentityService(
        IIdentityProvider provider,
        IMetadataRepository repository,
        IDiagnosticSink diagnostics,
        IProtectedValueStore? protectedValueStore = null,
        IEnterprisePolicy? enterprisePolicy = null)
    {
        _provider = provider;
        _repository = repository;
        _diagnostics = diagnostics;
        _protectedValueStore = protectedValueStore;
        _enterprisePolicy =
            enterprisePolicy ?? UnmanagedEnterprisePolicy.Instance;
    }

    public async Task<ConnectedIdentity> AddAsync(string clientId, string displayName, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(clientId?.Trim(), out var parsedClientId))
            throw new ArgumentException("A valid Microsoft Entra public-client application ID is required.", nameof(clientId));
        var policy = _enterprisePolicy.GetSnapshot();
        policy.EnsureProviderAllowed(EnterpriseProvider.AzureKeyVault);
        policy.EnsureIdentityTypeAllowed(IdentityType.InteractiveUser);
        var identity = await _provider.SignInAsync(parsedClientId.ToString("D"), displayName.Trim(), cancellationToken);
        try
        {
            policy.EnsureIdentityAllowed(identity);
            await _repository.UpsertIdentityAsync(identity, cancellationToken);
            Audit("identity_connected", identity, "ready");
            return identity;
        }
        catch (Exception persistenceException)
        {
            try
            {
                await _provider.RemoveAsync(identity, CancellationToken.None);
            }
            catch (Exception cleanupException)
            {
                throw new AggregateException(
                    "Identity metadata persistence and token-cache rollback both failed.",
                    persistenceException,
                    cleanupException);
            }

            throw;
        }
    }

    public async Task<ConnectedIdentity> AddWorkloadIdentityAsync(string clientId, string tenantId, string displayName, IdentityType type, string credentialData, CancellationToken cancellationToken)
    {
        if (type == IdentityType.InteractiveUser)
            throw new ArgumentException("Interactive users must use AddAsync.", nameof(type));

        var policy = _enterprisePolicy.GetSnapshot();
        policy.EnsureProviderAllowed(EnterpriseProvider.AzureKeyVault);
        policy.EnsureIdentityTypeAllowed(type);
        var normalizedClientId = NormalizeOptionalGuid(clientId, nameof(clientId));
        var normalizedTenantId = NormalizeOptionalGuid(tenantId, nameof(tenantId));
        policy.EnsureTenantAllowed(normalizedTenantId);
        var normalizedCredential = credentialData?.Trim() ?? string.Empty;
        if (type == IdentityType.ManagedIdentity)
        {
            if (!string.IsNullOrEmpty(normalizedCredential))
                throw new WorkloadIdentityConfigurationException("Managed identities do not accept certificate or secret data.", nameof(credentialData));
        }
        else if (type is IdentityType.ServicePrincipal or IdentityType.FederatedServicePrincipal)
        {
            if (string.IsNullOrEmpty(normalizedClientId))
                throw new WorkloadIdentityConfigurationException("A service-principal client ID is required.", nameof(clientId));
            if (string.IsNullOrEmpty(normalizedTenantId))
                throw new WorkloadIdentityConfigurationException("A service-principal tenant ID is required.", nameof(tenantId));
            normalizedCredential = NormalizeWorkloadCredential(type, normalizedCredential);
        }
        else
        {
            throw new ArgumentOutOfRangeException(nameof(type), type, "Unsupported identity type.");
        }

        var identity = new ConnectedIdentity(
            Guid.NewGuid(),
            normalizedClientId,
            Guid.NewGuid().ToString("D"),
            string.Empty,
            string.IsNullOrWhiteSpace(displayName) ? type.ToString() : displayName.Trim(),
            normalizedTenantId,
            AuthenticationState.Ready,
            DateTimeOffset.UtcNow,
            true,
            type,
            normalizedCredential
        );

        var validated = await _provider.ReauthenticateAsync(identity, cancellationToken);
        policy.EnsureIdentityAllowed(validated);
        await _repository.UpsertIdentityAsync(validated, cancellationToken);
        Audit("workload_identity_connected", validated, "ready");
        return validated;
    }

    private static string NormalizeOptionalGuid(string? value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        if (!Guid.TryParse(value, out var parsed))
            throw new WorkloadIdentityConfigurationException("The identifier must be a GUID.", parameterName);
        return parsed.ToString("D");
    }

    private static string NormalizeCertificateThumbprint(string value)
    {
        var normalized = value.Replace(" ", string.Empty, StringComparison.Ordinal).ToUpperInvariant();
        if (normalized.Length is not (40 or 64) || normalized.Any(character => !Uri.IsHexDigit(character)))
            throw new WorkloadIdentityConfigurationException("A 40- or 64-character hexadecimal certificate thumbprint is required.", nameof(value));
        return normalized;
    }

    private static string NormalizeFederatedTokenFilePath(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new WorkloadIdentityConfigurationException(
                "A federated token file path is required.",
                nameof(value));

        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(value.Trim());
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            throw new WorkloadIdentityConfigurationException(
                "The federated token file path is invalid.",
                nameof(value));
        }

        if (!File.Exists(fullPath))
            throw new WorkloadIdentityConfigurationException(
                "The federated token file does not exist.",
                nameof(value));

        try
        {
            using var tokenFile = new FileStream(
                fullPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            if (tokenFile.Length == 0)
                throw new WorkloadIdentityConfigurationException(
                    "The federated token file is empty.",
                    nameof(value));
        }
        catch (WorkloadIdentityConfigurationException)
        {
            throw;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new WorkloadIdentityConfigurationException(
                "The federated token file is not readable.",
                nameof(value));
        }

        return fullPath;
    }

    private static string NormalizeWorkloadCredential(IdentityType type, string value) =>
        type switch
        {
            IdentityType.ServicePrincipal => NormalizeCertificateThumbprint(value),
            IdentityType.FederatedServicePrincipal => NormalizeFederatedTokenFilePath(value),
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, "The identity type has no rotatable credential."),
        };

    public async Task RemoveAsync(Guid identityId, CancellationToken cancellationToken)
    {
        var identity = await _repository.GetIdentityAsync(identityId, cancellationToken);
        if (identity is null) return;
        await _provider.RemoveAsync(identity, cancellationToken);
        await _repository.RemoveIdentityAsync(identityId, cancellationToken);
        Audit("identity_removed", identity, "removed");
    }

    public async Task DisableAsync(Guid identityId, CancellationToken cancellationToken)
    {
        var identity = await _repository.GetIdentityAsync(identityId, cancellationToken);
        if (identity is null) return;
        var disabled = identity with { IsEnabled = false, AuthenticationState = AuthenticationState.Disabled };
        await _repository.UpsertIdentityAsync(disabled, cancellationToken);
        Audit("identity_disabled", disabled, "disabled");
    }

    public async Task EnableAsync(Guid identityId, CancellationToken cancellationToken)
    {
        var identity = await _repository.GetIdentityAsync(identityId, cancellationToken);
        if (identity is null) return;
        _enterprisePolicy.GetSnapshot().EnsureIdentityAllowed(identity);
        var validated = await _provider.ReauthenticateAsync(identity, cancellationToken);
        _enterprisePolicy.GetSnapshot().EnsureIdentityAllowed(validated);
        var enabled = validated with { IsEnabled = true, AuthenticationState = AuthenticationState.Ready };
        await _repository.UpsertIdentityAsync(enabled, cancellationToken);
        Audit("identity_enabled", enabled, "ready");
    }

    public async Task ReauthenticateAsync(Guid identityId, CancellationToken cancellationToken)
    {
        var identity = await _repository.GetIdentityAsync(identityId, cancellationToken);
        if (identity is null) return;
        _enterprisePolicy.GetSnapshot().EnsureIdentityAllowed(identity);
        var refreshed = await _provider.ReauthenticateAsync(identity, cancellationToken);
        _enterprisePolicy.GetSnapshot().EnsureIdentityAllowed(refreshed);
        var updated = refreshed with { IsEnabled = true, AuthenticationState = AuthenticationState.Ready };
        await _repository.UpsertIdentityAsync(updated, cancellationToken);
        Audit("identity_reauthenticated", updated, "ready");
    }

    public async Task AuthorizeDirectoryReadAsync(
        Guid identityId,
        CancellationToken cancellationToken)
    {
        var identity = await _repository.GetIdentityAsync(identityId, cancellationToken)
            ?? throw new KeyNotFoundException("The selected identity no longer exists.");
        if (identity.Type != IdentityType.InteractiveUser)
            throw new InvalidOperationException(
                "Microsoft Graph directory discovery requires an explicit interactive administrator identity.");
        _enterprisePolicy.GetSnapshot().EnsureIdentityAllowed(identity);

        var authorized = await _provider.AuthorizeDirectoryReadAsync(identity, cancellationToken);
        _enterprisePolicy.GetSnapshot().EnsureIdentityAllowed(authorized);
        var updated = authorized with
        {
            IsEnabled = true,
            AuthenticationState = AuthenticationState.Ready,
        };
        await _repository.UpsertIdentityAsync(updated, cancellationToken);
        Audit("directory_read_authorized", updated, "ready");
    }

    public async Task RotateWorkloadCredentialAsync(
        Guid identityId,
        string replacementCredentialData,
        CancellationToken cancellationToken)
    {
        var identity = await _repository.GetIdentityAsync(identityId, cancellationToken)
            ?? throw new KeyNotFoundException("The selected identity no longer exists.");
        if (identity.Type is not (IdentityType.ServicePrincipal or IdentityType.FederatedServicePrincipal))
            throw new WorkloadIdentityConfigurationException(
                "Only certificate and federated service-principal credentials can be rotated here.",
                nameof(identityId));
        _enterprisePolicy.GetSnapshot().EnsureIdentityAllowed(identity);

        var candidate = identity with
        {
            CredentialData = NormalizeWorkloadCredential(identity.Type, replacementCredentialData),
            AuthenticationState = AuthenticationState.Unknown,
        };

        try
        {
            var validated = await _provider.ReauthenticateAsync(candidate, cancellationToken);
            _enterprisePolicy.GetSnapshot().EnsureIdentityAllowed(validated);
            var updated = validated with
            {
                IsEnabled = true,
                AuthenticationState = AuthenticationState.Ready,
            };
            await _repository.UpsertIdentityAsync(updated, cancellationToken);
            Audit("workload_credential_rotated", updated, "ready");
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _diagnostics.WriteError(
                "workload_credential_rotation_failed",
                exception,
                AuditFields(identity, "failed"));
            throw;
        }
    }

    public async Task<LocalIdentityRevocationResult> RevokeLocalAccessAsync(
        Guid identityId,
        CancellationToken cancellationToken)
    {
        var identity = await _repository.GetIdentityAsync(identityId, cancellationToken);
        if (identity is null)
            return new LocalIdentityRevocationResult(true, 0);
        var associatedVaultIds = await _repository.GetVaultIdsForIdentityAsync(
            identityId,
            cancellationToken);

        var revoked = identity with
        {
            IsEnabled = false,
            AuthenticationState = AuthenticationState.Revoked,
            CredentialData = string.Empty,
        };
        await _repository.UpsertIdentityAsync(revoked, cancellationToken);
        var providerCredentialRemoved = true;
        try
        {
            await _provider.RemoveAsync(identity, CancellationToken.None);
        }
        catch (Exception exception)
        {
            providerCredentialRemoved = false;
            _diagnostics.WriteError(
                "identity_provider_credential_removal_failed",
                exception,
                AuditFields(revoked, "revoked"));
        }

        var purgeFailures = new List<Exception>();
        var purgedVaultCount = 0;
        if (_protectedValueStore is null && associatedVaultIds.Count > 0)
        {
            purgeFailures.Add(
                new InvalidOperationException(
                    "The protected-value store is unavailable for identity revocation."));
        }
        else if (_protectedValueStore is not null)
        {
            foreach (var vaultId in associatedVaultIds.Distinct())
            {
                try
                {
                    await _protectedValueStore.PurgeVaultAsync(
                        vaultId,
                        CancellationToken.None);
                    purgedVaultCount++;
                }
                catch (Exception exception)
                {
                    purgeFailures.Add(exception);
                    _diagnostics.WriteError(
                        "identity_offline_value_purge_failed",
                        exception,
                        AuditFields(revoked, "revoked"));
                }
            }
        }

        Audit("identity_access_revoked", revoked, "revoked");
        if (purgeFailures.Count > 0)
        {
            throw new LocalRevocationCleanupException(
                purgeFailures.Count,
                purgeFailures.Count == 1
                    ? purgeFailures[0]
                    : new AggregateException(purgeFailures));
        }

        return new LocalIdentityRevocationResult(
            providerCredentialRemoved,
            purgedVaultCount);
    }

    public async Task<int> PurgeOfflineValuesAsync(
        Guid identityId,
        CancellationToken cancellationToken)
    {
        var identity = await _repository.GetIdentityAsync(
            identityId,
            cancellationToken);
        if (identity is null)
            return 0;
        if (_protectedValueStore is null)
        {
            throw new PlatformNotSupportedException(
                "The protected-value store is unavailable.");
        }

        var vaultIds = await _repository.GetVaultIdsForIdentityAsync(
            identityId,
            cancellationToken);
        var purgedVaultCount = 0;
        foreach (var vaultId in vaultIds.Distinct())
        {
            await _protectedValueStore.PurgeVaultAsync(
                vaultId,
                cancellationToken);
            purgedVaultCount++;
        }

        Audit("identity_offline_values_purged", identity, "purged");
        return purgedVaultCount;
    }

    private void Audit(string eventName, ConnectedIdentity identity, string status) =>
        _diagnostics.Information(eventName, AuditFields(identity, status));

    private static Dictionary<string, object?> AuditFields(ConnectedIdentity identity, string status) =>
        new()
        {
            ["identity_id"] = identity.Id,
            ["identity_type"] = identity.Type.ToString(),
            ["status"] = status,
        };

    private sealed class NullDiagnosticSink : IDiagnosticSink
    {
        public void Information(string eventName, IReadOnlyDictionary<string, object?> fields)
        {
        }

        public void WriteError(
            string eventName,
            Exception exception,
            IReadOnlyDictionary<string, object?> fields)
        {
        }
    }
}

public sealed class SynchronizationService(
    IVaultProvider provider,
    IMetadataRepository repository,
    IClock clock,
    IDiagnosticSink diagnostics,
    IEnterprisePolicy? enterprisePolicy = null)
{
    public async Task<SyncRun> SynchronizeAsync(ConnectedIdentity identity, CancellationToken cancellationToken)
        => await SynchronizeCoreAsync(
            identity,
            allowInteractiveAuthentication: false,
            cancellationToken);

    public async Task<SyncRun> SynchronizeInteractivelyAsync(
        ConnectedIdentity identity,
        CancellationToken cancellationToken) =>
        await SynchronizeCoreAsync(
            identity,
            allowInteractiveAuthentication: true,
            cancellationToken);

    private async Task<SyncRun> SynchronizeCoreAsync(
        ConnectedIdentity identity,
        bool allowInteractiveAuthentication,
        CancellationToken cancellationToken)
    {
        identity = await repository.GetIdentityAsync(identity.Id, cancellationToken)
            ?? throw new KeyNotFoundException("The selected identity no longer exists.");
        EnsureOnlineIdentityIsUsable(identity);
        var policy = (enterprisePolicy ?? UnmanagedEnterprisePolicy.Instance)
            .GetSnapshot();
        policy.EnsureIdentityAllowed(identity);
        var started = clock.UtcNow;
        try
        {
            var knownTenants = await repository.GetTenantsAsync(identity.Id, cancellationToken);
            var excludedTenantIds = knownTenants
                .Where(tenant => !tenant.IsSelected)
                .Select(tenant => tenant.TenantId)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var constraints = new VaultDiscoveryConstraints(
                allowedTenantIds: policy.AllowedTenantIds,
                excludedTenantIds: excludedTenantIds);
            var knownSubscriptions = await repository.GetSubscriptionsAsync(identity.Id, cancellationToken);
            var excludedSubscriptions = knownSubscriptions
                .Where(subscription => !subscription.IsSelected)
                .Select(subscription => subscription.SubscriptionId)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var knownVaults = await repository.GetVaultAccessSummariesAsync(identity.Id, cancellationToken);
            var excludedVaultResourceIds = knownVaults
                .Where(summary => !summary.Access.IsSelected)
                .Select(summary => summary.Vault.ProviderResourceId)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var snapshot = allowInteractiveAuthentication
                ? await provider.DiscoverInteractivelyAsync(
                    identity,
                    excludedSubscriptions,
                    excludedVaultResourceIds,
                    constraints,
                    cancellationToken)
                : await provider.DiscoverAsync(
                    identity,
                    excludedSubscriptions,
                    excludedVaultResourceIds,
                    constraints,
                    cancellationToken);
            snapshot = ApplyTenantConstraints(
                snapshot,
                constraints);
            var retainedExcludedPaths = knownVaults
                .Where(summary =>
                    !summary.Access.IsSelected ||
                    excludedSubscriptions.Contains(summary.Vault.SubscriptionId, StringComparer.OrdinalIgnoreCase) ||
                    excludedTenantIds.Contains(summary.Vault.TenantId, StringComparer.OrdinalIgnoreCase))
                .ToArray();
            if (retainedExcludedPaths.Length > 0)
            {
                var discoveredVaultIds = snapshot.Vaults.Select(vault => vault.Id).ToHashSet();
                var discoveredAccessIds = snapshot.AccessPaths.Select(access => access.Id).ToHashSet();
                snapshot = snapshot with
                {
                    Vaults = snapshot.Vaults
                        .Concat(retainedExcludedPaths
                            .Where(summary =>
                                constraints.IsTenantVisible(
                                    summary.Vault.TenantId) &&
                                discoveredVaultIds.Add(summary.Vault.Id))
                            .Select(summary => summary.Vault))
                        .ToArray(),
                    AccessPaths = snapshot.AccessPaths
                        .Concat(retainedExcludedPaths
                            .Where(summary =>
                                constraints.IsTenantVisible(
                                    summary.Vault.TenantId) &&
                                discoveredAccessIds.Add(summary.Access.Id))
                            .Select(summary => summary.Access))
                        .ToArray(),
                };
            }
            var status = snapshot.Errors.Count == 0 ? SyncStatus.Completed : SyncStatus.CompletedWithErrors;
            var runId = Guid.NewGuid();
            var completedAt = clock.UtcNow;
            var run = new SyncRun(
                runId,
                identity.DisplayName,
                started,
                completedAt,
                status,
                snapshot.Vaults.Count,
                snapshot.Items.Count,
                snapshot.Errors.Select(error => error.SafeMessage).ToArray(),
                ErrorDetails: snapshot.Errors
                    .Select(error => ToSyncErrorDetail(error, runId, completedAt))
                    .ToArray());
            await repository.ApplyDiscoveryAsync(identity.Id, snapshot, run, cancellationToken);
            diagnostics.Information("sync_completed", new Dictionary<string, object?> { ["identity_id"] = identity.Id, ["vault_count"] = run.VaultCount, ["item_count"] = run.ItemCount, ["error_count"] = run.NonSensitiveErrors.Count });
            WriteScopeDiagnostics(run, identity.Id);
            return run;
        }
        catch (OperationCanceledException)
        {
            return new SyncRun(Guid.NewGuid(), identity.DisplayName, started, clock.UtcNow, SyncStatus.Cancelled, 0, 0, [], "User cancelled");
        }
        catch (Exception ex) when (ex.GetType().Name == "AuthenticationFailedException" || ex.GetType().Name == "MsalUiRequiredException")
        {
            diagnostics.WriteError("sync_auth_failed", ex, new Dictionary<string, object?> { ["identity_id"] = identity.Id });
            var required = identity with { AuthenticationState = AuthenticationState.InteractionRequired };
            await repository.UpsertIdentityAsync(required, cancellationToken);
            return new SyncRun(
                Guid.NewGuid(),
                identity.DisplayName,
                started,
                clock.UtcNow,
                SyncStatus.Failed,
                0,
                0,
                ["Interactive Microsoft Entra authentication is required."],
                "Authentication required");
        }
        catch (Exception ex)
        {
            diagnostics.WriteError("sync_failed", ex, new Dictionary<string, object?> { ["identity_id"] = identity.Id });
            throw;
        }
    }

    public async Task<SyncRun> RetryFailedScopesAsync(
        ConnectedIdentity identity,
        IReadOnlyCollection<SyncErrorDetail> failedScopes,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(failedScopes);
        identity = await repository.GetIdentityAsync(identity.Id, cancellationToken)
            ?? throw new KeyNotFoundException("The selected identity no longer exists.");
        EnsureOnlineIdentityIsUsable(identity);

        var retryScopes = failedScopes
            .Select(detail => detail.RetryScope)
            .Where(scope => scope is not null)
            .Cast<ProviderRetryScope>()
            .ToArray();
        var subscriptionIds = retryScopes
            .Select(scope => scope.SubscriptionId)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Cast<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var vaultResourceIds = retryScopes
            .Select(scope => scope.VaultResourceId)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Cast<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (subscriptionIds.Length == 0 && vaultResourceIds.Length == 0)
            throw new InvalidOperationException("The selected synchronization error does not expose a retryable Azure scope.");

        var policy = (enterprisePolicy ?? UnmanagedEnterprisePolicy.Instance).GetSnapshot();
        policy.EnsureIdentityAllowed(identity);
        var knownTenants = await repository.GetTenantsAsync(identity.Id, cancellationToken);
        var constraints = new VaultDiscoveryConstraints(
            allowedTenantIds: policy.AllowedTenantIds,
            allowedSubscriptionIds: subscriptionIds,
            allowedVaultResourceIds: vaultResourceIds,
            excludedTenantIds: knownTenants
                .Where(tenant => !tenant.IsSelected)
                .Select(tenant => tenant.TenantId));
        var started = clock.UtcNow;
        var snapshot = await provider.DiscoverInteractivelyAsync(
            identity,
            [],
            [],
            constraints,
            cancellationToken);
        snapshot = ApplyTenantConstraints(snapshot, constraints);

        var status = snapshot.Errors.Count == 0
            ? SyncStatus.Completed
            : SyncStatus.CompletedWithErrors;
        var runId = Guid.NewGuid();
        var completedAt = clock.UtcNow;
        var run = new SyncRun(
            runId,
            $"Retry: {identity.DisplayName}",
            started,
            completedAt,
            status,
            snapshot.Vaults.Count,
            snapshot.Items.Count,
            snapshot.Errors.Select(error => error.SafeMessage).ToArray(),
            ErrorDetails: snapshot.Errors
                .Select(error => ToSyncErrorDetail(error, runId, completedAt))
                .ToArray());
        await repository.ApplyDiscoveryPatchAsync(
            identity.Id,
            snapshot,
            run,
            cancellationToken);
        diagnostics.Information(
            "sync_scope_retry_completed",
            new Dictionary<string, object?>
            {
                ["identity_id"] = identity.Id,
                ["vault_count"] = run.VaultCount,
                ["item_count"] = run.ItemCount,
                ["error_count"] = run.NonSensitiveErrors.Count,
                ["status"] = status == SyncStatus.Completed ? "completed" : "partial",
            });
        WriteScopeDiagnostics(run, identity.Id);
        return run;
    }

    private static void EnsureOnlineIdentityIsUsable(ConnectedIdentity identity)
    {
        if (!identity.IsEnabled || identity.AuthenticationState != AuthenticationState.Ready)
            throw new InvalidOperationException("The selected identity is disabled, revoked, or requires authentication.");
    }

    private static SyncErrorDetail ToSyncErrorDetail(
        ProviderError error,
        Guid runId,
        DateTimeOffset occurredAt)
    {
        var recovery = error.Category switch
        {
            "AuthenticationFailedException" or "MsalUiRequiredException" =>
                "Reauthenticate the affected identity, then retry synchronization.",
            "RequestFailedException" when error.SafeMessage.Contains("403", StringComparison.Ordinal) =>
                "Review metadata-list access for this scope. Other synchronized results remain available.",
            "RequestFailedException" when error.SafeMessage.Contains("429", StringComparison.Ordinal) =>
                "Azure throttled this scope. Wait briefly, then retry synchronization.",
            "RequestFailedException" =>
                "Verify network, private-endpoint, and Azure service availability for this scope, then retry.",
            _ =>
                "Use the safe category shown here to correct the affected scope, then retry synchronization.",
        };
        return new SyncErrorDetail(
            error.Scope,
            error.Category,
            error.SafeMessage,
            recovery,
            runId,
            occurredAt,
            CreateCorrelationId(runId, error),
            error.RetryScope);
    }

    private void WriteScopeDiagnostics(SyncRun run, Guid identityId)
    {
        foreach (var detail in run.ErrorDetails ?? [])
        {
            diagnostics.Information(
                "sync_scope_failed",
                new Dictionary<string, object?>
                {
                    ["identity_id"] = identityId,
                    ["scope_id"] = detail.Scope,
                    ["correlation_id"] = detail.CorrelationId,
                    ["error_category"] = detail.Category,
                    ["status"] = run.Status == SyncStatus.Completed ? "completed" : "partial",
                });
        }
    }

    private static string CreateCorrelationId(Guid runId, ProviderError error)
    {
        var input = System.Text.Encoding.UTF8.GetBytes(
            $"{runId:D}|{error.Scope}|{error.Category}");
        return Convert.ToHexString(SHA256.HashData(input))[..16];
    }

    private static DiscoverySnapshot ApplyTenantConstraints(
        DiscoverySnapshot snapshot,
        VaultDiscoveryConstraints constraints)
    {
        if (!constraints.RestrictsTenants)
            return snapshot;

        var tenants = snapshot.Tenants
            .Where(tenant =>
                constraints.IsTenantVisible(tenant.TenantId))
            .ToArray();
        var tenantAccessIds = tenants
            .Where(tenant => constraints.IsTenantAllowed(tenant.TenantId))
            .Select(tenant => tenant.Id)
            .ToHashSet();
        var subscriptions = snapshot.Subscriptions
            .Where(subscription =>
                tenantAccessIds.Contains(
                    subscription.TenantAccessId))
            .ToArray();
        var vaults = snapshot.Vaults
            .Where(vault =>
                constraints.IsTenantAllowed(vault.TenantId))
            .ToArray();
        var vaultIds = vaults
            .Select(vault => vault.Id)
            .ToHashSet();
        var accessPaths = snapshot.AccessPaths
            .Where(access =>
                constraints.IsTenantAllowed(access.TenantId) &&
                vaultIds.Contains(access.VaultId))
            .ToArray();
        var items = snapshot.Items
            .Where(item => vaultIds.Contains(item.VaultId))
            .ToArray();

        return snapshot with
        {
            Tenants = tenants,
            Subscriptions = subscriptions,
            Vaults = vaults,
            AccessPaths = accessPaths,
            Items = items,
        };
    }
}

public sealed class SearchService(
    IMetadataRepository repository,
    IClock clock,
    IEnterprisePolicy? enterprisePolicy = null)
{
    public async Task<IReadOnlyList<SearchResult>> SearchAsync(
        SearchRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Limit is < 1 or > 1000) throw new ArgumentOutOfRangeException(nameof(request), "Search limit must be between 1 and 1000.");
        var policy = (enterprisePolicy ?? UnmanagedEnterprisePolicy.Instance)
            .GetSnapshot();
        if (!policy.AllowedProviders.Contains(
                EnterpriseProvider.AzureKeyVault))
        {
            return [];
        }

        var results = await repository.SearchAsync(
            request,
            clock.UtcNow,
            cancellationToken);
        return policy.RestrictsTenants
            ? results
                .Where(result =>
                    policy.AllowedTenantIds.Contains(
                        result.Vault.TenantId))
                .ToArray()
            : results;
    }
}

public sealed class SecretAccessService(
    IVaultProvider provider,
    IMetadataRepository repository,
    IProtectedValueStore cache,
    IClipboardService clipboard,
    IUserVerificationService verification,
    IClock clock,
    IEnterprisePolicy? enterprisePolicy = null,
    IRevealVerificationSession? revealVerificationSession = null)
{
    public Task<SensitiveValue> RetrieveAsync(
        Guid itemId,
        CancellationToken cancellationToken) =>
        RetrieveAsync(itemId, TimeSpan.Zero, cancellationToken);

    public async Task<SensitiveValue> RetrieveAsync(
        Guid itemId,
        TimeSpan revealVerificationGracePeriod,
        CancellationToken cancellationToken)
    {
        var source = await repository.ResolveItemAsync(itemId, cancellationToken) ?? throw new KeyNotFoundException("The selected vault item no longer exists.");
        if (source.Item.ObjectType != VaultObjectType.Secret) throw new InvalidOperationException("Only secret values can be retrieved. Key material and certificate private keys are never exported.");
        EnsureSourceAllowed(source);
        EnsureOnlineIdentityIsUsable(source.Identity);
        var verified = revealVerificationSession is not null
            ? await revealVerificationSession.EnsureVerifiedAsync(
                revealVerificationGracePeriod,
                cancellationToken)
            : verification.IsAvailable &&
              await verification.VerifyAsync(
                  "Reveal an Azure Key Vault secret",
                  cancellationToken) ==
              UserVerificationResult.Verified;
        if (!verified)
            throw new UnauthorizedAccessException("Local verification was not completed.");
        var value = await provider.RetrieveSecretAsync(source.Identity, source.Vault, source.Item, cancellationToken);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            await repository.RecordAccessAsync(itemId, clock.UtcNow, cancellationToken);
            return value;
        }
        catch
        {
            value.Dispose();
            throw;
        }
    }

    public async Task<SensitiveValue> RetrieveForIdentityAsync(
        Guid itemId,
        Guid identityId,
        string verificationReason,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(verificationReason) || verificationReason.Length > 160)
            throw new ArgumentException("A bounded verification reason is required.", nameof(verificationReason));
        var source = await repository.ResolveItemForIdentityAsync(itemId, identityId, cancellationToken)
            ?? throw new KeyNotFoundException("The mapped vault item or identity access path no longer exists.");
        if (source.Item.ObjectType != VaultObjectType.Secret)
            throw new InvalidOperationException("Only secret values can be filled.");
        EnsureSourceAllowed(source);
        EnsureOnlineIdentityIsUsable(source.Identity);
        if (!verification.IsAvailable ||
            await verification.VerifyAsync(verificationReason, cancellationToken) !=
            UserVerificationResult.Verified)
        {
            throw new UnauthorizedAccessException("Local verification was not completed.");
        }

        var value = await provider.RetrieveSecretAsync(
            source.Identity,
            source.Vault,
            source.Item,
            cancellationToken);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            await repository.RecordAccessAsync(itemId, clock.UtcNow, cancellationToken);
            return value;
        }
        catch
        {
            value.Dispose();
            throw;
        }
    }

    public async Task RetrieveAndCopyAsync(Guid itemId, TimeSpan clearAfter, CachePolicy policy, CancellationToken cancellationToken)
    {
        var enterprise = EnterprisePolicy();
        enterprise.EnsureClipboardAllowed();
        policy = enterprise.Constrain(policy);
        if (!policy.AllowClipboard) throw new InvalidOperationException("Clipboard use is disabled by policy.");
        var source = await repository.ResolveItemAsync(itemId, cancellationToken) ?? throw new KeyNotFoundException("The selected vault item no longer exists.");
        if (source.Item.ObjectType != VaultObjectType.Secret) throw new InvalidOperationException("Only secret values can be copied.");
        EnsureSourceAllowed(source, enterprise);
        EnsureOnlineIdentityIsUsable(source.Identity);
        if (!verification.IsAvailable || await verification.VerifyAsync("Copy an Azure Key Vault secret", cancellationToken) != UserVerificationResult.Verified)
            throw new UnauthorizedAccessException("Local verification was not completed.");

        using var value = await provider.RetrieveSecretAsync(source.Identity, source.Vault, source.Item, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        await clipboard.CopyWithAutoClearAsync(value, clearAfter, cancellationToken);
        await repository.RecordAccessAsync(itemId, clock.UtcNow, cancellationToken);
    }

    public async Task<CachedSecretDescriptor> RetrieveAndCacheAsync(Guid itemId, Guid? workspaceId, TimeSpan lifetime, CachePolicy policy, CancellationToken cancellationToken)
    {
        var enterprise = EnterprisePolicy();
        enterprise.EnsureOfflineCacheAllowed();
        policy = enterprise.Constrain(policy);
        var expiresAt = policy.GetExpiration(clock.UtcNow, lifetime);
        var source = await repository.ResolveItemAsync(itemId, cancellationToken) ?? throw new KeyNotFoundException("The selected vault item no longer exists.");
        if (source.Item.ObjectType != VaultObjectType.Secret) throw new InvalidOperationException("Only secret values can be cached.");
        EnsureSourceAllowed(source, enterprise);
        EnsureOnlineIdentityIsUsable(source.Identity);
        if (!verification.IsAvailable || await verification.VerifyAsync("Retrieve and cache an Azure Key Vault secret", cancellationToken) != UserVerificationResult.Verified)
            throw new UnauthorizedAccessException("Local verification was not completed.");

        using var value = await provider.RetrieveSecretAsync(source.Identity, source.Vault, source.Item, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        await repository.RecordAccessAsync(itemId, clock.UtcNow, cancellationToken);
        return await cache.StoreAsync(itemId, source.Vault.Id, workspaceId, value, source.Item.MetadataFingerprint, expiresAt, cancellationToken);
    }

    public async Task<SensitiveValue> RetrieveCachedAsync(Guid itemId, CancellationToken cancellationToken)
    {
        var enterprise = EnterprisePolicy();
        enterprise.EnsureOfflineCacheAllowed();
        var source = await repository.ResolveItemAsync(itemId, cancellationToken)
            ?? throw new KeyNotFoundException("The selected vault item no longer exists.");
        if (source.Item.ObjectType != VaultObjectType.Secret) throw new InvalidOperationException("Only cached secret values can be opened.");
        EnsureSourceAllowed(source, enterprise);
        if (!verification.IsAvailable || await verification.VerifyAsync("Open an offline secret", cancellationToken) != UserVerificationResult.Verified)
            throw new UnauthorizedAccessException("Local verification was not completed.");

        var value = await cache.RetrieveAsync(itemId, clock.UtcNow, source.Item.MetadataFingerprint, cancellationToken)
            ?? throw new KeyNotFoundException("No unexpired offline copy exists for the selected secret.");
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            await repository.RecordAccessAsync(itemId, clock.UtcNow, cancellationToken);
            return value;
        }
        catch
        {
            value.Dispose();
            throw;
        }
    }

    private static void EnsureOnlineIdentityIsUsable(ConnectedIdentity identity)
    {
        if (!identity.IsEnabled || identity.AuthenticationState != AuthenticationState.Ready)
            throw new InvalidOperationException("The selected identity is disabled, revoked, or requires authentication.");
    }

    private EnterprisePolicySnapshot EnterprisePolicy() =>
        (enterprisePolicy ?? UnmanagedEnterprisePolicy.Instance)
            .GetSnapshot();

    private void EnsureSourceAllowed(
        (VaultItem Item, VaultResource Vault, ConnectedIdentity Identity) source,
        EnterprisePolicySnapshot? policy = null)
    {
        policy ??= EnterprisePolicy();
        policy.EnsureIdentityAllowed(source.Identity);
        policy.EnsureTenantAllowed(source.Vault.TenantId);
    }
}

public sealed class BrowserFillService(
    IMetadataRepository repository,
    SecretAccessService secretAccess,
    IClock clock,
    IBrowserFillPolicy? policy = null)
{
    private static readonly TimeSpan ApprovalLifetime = TimeSpan.FromSeconds(30);

    public Task<IReadOnlyList<BrowserFillMapping>> GetMappingsAsync(
        CancellationToken cancellationToken) =>
        repository.GetBrowserFillMappingsAsync(cancellationToken);

    public Task<IReadOnlyList<BrowserFillAuditEvent>> GetAuditAsync(
        int limit,
        CancellationToken cancellationToken) =>
        repository.GetBrowserFillAuditAsync(limit, cancellationToken);

    public Task<string> GetPolicyStatusAsync(CancellationToken cancellationToken) =>
        policy?.GetStatusAsync(cancellationToken) ??
        Task.FromResult(
            "Browser fill is disabled because no machine policy provider is configured.");

    public async Task<BrowserDestinationAssessment> AssessDestinationAsync(
        ValidatedBrowserFillRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var fieldPurpose = ToDomainPurpose(
            request.Request.FieldPurpose);
        var policyDecision = policy is null
            ? new BrowserFillPolicyDecision(
                false,
                "Browser fill is disabled because no machine policy provider is configured.")
            : await policy.EvaluateAsync(
                request.Request.BrowserFamily,
                request.TopOrigin,
                request.FrameOrigin,
                fieldPurpose,
                cancellationToken);
        var mapping = policyDecision.IsAllowed
            ? await repository.FindBrowserFillMappingAsync(
                request.TopOrigin.SerializedOrigin,
                request.FrameOrigin.SerializedOrigin,
                fieldPurpose,
                cancellationToken)
            : null;
        return new BrowserDestinationAssessment(
            request,
            fieldPurpose,
            policyDecision,
            mapping);
    }

    public async Task<BrowserFillMapping> SaveMappingAsync(
        Guid? expectedMappingId,
        Guid itemId,
        Guid identityId,
        string topOrigin,
        string frameOrigin,
        BrowserMappingFieldPurpose fieldPurpose,
        bool isEnabled,
        CancellationToken cancellationToken)
    {
        if (itemId == Guid.Empty || identityId == Guid.Empty || !Enum.IsDefined(fieldPurpose))
            throw new ArgumentException("Mapping item, identity, and field purpose are required.");

        var canonicalTop = CanonicalBrowserOrigin.Parse(topOrigin);
        var canonicalFrame = CanonicalBrowserOrigin.Parse(frameOrigin);
        var source = await repository.ResolveItemForIdentityAsync(
            itemId,
            identityId,
            cancellationToken)
            ?? throw new KeyNotFoundException(
                "The selected item is not reachable through the selected identity.");
        if (source.Item.ObjectType != VaultObjectType.Secret ||
            source.Item.IsDeletedOrUnavailable ||
            !source.Item.Enabled)
        {
            throw new InvalidOperationException("Only an enabled, available secret can be mapped.");
        }

        var existing = await repository.FindBrowserFillMappingAsync(
            canonicalTop.SerializedOrigin,
            canonicalFrame.SerializedOrigin,
            fieldPurpose,
            cancellationToken);
        if (existing is not null && existing.Id != expectedMappingId)
        {
            throw new InvalidOperationException(
                "That top origin, frame origin, and field purpose already have a mapping. Select it before replacing the mapped item.");
        }

        BrowserFillMapping? selected = null;
        if (expectedMappingId is { } mappingId)
        {
            selected = await repository.GetBrowserFillMappingAsync(mappingId, cancellationToken)
                ?? throw new KeyNotFoundException("The selected browser mapping no longer exists.");
        }

        var now = clock.UtcNow;
        var mapping = new BrowserFillMapping(
            selected?.Id ?? Guid.NewGuid(),
            source.Item.Id,
            source.Identity.Id,
            canonicalTop.SerializedOrigin,
            canonicalFrame.SerializedOrigin,
            fieldPurpose,
            isEnabled,
            selected?.CreatedAt ?? now,
            now);
        await repository.UpsertBrowserFillMappingAsync(mapping, cancellationToken);
        return mapping;
    }

    public Task RemoveMappingAsync(Guid mappingId, CancellationToken cancellationToken)
    {
        if (mappingId == Guid.Empty)
            throw new ArgumentException("Mapping identifier is required.", nameof(mappingId));
        return repository.RemoveBrowserFillMappingAsync(mappingId, cancellationToken);
    }

    public async Task<BrowserFillApproval?> PrepareAsync(
        ValidatedBrowserFillRequest request,
        CancellationToken cancellationToken)
    {
        var assessment = await AssessDestinationAsync(
            request,
            cancellationToken);
        if (!assessment.PolicyDecision.IsAllowed)
        {
            await RecordAuditAsync(request, null, "DeniedPolicy", cancellationToken);
            return null;
        }
        var mapping = assessment.ExistingMapping;
        if (mapping is null)
        {
            await RecordAuditAsync(request, null, "DeniedUnmapped", cancellationToken);
            return null;
        }
        if (!mapping.IsEnabled)
        {
            await RecordAuditAsync(request, mapping, "DeniedDisabled", cancellationToken);
            return null;
        }

        var source = await repository.ResolveItemForIdentityAsync(
            mapping.VaultItemId,
            mapping.ConnectedIdentityId,
            cancellationToken);
        if (source is null)
        {
            await RecordAuditAsync(request, mapping, "DeniedUnavailable", cancellationToken);
            return null;
        }
        var resolved = source.Value;
        if (resolved.Item.ObjectType != VaultObjectType.Secret ||
            resolved.Item.IsDeletedOrUnavailable ||
            !resolved.Item.Enabled ||
            !resolved.Identity.IsEnabled ||
            resolved.Identity.AuthenticationState != AuthenticationState.Ready)
        {
            await RecordAuditAsync(request, mapping, "DeniedUnavailable", cancellationToken);
            return null;
        }

        var expiresAt = request.Request.CreatedAtUtc + ApprovalLifetime;
        if (clock.UtcNow > expiresAt)
        {
            await RecordAuditAsync(request, mapping, "DeniedExpired", cancellationToken);
            return null;
        }

        return new BrowserFillApproval(
            Guid.NewGuid(),
            request,
            mapping,
            resolved.Item.ProviderObjectName,
            resolved.Vault.Name,
            resolved.Identity.DisplayName,
            expiresAt);
    }

    public async Task<BrowserFillResponse> ApproveAsync(
        BrowserFillApproval approval,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(approval);
        if (approval.ApprovalId == Guid.Empty || clock.UtcNow > approval.ExpiresAt)
        {
            await RecordAuditAsync(
                approval.Request,
                approval.Mapping,
                "DeniedExpired",
                cancellationToken);
            return BrowserFillResponse.Failure(
                approval.Request.Request.RequestId,
                BrowserFillResultCode.Expired);
        }

        var current = await repository.GetBrowserFillMappingAsync(
            approval.Mapping.Id,
            cancellationToken);
        if (current is null ||
            current != approval.Mapping ||
            !current.IsEnabled ||
            current.TopOrigin != approval.Request.TopOrigin.SerializedOrigin ||
            current.FrameOrigin != approval.Request.FrameOrigin.SerializedOrigin ||
            current.FieldPurpose != ToDomainPurpose(approval.Request.Request.FieldPurpose))
        {
            await RecordAuditAsync(
                approval.Request,
                current ?? approval.Mapping,
                "DeniedChanged",
                cancellationToken);
            return BrowserFillResponse.Failure(
                approval.Request.Request.RequestId,
                BrowserFillResultCode.ChangedContext);
        }

        if (policy is null ||
            !(await policy.EvaluateAsync(
                approval.Request.Request.BrowserFamily,
                approval.Request.TopOrigin,
                approval.Request.FrameOrigin,
                current.FieldPurpose,
                cancellationToken)).IsAllowed)
        {
            await RecordAuditAsync(
                approval.Request,
                current,
                "DeniedPolicy",
                cancellationToken);
            return BrowserFillResponse.Failure(
                approval.Request.Request.RequestId,
                BrowserFillResultCode.Denied);
        }

        byte[]? valueBytes = null;
        try
        {
            using var value = await secretAccess.RetrieveForIdentityAsync(
                current.VaultItemId,
                current.ConnectedIdentityId,
                $"Fill the approved {current.FieldPurpose} field for {current.TopOrigin}",
                cancellationToken);
            valueBytes = value.CopyUtf8Bytes();
            cancellationToken.ThrowIfCancellationRequested();
            await RecordAuditAsync(approval.Request, current, "Approved", cancellationToken);
            return new BrowserFillResponse(
                BrowserProtocolConstants.CurrentVersion,
                approval.Request.Request.RequestId,
                BrowserFillResultCode.Approved,
                Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)),
                current.Id,
                valueBytes);
        }
        catch
        {
            if (valueBytes is not null)
                CryptographicOperations.ZeroMemory(valueBytes);
            await RecordAuditAsync(approval.Request, current, "Denied", CancellationToken.None);
            throw;
        }
    }

    public Task DenyAsync(
        BrowserFillApproval approval,
        CancellationToken cancellationToken) =>
        RecordAuditAsync(approval.Request, approval.Mapping, "DeniedByUser", cancellationToken);

    private Task RecordAuditAsync(
        ValidatedBrowserFillRequest request,
        BrowserFillMapping? mapping,
        string result,
        CancellationToken cancellationToken) =>
        repository.RecordBrowserFillAuditAsync(
            new BrowserFillAuditEvent(
                Guid.NewGuid(),
                clock.UtcNow,
                mapping?.Id,
                mapping?.VaultItemId,
                mapping?.ConnectedIdentityId,
                request.TopOrigin.SerializedOrigin,
                request.FrameOrigin.SerializedOrigin,
                ToDomainPurpose(request.Request.FieldPurpose),
                result),
            cancellationToken);

    private static BrowserMappingFieldPurpose ToDomainPurpose(
        BrowserFieldPurpose purpose) =>
        purpose switch
        {
            BrowserFieldPurpose.Username => BrowserMappingFieldPurpose.Username,
            BrowserFieldPurpose.Password => BrowserMappingFieldPurpose.Password,
            BrowserFieldPurpose.OneTimeCode => BrowserMappingFieldPurpose.OneTimeCode,
            _ => throw new BrowserProtocolException("Field purpose is not supported."),
        };
}

public sealed class WorkspaceService(IMetadataRepository repository)
{
    public Task<IReadOnlyList<Workspace>> GetAllAsync(CancellationToken cancellationToken) => repository.GetWorkspacesAsync(cancellationToken);

    public Task SaveAsync(Workspace workspace, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(workspace.Name)) throw new ArgumentException("Workspace name is required.", nameof(workspace));
        return repository.UpsertWorkspaceAsync(workspace, cancellationToken);
    }

    public Task RemoveAsync(Guid id, CancellationToken cancellationToken) => repository.RemoveWorkspaceAsync(id, cancellationToken);

    public Task AddResourceAsync(Guid workspaceId, ResourceLinkType resourceType, string resourceId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(resourceId)) throw new ArgumentException("A resource identifier is required.", nameof(resourceId));
        return repository.AddWorkspaceLinkAsync(new WorkspaceResourceLink(Guid.NewGuid(), workspaceId, resourceType, resourceId), cancellationToken);
    }

    public Task RemoveResourceAsync(Guid workspaceId, ResourceLinkType resourceType, string resourceId, CancellationToken cancellationToken) =>
        repository.RemoveWorkspaceLinkAsync(workspaceId, resourceType, resourceId, cancellationToken);
}

public sealed class LocalDataRecoveryService(
    IUserVerificationService verification,
    ILocalDataResetter resetter)
{
    public const string ConfirmationPhrase = "RESET";

    public async Task<LocalDataArchive> ArchiveAndResetAsync(
        string confirmation,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(confirmation.Trim(), ConfirmationPhrase, StringComparison.Ordinal))
            throw new LocalDataResetConfirmationException();
        if (!verification.IsAvailable)
            throw new PlatformNotSupportedException("Windows verification is unavailable.");

        var result = await verification.VerifyAsync(
            "Archive current Vault Prospector data and start fresh",
            cancellationToken);
        if (result != UserVerificationResult.Verified)
            throw new UnauthorizedAccessException("Local verification was not completed.");

        return await resetter.ArchiveForResetAsync(cancellationToken);
    }
}

public sealed class LocalRecoveryArchiveService(
    IUserVerificationService verification,
    ILocalRecoveryArchiveStore archiveStore,
    IDiagnosticSink diagnostics)
{
    public const string ConfirmationPhrase = "DELETE ARCHIVE";

    public Task<IReadOnlyList<LocalRecoveryArchive>> ListAsync(
        CancellationToken cancellationToken) =>
        archiveStore.ListAsync(cancellationToken);

    public async Task DeleteAsync(
        string archiveId,
        string confirmation,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(archiveId);
        if (!string.Equals(
                confirmation.Trim(),
                ConfirmationPhrase,
                StringComparison.Ordinal))
        {
            throw new LocalRecoveryArchiveConfirmationException();
        }

        if (!verification.IsAvailable)
            throw new PlatformNotSupportedException(
                "Windows verification is unavailable.");

        var result = await verification.VerifyAsync(
            "Permanently delete the selected Vault Prospector recovery archive",
            cancellationToken);
        if (result != UserVerificationResult.Verified)
            throw new LocalRecoveryArchiveVerificationException();

        diagnostics.Information(
            "local_recovery_archive_delete_authorized",
            new Dictionary<string, object?>
            {
                ["status"] = "authorized",
            });

        try
        {
            await archiveStore.DeleteAsync(
                archiveId,
                cancellationToken);
        }
        catch (Exception exception)
        {
            try
            {
                diagnostics.WriteError(
                    "local_recovery_archive_delete_failed",
                    exception,
                    new Dictionary<string, object?>
                    {
                        ["status"] = "failed",
                    });
            }
            catch
            {
                // Preserve the deletion failure as the actionable exception.
            }

            throw;
        }

        try
        {
            diagnostics.Information(
                "local_recovery_archive_deleted",
                new Dictionary<string, object?>
                {
                    ["status"] = "deleted",
                });
        }
        catch
        {
            // Authorization was durably logged before the irreversible action.
        }
    }
}
