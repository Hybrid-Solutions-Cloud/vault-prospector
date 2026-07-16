using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using VaultProspector.Application;

namespace VaultProspector.Infrastructure;

public sealed class RedactingDiagnosticSink(string logPath) : IDiagnosticSink
{
    private static readonly HashSet<string> AllowedFields = new(StringComparer.Ordinal)
    {
        "identity_id", "vault_count", "item_count", "error_count", "duration_ms", "status",
    };
    private readonly object _gate = new();

    public void Information(string eventName, IReadOnlyDictionary<string, object?> fields) => Write("information", eventName, null, fields);
    public void WriteError(string eventName, Exception exception, IReadOnlyDictionary<string, object?> fields) => Write("error", eventName, exception.GetType().Name, fields);

    private void Write(string level, string eventName, string? exceptionType, IReadOnlyDictionary<string, object?> fields)
    {
        var safe = fields
            .Where(x => AllowedFields.Contains(x.Key))
            .ToDictionary(x => x.Key, x => x.Key.EndsWith("_id", StringComparison.Ordinal) ? Pseudonymize(x.Value) : x.Value);
        var record = JsonSerializer.Serialize(new { timestamp = DateTimeOffset.UtcNow, level, event_name = eventName, exception_type = exceptionType, fields = safe });
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
}
