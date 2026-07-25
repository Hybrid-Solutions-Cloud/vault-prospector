using System.Security.Cryptography;
using System.Text;

namespace VaultProspector.Application;

public static class DiagnosticPrivacy
{
    private static readonly HashSet<string> AllowedEventNames =
        new(
            [
                "application_event",
                "browser_broker_start_failed",
                "browser_fill_request_failed",
                "clipboard_shutdown_clear_failed",
                "identity_access_revoked",
                "identity_connected",
                "identity_disabled",
                "identity_enabled",
                "identity_offline_value_purge_failed",
                "identity_offline_values_purged",
                "identity_provider_credential_removal_failed",
                "identity_reauthenticated",
                "identity_removed",
                "local_recovery_archive_delete_authorized",
                "local_recovery_archive_delete_failed",
                "local_recovery_archive_deleted",
                "sync_auth_failed",
                "sync_completed",
                "sync_failed",
                "windows_security_boundary_monitor_unavailable",
                "workload_credential_rotated",
                "workload_credential_rotation_failed",
                "workload_identity_connected",
            ],
            StringComparer.Ordinal);

    private static readonly HashSet<string> AllowedFieldNames =
        new(
            [
                "identity_id",
                "identity_type",
                "vault_count",
                "item_count",
                "error_count",
                "duration_ms",
                "status",
            ],
            StringComparer.Ordinal);

    private static readonly HashSet<string> AllowedIdentityTypes =
        new(
            [
                "InteractiveUser",
                "ManagedIdentity",
                "ServicePrincipal",
                "FederatedServicePrincipal",
            ],
            StringComparer.Ordinal);

    private static readonly HashSet<string> AllowedLevels =
        new(
            [
                "information",
                "error",
            ],
            StringComparer.Ordinal);

    private static readonly HashSet<string> AllowedStatuses =
        new(
            [
                "authorized",
                "cancelled",
                "completed",
                "deleted",
                "disabled",
                "failed",
                "partial",
                "purged",
                "ready",
                "removed",
                "revoked",
            ],
            StringComparer.Ordinal);

    public static bool IsAllowedFieldName(string fieldName) =>
        AllowedFieldNames.Contains(fieldName);

    public static string NormalizeEventName(string? eventName) =>
        eventName is not null &&
        AllowedEventNames.Contains(eventName)
            ? eventName
            : "application_event";

    public static string NormalizeIdentityType(string? identityType) =>
        identityType is not null &&
        AllowedIdentityTypes.Contains(identityType)
            ? identityType
            : "Unknown";

    public static string NormalizeLevel(string? level) =>
        level is not null &&
        AllowedLevels.Contains(level)
            ? level
            : "information";

    public static string NormalizeStatus(string? status) =>
        status is not null &&
        AllowedStatuses.Contains(status)
            ? status
            : "unknown";

    public static string Pseudonymize(object? value)
    {
        var hash = SHA256.HashData(
            Encoding.UTF8.GetBytes(
                value?.ToString() ?? string.Empty));
        return Convert.ToHexString(hash.AsSpan(0, 8));
    }
}
