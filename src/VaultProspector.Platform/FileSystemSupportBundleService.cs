using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text.Json;
using VaultProspector.Application;

namespace VaultProspector.Platform;

public sealed class FileSystemSupportBundleService(
    string diagnosticLogPath,
    string supportDirectory,
    string applicationVersion,
    IClock clock) : ISupportBundleService
{
    private const long MaximumDiagnosticLogBytes = 4 * 1024 * 1024;
    private const long MaximumViewerBytes = 1024 * 1024;

    public string DiagnosticLogPath { get; } =
        Path.GetFullPath(diagnosticLogPath);

    public async Task<IReadOnlyList<DiagnosticEvent>> ReadRecentAsync(
        int maximumEvents,
        CancellationToken cancellationToken)
    {
        if (maximumEvents is < 1 or > 200)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumEvents));
        }

        if (!File.Exists(DiagnosticLogPath))
            return [];

        var events = new Queue<DiagnosticEvent>(
            maximumEvents);
        await using var input = new FileStream(
            DiagnosticLogPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete,
            4096,
            FileOptions.Asynchronous |
            FileOptions.SequentialScan);
        var skippedPartialLine = false;
        if (input.Length > MaximumViewerBytes)
        {
            input.Seek(
                -MaximumViewerBytes,
                SeekOrigin.End);
            skippedPartialLine = true;
        }

        using var reader = new StreamReader(
            input,
            detectEncodingFromByteOrderMarks: false,
            leaveOpen: false);
        if (skippedPartialLine)
        {
            _ = await reader.ReadLineAsync(
                cancellationToken);
        }

        while (await reader.ReadLineAsync(
                   cancellationToken) is { } line)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var diagnosticEvent = TryParseEvent(line);
            if (diagnosticEvent is null)
                continue;
            if (events.Count == maximumEvents)
                events.Dequeue();
            events.Enqueue(diagnosticEvent);
        }

        return events
            .Reverse()
            .ToArray();
    }

    public async Task<string> CreateAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var destinationDirectory = Path.GetFullPath(supportDirectory);
        Directory.CreateDirectory(destinationDirectory);
        var generatedAt = clock.UtcNow;
        var finalPath = Path.Combine(
            destinationDirectory,
            $"vault-prospector-support-{generatedAt:yyyyMMdd-HHmmss-fff}.zip");
        var temporaryPath = finalPath + $".{Guid.NewGuid():N}.tmp";

        try
        {
            await using (var output = new FileStream(
                             temporaryPath,
                             FileMode.CreateNew,
                             FileAccess.ReadWrite,
                             FileShare.None,
                             4096,
                             FileOptions.Asynchronous))
            {
                using var archive = new ZipArchive(
                    output,
                    ZipArchiveMode.Create,
                    leaveOpen: true);
                await WriteManifestAsync(
                    archive,
                    generatedAt,
                    File.Exists(DiagnosticLogPath),
                    cancellationToken);
                if (File.Exists(DiagnosticLogPath))
                    await CopyBoundedLogAsync(archive, cancellationToken);
            }

            File.Move(temporaryPath, finalPath);
            return finalPath;
        }
        catch
        {
            try
            {
                if (File.Exists(temporaryPath))
                    File.Delete(temporaryPath);
            }
            catch
            {
                // Preserve the original failure.
            }

            throw;
        }
    }

    private async Task WriteManifestAsync(
        ZipArchive archive,
        DateTimeOffset generatedAt,
        bool diagnosticLogIncluded,
        CancellationToken cancellationToken)
    {
        var manifest = new
        {
            schema = 1,
            generatedAtUtc = generatedAt,
            applicationVersion,
            operatingSystem = RuntimeInformation.OSDescription,
            processArchitecture = RuntimeInformation.ProcessArchitecture.ToString(),
            diagnosticLogIncluded,
            privacy = new
            {
                secretValues = false,
                accessTokens = false,
                userNames = false,
                vaultNames = false,
                objectNames = false,
                automaticUpload = false,
            },
        };
        var entry = archive.CreateEntry(
            "manifest.json",
            CompressionLevel.Optimal);
        await using var stream = entry.Open();
        await JsonSerializer.SerializeAsync(
            stream,
            manifest,
            cancellationToken: cancellationToken);
    }

    private async Task CopyBoundedLogAsync(
        ZipArchive archive,
        CancellationToken cancellationToken)
    {
        await using var input = new FileStream(
            DiagnosticLogPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete,
            4096,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        if (input.Length > MaximumDiagnosticLogBytes)
            input.Seek(-MaximumDiagnosticLogBytes, SeekOrigin.End);

        var entry = archive.CreateEntry(
            "diagnostics/vault-prospector.log",
            CompressionLevel.Optimal);
        await using var output = entry.Open();
        await input.CopyToAsync(output, cancellationToken);
    }

    private static DiagnosticEvent? TryParseEvent(
        string line)
    {
        if (string.IsNullOrWhiteSpace(line) ||
            line.Length > 65_536)
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(
                line,
                new JsonDocumentOptions
                {
                    AllowTrailingCommas = false,
                    CommentHandling =
                        JsonCommentHandling.Disallow,
                    MaxDepth = 8,
                });
            var root = document.RootElement;
            var timestamp = root
                .GetProperty("timestamp")
                .GetDateTimeOffset();
            var level = ReadBoundedString(
                root,
                "level",
                24);
            var eventName = ReadBoundedString(
                root,
                "event_name",
                128);
            if (level is null ||
                eventName is null)
            {
                return null;
            }

            var fields = root.TryGetProperty(
                "fields",
                out var fieldElement) &&
                fieldElement.ValueKind ==
                JsonValueKind.Object
                    ? fieldElement
                    : default;
            var scope = fields.ValueKind ==
                        JsonValueKind.Object &&
                        fields.TryGetProperty(
                            "identity_id",
                            out var identity) &&
                        identity.ValueKind ==
                        JsonValueKind.String
                ? $"Identity {Bound(
                    identity.GetString(),
                    24)}"
                : "Application";
            var category = Classify(eventName);
            var safeStatus = fields.ValueKind ==
                             JsonValueKind.Object &&
                             fields.TryGetProperty(
                                 "status",
                                 out var status) &&
                             status.ValueKind ==
                             JsonValueKind.String
                ? Bound(
                    status.GetString(),
                    64)
                : null;
            var summary = safeStatus is null
                ? Humanize(eventName)
                : $"{Humanize(eventName)} · {safeStatus}";
            return new DiagnosticEvent(
                timestamp,
                level,
                category,
                scope,
                summary,
                RecoveryFor(category));
        }
        catch (Exception exception) when (
            exception is JsonException or
                InvalidOperationException or
                KeyNotFoundException or
                FormatException)
        {
            return null;
        }
    }

    private static string? ReadBoundedString(
        JsonElement element,
        string propertyName,
        int maximumLength)
    {
        if (!element.TryGetProperty(
                propertyName,
                out var property) ||
            property.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        var value = property.GetString();
        return string.IsNullOrWhiteSpace(value) ||
               value.Length > maximumLength
            ? null
            : value;
    }

    private static string Bound(
        string? value,
        int maximumLength)
    {
        var safe = value ?? string.Empty;
        return safe.Length <= maximumLength
            ? safe
            : safe[..maximumLength];
    }

    private static string Classify(
        string eventName)
    {
        if (eventName.Contains(
                "sync",
                StringComparison.OrdinalIgnoreCase))
        {
            return "Synchronization";
        }

        if (eventName.Contains(
                "identity",
                StringComparison.OrdinalIgnoreCase) ||
            eventName.Contains(
                "auth",
                StringComparison.OrdinalIgnoreCase))
        {
            return "Identity";
        }

        if (eventName.Contains(
                "browser",
                StringComparison.OrdinalIgnoreCase))
        {
            return "Browser fill";
        }

        if (eventName.Contains(
                "recovery",
                StringComparison.OrdinalIgnoreCase) ||
            eventName.Contains(
                "archive",
                StringComparison.OrdinalIgnoreCase))
        {
            return "Local recovery";
        }

        return "Application";
    }

    private static string Humanize(
        string eventName)
    {
        var words = eventName
            .Replace(
                '_',
                ' ')
            .Trim();
        return words.Length == 0
            ? "Application event"
            : char.ToUpperInvariant(words[0]) +
              words[1..];
    }

    private static string RecoveryFor(
        string category) =>
        category switch
        {
            "Synchronization" =>
                "Review isolated errors, confirm the identity is Ready, then retry the affected synchronization.",
            "Identity" =>
                "Open Connections, select the affected identity, and follow its displayed authentication recovery action.",
            "Browser fill" =>
                "Open Browser fill and run the setup check for the exact destination.",
            "Local recovery" =>
                "Open Settings and review retained recovery archives before changing local data.",
            _ =>
                "Retry the action. If it repeats, create a privacy-safe support bundle and review it before sharing.",
        };
}
