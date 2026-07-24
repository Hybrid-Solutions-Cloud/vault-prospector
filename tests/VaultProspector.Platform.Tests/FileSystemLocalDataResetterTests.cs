using VaultProspector.Platform;

namespace VaultProspector.Platform.Tests;

public sealed class FileSystemLocalDataResetterTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        $"vault-prospector-reset-tests-{Guid.NewGuid():N}");

    [Fact]
    public async Task ArchiveMovesCompleteLocalStateAndCreatesEmptyDataDirectory()
    {
        var dataDirectory = Path.Combine(_root, "VaultProspector");
        Directory.CreateDirectory(Path.Combine(dataDirectory, "keys"));
        Directory.CreateDirectory(Path.Combine(dataDirectory, "identity-cache"));
        await File.WriteAllTextAsync(
            Path.Combine(dataDirectory, "vault-prospector.db"),
            "encrypted-database",
            TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(
            Path.Combine(dataDirectory, "vault-prospector.db-wal"),
            "encrypted-sidecar",
            TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(
            Path.Combine(dataDirectory, "keys", "metadata-database.key"),
            "protected-key",
            TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(
            Path.Combine(dataDirectory, "identity-cache", "account.bin"),
            "token-cache",
            TestContext.Current.CancellationToken);

        var result = await new FileSystemLocalDataResetter(dataDirectory)
            .ArchiveForResetAsync(TestContext.Current.CancellationToken);

        Assert.True(result.HadExistingData);
        Assert.StartsWith(Path.Combine(_root, "VaultProspector-Recovery"), result.ArchivePath, StringComparison.OrdinalIgnoreCase);
        Assert.True(File.Exists(Path.Combine(result.ArchivePath, "vault-prospector.db")));
        Assert.True(File.Exists(Path.Combine(result.ArchivePath, "vault-prospector.db-wal")));
        Assert.True(File.Exists(Path.Combine(result.ArchivePath, "keys", "metadata-database.key")));
        Assert.True(File.Exists(Path.Combine(result.ArchivePath, "identity-cache", "account.bin")));
        Assert.True(Directory.Exists(dataDirectory));
        Assert.Empty(Directory.EnumerateFileSystemEntries(dataDirectory));
    }

    [Fact]
    public async Task EmptyStateCreatesDataDirectoryWithoutAnArchive()
    {
        var dataDirectory = Path.Combine(_root, "VaultProspector");

        var result = await new FileSystemLocalDataResetter(dataDirectory)
            .ArchiveForResetAsync(TestContext.Current.CancellationToken);

        Assert.False(result.HadExistingData);
        Assert.Equal(string.Empty, result.ArchivePath);
        Assert.True(Directory.Exists(dataDirectory));
        Assert.False(Directory.Exists(Path.Combine(_root, "VaultProspector-Recovery")));
    }

    [Fact]
    public async Task CancellationPreservesExistingState()
    {
        var dataDirectory = Path.Combine(_root, "VaultProspector");
        Directory.CreateDirectory(dataDirectory);
        var statePath = Path.Combine(dataDirectory, "vault-prospector.db");
        await File.WriteAllTextAsync(statePath, "encrypted", TestContext.Current.CancellationToken);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            new FileSystemLocalDataResetter(dataDirectory).ArchiveForResetAsync(cancellation.Token));

        Assert.True(File.Exists(statePath));
        Assert.False(Directory.Exists(Path.Combine(_root, "VaultProspector-Recovery")));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
        GC.SuppressFinalize(this);
    }
}
