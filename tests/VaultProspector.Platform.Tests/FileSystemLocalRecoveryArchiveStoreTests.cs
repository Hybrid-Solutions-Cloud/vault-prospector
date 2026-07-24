using VaultProspector.Application;
using VaultProspector.Platform;

namespace VaultProspector.Platform.Tests;

public sealed class FileSystemLocalRecoveryArchiveStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        $"vault-prospector-archive-store-tests-{Guid.NewGuid():N}");
    private readonly string _dataDirectory;
    private readonly string _recoveryRoot;

    public FileSystemLocalRecoveryArchiveStoreTests()
    {
        _dataDirectory = Path.Combine(_root, "VaultProspector");
        _recoveryRoot = Path.Combine(_root, "VaultProspector-Recovery");
        Directory.CreateDirectory(_dataDirectory);
        Directory.CreateDirectory(_recoveryRoot);
    }

    [Fact]
    public async Task ListReportsOnlyGeneratedArchivesWithSafeMetadata()
    {
        var resetId =
            $"20260723-120102003-{Guid.NewGuid():N}";
        var rotationId =
            $"rotation-20260722-110102003-{Guid.NewGuid():N}";
        var failedId =
            $"failed-rotation-20260721-100102003-{Guid.NewGuid():N}";
        WriteArchive(resetId, 13);
        WriteArchive(rotationId, 17);
        WriteArchive(failedId, 19);
        WriteArchive("operator-notes", 23);

        var archives = await Store().ListAsync(
            TestContext.Current.CancellationToken);

        Assert.Collection(
            archives,
            archive =>
            {
                Assert.Equal(resetId, archive.Id);
                Assert.Equal(
                    LocalRecoveryArchiveKind.Reset,
                    archive.Kind);
                Assert.Equal(13, archive.SizeBytes);
                Assert.Equal(
                    new DateTimeOffset(
                        2026,
                        7,
                        23,
                        12,
                        1,
                        2,
                        3,
                        TimeSpan.Zero),
                    archive.CreatedAtUtc);
            },
            archive =>
            {
                Assert.Equal(rotationId, archive.Id);
                Assert.Equal(
                    LocalRecoveryArchiveKind.Rotation,
                    archive.Kind);
                Assert.Equal(17, archive.SizeBytes);
            },
            archive =>
            {
                Assert.Equal(failedId, archive.Id);
                Assert.Equal(
                    LocalRecoveryArchiveKind.FailedRotation,
                    archive.Kind);
                Assert.Equal(19, archive.SizeBytes);
            });
    }

    [Fact]
    public async Task DeleteRemovesOnlyTheExactSelectedArchive()
    {
        var selectedId =
            $"20260723-120102003-{Guid.NewGuid():N}";
        var retainedId =
            $"rotation-20260722-110102003-{Guid.NewGuid():N}";
        WriteArchive(selectedId, 13);
        WriteArchive(retainedId, 17);

        await Store().DeleteAsync(
            selectedId,
            TestContext.Current.CancellationToken);

        Assert.False(
            Directory.Exists(
                Path.Combine(_recoveryRoot, selectedId)));
        Assert.True(
            Directory.Exists(
                Path.Combine(_recoveryRoot, retainedId)));
        Assert.Empty(
            Directory.EnumerateDirectories(
                _recoveryRoot,
                ".deleting-*",
                SearchOption.TopDirectoryOnly));
    }

    [Fact]
    public async Task DeleteRejectsNonGeneratedIdentifier()
    {
        var outside = Path.Combine(_root, "outside");
        Directory.CreateDirectory(outside);

        await Assert.ThrowsAsync<LocalRecoveryArchiveValidationException>(
            () => Store().DeleteAsync(
                "..",
                TestContext.Current.CancellationToken));

        Assert.True(Directory.Exists(outside));
    }

    [Fact]
    public async Task DeleteRefusesWhileRotationRecoveryIsPending()
    {
        var archiveId =
            $"rotation-20260722-110102003-{Guid.NewGuid():N}";
        WriteArchive(archiveId, 17);
        await File.WriteAllTextAsync(
            Path.Combine(
                _dataDirectory,
                "local-encryption.rotation"),
            "pending",
            TestContext.Current.CancellationToken);

        var exception =
            await Assert.ThrowsAsync<LocalRecoveryArchiveValidationException>(
                () => Store().DeleteAsync(
                    archiveId,
                    TestContext.Current.CancellationToken));

        Assert.Contains(
            "pending",
            exception.Message,
            StringComparison.OrdinalIgnoreCase);
        Assert.True(
            Directory.Exists(
                Path.Combine(_recoveryRoot, archiveId)));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, true);
    }

    private FileSystemLocalRecoveryArchiveStore Store() =>
        new(_dataDirectory);

    private void WriteArchive(
        string archiveId,
        int size)
    {
        var path = Path.Combine(_recoveryRoot, archiveId);
        Directory.CreateDirectory(
            Path.Combine(path, "nested"));
        File.WriteAllBytes(
            Path.Combine(path, "nested", "state.bin"),
            new byte[size]);
    }
}
