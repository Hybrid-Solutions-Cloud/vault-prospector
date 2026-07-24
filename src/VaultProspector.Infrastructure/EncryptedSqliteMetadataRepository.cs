using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using VaultProspector.Application;
using VaultProspector.Domain;

namespace VaultProspector.Infrastructure;

public sealed class EncryptedSqliteMetadataRepository(string databasePath, IKeyMaterialProvider keyMaterial) : IMetadataRepository
{
    private const int CurrentSchemaVersion = 4;
    private string? _connectionString;

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
        try
        {
            _connectionString = new SqliteConnectionStringBuilder
            {
                DataSource = databasePath,
                Mode = SqliteOpenMode.ReadWriteCreate,
                Password = Convert.ToBase64String(key),
                Pooling = false,
            }.ToString();
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
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

                if (schemaVersion != CurrentSchemaVersion)
                {
                    throw new InvalidOperationException($"Migration failed. Expected version {CurrentSchemaVersion}, but ended up at {schemaVersion}.");
                }
            }

            await ExecuteAsync(connection, transaction, $"PRAGMA user_version={CurrentSchemaVersion}", cancellationToken);
        }

        await ValidateSchemaAsync(connection, transaction, cancellationToken);
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
            SELECT id,identity_id,tenant_id,display_name,tenant_type,last_validated,status
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
                reader.GetString(6)));
        }

        return tenants;
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

    public async Task ApplyDiscoveryAsync(Guid identityId, DiscoverySnapshot snapshot, SyncRun run, CancellationToken cancellationToken)
    {
        _ = identityId;
        await using var connection = await OpenAsync(cancellationToken);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
        foreach (var x in snapshot.Tenants) await UpsertTenantAsync(connection, transaction, x, cancellationToken);
        foreach (var x in snapshot.Subscriptions) await UpsertSubscriptionAsync(connection, transaction, x, cancellationToken);
        foreach (var x in snapshot.Vaults) await UpsertVaultAsync(connection, transaction, x, cancellationToken);
        foreach (var x in snapshot.AccessPaths) await UpsertAccessAsync(connection, transaction, x, cancellationToken);
        foreach (var x in snapshot.Items) await UpsertItemAsync(connection, transaction, x, cancellationToken);
        await ExecuteAsync(connection, transaction, "INSERT INTO sync_runs(id,scope,started_at,completed_at,status,vault_count,item_count,error_count) VALUES($id,$scope,$started,$completed,$status,$vaults,$items,$errors)", cancellationToken,
            ("$id", run.Id.ToString("D")), ("$scope", run.Scope), ("$started", Format(run.StartedAt)),
            ("$completed", run.CompletedAt is null ? DBNull.Value : Format(run.CompletedAt.Value)), ("$status", (int)run.Status),
            ("$vaults", run.VaultCount), ("$items", run.ItemCount), ("$errors", run.NonSensitiveErrors.Count));
        if (snapshot.Errors.Count == 0)
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

    private async Task<SqliteConnection> OpenAsync(CancellationToken cancellationToken)
    {
        if (_connectionString is null) throw new InvalidOperationException("Repository has not been initialized.");
        var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
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
            ["tenants"] = ["id", "identity_id", "tenant_id", "display_name", "tenant_type", "last_validated", "status"],
            ["subscriptions"] = ["id", "tenant_access_id", "subscription_id", "display_name", "state", "is_selected", "last_discovered"],
            ["vaults"] = ["id", "resource_id", "name", "tenant_id", "subscription_id", "resource_group", "location", "tags", "vault_uri", "last_indexed"],
            ["vault_access"] = ["id", "vault_id", "identity_id", "tenant_id", "status", "last_validated", "failure_category", "preferred_rank", "is_selected"],
            ["items"] = ["id", "vault_id", "name", "object_type", "enabled", "tags", "content_type", "created_at", "updated_at", "expires_at", "provider_version", "fingerprint", "last_indexed", "is_deleted"],
            ["favorites"] = ["item_id"],
            ["access_history"] = ["item_id", "last_accessed"],
            ["workspaces"] = ["id", "name", "description", "sort_order", "cache_enabled", "cache_lifetime_minutes", "require_unlock", "allow_clipboard"],
            ["workspace_links"] = ["id", "workspace_id", "resource_type", "resource_id"],
            ["sync_runs"] = ["id", "scope", "started_at", "completed_at", "status", "vault_count", "item_count", "error_count"],
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

    private static async Task ExecuteAsync(SqliteConnection connection, SqliteTransaction? transaction, string sql, CancellationToken cancellationToken, params (string Name, object Value)[] values)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        foreach (var value in values) command.Parameters.AddWithValue(value.Name, value.Value);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static Task UpsertTenantAsync(SqliteConnection c, SqliteTransaction t, TenantAccess x, CancellationToken ct) => ExecuteAsync(c, t, "INSERT INTO tenants(id,identity_id,tenant_id,display_name,tenant_type,last_validated,status) VALUES($id,$identity,$tenant,$name,$type,$last,$status) ON CONFLICT(id) DO UPDATE SET display_name=excluded.display_name,last_validated=excluded.last_validated,status=excluded.status", ct, ("$id", x.Id.ToString("D")), ("$identity", x.ConnectedIdentityId.ToString("D")), ("$tenant", x.TenantId), ("$name", x.DisplayName), ("$type", x.TenantType), ("$last", Format(x.LastValidatedAt)), ("$status", x.Status));
    private static Task UpsertSubscriptionAsync(SqliteConnection c, SqliteTransaction t, SubscriptionAccess x, CancellationToken ct) => ExecuteAsync(c, t, "INSERT INTO subscriptions(id,tenant_access_id,subscription_id,display_name,state,is_selected,last_discovered) VALUES($id,$tenant,$subscription,$name,$state,$selected,$last) ON CONFLICT(id) DO UPDATE SET display_name=excluded.display_name,state=excluded.state,last_discovered=excluded.last_discovered", ct, ("$id", x.Id.ToString("D")), ("$tenant", x.TenantAccessId.ToString("D")), ("$subscription", x.SubscriptionId), ("$name", x.DisplayName), ("$state", x.State), ("$selected", x.IsSelected ? 1 : 0), ("$last", Format(x.LastDiscoveredAt)));
    private static Task UpsertVaultAsync(SqliteConnection c, SqliteTransaction t, VaultResource x, CancellationToken ct) => ExecuteAsync(c, t, "INSERT INTO vaults(id,resource_id,name,tenant_id,subscription_id,resource_group,location,tags,vault_uri,last_indexed) VALUES($id,$resource,$name,$tenant,$subscription,$group,$location,$tags,$uri,$last) ON CONFLICT(id) DO UPDATE SET name=excluded.name,tags=excluded.tags,vault_uri=excluded.vault_uri,last_indexed=excluded.last_indexed", ct, ("$id", x.Id.ToString("D")), ("$resource", x.ProviderResourceId), ("$name", x.Name), ("$tenant", x.TenantId), ("$subscription", x.SubscriptionId), ("$group", x.ResourceGroup), ("$location", x.Location), ("$tags", JsonSerializer.Serialize(x.Tags)), ("$uri", x.VaultUri.ToString()), ("$last", Format(x.LastIndexedAt)));
    private static Task UpsertAccessAsync(SqliteConnection c, SqliteTransaction t, VaultAccess x, CancellationToken ct) => ExecuteAsync(c, t, "INSERT INTO vault_access(id,vault_id,identity_id,tenant_id,status,last_validated,failure_category,preferred_rank,is_selected) VALUES($id,$vault,$identity,$tenant,$status,$last,$failure,$rank,$selected) ON CONFLICT(id) DO UPDATE SET status=excluded.status,last_validated=excluded.last_validated,failure_category=excluded.failure_category,preferred_rank=excluded.preferred_rank", ct, ("$id", x.Id.ToString("D")), ("$vault", x.VaultId.ToString("D")), ("$identity", x.ConnectedIdentityId.ToString("D")), ("$tenant", x.TenantId), ("$status", x.AccessStatus), ("$last", Format(x.LastValidatedAt)), ("$failure", x.LastFailureCategory ?? (object)DBNull.Value), ("$rank", x.PreferredRank), ("$selected", x.IsSelected ? 1 : 0));
    private static Task UpsertItemAsync(SqliteConnection c, SqliteTransaction t, VaultItem x, CancellationToken ct) => ExecuteAsync(c, t, "INSERT INTO items(id,vault_id,name,object_type,enabled,tags,content_type,created_at,updated_at,expires_at,provider_version,fingerprint,last_indexed,is_deleted) VALUES($id,$vault,$name,$type,$enabled,$tags,$content,$created,$updated,$expires,$version,$fingerprint,$last,$deleted) ON CONFLICT(id) DO UPDATE SET enabled=excluded.enabled,tags=excluded.tags,content_type=excluded.content_type,updated_at=excluded.updated_at,expires_at=excluded.expires_at,provider_version=excluded.provider_version,fingerprint=excluded.fingerprint,last_indexed=excluded.last_indexed,is_deleted=excluded.is_deleted", ct, ("$id", x.Id.ToString("D")), ("$vault", x.VaultId.ToString("D")), ("$name", x.ProviderObjectName), ("$type", (int)x.ObjectType), ("$enabled", x.Enabled ? 1 : 0), ("$tags", JsonSerializer.Serialize(x.Tags)), ("$content", x.ContentType ?? (object)DBNull.Value), ("$created", x.CreatedAt is null ? DBNull.Value : Format(x.CreatedAt.Value)), ("$updated", x.UpdatedAt is null ? DBNull.Value : Format(x.UpdatedAt.Value)), ("$expires", x.ExpiresAt is null ? DBNull.Value : Format(x.ExpiresAt.Value)), ("$version", x.ProviderVersion), ("$fingerprint", x.MetadataFingerprint), ("$last", Format(x.LastIndexedAt)), ("$deleted", x.IsDeletedOrUnavailable ? 1 : 0));

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

    private static string Format(DateTimeOffset value) => value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
    private static DateTimeOffset? ReadDate(SqliteDataReader reader, int ordinal) => reader.IsDBNull(ordinal) ? null : DateTimeOffset.Parse(reader.GetString(ordinal), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
    private static string EscapeLike(string value) => value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("%", "\\%", StringComparison.Ordinal).Replace("_", "\\_", StringComparison.Ordinal);
    private static Dictionary<string, string> ReadTags(SqliteDataReader reader, int ordinal) => JsonSerializer.Deserialize<Dictionary<string, string>>(reader.GetString(ordinal)) ?? [];
    private static ConnectedIdentity ReadIdentity(SqliteDataReader r, int o = 0) => new(r.GetGuid(o), r.GetString(o + 1), r.GetString(o + 2), r.GetString(o + 3), r.GetString(o + 4), r.GetString(o + 5), (AuthenticationState)r.GetInt32(o + 6), DateTimeOffset.Parse(r.GetString(o + 7), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind), r.GetBoolean(o + 8), (IdentityType)r.GetInt32(o + 9), r.GetString(o + 10));
    private static VaultItem ReadItem(SqliteDataReader r, int o) => new(r.GetGuid(o), r.GetGuid(o + 1), r.GetString(o + 2), (VaultObjectType)r.GetInt32(o + 3), r.GetBoolean(o + 4), ReadTags(r, o + 5), r.IsDBNull(o + 6) ? null : r.GetString(o + 6), ReadDate(r, o + 7), ReadDate(r, o + 8), ReadDate(r, o + 9), r.GetString(o + 10), r.GetString(o + 11), DateTimeOffset.Parse(r.GetString(o + 12), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind), r.GetBoolean(o + 13));
    private static VaultResource ReadVault(SqliteDataReader r, int o, Guid id) => new(id, r.GetString(o), r.GetString(o + 1), r.GetString(o + 2), r.GetString(o + 3), r.GetString(o + 4), r.GetString(o + 5), ReadTags(r, o + 6), new Uri(r.GetString(o + 7)), DateTimeOffset.Parse(r.GetString(o + 8), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind));

    private const string IdentitySelect = "SELECT id,client_id,account_identifier,username_hint,display_name,home_tenant_id,auth_state,last_interactive,is_enabled,identity_type,credential_data FROM identities";
    private const string SearchSql = """
        SELECT i.id,i.vault_id,i.name,i.object_type,i.enabled,i.tags,i.content_type,i.created_at,i.updated_at,i.expires_at,i.provider_version,i.fingerprint,i.last_indexed,i.is_deleted,
               v.resource_id,v.name,v.tenant_id,v.subscription_id,v.resource_group,v.location,v.tags,v.vault_uri,v.last_indexed,
               ident.display_name,COALESCE(t.display_name,v.tenant_id),EXISTS(SELECT 1 FROM favorites f WHERE f.item_id=i.id),a.last_accessed,
               CASE WHEN julianday($now)-julianday(i.last_indexed)>1 THEN 1 ELSE 0 END,va.status
        FROM items i JOIN vaults v ON v.id=i.vault_id JOIN vault_access va ON va.vault_id=v.id AND va.status!='Removed' JOIN identities ident ON ident.id=va.identity_id
        LEFT JOIN tenants t ON t.identity_id=ident.id AND t.tenant_id=v.tenant_id LEFT JOIN access_history a ON a.item_id=i.id
        WHERE i.is_deleted=0 AND ident.is_enabled=1 AND ($text='' OR i.name LIKE '%'||$text||'%' ESCAPE '\' OR i.tags LIKE '%'||$text||'%' ESCAPE '\')
          AND ($identity IS NULL OR ident.id=$identity) AND ($tenant IS NULL OR v.tenant_id LIKE '%'||$tenant||'%' ESCAPE '\') AND ($subscription IS NULL OR v.subscription_id LIKE '%'||$subscription||'%' ESCAPE '\')
          AND ($vault IS NULL OR v.id=$vault) AND ($vault_name IS NULL OR v.name LIKE '%'||$vault_name||'%' ESCAPE '\') AND ($type IS NULL OR i.object_type=$type) AND ($enabled IS NULL OR i.enabled=$enabled)
          AND ($favorites=0 OR EXISTS(SELECT 1 FROM favorites f WHERE f.item_id=i.id)) AND ($expired=0 OR (i.expires_at IS NOT NULL AND i.expires_at<$now))
          AND ($stale=0 OR julianday($now)-julianday(i.last_indexed)>1)
          AND ($workspace IS NULL OR EXISTS(SELECT 1 FROM workspace_links wl WHERE wl.workspace_id=$workspace AND ((wl.resource_type=0 AND wl.resource_id=ident.id) OR (wl.resource_type=1 AND wl.resource_id=v.tenant_id) OR (wl.resource_type=2 AND wl.resource_id=v.subscription_id) OR (wl.resource_type=3 AND wl.resource_id=v.id))))
        GROUP BY i.id ORDER BY CASE WHEN $recent_first=1 AND a.last_accessed IS NULL THEN 1 ELSE 0 END,CASE WHEN $recent_first=1 THEN a.last_accessed END DESC,i.name COLLATE NOCASE,v.name COLLATE NOCASE,i.provider_version DESC LIMIT $limit
        """;
    private const string ResolveSql = """
        SELECT i.id,i.vault_id,i.name,i.object_type,i.enabled,i.tags,i.content_type,i.created_at,i.updated_at,i.expires_at,i.provider_version,i.fingerprint,i.last_indexed,i.is_deleted,
               v.resource_id,v.name,v.tenant_id,v.subscription_id,v.resource_group,v.location,v.tags,v.vault_uri,v.last_indexed,
               ident.id,ident.client_id,ident.account_identifier,ident.username_hint,ident.display_name,ident.home_tenant_id,ident.auth_state,ident.last_interactive,ident.is_enabled,ident.identity_type,ident.credential_data
        FROM items i JOIN vaults v ON v.id=i.vault_id JOIN vault_access va ON va.vault_id=v.id AND va.status!='Removed' JOIN identities ident ON ident.id=va.identity_id
        WHERE i.id=$id AND ident.is_enabled=1 ORDER BY va.preferred_rank LIMIT 1
        """;
    private const string Configuration = "PRAGMA foreign_keys=ON; PRAGMA journal_mode=WAL; PRAGMA secure_delete=ON;";
    private const string Schema = """
        CREATE TABLE IF NOT EXISTS identities(id TEXT PRIMARY KEY,client_id TEXT NOT NULL,account_identifier TEXT NOT NULL UNIQUE,username_hint TEXT NOT NULL,display_name TEXT NOT NULL,home_tenant_id TEXT NOT NULL,auth_state INTEGER NOT NULL,last_interactive TEXT NOT NULL,is_enabled INTEGER NOT NULL,identity_type INTEGER NOT NULL DEFAULT 0,credential_data TEXT NOT NULL DEFAULT '');
        CREATE TABLE IF NOT EXISTS tenants(id TEXT PRIMARY KEY,identity_id TEXT NOT NULL REFERENCES identities(id) ON DELETE CASCADE,tenant_id TEXT NOT NULL,display_name TEXT NOT NULL,tenant_type TEXT NOT NULL,last_validated TEXT NOT NULL,status TEXT NOT NULL,UNIQUE(identity_id,tenant_id));
        CREATE TABLE IF NOT EXISTS subscriptions(id TEXT PRIMARY KEY,tenant_access_id TEXT NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,subscription_id TEXT NOT NULL,display_name TEXT NOT NULL,state TEXT NOT NULL,is_selected INTEGER NOT NULL,last_discovered TEXT NOT NULL,UNIQUE(tenant_access_id,subscription_id));
        CREATE TABLE IF NOT EXISTS vaults(id TEXT PRIMARY KEY,resource_id TEXT NOT NULL UNIQUE,name TEXT NOT NULL,tenant_id TEXT NOT NULL,subscription_id TEXT NOT NULL,resource_group TEXT NOT NULL,location TEXT NOT NULL,tags TEXT NOT NULL,vault_uri TEXT NOT NULL,last_indexed TEXT NOT NULL);
        CREATE TABLE IF NOT EXISTS vault_access(id TEXT PRIMARY KEY,vault_id TEXT NOT NULL REFERENCES vaults(id) ON DELETE CASCADE,identity_id TEXT NOT NULL REFERENCES identities(id) ON DELETE CASCADE,tenant_id TEXT NOT NULL,status TEXT NOT NULL,last_validated TEXT NOT NULL,failure_category TEXT,preferred_rank INTEGER NOT NULL,is_selected INTEGER NOT NULL DEFAULT 1,UNIQUE(vault_id,identity_id,tenant_id));
        CREATE TABLE IF NOT EXISTS items(id TEXT PRIMARY KEY,vault_id TEXT NOT NULL REFERENCES vaults(id) ON DELETE CASCADE,name TEXT NOT NULL,object_type INTEGER NOT NULL,enabled INTEGER NOT NULL,tags TEXT NOT NULL,content_type TEXT,created_at TEXT,updated_at TEXT,expires_at TEXT,provider_version TEXT NOT NULL,fingerprint TEXT NOT NULL,last_indexed TEXT NOT NULL,is_deleted INTEGER NOT NULL,UNIQUE(vault_id,object_type,name,provider_version));
        CREATE INDEX IF NOT EXISTS ix_items_search ON items(name COLLATE NOCASE,object_type,enabled); CREATE INDEX IF NOT EXISTS ix_items_vault ON items(vault_id); CREATE INDEX IF NOT EXISTS ix_vaults_scope ON vaults(tenant_id,subscription_id);
        CREATE TABLE IF NOT EXISTS favorites(item_id TEXT PRIMARY KEY REFERENCES items(id) ON DELETE CASCADE); CREATE TABLE IF NOT EXISTS access_history(item_id TEXT PRIMARY KEY REFERENCES items(id) ON DELETE CASCADE,last_accessed TEXT NOT NULL);
        CREATE TABLE IF NOT EXISTS workspaces(id TEXT PRIMARY KEY,name TEXT NOT NULL UNIQUE,description TEXT NOT NULL,sort_order INTEGER NOT NULL,cache_enabled INTEGER NOT NULL,cache_lifetime_minutes INTEGER NOT NULL,require_unlock INTEGER NOT NULL,allow_clipboard INTEGER NOT NULL);
        CREATE TABLE IF NOT EXISTS workspace_links(id TEXT PRIMARY KEY,workspace_id TEXT NOT NULL REFERENCES workspaces(id) ON DELETE CASCADE,resource_type INTEGER NOT NULL,resource_id TEXT NOT NULL,UNIQUE(workspace_id,resource_type,resource_id));
        CREATE TABLE IF NOT EXISTS sync_runs(id TEXT PRIMARY KEY,scope TEXT NOT NULL,started_at TEXT NOT NULL,completed_at TEXT,status INTEGER NOT NULL,vault_count INTEGER NOT NULL,item_count INTEGER NOT NULL,error_count INTEGER NOT NULL);
        """;
}
