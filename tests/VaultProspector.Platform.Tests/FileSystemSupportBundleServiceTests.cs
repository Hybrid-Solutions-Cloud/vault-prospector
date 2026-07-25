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
