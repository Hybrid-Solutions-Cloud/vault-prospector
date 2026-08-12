using System.Buffers.Text;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using VaultProspector.Application;
using VaultProspector.Domain;

namespace VaultProspector.Infrastructure;

public sealed class EncryptedSqliteMetadataRepository(
    string databasePath,
    IKeyMaterialProvider keyMaterial) : IMetadataRepository, IDisposable
{
    private const int CurrentSchemaVersion = 8;
    private const int SqlCipherKeyLength = 32;
    private const int SqlCipherSaltLength = 16;
    private const int SqlCipherKdfIterations = 256_000;
    private string? _connectionString;
    private byte[]? _sqlCipherRawKey;

    public void Dispose()
    {
        if (_sqlCipherRawKey is not null)
        {
            CryptographicOperations.ZeroMemory(_sqlCipherRawKey);
            _sqlCipherRawKey = null;
        }

        _connectionString = null;
        GC.SuppressFinalize(this);
    }

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        if (!keyMaterial.IsAvailable)
            throw new PlatformNotSupportedException("Platform-protected key storage is required. Plaintext databases are prohibited.");

        var databaseExists = File.Exists(databasePath);
        Directory.CreateDirectory(Path.GetDirectoryName(databasePath) ?? ".");
        SQLitePCL.Batteries_V2.Init();
        var key = databaseExists
            ? await keyMaterial.GetExistingKeyAsync("metadata-database", cancellationToken)
            : await keyMaterial.GetOrCreateKeyAsync("metadata-database", cancellationToken);
        byte[]? salt = null;
        try
        {
            salt = databaseExists
                ? await ReadSqlCipherSaltAsync(databasePath, cancellationToken)
                : RandomNumberGenerator.GetBytes(SqlCipherSaltLength);
            var rawKey = CreateSqlCipherRawKey(key, salt);
            if (_sqlCipherRawKey is not null)
                CryptographicOperations.ZeroMemory(_sqlCipherRawKey);
            _sqlCipherRawKey = rawKey;
            _connectionString = new SqliteConnectionStringBuilder
            {
                DataSource = databasePath,
                Mode = SqliteOpenMode.ReadWriteCreate,
                Pooling = false,
            }.ToString();
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
            if (salt is not null)
                CryptographicOperations.ZeroMemory(salt);
        }

        var validated = await OpenValidatedAsync(cancellationToken);
        await using var connection = validated.Connection;
        await ExecuteAsync(connection, null, Configuration, cancellationToken);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
        if (validated.SchemaVersion < CurrentSchemaVersion)
        {
            var schemaVersion = validated.SchemaVersion;

            if (schemaVersion == 0)
            {
                await ExecuteAsync(connection, transaction, Schema, cancellationToken);
                schemaVersion = CurrentSchemaVersion;
            }
            else
            {
                await ExecuteAsync(connection, transaction, Schema, cancellationToken);

                if (schemaVersion == 1)
                {
                    await EnsureClientIdColumnAsync(connection, transaction, cancellationToken);
                    schemaVersion = 2;
                }

                if (schemaVersion == 2)
                {
                    await EnsureIdentityProfilesColumnsAsync(connection, transaction, cancellationToken);
                    schemaVersion = 3;
                }

                if (schemaVersion == 3)
                {
                    await EnsureVaultSelectionColumnAsync(connection, transaction, cancellationToken);
                    schemaVersion = 4;
                }

                if (schemaVersion == 4)
                {
                    await EnsureBrowserIntegrationTablesAsync(connection, transaction, cancellationToken);
                    schemaVersion = 5;
                }

                if (schemaVersion == 5)
                {
                    await EnsureCyberArkTablesAsync(connection, transaction, cancellationToken);
                    schemaVersion = 6;
                }

                if (schemaVersion == 6)
                {
                    await EnsureGovernedMutationAuditTableAsync(
                        connection,
                        transaction,
                        cancellationToken);
                    schemaVersion = 7;
                }

                if (schemaVersion == 7)
                {
                    await EnsureTenantSelectionColumnAsync(
                        connection,
                        transaction,
                        cancellationToken);
                    schemaVersion = 8;
                }

                if (schemaVersion != CurrentSchemaVersion)
                {
                    throw new InvalidOperationException($"Migration failed. Expected version {CurrentSchemaVersion}, but ended up at {schemaVersion}.");
                }
            }

            await ExecuteAsync(connection, transaction, $"PRAGMA user_version={CurrentSchemaVersion}", cancellationToken);
        }

        await ValidateSchemaAsync(connection, transaction, cancellationToken);
        await ValidateGovernedMutationAuditChainAsync(
            connection,
            transaction,
            cancellationToken);
        await ValidateForeignKeysAsync(connection, transaction, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        await VerifyDatabaseIntegrityAsync(connection, cancellationToken);
    }

    public async Task<IReadOnlyList<ConnectedIdentity>> GetIdentitiesAsync(CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = IdentitySelect + " ORDER BY display_name COLLATE NOCASE";
        var result = new List<ConnectedIdentity>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken)) result.Add(ReadIdentity(reader));
        return result;
    }

    public async Task<ConnectedIdentity?> GetIdentityAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = IdentitySelect + " WHERE id=$id";
        command.Parameters.AddWithValue("$id", id.ToString("D"));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadIdentity(reader) : null;
    }

    public async Task UpsertIdentityAsync(ConnectedIdentity identity, CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await ExecuteAsync(connection, null, """
            INSERT INTO identities(id,client_id,account_identifier,username_hint,display_name,home_tenant_id,auth_state,last_interactive,is_enabled,identity_type,credential_data)
            VALUES($id,$client,$account,$username,$display,$tenant,$state,$last,$enabled,$type,$credential)
            ON CONFLICT(id) DO UPDATE SET client_id=excluded.client_id,account_identifier=excluded.account_identifier,username_hint=excluded.username_hint,display_name=excluded.display_name,home_tenant_id=excluded.home_tenant_id,auth_state=excluded.auth_state,last_interactive=excluded.last_interactive,is_enabled=excluded.is_enabled,identity_type=excluded.identity_type,credential_data=excluded.credential_data
            """, cancellationToken,
            ("$id", identity.Id.ToString("D")), ("$client", identity.ClientId), ("$account", identity.AccountIdentifier), ("$username", identity.UsernameHint),
            ("$display", identity.DisplayName), ("$tenant", identity.HomeTenantId), ("$state", (int)identity.AuthenticationState),
            ("$last", Format(identity.LastInteractiveAuthentication)), ("$enabled", identity.IsEnabled ? 1 : 0),
            ("$type", (int)identity.Type), ("$credential", identity.CredentialData));
    }

    public async Task RemoveIdentityAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await ExecuteAsync(connection, (SqliteTransaction)transaction, "DELETE FROM vault_access WHERE identity_id=$id; DELETE FROM tenants WHERE identity_id=$id; DELETE FROM identities WHERE id=$id", cancellationToken, ("$id", id.ToString("D")));
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<SubscriptionAccess>> GetSubscriptionsAsync(
        Guid identityId,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT s.id,s.tenant_access_id,s.subscription_id,s.display_name,s.state,s.is_selected,s.last_discovered
            FROM subscriptions s
            JOIN tenants t ON t.id=s.tenant_access_id
            WHERE t.identity_id=$identity
            ORDER BY s.display_name COLLATE NOCASE,s.subscription_id
            """;
        command.Parameters.AddWithValue("$identity", identityId.ToString("D"));
        var subscriptions = new List<SubscriptionAccess>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            subscriptions.Add(new SubscriptionAccess(
                reader.GetGuid(0),
                reader.GetGuid(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetString(4),
                reader.GetBoolean(5),
                DateTimeOffset.Parse(reader.GetString(6), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind)));
        }

        return subscriptions;
    }

    public async Task<IReadOnlyList<TenantAccess>> GetTenantsAsync(
        Guid identityId,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id,identity_id,tenant_id,display_name,tenant_type,last_validated,status,is_selected
            FROM tenants
            WHERE identity_id=$identity
            ORDER BY display_name COLLATE NOCASE,tenant_id
            """;
        command.Parameters.AddWithValue("$identity", identityId.ToString("D"));
        var tenants = new List<TenantAccess>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            tenants.Add(new TenantAccess(
                reader.GetGuid(0),
                reader.GetGuid(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetString(4),
                DateTimeOffset.Parse(reader.GetString(5), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
                reader.GetString(6),
                reader.GetBoolean(7)));
        }

        return tenants;
    }

    public async Task SetTenantSelectedAsync(
        Guid tenantAccessId,
        bool isSelected,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await ExecuteAsync(
            connection,
            null,
            "UPDATE tenants SET is_selected=$selected WHERE id=$id",
            cancellationToken,
            ("$selected", isSelected ? 1 : 0),
            ("$id", tenantAccessId.ToString("D")));
    }

    public async Task SetSubscriptionSelectedAsync(
        Guid subscriptionAccessId,
        bool isSelected,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await ExecuteAsync(
            connection,
            null,
            "UPDATE subscriptions SET is_selected=$selected WHERE id=$id",
            cancellationToken,
            ("$selected", isSelected ? 1 : 0),
            ("$id", subscriptionAccessId.ToString("D")));
    }

    public async Task<IReadOnlyList<VaultAccessSummary>> GetVaultAccessSummariesAsync(
        Guid identityId,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT v.id,v.resource_id,v.name,v.tenant_id,v.subscription_id,v.resource_group,v.location,v.tags,v.vault_uri,v.last_indexed,
                   va.id,va.identity_id,va.tenant_id,va.status,va.last_validated,va.failure_category,va.preferred_rank,va.is_selected,
                   ident.display_name,COALESCE(t.display_name,v.tenant_id)
            FROM vault_access va
            JOIN vaults v ON v.id=va.vault_id
            JOIN identities ident ON ident.id=va.identity_id
            LEFT JOIN tenants t ON t.identity_id=ident.id AND t.tenant_id=v.tenant_id
            WHERE va.identity_id=$identity AND va.status!='Removed'
            ORDER BY v.name COLLATE NOCASE,v.subscription_id
            """;
        command.Parameters.AddWithValue("$identity", identityId.ToString("D"));
        var summaries = new List<VaultAccessSummary>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var vaultId = reader.GetGuid(0);
            var vault = ReadVault(reader, 1, vaultId);
            var access = new VaultAccess(
                reader.GetGuid(10),
                vaultId,
                reader.GetGuid(11),
                reader.GetString(12),
                reader.GetString(13),
                DateTimeOffset.Parse(reader.GetString(14), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
                reader.IsDBNull(15) ? null : reader.GetString(15),
                reader.GetInt32(16),
                reader.GetBoolean(17));
            summaries.Add(new VaultAccessSummary(vault, access, reader.GetString(18), reader.GetString(19)));
        }

        return summaries;
    }

    public async Task SetVaultSelectedAsync(
        Guid vaultAccessId,
        bool isSelected,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await ExecuteAsync(
            connection,
            null,
            "UPDATE vault_access SET is_selected=$selected WHERE id=$id",
            cancellationToken,
            ("$selected", isSelected ? 1 : 0),
            ("$id", vaultAccessId.ToString("D")));
    }

    public async Task<IReadOnlyList<Guid>> GetVaultIdsForIdentityAsync(
        Guid identityId,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT DISTINCT vault_id
            FROM vault_access
            WHERE identity_id=$identity
            ORDER BY vault_id
            """;
        command.Parameters.AddWithValue(
            "$identity",
            identityId.ToString("D"));
        var vaultIds = new List<Guid>();
        await using var reader =
            await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            vaultIds.Add(reader.GetGuid(0));
        return vaultIds;
    }

    public Task ApplyDiscoveryAsync(Guid identityId, DiscoverySnapshot snapshot, SyncRun run, CancellationToken cancellationToken) =>
        ApplyDiscoveryCoreAsync(identityId, snapshot, run, reconcileMissing: true, cancellationToken);

    public Task ApplyDiscoveryPatchAsync(Guid identityId, DiscoverySnapshot snapshot, SyncRun run, CancellationToken cancellationToken) =>
        ApplyDiscoveryCoreAsync(identityId, snapshot, run, reconcileMissing: false, cancellationToken);

    private async Task ApplyDiscoveryCoreAsync(
        Guid identityId,
        DiscoverySnapshot snapshot,
        SyncRun run,
        bool reconcileMissing,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
        foreach (var x in snapshot.Tenants) await UpsertTenantAsync(connection, transaction, x, cancellationToken);
        foreach (var x in snapshot.Subscriptions) await UpsertSubscriptionAsync(connection, transaction, x, cancellationToken);
        foreach (var x in snapshot.Vaults) await UpsertVaultAsync(connection, transaction, x, cancellationToken);
        foreach (var x in snapshot.AccessPaths) await UpsertAccessAsync(connection, transaction, x, cancellationToken);
        await UpsertItemsAsync(connection, transaction, snapshot.Items, cancellationToken);
        await ExecuteAsync(connection, transaction, "INSERT INTO sync_runs(id,scope,started_at,completed_at,status,vault_count,item_count,error_count) VALUES($id,$scope,$started,$completed,$status,$vaults,$items,$errors)", cancellationToken,
            ("$id", run.Id.ToString("D")), ("$scope", run.Scope), ("$started", Format(run.StartedAt)),
            ("$completed", run.CompletedAt is null ? DBNull.Value : Format(run.CompletedAt.Value)), ("$status", (int)run.Status),
            ("$vaults", run.VaultCount), ("$items", run.ItemCount), ("$errors", run.NonSensitiveErrors.Count));
        if (reconcileMissing && snapshot.Errors.Count == 0)
        {
            var discoveredVaultIds = string.Join(",", snapshot.Vaults.Select(v => $"'{v.Id:D}'"));
            var accessSql = string.IsNullOrEmpty(discoveredVaultIds)
                ? "UPDATE vault_access SET status = 'Removed' WHERE identity_id = $id"
                : $"UPDATE vault_access SET status = 'Removed' WHERE identity_id = $id AND vault_id NOT IN ({discoveredVaultIds})";
            await ExecuteAsync(connection, transaction, accessSql, cancellationToken, ("$id", identityId.ToString("D")));

            var vaultIds = string.Join(",", snapshot.Vaults.Select(v => $"'{v.Id:D}'"));
            if (!string.IsNullOrEmpty(vaultIds))
            {
                var discoveredItemIds = string.Join(",", snapshot.Items.Select(i => $"'{i.Id:D}'"));
                var itemSql = string.IsNullOrEmpty(discoveredItemIds)
                    ? $"UPDATE items SET is_deleted = 1 WHERE vault_id IN ({vaultIds})"
                    : $"UPDATE items SET is_deleted = 1 WHERE vault_id IN ({vaultIds}) AND id NOT IN ({discoveredItemIds})";
                await ExecuteAsync(connection, transaction, itemSql, cancellationToken);
            }
        }
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task RecordGovernedMutationAuditAsync(
        GovernedMutationAuditEvent auditEvent,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(auditEvent);
        await using var connection = await OpenAsync(cancellationToken);
        await using var transaction =
            (SqliteTransaction)await connection.BeginTransactionAsync(
                cancellationToken);
        string currentHash;
        await using (var current = connection.CreateCommand())
        {
            current.Transaction = transaction;
            current.CommandText =
                "SELECT record_hash FROM governed_mutation_audit ORDER BY sequence DESC LIMIT 1";
            currentHash =
                (await current.ExecuteScalarAsync(cancellationToken)
                    as string) ?? string.Empty;
        }
        if (!string.Equals(
                currentHash,
                auditEvent.PreviousHash,
                StringComparison.Ordinal))
        {
            throw new LocalDataIntegrityException(
                "The governed-mutation audit chain changed before the new record could be appended.");
        }

        await ExecuteAsync(
            connection,
            transaction,
            """
            INSERT INTO governed_mutation_audit(
                id,preview_id,occurred_at,operation,identity_id,tenant_id,
                subscription_id,vault_resource_id,object_name_hash,
                sensitive_value_length,result,provider_version,safe_message,
                previous_hash,record_hash)
            VALUES(
                $id,$preview,$occurred,$operation,$identity,$tenant,
                $subscription,$vault,$object_hash,$value_length,$result,
                $version,$message,$previous_hash,$record_hash)
            """,
            cancellationToken,
            ("$id", auditEvent.Id.ToString("D")),
            ("$preview", auditEvent.PreviewId.ToString("D")),
            ("$occurred", Format(auditEvent.OccurredAt)),
            ("$operation", (int)auditEvent.Operation),
            ("$identity", auditEvent.IdentityId.ToString("D")),
            ("$tenant", auditEvent.TenantId),
            ("$subscription", auditEvent.SubscriptionId),
            ("$vault", auditEvent.VaultResourceId),
            ("$object_hash", auditEvent.ObjectNameHash),
            ("$value_length", auditEvent.SensitiveValueLength),
            ("$result", (int)auditEvent.Result),
            ("$version", auditEvent.ProviderVersion),
            ("$message", auditEvent.SafeMessage),
            ("$previous_hash", auditEvent.PreviousHash),
            ("$record_hash", auditEvent.RecordHash));
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<GovernedMutationAuditEvent?>
        GetLatestGovernedMutationAuditAsync(
            CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT id,preview_id,occurred_at,operation,identity_id,tenant_id,
                   subscription_id,vault_resource_id,object_name_hash,
                   sensitive_value_length,result,provider_version,safe_message,
                   previous_hash,record_hash
            FROM governed_mutation_audit
            ORDER BY sequence DESC
            LIMIT 1
            """;
        await using var reader =
            await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return null;
        return new GovernedMutationAuditEvent(
            reader.GetGuid(0),
            reader.GetGuid(1),
            DateTimeOffset.Parse(
                reader.GetString(2),
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind),
            (GovernedAzureOperation)reader.GetInt32(3),
            reader.GetGuid(4),
            reader.GetString(5),
            reader.GetString(6),
            reader.GetString(7),
            reader.GetString(8),
            reader.GetInt32(9),
            (GovernedMutationAuditResult)reader.GetInt32(10),
            reader.GetString(11),
            reader.GetString(12),
            reader.GetString(13),
            reader.GetString(14));
    }

    public async Task<IReadOnlyList<SearchResult>> SearchAsync(SearchRequest request, DateTimeOffset now, CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = SearchSql;
        command.Parameters.AddWithValue("$now", Format(now));
        command.Parameters.AddWithValue("$text", EscapeLike(request.Text.Trim()));
        AddNullable(command, "$identity", request.IdentityId?.ToString("D"));
        AddNullable(command, "$tenant", request.TenantId);
        AddNullable(command, "$subscription", request.SubscriptionId);
        AddNullable(command, "$vault", request.VaultId?.ToString("D"));
        AddNullable(command, "$vault_name", request.VaultName);
        AddNullable(command, "$type", request.ObjectType is null ? null : (int)request.ObjectType.Value);
        AddNullable(command, "$enabled", request.Enabled is null ? null : request.Enabled.Value ? 1 : 0);
        AddNullable(command, "$workspace", request.WorkspaceId?.ToString("D"));
        command.Parameters.AddWithValue("$favorites", request.FavoritesOnly ? 1 : 0);
        command.Parameters.AddWithValue("$expired", request.ExpiredOnly ? 1 : 0);
        command.Parameters.AddWithValue("$stale", request.StaleOnly ? 1 : 0);
        command.Parameters.AddWithValue("$recent_first", request.RecentlyAccessedFirst ? 1 : 0);
        command.Parameters.AddWithValue("$limit", request.Limit);

        var results = new List<SearchResult>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var item = ReadItem(reader, 0);
            var vault = ReadVault(reader, 14, item.VaultId);
            results.Add(new SearchResult(item, vault, reader.GetString(23), reader.GetString(24), reader.GetBoolean(25), ReadDate(reader, 26), reader.GetBoolean(27), reader.GetString(28)));
        }
        return results;
    }

    public async Task<(VaultItem Item, VaultResource Vault, ConnectedIdentity Identity)?> ResolveItemAsync(Guid itemId, CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = ResolveSql;
        command.Parameters.AddWithValue("$id", itemId.ToString("D"));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;
        var item = ReadItem(reader, 0);
        return (item, ReadVault(reader, 14, item.VaultId), ReadIdentity(reader, 23));
    }

    public async Task<(VaultItem Item, VaultResource Vault, ConnectedIdentity Identity)?> ResolveItemForIdentityAsync(
        Guid itemId,
        Guid identityId,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = ResolveSql.Replace(
            "WHERE i.id=$id AND ident.is_enabled=1",
            "WHERE i.id=$id AND ident.id=$identity AND ident.is_enabled=1",
            StringComparison.Ordinal);
        command.Parameters.AddWithValue("$id", itemId.ToString("D"));
        command.Parameters.AddWithValue("$identity", identityId.ToString("D"));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;
        var item = ReadItem(reader, 0);
        return (item, ReadVault(reader, 14, item.VaultId), ReadIdentity(reader, 23));
    }

    public async Task RecordAccessAsync(Guid itemId, DateTimeOffset accessedAt, CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await ExecuteAsync(connection, null, "INSERT INTO access_history(item_id,last_accessed) VALUES($id,$at) ON CONFLICT(item_id) DO UPDATE SET last_accessed=excluded.last_accessed", cancellationToken, ("$id", itemId.ToString("D")), ("$at", Format(accessedAt)));
    }

    public async Task SetFavoriteAsync(Guid itemId, bool isFavorite, CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await ExecuteAsync(connection, null, isFavorite ? "INSERT OR IGNORE INTO favorites(item_id) VALUES($id)" : "DELETE FROM favorites WHERE item_id=$id", cancellationToken, ("$id", itemId.ToString("D")));
    }

    public async Task<IReadOnlyList<Workspace>> GetWorkspacesAsync(CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT id,name,description,sort_order,cache_enabled,cache_lifetime_minutes,require_unlock,allow_clipboard FROM workspaces ORDER BY sort_order,name";
        var result = new List<Workspace>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            result.Add(new Workspace(reader.GetGuid(0), reader.GetString(1), reader.GetString(2), reader.GetInt32(3), new CachePolicy(reader.GetBoolean(4), TimeSpan.FromMinutes(reader.GetInt32(5)), reader.GetBoolean(6), reader.GetBoolean(7))));
        return result;
    }

    public async Task UpsertWorkspaceAsync(Workspace workspace, CancellationToken cancellationToken)
    {
        var policy = workspace.CachePolicyOverride ?? CachePolicy.SecureDefault;
        await using var connection = await OpenAsync(cancellationToken);
        await ExecuteAsync(connection, null, """
            INSERT INTO workspaces(id,name,description,sort_order,cache_enabled,cache_lifetime_minutes,require_unlock,allow_clipboard)
            VALUES($id,$name,$description,$sort,$enabled,$lifetime,$unlock,$clipboard)
            ON CONFLICT(id) DO UPDATE SET name=excluded.name,description=excluded.description,sort_order=excluded.sort_order,cache_enabled=excluded.cache_enabled,cache_lifetime_minutes=excluded.cache_lifetime_minutes,require_unlock=excluded.require_unlock,allow_clipboard=excluded.allow_clipboard
            """, cancellationToken,
            ("$id", workspace.Id.ToString("D")), ("$name", workspace.Name), ("$description", workspace.Description), ("$sort", workspace.SortOrder),
            ("$enabled", policy.IsEnabled ? 1 : 0), ("$lifetime", (int)policy.MaximumLifetime.TotalMinutes),
            ("$unlock", policy.RequireLocalUnlock ? 1 : 0), ("$clipboard", policy.AllowClipboard ? 1 : 0));
    }

    public async Task RemoveWorkspaceAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
        await ExecuteAsync(
            connection,
            transaction,
            "DELETE FROM workspace_links WHERE workspace_id=$id; DELETE FROM workspaces WHERE id=$id",
            cancellationToken,
            ("$id", id.ToString("D")));
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task AddWorkspaceLinkAsync(WorkspaceResourceLink link, CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await ExecuteAsync(connection, null, "INSERT OR IGNORE INTO workspace_links(id,workspace_id,resource_type,resource_id) VALUES($id,$workspace,$type,$resource)", cancellationToken,
            ("$id", link.Id.ToString("D")), ("$workspace", link.WorkspaceId.ToString("D")), ("$type", (int)link.ResourceType), ("$resource", link.ResourceId));
    }

    public async Task RemoveWorkspaceLinkAsync(Guid workspaceId, ResourceLinkType resourceType, string resourceId, CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await ExecuteAsync(connection, null, "DELETE FROM workspace_links WHERE workspace_id=$workspace AND resource_type=$type AND resource_id=$resource", cancellationToken,
            ("$workspace", workspaceId.ToString("D")), ("$type", (int)resourceType), ("$resource", resourceId));
    }

    public async Task<IReadOnlyList<BrowserFillMapping>> GetBrowserFillMappingsAsync(
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = BrowserMappingSelect + " ORDER BY top_origin,frame_origin,field_purpose";
        var result = new List<BrowserFillMapping>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            result.Add(ReadBrowserMapping(reader));
        return result;
    }

    public async Task<BrowserFillMapping?> GetBrowserFillMappingAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = BrowserMappingSelect + " WHERE id=$id";
        command.Parameters.AddWithValue("$id", id.ToString("D"));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadBrowserMapping(reader) : null;
    }

    public async Task<BrowserFillMapping?> FindBrowserFillMappingAsync(
        string topOrigin,
        string frameOrigin,
        BrowserMappingFieldPurpose fieldPurpose,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = BrowserMappingSelect +
            " WHERE top_origin=$top AND frame_origin=$frame AND field_purpose=$purpose";
        command.Parameters.AddWithValue("$top", topOrigin);
        command.Parameters.AddWithValue("$frame", frameOrigin);
        command.Parameters.AddWithValue("$purpose", (int)fieldPurpose);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadBrowserMapping(reader) : null;
    }

    public async Task UpsertBrowserFillMappingAsync(
        BrowserFillMapping mapping,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await ExecuteAsync(
            connection,
            null,
            """
            INSERT INTO browser_fill_mappings(
                id,item_id,identity_id,top_origin,frame_origin,field_purpose,is_enabled,created_at,updated_at)
            VALUES($id,$item,$identity,$top,$frame,$purpose,$enabled,$created,$updated)
            ON CONFLICT(id) DO UPDATE SET
                item_id=excluded.item_id,
                identity_id=excluded.identity_id,
                top_origin=excluded.top_origin,
                frame_origin=excluded.frame_origin,
                field_purpose=excluded.field_purpose,
                is_enabled=excluded.is_enabled,
                updated_at=excluded.updated_at
            """,
            cancellationToken,
            ("$id", mapping.Id.ToString("D")),
            ("$item", mapping.VaultItemId.ToString("D")),
            ("$identity", mapping.ConnectedIdentityId.ToString("D")),
            ("$top", mapping.TopOrigin),
            ("$frame", mapping.FrameOrigin),
            ("$purpose", (int)mapping.FieldPurpose),
            ("$enabled", mapping.IsEnabled ? 1 : 0),
            ("$created", Format(mapping.CreatedAt)),
            ("$updated", Format(mapping.UpdatedAt)));
    }

    public async Task RemoveBrowserFillMappingAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await ExecuteAsync(
            connection,
            null,
            "DELETE FROM browser_fill_mappings WHERE id=$id",
            cancellationToken,
            ("$id", id.ToString("D")));
    }

    public async Task RecordBrowserFillAuditAsync(
        BrowserFillAuditEvent auditEvent,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var transaction =
            (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
        await ExecuteAsync(
            connection,
            transaction,
            """
            INSERT INTO browser_fill_audit(
                id,occurred_at,mapping_id,item_id,identity_id,top_origin,frame_origin,field_purpose,result)
            VALUES($id,$at,$mapping,$item,$identity,$top,$frame,$purpose,$result)
            """,
            cancellationToken,
            ("$id", auditEvent.Id.ToString("D")),
            ("$at", Format(auditEvent.OccurredAt)),
            ("$mapping", auditEvent.MappingId?.ToString("D") ?? (object)DBNull.Value),
            ("$item", auditEvent.VaultItemId?.ToString("D") ?? (object)DBNull.Value),
            ("$identity", auditEvent.ConnectedIdentityId?.ToString("D") ?? (object)DBNull.Value),
            ("$top", auditEvent.TopOrigin),
            ("$frame", auditEvent.FrameOrigin),
            ("$purpose", (int)auditEvent.FieldPurpose),
            ("$result", auditEvent.Result));
        await ExecuteAsync(
            connection,
            transaction,
            """
            DELETE FROM browser_fill_audit
            WHERE id IN (
                SELECT id FROM browser_fill_audit
                ORDER BY occurred_at DESC,id DESC
                LIMIT -1 OFFSET 500
            )
            """,
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<BrowserFillAuditEvent>> GetBrowserFillAuditAsync(
        int limit,
        CancellationToken cancellationToken)
    {
        if (limit is < 1 or > 500)
            throw new ArgumentOutOfRangeException(nameof(limit));
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id,occurred_at,mapping_id,item_id,identity_id,top_origin,frame_origin,field_purpose,result
            FROM browser_fill_audit
            ORDER BY occurred_at DESC,id DESC
            LIMIT $limit
            """;
        command.Parameters.AddWithValue("$limit", limit);
        var result = new List<BrowserFillAuditEvent>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(new BrowserFillAuditEvent(
                reader.GetGuid(0),
                DateTimeOffset.Parse(reader.GetString(1), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
                reader.IsDBNull(2) ? null : reader.GetGuid(2),
                reader.IsDBNull(3) ? null : reader.GetGuid(3),
                reader.IsDBNull(4) ? null : reader.GetGuid(4),
                reader.GetString(5),
                reader.GetString(6),
                (BrowserMappingFieldPurpose)reader.GetInt32(7),
                reader.GetString(8)));
        }
        return result;
    }

    public async Task<IReadOnlyList<CyberArkProfile>> GetCyberArkProfilesAsync(
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = CyberArkProfileSelect +
            " ORDER BY display_name COLLATE NOCASE,id";
        var result = new List<CyberArkProfile>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            result.Add(ReadCyberArkProfile(reader));
        return result;
    }

    public async Task<CyberArkProfile?> GetCyberArkProfileAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = CyberArkProfileSelect + " WHERE id=$id";
        command.Parameters.AddWithValue("$id", id.ToString("D"));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? ReadCyberArkProfile(reader)
            : null;
    }

    public async Task UpsertCyberArkProfileAsync(
        CyberArkProfile profile,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await ExecuteAsync(
            connection,
            null,
            """
            INSERT INTO cyberark_profiles(
                id,display_name,identity_url,privilege_cloud_url,service_user_name,
                application_name,auth_state,is_enabled,credential_updated_at,last_validated_at)
            VALUES($id,$display,$identity,$privilege,$user,$application,$state,$enabled,$credential,$validated)
            ON CONFLICT(id) DO UPDATE SET
                display_name=excluded.display_name,
                identity_url=excluded.identity_url,
                privilege_cloud_url=excluded.privilege_cloud_url,
                service_user_name=excluded.service_user_name,
                application_name=excluded.application_name,
                auth_state=excluded.auth_state,
                is_enabled=excluded.is_enabled,
                credential_updated_at=excluded.credential_updated_at,
                last_validated_at=excluded.last_validated_at
            """,
            cancellationToken,
            ("$id", profile.Id.ToString("D")),
            ("$display", profile.DisplayName),
            ("$identity", profile.IdentityUrl.AbsoluteUri),
            ("$privilege", profile.PrivilegeCloudUrl.AbsoluteUri),
            ("$user", profile.ServiceUserName),
            ("$application", profile.ApplicationName),
            ("$state", (int)profile.AuthenticationState),
            ("$enabled", profile.IsEnabled ? 1 : 0),
            ("$credential", Format(profile.CredentialUpdatedAt)),
            ("$validated", profile.LastValidatedAt is null
                ? DBNull.Value
                : Format(profile.LastValidatedAt.Value)));
    }

    public async Task RemoveCyberArkProfileAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await ExecuteAsync(
            connection,
            null,
            "DELETE FROM cyberark_profiles WHERE id=$id",
            cancellationToken,
            ("$id", id.ToString("D")));
    }

    public async Task ApplyCyberArkDiscoveryAsync(
        Guid profileId,
        CyberArkDiscoverySnapshot snapshot,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var transaction =
            (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
        await ExecuteAsync(
            connection,
            transaction,
            """
            DELETE FROM cyberark_permissions WHERE profile_id=$profile;
            DELETE FROM cyberark_versions WHERE profile_id=$profile;
            DELETE FROM cyberark_accounts WHERE profile_id=$profile;
            DELETE FROM cyberark_safes WHERE profile_id=$profile;
            """,
            cancellationToken,
            ("$profile", profileId.ToString("D")));

        foreach (var safe in snapshot.Safes)
        {
            EnsureCyberArkSource(profileId, safe.ProfileId);
            await ExecuteAsync(
                connection,
                transaction,
                """
                INSERT INTO cyberark_safes(
                    profile_id,safe_id,name,description,location,retention_days,
                    retention_versions,olac_enabled,created_at,updated_at)
                VALUES($profile,$safe,$name,$description,$location,$days,$versions,$olac,$created,$updated)
                """,
                cancellationToken,
                ("$profile", profileId.ToString("D")),
                ("$safe", safe.SafeId),
                ("$name", safe.Name),
                ("$description", safe.Description),
                ("$location", safe.Location),
                ("$days", safe.RetentionDays ?? (object)DBNull.Value),
                ("$versions", safe.RetentionVersions ?? (object)DBNull.Value),
                ("$olac", safe.ObjectLevelAccessControlEnabled ? 1 : 0),
                ("$created", safe.CreatedAt is null
                    ? DBNull.Value
                    : Format(safe.CreatedAt.Value)),
                ("$updated", safe.UpdatedAt is null
                    ? DBNull.Value
                    : Format(safe.UpdatedAt.Value)));
        }

        foreach (var account in snapshot.Accounts)
        {
            EnsureCyberArkSource(profileId, account.ProfileId);
            await ExecuteAsync(
                connection,
                transaction,
                """
                INSERT INTO cyberark_accounts(
                    profile_id,account_id,safe_name,name,user_name,address,platform_id,
                    secret_type,status,created_at,updated_at,fingerprint,last_indexed,is_deleted)
                VALUES($profile,$account,$safe,$name,$user,$address,$platform,$type,$status,
                       $created,$updated,$fingerprint,$indexed,$deleted)
                """,
                cancellationToken,
                ("$profile", profileId.ToString("D")),
                ("$account", account.AccountId),
                ("$safe", account.SafeName),
                ("$name", account.Name),
                ("$user", account.UserName ?? (object)DBNull.Value),
                ("$address", account.Address ?? (object)DBNull.Value),
                ("$platform", account.PlatformId ?? (object)DBNull.Value),
                ("$type", (int)account.SecretType),
                ("$status", account.Status ?? (object)DBNull.Value),
                ("$created", account.CreatedAt is null
                    ? DBNull.Value
                    : Format(account.CreatedAt.Value)),
                ("$updated", account.UpdatedAt is null
                    ? DBNull.Value
                    : Format(account.UpdatedAt.Value)),
                ("$fingerprint", account.MetadataFingerprint),
                ("$indexed", Format(account.LastIndexedAt)),
                ("$deleted", account.IsDeletedOrUnavailable ? 1 : 0));
        }

        foreach (var version in snapshot.Versions)
        {
            EnsureCyberArkSource(profileId, version.ProfileId);
            await ExecuteAsync(
                connection,
                transaction,
                """
                INSERT INTO cyberark_versions(
                    profile_id,account_id,version_id,is_temporary,modified_at,modified_by)
                VALUES($profile,$account,$version,$temporary,$modified,$by)
                """,
                cancellationToken,
                ("$profile", profileId.ToString("D")),
                ("$account", version.AccountId),
                ("$version", version.VersionId),
                ("$temporary", version.IsTemporary ? 1 : 0),
                ("$modified", Format(version.ModifiedAt)),
                ("$by", version.ModifiedBy));
        }

        foreach (var permission in snapshot.Permissions)
        {
            EnsureCyberArkSource(profileId, permission.ProfileId);
            await ExecuteAsync(
                connection,
                transaction,
                """
                INSERT INTO cyberark_permissions(
                    profile_id,safe_id,member_name,member_type,list_accounts,use_accounts,
                    retrieve_accounts,view_audit_log,access_without_confirmation,
                    requests_authorization_level1,requests_authorization_level2,observed_at,evidence_state)
                VALUES($profile,$safe,$member,$type,$list,$use,$retrieve,$audit,$without,
                       $level1,$level2,$observed,$evidence)
                """,
                cancellationToken,
                ("$profile", profileId.ToString("D")),
                ("$safe", permission.SafeId),
                ("$member", permission.MemberName),
                ("$type", permission.MemberType),
                ("$list", permission.ListAccounts ? 1 : 0),
                ("$use", permission.UseAccounts ? 1 : 0),
                ("$retrieve", permission.RetrieveAccounts ? 1 : 0),
                ("$audit", permission.ViewAuditLog ? 1 : 0),
                ("$without", permission.AccessWithoutConfirmation ? 1 : 0),
                ("$level1", permission.RequestsAuthorizationLevel1 ? 1 : 0),
                ("$level2", permission.RequestsAuthorizationLevel2 ? 1 : 0),
                ("$observed", Format(permission.ObservedAt)),
                ("$evidence", permission.EvidenceState));
        }

        await ExecuteAsync(
            connection,
            transaction,
            """
            UPDATE cyberark_profiles
            SET last_validated_at=$validated,auth_state=$state
            WHERE id=$profile
            """,
            cancellationToken,
            ("$validated", Format(snapshot.CompletedAt)),
            ("$state", (int)CyberArkAuthenticationState.Ready),
            ("$profile", profileId.ToString("D")));
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<CyberArkSafe>> GetCyberArkSafesAsync(
        Guid profileId,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT profile_id,safe_id,name,description,location,retention_days,
                   retention_versions,olac_enabled,created_at,updated_at
            FROM cyberark_safes
            WHERE profile_id=$profile
            ORDER BY name COLLATE NOCASE,safe_id
            """;
        command.Parameters.AddWithValue("$profile", profileId.ToString("D"));
        var result = new List<CyberArkSafe>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(new CyberArkSafe(
                reader.GetGuid(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetString(4),
                reader.IsDBNull(5) ? null : reader.GetInt32(5),
                reader.IsDBNull(6) ? null : reader.GetInt32(6),
                reader.GetBoolean(7),
                ReadDate(reader, 8),
                ReadDate(reader, 9)));
        }
        return result;
    }

    public async Task<IReadOnlyList<CyberArkAccount>> SearchCyberArkAccountsAsync(
        Guid profileId,
        string searchText,
        int limit,
        CancellationToken cancellationToken)
    {
        if (limit is < 1 or > 500)
            throw new ArgumentOutOfRangeException(nameof(limit));
        if (searchText.Length > 500)
            throw new ArgumentOutOfRangeException(nameof(searchText));
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT profile_id,account_id,safe_name,name,user_name,address,platform_id,
                   secret_type,status,created_at,updated_at,fingerprint,last_indexed,is_deleted
            FROM cyberark_accounts
            WHERE profile_id=$profile AND is_deleted=0
              AND ($search='' OR name LIKE '%'||$search||'%' ESCAPE '\'
                   OR safe_name LIKE '%'||$search||'%' ESCAPE '\'
                   OR COALESCE(user_name,'') LIKE '%'||$search||'%' ESCAPE '\'
                   OR COALESCE(address,'') LIKE '%'||$search||'%' ESCAPE '\')
            ORDER BY name COLLATE NOCASE,safe_name COLLATE NOCASE,account_id
            LIMIT $limit
            """;
        command.Parameters.AddWithValue("$profile", profileId.ToString("D"));
        command.Parameters.AddWithValue("$search", EscapeLike(searchText));
        command.Parameters.AddWithValue("$limit", limit);
        var result = new List<CyberArkAccount>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            result.Add(ReadCyberArkAccount(reader));
        return result;
    }

    public async Task<IReadOnlyList<CyberArkSecretVersion>> GetCyberArkVersionsAsync(
        Guid profileId,
        string accountId,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT profile_id,account_id,version_id,is_temporary,modified_at,modified_by
            FROM cyberark_versions
            WHERE profile_id=$profile AND account_id=$account
            ORDER BY version_id DESC
            """;
        command.Parameters.AddWithValue("$profile", profileId.ToString("D"));
        command.Parameters.AddWithValue("$account", accountId);
        var result = new List<CyberArkSecretVersion>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(new CyberArkSecretVersion(
                reader.GetGuid(0),
                reader.GetString(1),
                reader.GetInt32(2),
                reader.GetBoolean(3),
                DateTimeOffset.Parse(
                    reader.GetString(4),
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind),
                reader.GetString(5)));
        }
        return result;
    }

    public async Task<CyberArkSafePermissionEvidence?> GetCyberArkPermissionAsync(
        Guid profileId,
        string safeId,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT profile_id,safe_id,member_name,member_type,list_accounts,use_accounts,
                   retrieve_accounts,view_audit_log,access_without_confirmation,
                   requests_authorization_level1,requests_authorization_level2,
                   observed_at,evidence_state
            FROM cyberark_permissions
            WHERE profile_id=$profile AND safe_id=$safe
            """;
        command.Parameters.AddWithValue("$profile", profileId.ToString("D"));
        command.Parameters.AddWithValue("$safe", safeId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return null;
        return new CyberArkSafePermissionEvidence(
            reader.GetGuid(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.GetBoolean(4),
            reader.GetBoolean(5),
            reader.GetBoolean(6),
            reader.GetBoolean(7),
            reader.GetBoolean(8),
            reader.GetBoolean(9),
            reader.GetBoolean(10),
            DateTimeOffset.Parse(
                reader.GetString(11),
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind),
            reader.GetString(12));
    }

    public async Task RecordCyberArkAuditAsync(
        CyberArkAuditEvent auditEvent,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var transaction =
            (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
        await ExecuteAsync(
            connection,
            transaction,
            """
            INSERT INTO cyberark_audit(
                id,profile_id,account_id,safe_name,version_id,operation,result,safe_message,occurred_at)
            VALUES($id,$profile,$account,$safe,$version,$operation,$result,$message,$at)
            """,
            cancellationToken,
            ("$id", auditEvent.Id.ToString("D")),
            ("$profile", auditEvent.ProfileId.ToString("D")),
            ("$account", auditEvent.AccountId ?? (object)DBNull.Value),
            ("$safe", auditEvent.SafeName ?? (object)DBNull.Value),
            ("$version", auditEvent.VersionId ?? (object)DBNull.Value),
            ("$operation", auditEvent.Operation),
            ("$result", (int)auditEvent.Result),
            ("$message", auditEvent.SafeMessage),
            ("$at", Format(auditEvent.OccurredAt)));
        await ExecuteAsync(
            connection,
            transaction,
            """
            DELETE FROM cyberark_audit
            WHERE id IN (
                SELECT id FROM cyberark_audit
                ORDER BY occurred_at DESC,id DESC
                LIMIT -1 OFFSET 2000
            )
            """,
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<CyberArkAuditEvent>> GetCyberArkAuditAsync(
        Guid profileId,
        int limit,
        CancellationToken cancellationToken)
    {
        if (limit is < 1 or > 500)
            throw new ArgumentOutOfRangeException(nameof(limit));
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id,profile_id,account_id,safe_name,version_id,operation,result,safe_message,occurred_at
            FROM cyberark_audit
            WHERE profile_id=$profile
            ORDER BY occurred_at DESC,id DESC
            LIMIT $limit
            """;
        command.Parameters.AddWithValue("$profile", profileId.ToString("D"));
        command.Parameters.AddWithValue("$limit", limit);
        var result = new List<CyberArkAuditEvent>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(new CyberArkAuditEvent(
                reader.GetGuid(0),
                reader.GetGuid(1),
                reader.IsDBNull(2) ? null : reader.GetString(2),
                reader.IsDBNull(3) ? null : reader.GetString(3),
                reader.IsDBNull(4) ? null : reader.GetInt32(4),
                reader.GetString(5),
                (CyberArkAuditResult)reader.GetInt32(6),
                reader.GetString(7),
                DateTimeOffset.Parse(
                    reader.GetString(8),
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind)));
        }
        return result;
    }

    private async Task<SqliteConnection> OpenAsync(CancellationToken cancellationToken)
    {
        if (_connectionString is null || _sqlCipherRawKey is null)
            throw new InvalidOperationException("Repository has not been initialized.");
        var connection = new SqliteConnection(_connectionString);
        try
        {
            await connection.OpenAsync(cancellationToken);
            var result = SQLitePCL.raw.sqlite3_key(
                connection.Handle,
                _sqlCipherRawKey);
            if (result != SQLitePCL.raw.SQLITE_OK)
                throw new SqliteException("Encrypted local metadata could not be keyed.", result);
            return connection;
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }

    private static async Task<byte[]> ReadSqlCipherSaltAsync(
        string path,
        CancellationToken cancellationToken)
    {
        var salt = new byte[SqlCipherSaltLength];
        try
        {
            await using var stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read);
            await stream.ReadExactlyAsync(salt, cancellationToken);
            return salt;
        }
        catch
        {
            CryptographicOperations.ZeroMemory(salt);
            throw;
        }
    }

    private static byte[] CreateSqlCipherRawKey(
        ReadOnlySpan<byte> protectedKey,
        ReadOnlySpan<byte> salt)
    {
        if (protectedKey.Length != SqlCipherKeyLength)
            throw new LocalDataIntegrityException(
                "The protected metadata key has an invalid length.");
        if (salt.Length != SqlCipherSaltLength)
            throw new LocalDataIntegrityException(
                "The encrypted metadata salt has an invalid length.");

        // Microsoft.Data.Sqlite historically supplied the random 32-byte key as a Base64
        // password. Derive SQLCipher v4's effective key once, then use SQLCipher's raw-key form
        // for each short-lived connection. This preserves the existing on-disk format without
        // retaining an immutable password string or repeating the 256,000-round KDF per query.
        var passphrase = new byte[Base64.GetMaxEncodedToUtf8Length(
            protectedKey.Length)];
        Span<byte> derivedKey = stackalloc byte[SqlCipherKeyLength];
        try
        {
            var status = Base64.EncodeToUtf8(
                protectedKey,
                passphrase,
                out var consumed,
                out var written);
            if (status != System.Buffers.OperationStatus.Done ||
                consumed != protectedKey.Length)
            {
                throw new LocalDataIntegrityException(
                    "The protected metadata key could not be prepared.");
            }

            Rfc2898DeriveBytes.Pbkdf2(
                passphrase.AsSpan(0, written),
                salt,
                derivedKey,
                SqlCipherKdfIterations,
                HashAlgorithmName.SHA512);

            var rawKey = new byte[
                2 + ((SqlCipherKeyLength + SqlCipherSaltLength) * 2) + 1];
            rawKey[0] = (byte)'x';
            rawKey[1] = (byte)'\'';
            WriteHex(derivedKey, rawKey.AsSpan(2));
            WriteHex(
                salt,
                rawKey.AsSpan(2 + (SqlCipherKeyLength * 2)));
            rawKey[^1] = (byte)'\'';
            return rawKey;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(passphrase);
            CryptographicOperations.ZeroMemory(derivedKey);
        }
    }

    private static void WriteHex(
        ReadOnlySpan<byte> source,
        Span<byte> destination)
    {
        const string hex = "0123456789abcdef";
        for (var index = 0; index < source.Length; index++)
        {
            destination[index * 2] = (byte)hex[source[index] >> 4];
            destination[(index * 2) + 1] =
                (byte)hex[source[index] & 0x0f];
        }
    }

    private async Task<(SqliteConnection Connection, int SchemaVersion)> OpenValidatedAsync(CancellationToken cancellationToken)
    {
        SqliteConnection? connection = null;
        try
        {
            connection = await OpenAsync(cancellationToken);
            var schemaVersion = await GetSchemaVersionAsync(connection, cancellationToken);
            if (schemaVersion > CurrentSchemaVersion)
                throw new IncompatibleLocalDataVersionException(schemaVersion, CurrentSchemaVersion);

            await VerifyDatabaseIntegrityAsync(connection, cancellationToken);
            return (connection, schemaVersion);
        }
        catch (IncompatibleLocalDataVersionException)
        {
            if (connection is not null) await connection.DisposeAsync();
            throw;
        }
        catch (LocalDataIntegrityException)
        {
            if (connection is not null) await connection.DisposeAsync();
            throw;
        }
        catch (SqliteException ex)
        {
            if (connection is not null) await connection.DisposeAsync();
            throw new LocalDataIntegrityException("Encrypted local metadata could not be opened or verified.", ex);
        }
    }

    private static async Task<int> GetSchemaVersionAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA user_version";
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken), CultureInfo.InvariantCulture);
    }

    private static async Task VerifyDatabaseIntegrityAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA quick_check";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var sawResult = false;
        while (await reader.ReadAsync(cancellationToken))
        {
            sawResult = true;
            if (!string.Equals(reader.GetString(0), "ok", StringComparison.OrdinalIgnoreCase))
                throw new LocalDataIntegrityException("Encrypted local metadata failed its integrity check.");
        }

        if (!sawResult) throw new LocalDataIntegrityException("Encrypted local metadata integrity could not be determined.");
    }

    private static async Task ValidateSchemaAsync(SqliteConnection connection, SqliteTransaction transaction, CancellationToken cancellationToken)
    {
        Dictionary<string, string[]> requiredSchema = new(StringComparer.Ordinal)
        {
            ["identities"] = ["id", "client_id", "account_identifier", "username_hint", "display_name", "home_tenant_id", "auth_state", "last_interactive", "is_enabled", "identity_type", "credential_data"],
            ["tenants"] = ["id", "identity_id", "tenant_id", "display_name", "tenant_type", "last_validated", "status", "is_selected"],
            ["subscriptions"] = ["id", "tenant_access_id", "subscription_id", "display_name", "state", "is_selected", "last_discovered"],
            ["vaults"] = ["id", "resource_id", "name", "tenant_id", "subscription_id", "resource_group", "location", "tags", "vault_uri", "last_indexed"],
            ["vault_access"] = ["id", "vault_id", "identity_id", "tenant_id", "status", "last_validated", "failure_category", "preferred_rank", "is_selected"],
            ["items"] = ["id", "vault_id", "name", "object_type", "enabled", "tags", "content_type", "created_at", "updated_at", "expires_at", "provider_version", "fingerprint", "last_indexed", "is_deleted"],
            ["favorites"] = ["item_id"],
            ["access_history"] = ["item_id", "last_accessed"],
            ["workspaces"] = ["id", "name", "description", "sort_order", "cache_enabled", "cache_lifetime_minutes", "require_unlock", "allow_clipboard"],
            ["workspace_links"] = ["id", "workspace_id", "resource_type", "resource_id"],
            ["sync_runs"] = ["id", "scope", "started_at", "completed_at", "status", "vault_count", "item_count", "error_count"],
            ["browser_fill_mappings"] = ["id", "item_id", "identity_id", "top_origin", "frame_origin", "field_purpose", "is_enabled", "created_at", "updated_at"],
            ["browser_fill_audit"] = ["id", "occurred_at", "mapping_id", "item_id", "identity_id", "top_origin", "frame_origin", "field_purpose", "result"],
            ["cyberark_profiles"] = ["id", "display_name", "identity_url", "privilege_cloud_url", "service_user_name", "application_name", "auth_state", "is_enabled", "credential_updated_at", "last_validated_at"],
            ["cyberark_safes"] = ["profile_id", "safe_id", "name", "description", "location", "retention_days", "retention_versions", "olac_enabled", "created_at", "updated_at"],
            ["cyberark_accounts"] = ["profile_id", "account_id", "safe_name", "name", "user_name", "address", "platform_id", "secret_type", "status", "created_at", "updated_at", "fingerprint", "last_indexed", "is_deleted"],
            ["cyberark_versions"] = ["profile_id", "account_id", "version_id", "is_temporary", "modified_at", "modified_by"],
            ["cyberark_permissions"] = ["profile_id", "safe_id", "member_name", "member_type", "list_accounts", "use_accounts", "retrieve_accounts", "view_audit_log", "access_without_confirmation", "requests_authorization_level1", "requests_authorization_level2", "observed_at", "evidence_state"],
            ["cyberark_audit"] = ["id", "profile_id", "account_id", "safe_name", "version_id", "operation", "result", "safe_message", "occurred_at"],
            ["governed_mutation_audit"] = ["sequence", "id", "preview_id", "occurred_at", "operation", "identity_id", "tenant_id", "subscription_id", "vault_resource_id", "object_name_hash", "sensitive_value_length", "result", "provider_version", "safe_message", "previous_hash", "record_hash"],
        };
        var actualTables = new HashSet<string>(StringComparer.Ordinal);
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = "SELECT name FROM sqlite_schema WHERE type='table'";
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken)) actualTables.Add(reader.GetString(0));
        }

        foreach (var (table, requiredColumns) in requiredSchema)
        {
            if (!actualTables.Contains(table))
                throw new LocalDataIntegrityException("Encrypted local metadata schema is incomplete or incompatible.");

            var actualColumns = new HashSet<string>(StringComparer.Ordinal);
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "SELECT name FROM pragma_table_info($table)";
            command.Parameters.AddWithValue("$table", table);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken)) actualColumns.Add(reader.GetString(0));
            if (requiredColumns.Any(column => !actualColumns.Contains(column)))
                throw new LocalDataIntegrityException("Encrypted local metadata schema is incomplete or incompatible.");
        }
    }

    private static async Task ValidateForeignKeysAsync(SqliteConnection connection, SqliteTransaction transaction, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "PRAGMA foreign_key_check";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (await reader.ReadAsync(cancellationToken))
            throw new LocalDataIntegrityException("Encrypted local metadata contains invalid resource relationships.");
    }

    private static async Task ValidateGovernedMutationAuditChainAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            SELECT sequence,id,preview_id,occurred_at,operation,identity_id,
                   tenant_id,subscription_id,vault_resource_id,object_name_hash,
                   sensitive_value_length,result,provider_version,safe_message,
                   previous_hash,record_hash
            FROM governed_mutation_audit
            ORDER BY sequence
            """;
        await using var reader =
            await command.ExecuteReaderAsync(cancellationToken);
        var expectedSequence = 1L;
        var previousHash = string.Empty;
        while (await reader.ReadAsync(cancellationToken))
        {
            var sequence = reader.GetInt64(0);
            var observedPreviousHash = reader.GetString(14);
            if (sequence != expectedSequence ||
                !string.Equals(
                    previousHash,
                    observedPreviousHash,
                    StringComparison.Ordinal))
            {
                throw new LocalDataIntegrityException(
                    "The governed-mutation audit sequence or hash chain is invalid.");
            }

            var canonical = string.Join(
                "|",
                reader.GetString(1),
                reader.GetString(2),
                DateTimeOffset.Parse(
                        reader.GetString(3),
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.RoundtripKind)
                    .ToUniversalTime()
                    .ToString("O"),
                reader.GetInt32(4),
                reader.GetString(5),
                reader.GetString(6),
                reader.GetString(7),
                reader.GetString(8),
                reader.GetString(9),
                reader.GetInt32(10),
                reader.GetInt32(11),
                reader.GetString(12),
                reader.GetString(13),
                observedPreviousHash);
            var computed = Convert.ToHexString(
                SHA256.HashData(
                    Encoding.UTF8.GetBytes(canonical)));
            var observed = reader.GetString(15);
            if (!string.Equals(
                    computed,
                    observed,
                    StringComparison.Ordinal))
            {
                throw new LocalDataIntegrityException(
                    "The governed-mutation audit integrity chain failed validation.");
            }
            previousHash = observed;
            expectedSequence++;
        }
    }

    private static async Task ExecuteAsync(SqliteConnection connection, SqliteTransaction? transaction, string sql, CancellationToken cancellationToken, params (string Name, object Value)[] values)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        foreach (var value in values) command.Parameters.AddWithValue(value.Name, value.Value);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static Task UpsertTenantAsync(SqliteConnection c, SqliteTransaction t, TenantAccess x, CancellationToken ct) => ExecuteAsync(c, t, "INSERT INTO tenants(id,identity_id,tenant_id,display_name,tenant_type,last_validated,status,is_selected) VALUES($id,$identity,$tenant,$name,$type,$last,$status,$selected) ON CONFLICT(id) DO UPDATE SET display_name=excluded.display_name,tenant_type=excluded.tenant_type,last_validated=excluded.last_validated,status=excluded.status", ct, ("$id", x.Id.ToString("D")), ("$identity", x.ConnectedIdentityId.ToString("D")), ("$tenant", x.TenantId), ("$name", x.DisplayName), ("$type", x.TenantType), ("$last", Format(x.LastValidatedAt)), ("$status", x.Status), ("$selected", x.IsSelected ? 1 : 0));
    private static Task UpsertSubscriptionAsync(SqliteConnection c, SqliteTransaction t, SubscriptionAccess x, CancellationToken ct) => ExecuteAsync(c, t, "INSERT INTO subscriptions(id,tenant_access_id,subscription_id,display_name,state,is_selected,last_discovered) VALUES($id,$tenant,$subscription,$name,$state,$selected,$last) ON CONFLICT(id) DO UPDATE SET display_name=excluded.display_name,state=excluded.state,last_discovered=excluded.last_discovered", ct, ("$id", x.Id.ToString("D")), ("$tenant", x.TenantAccessId.ToString("D")), ("$subscription", x.SubscriptionId), ("$name", x.DisplayName), ("$state", x.State), ("$selected", x.IsSelected ? 1 : 0), ("$last", Format(x.LastDiscoveredAt)));
    private static Task UpsertVaultAsync(SqliteConnection c, SqliteTransaction t, VaultResource x, CancellationToken ct) => ExecuteAsync(c, t, "INSERT INTO vaults(id,resource_id,name,tenant_id,subscription_id,resource_group,location,tags,vault_uri,last_indexed) VALUES($id,$resource,$name,$tenant,$subscription,$group,$location,$tags,$uri,$last) ON CONFLICT(id) DO UPDATE SET name=excluded.name,tags=excluded.tags,vault_uri=excluded.vault_uri,last_indexed=excluded.last_indexed", ct, ("$id", x.Id.ToString("D")), ("$resource", x.ProviderResourceId), ("$name", x.Name), ("$tenant", x.TenantId), ("$subscription", x.SubscriptionId), ("$group", x.ResourceGroup), ("$location", x.Location), ("$tags", JsonSerializer.Serialize(x.Tags, InfrastructureJsonContext.Default.DictionaryStringString)), ("$uri", x.VaultUri.ToString()), ("$last", Format(x.LastIndexedAt)));
    private static Task UpsertAccessAsync(SqliteConnection c, SqliteTransaction t, VaultAccess x, CancellationToken ct) => ExecuteAsync(c, t, "INSERT INTO vault_access(id,vault_id,identity_id,tenant_id,status,last_validated,failure_category,preferred_rank,is_selected) VALUES($id,$vault,$identity,$tenant,$status,$last,$failure,$rank,$selected) ON CONFLICT(id) DO UPDATE SET status=excluded.status,last_validated=excluded.last_validated,failure_category=excluded.failure_category,preferred_rank=excluded.preferred_rank", ct, ("$id", x.Id.ToString("D")), ("$vault", x.VaultId.ToString("D")), ("$identity", x.ConnectedIdentityId.ToString("D")), ("$tenant", x.TenantId), ("$status", x.AccessStatus), ("$last", Format(x.LastValidatedAt)), ("$failure", x.LastFailureCategory ?? (object)DBNull.Value), ("$rank", x.PreferredRank), ("$selected", x.IsSelected ? 1 : 0));
    private static async Task UpsertItemsAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        IReadOnlyList<VaultItem> items,
        CancellationToken cancellationToken)
    {
        if (items.Count == 0)
            return;

        const int batchSize = 50;
        var fullBatchCount = Math.Min(batchSize, items.Count);
        await using var fullBatch = CreateItemBatchCommand(
            connection,
            transaction,
            fullBatchCount);
        await fullBatch.PrepareAsync(cancellationToken);

        for (var offset = 0; offset < items.Count; offset += batchSize)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var count = Math.Min(batchSize, items.Count - offset);
            if (count == fullBatchCount)
            {
                BindItemBatch(fullBatch, items, offset, count);
                await fullBatch.ExecuteNonQueryAsync(cancellationToken);
                continue;
            }

            await using var finalBatch = CreateItemBatchCommand(
                connection,
                transaction,
                count);
            BindItemBatch(finalBatch, items, offset, count);
            await finalBatch.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private static SqliteCommand CreateItemBatchCommand(
        SqliteConnection connection,
        SqliteTransaction transaction,
        int rowCount)
    {
        var command = connection.CreateCommand();
        command.Transaction = transaction;
        var sql = new StringBuilder(
            """
            INSERT INTO items(
                id,vault_id,name,object_type,enabled,tags,content_type,created_at,updated_at,
                expires_at,provider_version,fingerprint,last_indexed,is_deleted)
            VALUES
            """);
        for (var row = 0; row < rowCount; row++)
        {
            if (row > 0)
                sql.Append(',');
            sql.Append(
                CultureInfo.InvariantCulture,
                $"""
                 ($id{row},$vault{row},$name{row},$type{row},$enabled{row},$tags{row},
                 $content{row},$created{row},$updated{row},$expires{row},$version{row},
                 $fingerprint{row},$last{row},$deleted{row})
                 """);
            command.Parameters.Add($"$id{row}", SqliteType.Text);
            command.Parameters.Add($"$vault{row}", SqliteType.Text);
            command.Parameters.Add($"$name{row}", SqliteType.Text);
            command.Parameters.Add($"$type{row}", SqliteType.Integer);
            command.Parameters.Add($"$enabled{row}", SqliteType.Integer);
            command.Parameters.Add($"$tags{row}", SqliteType.Text);
            command.Parameters.Add($"$content{row}", SqliteType.Text);
            command.Parameters.Add($"$created{row}", SqliteType.Text);
            command.Parameters.Add($"$updated{row}", SqliteType.Text);
            command.Parameters.Add($"$expires{row}", SqliteType.Text);
            command.Parameters.Add($"$version{row}", SqliteType.Text);
            command.Parameters.Add($"$fingerprint{row}", SqliteType.Text);
            command.Parameters.Add($"$last{row}", SqliteType.Text);
            command.Parameters.Add($"$deleted{row}", SqliteType.Integer);
        }

        sql.Append(
            """
            ON CONFLICT(id) DO UPDATE SET
                enabled=excluded.enabled,
                tags=excluded.tags,
                content_type=excluded.content_type,
                updated_at=excluded.updated_at,
                expires_at=excluded.expires_at,
                provider_version=excluded.provider_version,
                fingerprint=excluded.fingerprint,
                last_indexed=excluded.last_indexed,
                is_deleted=excluded.is_deleted
            """);
        command.CommandText = sql.ToString();
        return command;
    }

    private static void BindItemBatch(
        SqliteCommand command,
        IReadOnlyList<VaultItem> items,
        int offset,
        int count)
    {
        const int parametersPerRow = 14;
        for (var row = 0; row < count; row++)
        {
            var item = items[offset + row];
            var parameter = row * parametersPerRow;
            command.Parameters[parameter++].Value = item.Id.ToString("D");
            command.Parameters[parameter++].Value = item.VaultId.ToString("D");
            command.Parameters[parameter++].Value = item.ProviderObjectName;
            command.Parameters[parameter++].Value = (int)item.ObjectType;
            command.Parameters[parameter++].Value = item.Enabled ? 1 : 0;
            command.Parameters[parameter++].Value = JsonSerializer.Serialize(
                item.Tags,
                InfrastructureJsonContext.Default.DictionaryStringString);
            command.Parameters[parameter++].Value =
                item.ContentType ?? (object)DBNull.Value;
            command.Parameters[parameter++].Value = item.CreatedAt is null
                ? DBNull.Value
                : Format(item.CreatedAt.Value);
            command.Parameters[parameter++].Value = item.UpdatedAt is null
                ? DBNull.Value
                : Format(item.UpdatedAt.Value);
            command.Parameters[parameter++].Value = item.ExpiresAt is null
                ? DBNull.Value
                : Format(item.ExpiresAt.Value);
            command.Parameters[parameter++].Value = item.ProviderVersion;
            command.Parameters[parameter++].Value = item.MetadataFingerprint;
            command.Parameters[parameter++].Value = Format(item.LastIndexedAt);
            command.Parameters[parameter].Value =
                item.IsDeletedOrUnavailable ? 1 : 0;
        }
    }

    private static void AddNullable(SqliteCommand command, string name, object? value) => command.Parameters.AddWithValue(name, value ?? DBNull.Value);
    private static async Task<bool> HasClientIdColumnAsync(SqliteConnection connection, SqliteTransaction transaction, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT COUNT(*) FROM pragma_table_info('identities') WHERE name='client_id'";
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken), CultureInfo.InvariantCulture) > 0;
    }
    private static async Task EnsureClientIdColumnAsync(SqliteConnection connection, SqliteTransaction transaction, CancellationToken cancellationToken)
    {
        if (!await HasClientIdColumnAsync(connection, transaction, cancellationToken))
            await ExecuteAsync(connection, transaction, "ALTER TABLE identities ADD COLUMN client_id TEXT NOT NULL DEFAULT ''", cancellationToken);
    }
    private static async Task EnsureIdentityProfilesColumnsAsync(SqliteConnection connection, SqliteTransaction transaction, CancellationToken cancellationToken)
    {
        await ExecuteAsync(connection, transaction, "ALTER TABLE identities ADD COLUMN identity_type INTEGER NOT NULL DEFAULT 0", cancellationToken);
        await ExecuteAsync(connection, transaction, "ALTER TABLE identities ADD COLUMN credential_data TEXT NOT NULL DEFAULT ''", cancellationToken);
    }
    private static async Task EnsureVaultSelectionColumnAsync(SqliteConnection connection, SqliteTransaction transaction, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT COUNT(*) FROM pragma_table_info('vault_access') WHERE name='is_selected'";
        var exists = Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken), CultureInfo.InvariantCulture) > 0;
        if (!exists)
            await ExecuteAsync(connection, transaction, "ALTER TABLE vault_access ADD COLUMN is_selected INTEGER NOT NULL DEFAULT 1", cancellationToken);
    }
    private static async Task EnsureTenantSelectionColumnAsync(SqliteConnection connection, SqliteTransaction transaction, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT COUNT(*) FROM pragma_table_info('tenants') WHERE name='is_selected'";
        var exists = Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken), CultureInfo.InvariantCulture) > 0;
        if (!exists)
            await ExecuteAsync(connection, transaction, "ALTER TABLE tenants ADD COLUMN is_selected INTEGER NOT NULL DEFAULT 1", cancellationToken);
    }

    private static Task EnsureBrowserIntegrationTablesAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        CancellationToken cancellationToken) =>
        ExecuteAsync(
            connection,
            transaction,
            BrowserIntegrationSchema,
            cancellationToken);

    private static Task EnsureCyberArkTablesAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        CancellationToken cancellationToken) =>
        ExecuteAsync(
            connection,
            transaction,
            CyberArkSchema,
            cancellationToken);

    private static Task EnsureGovernedMutationAuditTableAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        CancellationToken cancellationToken) =>
        ExecuteAsync(
            connection,
            transaction,
            GovernedMutationAuditSchema,
            cancellationToken);

    private static string Format(DateTimeOffset value) => value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
    private static DateTimeOffset? ReadDate(SqliteDataReader reader, int ordinal) => reader.IsDBNull(ordinal) ? null : DateTimeOffset.Parse(reader.GetString(ordinal), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
    private static string EscapeLike(string value) => value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("%", "\\%", StringComparison.Ordinal).Replace("_", "\\_", StringComparison.Ordinal);
    private static Dictionary<string, string> ReadTags(SqliteDataReader reader, int ordinal) => JsonSerializer.Deserialize(reader.GetString(ordinal), InfrastructureJsonContext.Default.DictionaryStringString) ?? [];
    private static ConnectedIdentity ReadIdentity(SqliteDataReader r, int o = 0) => new(r.GetGuid(o), r.GetString(o + 1), r.GetString(o + 2), r.GetString(o + 3), r.GetString(o + 4), r.GetString(o + 5), (AuthenticationState)r.GetInt32(o + 6), DateTimeOffset.Parse(r.GetString(o + 7), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind), r.GetBoolean(o + 8), (IdentityType)r.GetInt32(o + 9), r.GetString(o + 10));
    private static VaultItem ReadItem(SqliteDataReader r, int o) => new(r.GetGuid(o), r.GetGuid(o + 1), r.GetString(o + 2), (VaultObjectType)r.GetInt32(o + 3), r.GetBoolean(o + 4), ReadTags(r, o + 5), r.IsDBNull(o + 6) ? null : r.GetString(o + 6), ReadDate(r, o + 7), ReadDate(r, o + 8), ReadDate(r, o + 9), r.GetString(o + 10), r.GetString(o + 11), DateTimeOffset.Parse(r.GetString(o + 12), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind), r.GetBoolean(o + 13));
    private static VaultResource ReadVault(SqliteDataReader r, int o, Guid id) => new(id, r.GetString(o), r.GetString(o + 1), r.GetString(o + 2), r.GetString(o + 3), r.GetString(o + 4), r.GetString(o + 5), ReadTags(r, o + 6), new Uri(r.GetString(o + 7)), DateTimeOffset.Parse(r.GetString(o + 8), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind));
    private static BrowserFillMapping ReadBrowserMapping(SqliteDataReader r) => new(
        r.GetGuid(0),
        r.GetGuid(1),
        r.GetGuid(2),
        r.GetString(3),
        r.GetString(4),
        (BrowserMappingFieldPurpose)r.GetInt32(5),
        r.GetBoolean(6),
        DateTimeOffset.Parse(r.GetString(7), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
        DateTimeOffset.Parse(r.GetString(8), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind));
    private static CyberArkProfile ReadCyberArkProfile(SqliteDataReader r) => new(
        r.GetGuid(0),
        r.GetString(1),
        new Uri(r.GetString(2)),
        new Uri(r.GetString(3)),
        r.GetString(4),
        r.GetString(5),
        (CyberArkAuthenticationState)r.GetInt32(6),
        r.GetBoolean(7),
        DateTimeOffset.Parse(
            r.GetString(8),
            CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind),
        ReadDate(r, 9));
    private static CyberArkAccount ReadCyberArkAccount(SqliteDataReader r) => new(
        r.GetGuid(0),
        r.GetString(1),
        r.GetString(2),
        r.GetString(3),
        r.IsDBNull(4) ? null : r.GetString(4),
        r.IsDBNull(5) ? null : r.GetString(5),
        r.IsDBNull(6) ? null : r.GetString(6),
        (CyberArkSecretType)r.GetInt32(7),
        r.IsDBNull(8) ? null : r.GetString(8),
        ReadDate(r, 9),
        ReadDate(r, 10),
        r.GetString(11),
        DateTimeOffset.Parse(
            r.GetString(12),
            CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind),
        r.GetBoolean(13));

    private static void EnsureCyberArkSource(Guid expected, Guid actual)
    {
        if (expected != actual)
            throw new InvalidOperationException(
                "CyberArk discovery data crossed provider profile boundaries.");
    }

    private const string IdentitySelect = "SELECT id,client_id,account_identifier,username_hint,display_name,home_tenant_id,auth_state,last_interactive,is_enabled,identity_type,credential_data FROM identities";
    private const string BrowserMappingSelect = "SELECT id,item_id,identity_id,top_origin,frame_origin,field_purpose,is_enabled,created_at,updated_at FROM browser_fill_mappings";
    private const string CyberArkProfileSelect = """
        SELECT id,display_name,identity_url,privilege_cloud_url,service_user_name,
               application_name,auth_state,is_enabled,credential_updated_at,last_validated_at
        FROM cyberark_profiles
        """;
    private const string SearchSql = """
        WITH ranked_access AS (
            SELECT
                va.vault_id,
                va.identity_id,
                va.status,
                ident.display_name,
                ROW_NUMBER() OVER (
                    PARTITION BY va.vault_id
                    ORDER BY va.preferred_rank,va.id
                ) AS access_rank
            FROM vault_access va
            JOIN identities ident ON ident.id=va.identity_id
            WHERE va.status!='Removed'
              AND ident.is_enabled=1
              AND ($identity IS NULL OR ident.id=$identity)
        )
        SELECT i.id,i.vault_id,i.name,i.object_type,i.enabled,i.tags,i.content_type,i.created_at,i.updated_at,i.expires_at,i.provider_version,i.fingerprint,i.last_indexed,i.is_deleted,
               v.resource_id,v.name,v.tenant_id,v.subscription_id,v.resource_group,v.location,v.tags,v.vault_uri,v.last_indexed,
               ra.display_name,COALESCE(t.display_name,v.tenant_id),EXISTS(SELECT 1 FROM favorites f WHERE f.item_id=i.id),a.last_accessed,
               CASE WHEN julianday($now)-julianday(i.last_indexed)>1 THEN 1 ELSE 0 END,ra.status
        FROM items i
        JOIN vaults v ON v.id=i.vault_id
        JOIN ranked_access ra ON ra.vault_id=v.id AND ra.access_rank=1
        LEFT JOIN tenants t ON t.identity_id=ra.identity_id AND t.tenant_id=v.tenant_id
        LEFT JOIN access_history a ON a.item_id=i.id
        WHERE i.is_deleted=0 AND ($text='' OR i.name LIKE '%'||$text||'%' ESCAPE '\' OR i.tags LIKE '%'||$text||'%' ESCAPE '\')
          AND ($tenant IS NULL OR v.tenant_id LIKE '%'||$tenant||'%' ESCAPE '\') AND ($subscription IS NULL OR v.subscription_id LIKE '%'||$subscription||'%' ESCAPE '\')
          AND ($vault IS NULL OR v.id=$vault) AND ($vault_name IS NULL OR v.name LIKE '%'||$vault_name||'%' ESCAPE '\') AND ($type IS NULL OR i.object_type=$type) AND ($enabled IS NULL OR i.enabled=$enabled)
          AND ($favorites=0 OR EXISTS(SELECT 1 FROM favorites f WHERE f.item_id=i.id)) AND ($expired=0 OR (i.expires_at IS NOT NULL AND i.expires_at<$now))
          AND ($stale=0 OR julianday($now)-julianday(i.last_indexed)>1)
          AND ($workspace IS NULL OR EXISTS(SELECT 1 FROM workspace_links wl WHERE wl.workspace_id=$workspace AND ((wl.resource_type=0 AND wl.resource_id=ra.identity_id) OR (wl.resource_type=1 AND wl.resource_id=v.tenant_id) OR (wl.resource_type=2 AND wl.resource_id=v.subscription_id) OR (wl.resource_type=3 AND wl.resource_id=v.id))))
        ORDER BY CASE WHEN $recent_first=1 AND a.last_accessed IS NULL THEN 1 ELSE 0 END,CASE WHEN $recent_first=1 THEN a.last_accessed END DESC,i.name COLLATE NOCASE,v.name COLLATE NOCASE,i.provider_version DESC LIMIT $limit
        """;
    private const string ResolveSql = """
        SELECT i.id,i.vault_id,i.name,i.object_type,i.enabled,i.tags,i.content_type,i.created_at,i.updated_at,i.expires_at,i.provider_version,i.fingerprint,i.last_indexed,i.is_deleted,
               v.resource_id,v.name,v.tenant_id,v.subscription_id,v.resource_group,v.location,v.tags,v.vault_uri,v.last_indexed,
               ident.id,ident.client_id,ident.account_identifier,ident.username_hint,ident.display_name,ident.home_tenant_id,ident.auth_state,ident.last_interactive,ident.is_enabled,ident.identity_type,ident.credential_data
        FROM items i JOIN vaults v ON v.id=i.vault_id JOIN vault_access va ON va.vault_id=v.id AND va.status!='Removed' JOIN identities ident ON ident.id=va.identity_id
        WHERE i.id=$id AND ident.is_enabled=1 ORDER BY va.preferred_rank LIMIT 1
        """;
    private const string Configuration = "PRAGMA foreign_keys=ON; PRAGMA journal_mode=WAL; PRAGMA secure_delete=ON;";
    private const string BrowserIntegrationSchema = """
        CREATE TABLE IF NOT EXISTS browser_fill_mappings(
            id TEXT PRIMARY KEY,
            item_id TEXT NOT NULL REFERENCES items(id) ON DELETE CASCADE,
            identity_id TEXT NOT NULL REFERENCES identities(id) ON DELETE CASCADE,
            top_origin TEXT NOT NULL,
            frame_origin TEXT NOT NULL,
            field_purpose INTEGER NOT NULL,
            is_enabled INTEGER NOT NULL,
            created_at TEXT NOT NULL,
            updated_at TEXT NOT NULL,
            UNIQUE(top_origin,frame_origin,field_purpose));
        CREATE INDEX IF NOT EXISTS ix_browser_fill_mappings_item
            ON browser_fill_mappings(item_id,identity_id);
        CREATE TABLE IF NOT EXISTS browser_fill_audit(
            id TEXT PRIMARY KEY,
            occurred_at TEXT NOT NULL,
            mapping_id TEXT,
            item_id TEXT,
            identity_id TEXT,
            top_origin TEXT NOT NULL,
            frame_origin TEXT NOT NULL,
            field_purpose INTEGER NOT NULL,
            result TEXT NOT NULL);
        CREATE INDEX IF NOT EXISTS ix_browser_fill_audit_time
            ON browser_fill_audit(occurred_at DESC);
        """;
    private const string CyberArkSchema = """
        CREATE TABLE IF NOT EXISTS cyberark_profiles(
            id TEXT PRIMARY KEY,
            display_name TEXT NOT NULL,
            identity_url TEXT NOT NULL,
            privilege_cloud_url TEXT NOT NULL,
            service_user_name TEXT NOT NULL,
            application_name TEXT NOT NULL,
            auth_state INTEGER NOT NULL,
            is_enabled INTEGER NOT NULL,
            credential_updated_at TEXT NOT NULL,
            last_validated_at TEXT);
        CREATE UNIQUE INDEX IF NOT EXISTS ix_cyberark_profiles_endpoint_user
            ON cyberark_profiles(privilege_cloud_url,service_user_name,application_name);
        CREATE TABLE IF NOT EXISTS cyberark_safes(
            profile_id TEXT NOT NULL REFERENCES cyberark_profiles(id) ON DELETE CASCADE,
            safe_id TEXT NOT NULL,
            name TEXT NOT NULL,
            description TEXT NOT NULL,
            location TEXT NOT NULL,
            retention_days INTEGER,
            retention_versions INTEGER,
            olac_enabled INTEGER NOT NULL,
            created_at TEXT,
            updated_at TEXT,
            PRIMARY KEY(profile_id,safe_id));
        CREATE INDEX IF NOT EXISTS ix_cyberark_safes_name
            ON cyberark_safes(profile_id,name COLLATE NOCASE);
        CREATE TABLE IF NOT EXISTS cyberark_accounts(
            profile_id TEXT NOT NULL REFERENCES cyberark_profiles(id) ON DELETE CASCADE,
            account_id TEXT NOT NULL,
            safe_name TEXT NOT NULL,
            name TEXT NOT NULL,
            user_name TEXT,
            address TEXT,
            platform_id TEXT,
            secret_type INTEGER NOT NULL,
            status TEXT,
            created_at TEXT,
            updated_at TEXT,
            fingerprint TEXT NOT NULL,
            last_indexed TEXT NOT NULL,
            is_deleted INTEGER NOT NULL,
            PRIMARY KEY(profile_id,account_id));
        CREATE INDEX IF NOT EXISTS ix_cyberark_accounts_search
            ON cyberark_accounts(profile_id,name COLLATE NOCASE,safe_name COLLATE NOCASE,is_deleted);
        CREATE TABLE IF NOT EXISTS cyberark_versions(
            profile_id TEXT NOT NULL,
            account_id TEXT NOT NULL,
            version_id INTEGER NOT NULL,
            is_temporary INTEGER NOT NULL,
            modified_at TEXT NOT NULL,
            modified_by TEXT NOT NULL,
            PRIMARY KEY(profile_id,account_id,version_id),
            FOREIGN KEY(profile_id,account_id)
                REFERENCES cyberark_accounts(profile_id,account_id) ON DELETE CASCADE);
        CREATE TABLE IF NOT EXISTS cyberark_permissions(
            profile_id TEXT NOT NULL,
            safe_id TEXT NOT NULL,
            member_name TEXT NOT NULL,
            member_type TEXT NOT NULL,
            list_accounts INTEGER NOT NULL,
            use_accounts INTEGER NOT NULL,
            retrieve_accounts INTEGER NOT NULL,
            view_audit_log INTEGER NOT NULL,
            access_without_confirmation INTEGER NOT NULL,
            requests_authorization_level1 INTEGER NOT NULL,
            requests_authorization_level2 INTEGER NOT NULL,
            observed_at TEXT NOT NULL,
            evidence_state TEXT NOT NULL,
            PRIMARY KEY(profile_id,safe_id),
            FOREIGN KEY(profile_id,safe_id)
                REFERENCES cyberark_safes(profile_id,safe_id) ON DELETE CASCADE);
        CREATE TABLE IF NOT EXISTS cyberark_audit(
            id TEXT PRIMARY KEY,
            profile_id TEXT NOT NULL,
            account_id TEXT,
            safe_name TEXT,
            version_id INTEGER,
            operation TEXT NOT NULL,
            result INTEGER NOT NULL,
            safe_message TEXT NOT NULL,
            occurred_at TEXT NOT NULL);
        CREATE INDEX IF NOT EXISTS ix_cyberark_audit_profile_time
            ON cyberark_audit(profile_id,occurred_at DESC);
        """;
    private const string GovernedMutationAuditSchema = """
        CREATE TABLE IF NOT EXISTS governed_mutation_audit(
            sequence INTEGER PRIMARY KEY AUTOINCREMENT,
            id TEXT NOT NULL UNIQUE,
            preview_id TEXT NOT NULL,
            occurred_at TEXT NOT NULL,
            operation INTEGER NOT NULL,
            identity_id TEXT NOT NULL,
            tenant_id TEXT NOT NULL,
            subscription_id TEXT NOT NULL,
            vault_resource_id TEXT NOT NULL,
            object_name_hash TEXT NOT NULL,
            sensitive_value_length INTEGER NOT NULL,
            result INTEGER NOT NULL,
            provider_version TEXT NOT NULL,
            safe_message TEXT NOT NULL,
            previous_hash TEXT NOT NULL,
            record_hash TEXT NOT NULL UNIQUE);
        CREATE INDEX IF NOT EXISTS ix_governed_mutation_audit_time
            ON governed_mutation_audit(occurred_at DESC);
        """;
    private const string Schema = """
        CREATE TABLE IF NOT EXISTS identities(id TEXT PRIMARY KEY,client_id TEXT NOT NULL,account_identifier TEXT NOT NULL UNIQUE,username_hint TEXT NOT NULL,display_name TEXT NOT NULL,home_tenant_id TEXT NOT NULL,auth_state INTEGER NOT NULL,last_interactive TEXT NOT NULL,is_enabled INTEGER NOT NULL,identity_type INTEGER NOT NULL DEFAULT 0,credential_data TEXT NOT NULL DEFAULT '');
        CREATE TABLE IF NOT EXISTS tenants(id TEXT PRIMARY KEY,identity_id TEXT NOT NULL REFERENCES identities(id) ON DELETE CASCADE,tenant_id TEXT NOT NULL,display_name TEXT NOT NULL,tenant_type TEXT NOT NULL,last_validated TEXT NOT NULL,status TEXT NOT NULL,is_selected INTEGER NOT NULL DEFAULT 1,UNIQUE(identity_id,tenant_id));
        CREATE TABLE IF NOT EXISTS subscriptions(id TEXT PRIMARY KEY,tenant_access_id TEXT NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,subscription_id TEXT NOT NULL,display_name TEXT NOT NULL,state TEXT NOT NULL,is_selected INTEGER NOT NULL,last_discovered TEXT NOT NULL,UNIQUE(tenant_access_id,subscription_id));
        CREATE TABLE IF NOT EXISTS vaults(id TEXT PRIMARY KEY,resource_id TEXT NOT NULL UNIQUE,name TEXT NOT NULL,tenant_id TEXT NOT NULL,subscription_id TEXT NOT NULL,resource_group TEXT NOT NULL,location TEXT NOT NULL,tags TEXT NOT NULL,vault_uri TEXT NOT NULL,last_indexed TEXT NOT NULL);
        CREATE TABLE IF NOT EXISTS vault_access(id TEXT PRIMARY KEY,vault_id TEXT NOT NULL REFERENCES vaults(id) ON DELETE CASCADE,identity_id TEXT NOT NULL REFERENCES identities(id) ON DELETE CASCADE,tenant_id TEXT NOT NULL,status TEXT NOT NULL,last_validated TEXT NOT NULL,failure_category TEXT,preferred_rank INTEGER NOT NULL,is_selected INTEGER NOT NULL DEFAULT 1,UNIQUE(vault_id,identity_id,tenant_id));
        CREATE TABLE IF NOT EXISTS items(id TEXT PRIMARY KEY,vault_id TEXT NOT NULL REFERENCES vaults(id) ON DELETE CASCADE,name TEXT NOT NULL,object_type INTEGER NOT NULL,enabled INTEGER NOT NULL,tags TEXT NOT NULL,content_type TEXT,created_at TEXT,updated_at TEXT,expires_at TEXT,provider_version TEXT NOT NULL,fingerprint TEXT NOT NULL,last_indexed TEXT NOT NULL,is_deleted INTEGER NOT NULL,UNIQUE(vault_id,object_type,name,provider_version));
        CREATE INDEX IF NOT EXISTS ix_items_search ON items(name COLLATE NOCASE,object_type,enabled); CREATE INDEX IF NOT EXISTS ix_items_vault ON items(vault_id); CREATE INDEX IF NOT EXISTS ix_vaults_scope ON vaults(tenant_id,subscription_id);
        CREATE TABLE IF NOT EXISTS favorites(item_id TEXT PRIMARY KEY REFERENCES items(id) ON DELETE CASCADE); CREATE TABLE IF NOT EXISTS access_history(item_id TEXT PRIMARY KEY REFERENCES items(id) ON DELETE CASCADE,last_accessed TEXT NOT NULL);
        CREATE TABLE IF NOT EXISTS workspaces(id TEXT PRIMARY KEY,name TEXT NOT NULL UNIQUE,description TEXT NOT NULL,sort_order INTEGER NOT NULL,cache_enabled INTEGER NOT NULL,cache_lifetime_minutes INTEGER NOT NULL,require_unlock INTEGER NOT NULL,allow_clipboard INTEGER NOT NULL);
        CREATE TABLE IF NOT EXISTS workspace_links(id TEXT PRIMARY KEY,workspace_id TEXT NOT NULL REFERENCES workspaces(id) ON DELETE CASCADE,resource_type INTEGER NOT NULL,resource_id TEXT NOT NULL,UNIQUE(workspace_id,resource_type,resource_id));
        CREATE TABLE IF NOT EXISTS sync_runs(id TEXT PRIMARY KEY,scope TEXT NOT NULL,started_at TEXT NOT NULL,completed_at TEXT,status INTEGER NOT NULL,vault_count INTEGER NOT NULL,item_count INTEGER NOT NULL,error_count INTEGER NOT NULL);
        CREATE TABLE IF NOT EXISTS browser_fill_mappings(id TEXT PRIMARY KEY,item_id TEXT NOT NULL REFERENCES items(id) ON DELETE CASCADE,identity_id TEXT NOT NULL REFERENCES identities(id) ON DELETE CASCADE,top_origin TEXT NOT NULL,frame_origin TEXT NOT NULL,field_purpose INTEGER NOT NULL,is_enabled INTEGER NOT NULL,created_at TEXT NOT NULL,updated_at TEXT NOT NULL,UNIQUE(top_origin,frame_origin,field_purpose));
        CREATE INDEX IF NOT EXISTS ix_browser_fill_mappings_item ON browser_fill_mappings(item_id,identity_id);
        CREATE TABLE IF NOT EXISTS browser_fill_audit(id TEXT PRIMARY KEY,occurred_at TEXT NOT NULL,mapping_id TEXT,item_id TEXT,identity_id TEXT,top_origin TEXT NOT NULL,frame_origin TEXT NOT NULL,field_purpose INTEGER NOT NULL,result TEXT NOT NULL);
        CREATE INDEX IF NOT EXISTS ix_browser_fill_audit_time ON browser_fill_audit(occurred_at DESC);
        CREATE TABLE IF NOT EXISTS cyberark_profiles(id TEXT PRIMARY KEY,display_name TEXT NOT NULL,identity_url TEXT NOT NULL,privilege_cloud_url TEXT NOT NULL,service_user_name TEXT NOT NULL,application_name TEXT NOT NULL,auth_state INTEGER NOT NULL,is_enabled INTEGER NOT NULL,credential_updated_at TEXT NOT NULL,last_validated_at TEXT);
        CREATE UNIQUE INDEX IF NOT EXISTS ix_cyberark_profiles_endpoint_user ON cyberark_profiles(privilege_cloud_url,service_user_name,application_name);
        CREATE TABLE IF NOT EXISTS cyberark_safes(profile_id TEXT NOT NULL REFERENCES cyberark_profiles(id) ON DELETE CASCADE,safe_id TEXT NOT NULL,name TEXT NOT NULL,description TEXT NOT NULL,location TEXT NOT NULL,retention_days INTEGER,retention_versions INTEGER,olac_enabled INTEGER NOT NULL,created_at TEXT,updated_at TEXT,PRIMARY KEY(profile_id,safe_id));
        CREATE INDEX IF NOT EXISTS ix_cyberark_safes_name ON cyberark_safes(profile_id,name COLLATE NOCASE);
        CREATE TABLE IF NOT EXISTS cyberark_accounts(profile_id TEXT NOT NULL REFERENCES cyberark_profiles(id) ON DELETE CASCADE,account_id TEXT NOT NULL,safe_name TEXT NOT NULL,name TEXT NOT NULL,user_name TEXT,address TEXT,platform_id TEXT,secret_type INTEGER NOT NULL,status TEXT,created_at TEXT,updated_at TEXT,fingerprint TEXT NOT NULL,last_indexed TEXT NOT NULL,is_deleted INTEGER NOT NULL,PRIMARY KEY(profile_id,account_id));
        CREATE INDEX IF NOT EXISTS ix_cyberark_accounts_search ON cyberark_accounts(profile_id,name COLLATE NOCASE,safe_name COLLATE NOCASE,is_deleted);
        CREATE TABLE IF NOT EXISTS cyberark_versions(profile_id TEXT NOT NULL,account_id TEXT NOT NULL,version_id INTEGER NOT NULL,is_temporary INTEGER NOT NULL,modified_at TEXT NOT NULL,modified_by TEXT NOT NULL,PRIMARY KEY(profile_id,account_id,version_id),FOREIGN KEY(profile_id,account_id) REFERENCES cyberark_accounts(profile_id,account_id) ON DELETE CASCADE);
        CREATE TABLE IF NOT EXISTS cyberark_permissions(profile_id TEXT NOT NULL,safe_id TEXT NOT NULL,member_name TEXT NOT NULL,member_type TEXT NOT NULL,list_accounts INTEGER NOT NULL,use_accounts INTEGER NOT NULL,retrieve_accounts INTEGER NOT NULL,view_audit_log INTEGER NOT NULL,access_without_confirmation INTEGER NOT NULL,requests_authorization_level1 INTEGER NOT NULL,requests_authorization_level2 INTEGER NOT NULL,observed_at TEXT NOT NULL,evidence_state TEXT NOT NULL,PRIMARY KEY(profile_id,safe_id),FOREIGN KEY(profile_id,safe_id) REFERENCES cyberark_safes(profile_id,safe_id) ON DELETE CASCADE);
        CREATE TABLE IF NOT EXISTS cyberark_audit(id TEXT PRIMARY KEY,profile_id TEXT NOT NULL,account_id TEXT,safe_name TEXT,version_id INTEGER,operation TEXT NOT NULL,result INTEGER NOT NULL,safe_message TEXT NOT NULL,occurred_at TEXT NOT NULL);
        CREATE INDEX IF NOT EXISTS ix_cyberark_audit_profile_time ON cyberark_audit(profile_id,occurred_at DESC);
        CREATE TABLE IF NOT EXISTS governed_mutation_audit(sequence INTEGER PRIMARY KEY AUTOINCREMENT,id TEXT NOT NULL UNIQUE,preview_id TEXT NOT NULL,occurred_at TEXT NOT NULL,operation INTEGER NOT NULL,identity_id TEXT NOT NULL,tenant_id TEXT NOT NULL,subscription_id TEXT NOT NULL,vault_resource_id TEXT NOT NULL,object_name_hash TEXT NOT NULL,sensitive_value_length INTEGER NOT NULL,result INTEGER NOT NULL,provider_version TEXT NOT NULL,safe_message TEXT NOT NULL,previous_hash TEXT NOT NULL,record_hash TEXT NOT NULL UNIQUE);
        CREATE INDEX IF NOT EXISTS ix_governed_mutation_audit_time ON governed_mutation_audit(occurred_at DESC);
        """;
}
