using System.Buffers;
using System.Text;
using System.Text.Json;
using VaultProspector.Application;

namespace VaultProspector.Infrastructure;

public sealed class RedactingDiagnosticSink(string logPath) : IDiagnosticSink
{
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
            writer.WriteString(
                "level",
                DiagnosticPrivacy.NormalizeLevel(level));
            writer.WriteString(
                "event_name",
                DiagnosticPrivacy.NormalizeEventName(eventName));
            if (exceptionType is null)
                writer.WriteNull("exception_type");
            else
                writer.WriteString("exception_type", "Exception");
            writer.WriteStartObject("fields");
            foreach (var field in fields.Where(field =>
                         DiagnosticPrivacy.IsAllowedFieldName(field.Key)))
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
                DiagnosticPrivacy.Pseudonymize(value));
            return;
        }

        if (string.Equals(
                fieldName,
                "identity_type",
                StringComparison.Ordinal))
        {
            writer.WriteString(
                fieldName,
                DiagnosticPrivacy.NormalizeIdentityType(
                    value as string));
            return;
        }

        if (string.Equals(
                fieldName,
                "status",
                StringComparison.Ordinal))
        {
            writer.WriteString(
                fieldName,
                DiagnosticPrivacy.NormalizeStatus(
                    value as string));
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
