using System.Buffers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using VaultProspector.Application;

namespace VaultProspector.Infrastructure;

public sealed class RedactingDiagnosticSink(string logPath) : IDiagnosticSink
{
    private static readonly HashSet<string> AllowedFields = new(StringComparer.Ordinal)
    {
        "identity_id", "identity_type", "vault_count", "item_count", "error_count", "duration_ms", "status",
    };
    private static readonly HashSet<string> AllowedIdentityTypes =
        new(
            [
                "InteractiveUser",
                "ManagedIdentity",
                "ServicePrincipal",
                "FederatedServicePrincipal",
            ],
            StringComparer.Ordinal);
    private static readonly HashSet<string> AllowedStatuses =
        new(
            [
                "authorized",
                "deleted",
                "disabled",
                "failed",
                "purged",
                "ready",
                "removed",
                "revoked",
            ],
            StringComparer.Ordinal);
    private readonly object _gate = new();

    public void Information(string eventName, IReadOnlyDictionary<string, object?> fields) => Write("information", eventName, null, fields);
    public void WriteError(string eventName, Exception exception, IReadOnlyDictionary<string, object?> fields) => Write("error", eventName, exception.GetType().Name, fields);

    private void Write(string level, string eventName, string? exceptionType, IReadOnlyDictionary<string, object?> fields)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WriteString("timestamp", DateTimeOffset.UtcNow);
            writer.WriteString("level", level);
            writer.WriteString("event_name", eventName);
            if (exceptionType is null)
                writer.WriteNull("exception_type");
            else
                writer.WriteString("exception_type", exceptionType);
            writer.WriteStartObject("fields");
            foreach (var field in fields.Where(field =>
                         AllowedFields.Contains(field.Key)))
            {
                WriteAllowedField(
                    writer,
                    field.Key,
                    field.Value);
            }
            writer.WriteEndObject();
            writer.WriteEndObject();
        }
        var record = Encoding.UTF8.GetString(buffer.WrittenSpan);
        lock (_gate)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(logPath) ?? ".");
            File.AppendAllText(logPath, record + Environment.NewLine);
        }
    }

    private static string Pseudonymize(object? value)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value?.ToString() ?? ""));
        return Convert.ToHexString(hash.AsSpan(0, 8));
    }

    private static void WriteAllowedField(
        Utf8JsonWriter writer,
        string fieldName,
        object? value)
    {
        if (string.Equals(
                fieldName,
                "identity_id",
                StringComparison.Ordinal))
        {
            writer.WriteString(
                fieldName,
                Pseudonymize(value));
            return;
        }

        if (string.Equals(
                fieldName,
                "identity_type",
                StringComparison.Ordinal))
        {
            var identityType = value as string;
            writer.WriteString(
                fieldName,
                identityType is not null &&
                AllowedIdentityTypes.Contains(identityType)
                    ? identityType
                    : "Unknown");
            return;
        }

        if (string.Equals(
                fieldName,
                "status",
                StringComparison.Ordinal))
        {
            var status = value as string;
            writer.WriteString(
                fieldName,
                status is not null &&
                AllowedStatuses.Contains(status)
                    ? status
                    : "unknown");
            return;
        }

        switch (value)
        {
            case byte number:
                writer.WriteNumber(fieldName, number);
                break;
            case sbyte number:
                writer.WriteNumber(fieldName, number);
                break;
            case short number:
                writer.WriteNumber(fieldName, number);
                break;
            case ushort number:
                writer.WriteNumber(fieldName, number);
                break;
            case int number:
                writer.WriteNumber(fieldName, number);
                break;
            case uint number:
                writer.WriteNumber(fieldName, number);
                break;
            case long number:
                writer.WriteNumber(fieldName, number);
                break;
            case ulong number:
                writer.WriteNumber(fieldName, number);
                break;
            case float number:
                writer.WriteNumber(fieldName, number);
                break;
            case double number:
                writer.WriteNumber(fieldName, number);
                break;
            case decimal number:
                writer.WriteNumber(fieldName, number);
                break;
            default:
                writer.WriteNull(fieldName);
                break;
        }
    }
}
