using System.IO.Compression;
using System.Text.Json;
using VaultProspector.Application;
using VaultProspector.Platform;

namespace VaultProspector.Platform.Tests;

public sealed class FileSystemSupportBundleServiceTests : IDisposable
{
    private static readonly string[] ExpectedBundleEntries =
        ["diagnostics/vault-prospector.log", "manifest.json"];

    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        $"vault-prospector-support-tests-{Guid.NewGuid():N}");

    private static readonly string[] ProhibitedCanaries =
    [
        "secret-value-canary",
        "access-token-canary",
        "private-key-canary",
        "certificate-payload-canary",
        "decrypted-cache-canary",
        "tenant-id-canary",
        "subscription-id-canary",
        "vault-name-canary",
        "object-name-canary",
        "username-canary",
        "client-credential-canary",
        "http-header-canary",
        "http-body-canary",
        "business-reason-canary",
        "diagnostic-path-canary",
    ];

    [Fact]
    public async Task BundleContainsOnlyManifestAndExistingRedactedLog()
    {
        Directory.CreateDirectory(_root);
        var logPath = Path.Combine(_root, "logs", "vault-prospector.log");
        Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
        await File.WriteAllTextAsync(
            logPath,
            """{"event_name":"sync_completed","fields":{"item_count":124}}""",
            TestContext.Current.CancellationToken);
        var service = new FileSystemSupportBundleService(
            logPath,
            Path.Combine(_root, "support"),
            "test-version",
            new FixedClock());

        var path = await service.CreateAsync(
            TestContext.Current.CancellationToken);

        using var archive = ZipFile.OpenRead(path);
        Assert.Equal(
            ExpectedBundleEntries,
            archive.Entries.Select(entry => entry.FullName).Order().ToArray());
        var manifestEntry = Assert.Single(
            archive.Entries,
            entry => entry.FullName == "manifest.json");
        using var manifest = JsonDocument.Parse(
            await ReadEntryAsync(
                manifestEntry,
                TestContext.Current.CancellationToken));
        Assert.Equal(
            "test-version",
            manifest.RootElement.GetProperty("applicationVersion").GetString());
        var privacy = manifest.RootElement.GetProperty("privacy");
        Assert.False(privacy.GetProperty("secretValues").GetBoolean());
        Assert.False(privacy.GetProperty("automaticUpload").GetBoolean());
    }

    [Fact]
    public async Task BundleOmitsMissingDiagnosticLog()
    {
        var service = new FileSystemSupportBundleService(
            Path.Combine(_root, "missing.log"),
            Path.Combine(_root, "support"),
            "test-version",
            new FixedClock());

        var path = await service.CreateAsync(
            TestContext.Current.CancellationToken);

        using var archive = ZipFile.OpenRead(path);
        Assert.Single(archive.Entries);
        Assert.Equal("manifest.json", archive.Entries[0].FullName);
    }

    [Fact]
    public async Task RecentDiagnosticsAreBoundedNewestFirstAndMalformedLinesAreIgnored()
    {
        Directory.CreateDirectory(_root);
        var logPath = Path.Combine(
            _root,
            "logs",
            "vault-prospector.log");
        Directory.CreateDirectory(
            Path.GetDirectoryName(logPath)!);
        var lines = new[]
        {
            """{"timestamp":"2026-07-25T18:00:00Z","level":"information","event_name":"identity_connected","fields":{"identity_id":"AABBCCDD","status":"ready"}}""",
            """not-json""",
            """{"timestamp":"2026-07-25T19:00:00Z","level":"error","event_name":"sync_failed","fields":{"identity_id":"11223344","status":"partial"}}""",
        };
        await File.WriteAllLinesAsync(
            logPath,
            lines,
            TestContext.Current.CancellationToken);
        var service = new FileSystemSupportBundleService(
            logPath,
            Path.Combine(_root, "support"),
            "test-version",
            new FixedClock());

        var events = await service.ReadRecentAsync(
            100,
            TestContext.Current.CancellationToken);

        Assert.Equal(2, events.Count);
        Assert.Equal(
            "Synchronization",
            events[0].Category);
        Assert.Equal(
            "Identity 11223344",
            events[0].Scope);
        Assert.Contains(
            "partial",
            events[0].Summary,
            StringComparison.Ordinal);
        Assert.Equal(
            "Identity",
            events[1].Category);
    }

    [Fact]
    public async Task RecentDiagnosticsRejectInvalidLimit()
    {
        var service = new FileSystemSupportBundleService(
            Path.Combine(_root, "missing.log"),
            Path.Combine(_root, "support"),
            "test-version",
            new FixedClock());

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => service.ReadRecentAsync(
                0,
                TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task BundleAndViewerResanitizeEveryProhibitedDataClass()
    {
        Directory.CreateDirectory(_root);
        var logPath = Path.Combine(
            _root,
            "logs",
            "vault-prospector.log");
        Directory.CreateDirectory(
            Path.GetDirectoryName(logPath)!);
        var maliciousFields = ProhibitedCanaries
            .Select(
                (canary, index) =>
                    new KeyValuePair<string, object?>(
                        $"prohibited_{index}",
                        canary))
            .ToDictionary(
                pair => pair.Key,
                pair => pair.Value);
        maliciousFields["identity_id"] =
            string.Join('|', ProhibitedCanaries);
        maliciousFields["identity_type"] =
            ProhibitedCanaries[0];
        maliciousFields["status"] =
            ProhibitedCanaries[1];
        maliciousFields["item_count"] =
            ProhibitedCanaries[2];
        var maliciousEvent = JsonSerializer.Serialize(
            new Dictionary<string, object?>
            {
                ["timestamp"] =
                    "2026-07-25T19:00:00Z",
                ["level"] =
                    ProhibitedCanaries[3],
                ["event_name"] =
                    ProhibitedCanaries[4],
                ["exception_type"] =
                    ProhibitedCanaries[5],
                ["fields"] =
                    maliciousFields,
            });
        await File.WriteAllLinesAsync(
            logPath,
            [
                maliciousEvent,
                string.Join('|', ProhibitedCanaries),
            ],
            TestContext.Current.CancellationToken);
        var service = new FileSystemSupportBundleService(
            logPath,
            Path.Combine(_root, "support"),
            "test-version",
            new FixedClock());

        var events = await service.ReadRecentAsync(
            100,
            TestContext.Current.CancellationToken);
        var diagnosticEvent = Assert.Single(events);
        var viewerText = string.Join(
            '|',
            diagnosticEvent.Level,
            diagnosticEvent.Category,
            diagnosticEvent.Scope,
            diagnosticEvent.Summary,
            diagnosticEvent.Recovery);
        foreach (var canary in ProhibitedCanaries)
        {
            Assert.DoesNotContain(
                canary,
                viewerText,
                StringComparison.Ordinal);
        }

        var bundlePath = await service.CreateAsync(
            TestContext.Current.CancellationToken);
        using var archive = ZipFile.OpenRead(bundlePath);
        var bundleText = string.Join(
            Environment.NewLine,
            await Task.WhenAll(
                archive.Entries.Select(
                    entry => ReadEntryAsync(
                        entry,
                        TestContext.Current.CancellationToken))));
        foreach (var canary in ProhibitedCanaries)
        {
            Assert.DoesNotContain(
                canary,
                bundleText,
                StringComparison.Ordinal);
        }

        Assert.Contains(
            "\"event_name\":\"application_event\"",
            bundleText,
            StringComparison.Ordinal);
        Assert.Contains(
            "\"identity_type\":\"Unknown\"",
            bundleText,
            StringComparison.Ordinal);
        Assert.Contains(
            "\"status\":\"unknown\"",
            bundleText,
            StringComparison.Ordinal);
        Assert.Contains(
            "\"item_count\":null",
            bundleText,
            StringComparison.Ordinal);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    private static async Task<string> ReadEntryAsync(
        ZipArchiveEntry entry,
        CancellationToken cancellationToken)
    {
        await using var stream = entry.Open();
        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync(cancellationToken);
    }

    private sealed class FixedClock : IClock
    {
        public DateTimeOffset UtcNow { get; } =
            new(2026, 7, 25, 19, 0, 0, TimeSpan.Zero);
    }
}
