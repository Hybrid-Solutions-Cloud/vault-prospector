using VaultProspector.Domain;

namespace VaultProspector.Application;

public sealed class SystemClock : IClock { public DateTimeOffset UtcNow => DateTimeOffset.UtcNow; }

public sealed class IdentityService(IIdentityProvider provider, IMetadataRepository repository)
{
    public async Task<ConnectedIdentity> AddAsync(string clientId, string displayName, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(clientId?.Trim(), out var parsedClientId))
            throw new ArgumentException("A valid Microsoft Entra public-client application ID is required.", nameof(clientId));
        var identity = await provider.SignInAsync(parsedClientId.ToString("D"), displayName.Trim(), cancellationToken);
        try
        {
            await repository.UpsertIdentityAsync(identity, cancellationToken);
            return identity;
        }
        catch (Exception persistenceException)
        {
            try
            {
                await provider.RemoveAsync(identity, CancellationToken.None);
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

    public async Task RemoveAsync(Guid identityId, CancellationToken cancellationToken)
    {
        var identity = await repository.GetIdentityAsync(identityId, cancellationToken);
        if (identity is null) return;
        await provider.RemoveAsync(identity, cancellationToken);
        await repository.RemoveIdentityAsync(identityId, cancellationToken);
    }
}

public sealed class SynchronizationService(IVaultProvider provider, IMetadataRepository repository, IClock clock, IDiagnosticSink diagnostics)
{
    public async Task<SyncRun> SynchronizeAsync(ConnectedIdentity identity, CancellationToken cancellationToken)
    {
        var started = clock.UtcNow;
        try
        {
            var snapshot = await provider.DiscoverAsync(identity, cancellationToken);
            var status = snapshot.Errors.Count == 0 ? SyncStatus.Completed : SyncStatus.CompletedWithErrors;
            var run = new SyncRun(Guid.NewGuid(), identity.DisplayName, started, clock.UtcNow, status, snapshot.Vaults.Count, snapshot.Items.Count, snapshot.Errors.Select(x => x.SafeMessage).ToArray());
            await repository.ApplyDiscoveryAsync(identity.Id, snapshot, run, cancellationToken);
            diagnostics.Information("sync_completed", new Dictionary<string, object?> { ["identity_id"] = identity.Id, ["vault_count"] = run.VaultCount, ["item_count"] = run.ItemCount, ["error_count"] = run.NonSensitiveErrors.Count });
            return run;
        }
        catch (OperationCanceledException)
        {
            return new SyncRun(Guid.NewGuid(), identity.DisplayName, started, clock.UtcNow, SyncStatus.Cancelled, 0, 0, [], "User cancelled");
        }
        catch (Exception ex)
        {
            diagnostics.WriteError("sync_failed", ex, new Dictionary<string, object?> { ["identity_id"] = identity.Id });
            throw;
        }
    }
}

public sealed class SearchService(IMetadataRepository repository, IClock clock)
{
    public Task<IReadOnlyList<SearchResult>> SearchAsync(SearchRequest request, CancellationToken cancellationToken)
    {
        if (request.Limit is < 1 or > 1000) throw new ArgumentOutOfRangeException(nameof(request), "Search limit must be between 1 and 1000.");
        return repository.SearchAsync(request, clock.UtcNow, cancellationToken);
    }
}

public sealed class SecretAccessService(
    IVaultProvider provider,
    IMetadataRepository repository,
    IProtectedValueStore cache,
    IClipboardService clipboard,
    IUserVerificationService verification,
    IClock clock)
{
    public async Task<SensitiveValue> RetrieveAsync(Guid itemId, CancellationToken cancellationToken)
    {
        var source = await repository.ResolveItemAsync(itemId, cancellationToken) ?? throw new KeyNotFoundException("The selected vault item no longer exists.");
        if (source.Item.ObjectType != VaultObjectType.Secret) throw new InvalidOperationException("Only secret values can be retrieved. Key material and certificate private keys are never exported.");
        if (!verification.IsAvailable || !await verification.VerifyAsync("Reveal an Azure Key Vault secret", cancellationToken)) throw new UnauthorizedAccessException("Local verification was not completed.");
        var value = await provider.RetrieveSecretAsync(source.Identity, source.Vault, source.Item, cancellationToken);
        try
        {
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
        if (!policy.AllowClipboard) throw new InvalidOperationException("Clipboard use is disabled by policy.");
        var source = await repository.ResolveItemAsync(itemId, cancellationToken) ?? throw new KeyNotFoundException("The selected vault item no longer exists.");
        if (source.Item.ObjectType != VaultObjectType.Secret) throw new InvalidOperationException("Only secret values can be copied.");
        if (!verification.IsAvailable || !await verification.VerifyAsync("Copy an Azure Key Vault secret", cancellationToken))
            throw new UnauthorizedAccessException("Local verification was not completed.");

        using var value = await provider.RetrieveSecretAsync(source.Identity, source.Vault, source.Item, cancellationToken);
        await clipboard.CopyWithAutoClearAsync(value, clearAfter, cancellationToken);
        await repository.RecordAccessAsync(itemId, clock.UtcNow, cancellationToken);
    }

    public async Task<CachedSecretDescriptor> RetrieveAndCacheAsync(Guid itemId, Guid? workspaceId, TimeSpan lifetime, CachePolicy policy, CancellationToken cancellationToken)
    {
        var expiresAt = policy.GetExpiration(clock.UtcNow, lifetime);
        var source = await repository.ResolveItemAsync(itemId, cancellationToken) ?? throw new KeyNotFoundException("The selected vault item no longer exists.");
        if (source.Item.ObjectType != VaultObjectType.Secret) throw new InvalidOperationException("Only secret values can be cached.");
        if (!verification.IsAvailable || !await verification.VerifyAsync("Retrieve and cache an Azure Key Vault secret", cancellationToken))
            throw new UnauthorizedAccessException("Local verification was not completed.");

        using var value = await provider.RetrieveSecretAsync(source.Identity, source.Vault, source.Item, cancellationToken);
        await repository.RecordAccessAsync(itemId, clock.UtcNow, cancellationToken);
        return await cache.StoreAsync(itemId, source.Vault.Id, workspaceId, value, source.Item.MetadataFingerprint, expiresAt, cancellationToken);
    }

    public async Task<SensitiveValue> RetrieveCachedAsync(Guid itemId, CancellationToken cancellationToken)
    {
        var source = await repository.ResolveItemAsync(itemId, cancellationToken)
            ?? throw new KeyNotFoundException("The selected vault item no longer exists.");
        if (source.Item.ObjectType != VaultObjectType.Secret) throw new InvalidOperationException("Only cached secret values can be opened.");
        if (!verification.IsAvailable || !await verification.VerifyAsync("Open an offline secret", cancellationToken))
            throw new UnauthorizedAccessException("Local verification was not completed.");

        var value = await cache.RetrieveAsync(itemId, clock.UtcNow, source.Item.MetadataFingerprint, cancellationToken)
            ?? throw new KeyNotFoundException("No unexpired offline copy exists for the selected secret.");
        try
        {
            await repository.RecordAccessAsync(itemId, clock.UtcNow, cancellationToken);
            return value;
        }
        catch
        {
            value.Dispose();
            throw;
        }
    }
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
