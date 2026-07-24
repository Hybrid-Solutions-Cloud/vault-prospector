using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using VaultProspector.Application;

namespace VaultProspector.Infrastructure;

public enum LocalEncryptionRotationCheckpoint
{
    ArchivePublished,
    JournalPublished,
    KeysStaged,
    DatabaseRekeyed,
    OfflineValuesReencrypted,
    MetadataKeyPublished,
    OfflineKeyPublished,
    ReplacementValidated,
    OldKeysRemoved,
}

public interface ILocalEncryptionRotationFaultInjector
{
    void Reached(LocalEncryptionRotationCheckpoint checkpoint);
}

public sealed class LocalEncryptionRotationEngine(
    string dataDirectory,
    string databasePath,
    string protectedValueDirectory,
    IRotatableKeyMaterialProvider keyMaterial,
    IClock clock,
    ILocalEncryptionRotationFaultInjector? faultInjector = null) : ILocalEncryptionRotationEngine
{
    private const int MaximumAuthenticatedDocumentBytes = 16 * 1024 * 1024;
    private const int JournalVersion = 1;
    private const string MetadataPurpose = "metadata-database";
    private const string OfflinePurpose = "offline-values-v2";
    private const string JournalPurpose = "rotation-journal";
    private const string JournalFileName = "local-encryption.rotation";
    private const string ManifestFileName = ".rotation-archive.manifest";
    private readonly ILocalEncryptionRotationFaultInjector _faultInjector =
        faultInjector ?? new NoFaultInjector();

    public async Task<LocalEncryptionRotationResult> RotateAsync(
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!keyMaterial.IsAvailable)
            throw new PlatformNotSupportedException(
                "Platform-protected key storage is required for local encryption rotation.");
        if (!File.Exists(databasePath))
            throw new InvalidOperationException(
                "The encrypted metadata database does not exist.");
        if (File.Exists(JournalPath()))
            throw new InvalidOperationException(
                "A previous local encryption rotation requires recovery.");

        var rotationId = Guid.NewGuid();
        byte[]? currentMetadataKey = null;
        byte[]? currentOfflineKey = null;
        byte[]? journalKey = null;
        StagedKeyRotation? stagedMetadata = null;
        StagedKeyRotation? stagedOffline = null;
        var journalPublished = false;
        try
        {
            currentMetadataKey = await keyMaterial.GetExistingKeyAsync(
                MetadataPurpose,
                cancellationToken);
            await CheckpointAndValidateDatabaseAsync(
                currentMetadataKey,
                cancellationToken);
            journalKey = await keyMaterial.GetOrCreateKeyAsync(
                JournalPurpose,
                cancellationToken);
            var archivePath = await CreateVerifiedArchiveAsync(
                rotationId,
                journalKey,
                cancellationToken);
            _faultInjector.Reached(
                LocalEncryptionRotationCheckpoint.ArchivePublished);

            var journal = new RotationJournal(
                JournalVersion,
                rotationId,
                Path.GetFileName(archivePath),
                "ArchivePublished");
            await WriteAuthenticatedJsonAsync(
                JournalPath(),
                journal,
                journalKey,
                cancellationToken);
            journalPublished = true;
            _faultInjector.Reached(
                LocalEncryptionRotationCheckpoint.JournalPublished);

            stagedMetadata = await keyMaterial.StageReplacementAsync(
                MetadataPurpose,
                rotationId,
                cancellationToken);
            if (keyMaterial.HasKey(OfflinePurpose))
            {
                currentOfflineKey = await keyMaterial.GetExistingKeyAsync(
                    OfflinePurpose,
                    cancellationToken);
                stagedOffline = await keyMaterial.StageReplacementAsync(
                    OfflinePurpose,
                    rotationId,
                    cancellationToken);
            }
            else if (Directory.Exists(protectedValueDirectory) &&
                Directory.EnumerateFiles(
                    protectedValueDirectory,
                    "*.vpcache").Any())
            {
                throw new ProtectedKeyUnavailableException(
                    "Offline values exist without their required protected key.");
            }

            await UpdateJournalAsync(
                journal with { Stage = "KeysStaged" },
                journalKey,
                cancellationToken);
            _faultInjector.Reached(
                LocalEncryptionRotationCheckpoint.KeysStaged);

            var replacementMetadataKey = stagedMetadata.ExportKey();
            try
            {
                await RekeyDatabaseAsync(
                    currentMetadataKey,
                    replacementMetadataKey,
                    cancellationToken);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(replacementMetadataKey);
            }
            await UpdateJournalAsync(
                journal with { Stage = "DatabaseRekeyed" },
                journalKey,
                cancellationToken);
            _faultInjector.Reached(
                LocalEncryptionRotationCheckpoint.DatabaseRekeyed);

            var offlineValuesRotated = 0;
            if (stagedOffline is not null && currentOfflineKey is not null)
            {
                var replacementOfflineKey = stagedOffline.ExportKey();
                try
                {
                    var store = new EncryptedFileValueStore(
                        protectedValueDirectory,
                        keyMaterial,
                        clock);
                    offlineValuesRotated = await store.RotateEncryptionKeyAsync(
                        currentOfflineKey,
                        replacementOfflineKey,
                        cancellationToken);
                }
                finally
                {
                    CryptographicOperations.ZeroMemory(replacementOfflineKey);
                }
            }
            await UpdateJournalAsync(
                journal with { Stage = "OfflineValuesReencrypted" },
                journalKey,
                cancellationToken);
            _faultInjector.Reached(
                LocalEncryptionRotationCheckpoint.OfflineValuesReencrypted);

            await keyMaterial.PublishReplacementAsync(
                stagedMetadata,
                cancellationToken);
            await UpdateJournalAsync(
                journal with { Stage = "MetadataKeyPublished" },
                journalKey,
                cancellationToken);
            _faultInjector.Reached(
                LocalEncryptionRotationCheckpoint.MetadataKeyPublished);

            if (stagedOffline is not null)
            {
                await keyMaterial.PublishReplacementAsync(
                    stagedOffline,
                    cancellationToken);
            }
            await UpdateJournalAsync(
                journal with { Stage = "OfflineKeyPublished" },
                journalKey,
                cancellationToken);
            _faultInjector.Reached(
                LocalEncryptionRotationCheckpoint.OfflineKeyPublished);

            var publishedMetadataKey = await keyMaterial.GetExistingKeyAsync(
                MetadataPurpose,
                cancellationToken);
            try
            {
                await ValidateDatabaseAsync(
                    publishedMetadataKey,
                    cancellationToken);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(publishedMetadataKey);
            }
            if (stagedOffline is not null)
            {
                var publishedOfflineKey = await keyMaterial.GetExistingKeyAsync(
                    OfflinePurpose,
                    cancellationToken);
                try
                {
                    var store = new EncryptedFileValueStore(
                        protectedValueDirectory,
                        keyMaterial,
                        clock);
                    await store.ValidateAllWithKeyAsync(
                        publishedOfflineKey,
                        cancellationToken);
                }
                finally
                {
                    CryptographicOperations.ZeroMemory(publishedOfflineKey);
                }
            }
            await UpdateJournalAsync(
                journal with { Stage = "ReplacementValidated" },
                journalKey,
                cancellationToken);
            _faultInjector.Reached(
                LocalEncryptionRotationCheckpoint.ReplacementValidated);

            await keyMaterial.CompleteReplacementAsync(
                MetadataPurpose,
                rotationId,
                cancellationToken);
            if (stagedOffline is not null)
            {
                await keyMaterial.CompleteReplacementAsync(
                    OfflinePurpose,
                    rotationId,
                    cancellationToken);
            }
            await UpdateJournalAsync(
                journal with { Stage = "OldKeysRemoved" },
                journalKey,
                cancellationToken);
            _faultInjector.Reached(
                LocalEncryptionRotationCheckpoint.OldKeysRemoved);

            File.Delete(JournalPath());
            return new LocalEncryptionRotationResult(
                rotationId,
                archivePath,
                offlineValuesRotated);
        }
        catch
        {
            if (!journalPublished)
            {
                await keyMaterial.AbortReplacementAsync(
                    MetadataPurpose,
                    rotationId,
                    CancellationToken.None);
                await keyMaterial.AbortReplacementAsync(
                    OfflinePurpose,
                    rotationId,
                    CancellationToken.None);
            }

            throw;
        }
        finally
        {
            stagedMetadata?.Dispose();
            stagedOffline?.Dispose();
            if (currentMetadataKey is not null)
                CryptographicOperations.ZeroMemory(currentMetadataKey);
            if (currentOfflineKey is not null)
                CryptographicOperations.ZeroMemory(currentOfflineKey);
            if (journalKey is not null)
                CryptographicOperations.ZeroMemory(journalKey);
        }
    }

    public async Task<LocalEncryptionRecoveryResult> RecoverIfNeededAsync(
        CancellationToken cancellationToken)
    {
        var journalPath = JournalPath();
        if (!File.Exists(journalPath))
            return new LocalEncryptionRecoveryResult(false);

        var journalKey = await keyMaterial.GetExistingKeyAsync(
            JournalPurpose,
            cancellationToken);
        try
        {
            var journal = await ReadAuthenticatedJsonAsync<RotationJournal>(
                journalPath,
                journalKey,
                cancellationToken);
            if (journal.Version != JournalVersion ||
                journal.RotationId == Guid.Empty ||
                !IsSafeArchiveName(journal.ArchiveDirectoryName))
            {
                throw new LocalDataIntegrityException(
                    "The local encryption rotation journal is invalid.");
            }

            var recoveryRoot = RecoveryRoot();
            var archivePath = Path.GetFullPath(
                Path.Combine(recoveryRoot, journal.ArchiveDirectoryName));
            EnsureChildPath(recoveryRoot, archivePath);
            if (!Directory.Exists(archivePath))
                throw new LocalDataIntegrityException(
                    "The pre-rotation recovery archive is missing.");
            await ValidateArchiveAsync(
                archivePath,
                journalKey,
                cancellationToken);

            var source = Path.GetFullPath(dataDirectory);
            var failedPath = Path.Combine(
                recoveryRoot,
                $"failed-rotation-{clock.UtcNow:yyyyMMdd-HHmmssfff}-{journal.RotationId:N}");
            EnsureChildPath(recoveryRoot, failedPath);
            await MoveDirectoryWithTransientRetryAsync(
                source,
                failedPath,
                cancellationToken);
            try
            {
                await MoveDirectoryWithTransientRetryAsync(
                    archivePath,
                    source,
                    CancellationToken.None);
            }
            catch
            {
                await MoveDirectoryWithTransientRetryAsync(
                    failedPath,
                    source,
                    CancellationToken.None);
                throw;
            }

            var restoredManifest = Path.Combine(source, ManifestFileName);
            if (File.Exists(restoredManifest))
                File.Delete(restoredManifest);
            return new LocalEncryptionRecoveryResult(true, failedPath);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(journalKey);
        }
    }

    private async Task<string> CreateVerifiedArchiveAsync(
        Guid rotationId,
        byte[] journalKey,
        CancellationToken cancellationToken)
    {
        var source = Path.GetFullPath(dataDirectory);
        var recoveryRoot = RecoveryRoot();
        Directory.CreateDirectory(recoveryRoot);
        var archiveName =
            $"rotation-{clock.UtcNow:yyyyMMdd-HHmmssfff}-{rotationId:N}";
        var temporaryPath = Path.Combine(
            recoveryRoot,
            $"{archiveName}.tmp");
        var archivePath = Path.Combine(recoveryRoot, archiveName);
        EnsureChildPath(recoveryRoot, temporaryPath);
        EnsureChildPath(recoveryRoot, archivePath);
        if (Directory.Exists(temporaryPath) || Directory.Exists(archivePath))
            throw new InvalidOperationException(
                "The rotation recovery archive target already exists.");

        var files = EnumerateArchiveFiles(source);
        EnsureFreeSpace(recoveryRoot, files.Sum(file => file.Length));
        var manifestEntries = new List<ArchiveManifestEntry>(files.Count);
        try
        {
            Directory.CreateDirectory(temporaryPath);
            foreach (var file in files)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var destination = Path.Combine(
                    temporaryPath,
                    file.RelativePath);
                Directory.CreateDirectory(
                    Path.GetDirectoryName(destination) ?? temporaryPath);
                File.Copy(file.FullPath, destination, false);
                var sourceHash = await HashFileAsync(
                    file.FullPath,
                    cancellationToken);
                var destinationHash = await HashFileAsync(
                    destination,
                    cancellationToken);
                if (!CryptographicOperations.FixedTimeEquals(
                    sourceHash,
                    destinationHash))
                {
                    throw new LocalDataIntegrityException(
                        "A local encryption recovery archive file failed verification.");
                }
                manifestEntries.Add(new ArchiveManifestEntry(
                    file.RelativePath,
                    file.Length,
                    Convert.ToHexString(destinationHash)));
                CryptographicOperations.ZeroMemory(sourceHash);
                CryptographicOperations.ZeroMemory(destinationHash);
            }

            var manifest = new ArchiveManifest(
                JournalVersion,
                rotationId,
                manifestEntries);
            await WriteAuthenticatedJsonAsync(
                Path.Combine(temporaryPath, ManifestFileName),
                manifest,
                journalKey,
                cancellationToken);
            await MoveDirectoryWithTransientRetryAsync(
                temporaryPath,
                archivePath,
                cancellationToken);
        }
        finally
        {
            if (Directory.Exists(temporaryPath))
                Directory.Delete(temporaryPath, true);
        }

        return archivePath;
    }

    private static async Task ValidateArchiveAsync(
        string archivePath,
        byte[] journalKey,
        CancellationToken cancellationToken)
    {
        var manifestPath = Path.Combine(archivePath, ManifestFileName);
        var manifest = await ReadAuthenticatedJsonAsync<ArchiveManifest>(
            manifestPath,
            journalKey,
            cancellationToken);
        if (manifest.Version != JournalVersion ||
            manifest.Files.Count == 0)
        {
            throw new LocalDataIntegrityException(
                "The rotation recovery archive manifest is invalid.");
        }

        var expectedFiles = manifest.Files
            .Select(file => file.RelativePath)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var actualFiles = Directory.EnumerateFiles(
                archivePath,
                "*",
                SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(archivePath, path))
            .Where(path => !string.Equals(
                path,
                ManifestFileName,
                StringComparison.OrdinalIgnoreCase))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (!expectedFiles.SetEquals(actualFiles))
            throw new LocalDataIntegrityException(
                "The rotation recovery archive file set changed.");

        foreach (var file in manifest.Files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var fullPath = Path.GetFullPath(
                Path.Combine(archivePath, file.RelativePath));
            EnsureChildPath(archivePath, fullPath);
            var info = new FileInfo(fullPath);
            if (!info.Exists || info.Length != file.Length)
                throw new LocalDataIntegrityException(
                    "A rotation recovery archive file changed.");
            var hash = await HashFileAsync(fullPath, cancellationToken);
            byte[]? expectedHash = null;
            try
            {
                expectedHash = Convert.FromHexString(file.Sha256);
                if (!CryptographicOperations.FixedTimeEquals(
                    hash,
                    expectedHash))
                {
                    throw new LocalDataIntegrityException(
                        "A rotation recovery archive file failed integrity validation.");
                }
            }
            catch (FormatException exception)
            {
                throw new LocalDataIntegrityException(
                    "The rotation recovery archive manifest contains an invalid hash.",
                    exception);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(hash);
                if (expectedHash is not null)
                    CryptographicOperations.ZeroMemory(expectedHash);
            }
        }
    }

    private async Task UpdateJournalAsync(
        RotationJournal journal,
        byte[] journalKey,
        CancellationToken cancellationToken) =>
        await WriteAuthenticatedJsonAsync(
            JournalPath(),
            journal,
            journalKey,
            cancellationToken);

    private static async Task WriteAuthenticatedJsonAsync<T>(
        string path,
        T payload,
        byte[] key,
        CancellationToken cancellationToken)
    {
        var payloadBytes = JsonSerializer.SerializeToUtf8Bytes(payload);
        var mac = HMACSHA256.HashData(key, payloadBytes);
        var wrapper = new AuthenticatedDocument(
            Convert.ToBase64String(payloadBytes),
            Convert.ToBase64String(mac));
        var temporaryPath = $"{path}.{Guid.NewGuid():N}.tmp";
        try
        {
            Directory.CreateDirectory(
                Path.GetDirectoryName(path) ?? ".");
            await File.WriteAllTextAsync(
                temporaryPath,
                JsonSerializer.Serialize(wrapper),
                cancellationToken);
            await MoveFileWithTransientRetryAsync(
                temporaryPath,
                path,
                cancellationToken);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(payloadBytes);
            CryptographicOperations.ZeroMemory(mac);
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }
    }

    private static async Task MoveFileWithTransientRetryAsync(
        string source,
        string destination,
        CancellationToken cancellationToken)
    {
        const int maximumAttempts = 7;
        for (var attempt = 1; ; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                File.Move(source, destination, true);
                return;
            }
            catch (Exception exception)
                when (attempt < maximumAttempts &&
                      exception is IOException or UnauthorizedAccessException)
            {
                await Task.Delay(
                    TimeSpan.FromMilliseconds(25 * attempt),
                    cancellationToken);
            }
        }
    }

    private static async Task MoveDirectoryWithTransientRetryAsync(
        string source,
        string destination,
        CancellationToken cancellationToken)
    {
        const int maximumAttempts = 10;
        for (var attempt = 1; ; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                Directory.Move(source, destination);
                return;
            }
            catch (Exception exception)
                when (attempt < maximumAttempts &&
                      exception is IOException or UnauthorizedAccessException)
            {
                await Task.Delay(
                    TimeSpan.FromMilliseconds(50 * attempt),
                    cancellationToken);
            }
        }
    }

    private static async Task<T> ReadAuthenticatedJsonAsync<T>(
        string path,
        byte[] key,
        CancellationToken cancellationToken)
    {
        AuthenticatedDocument? wrapper;
        try
        {
            await using var stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                4096,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            if (stream.Length > MaximumAuthenticatedDocumentBytes)
            {
                throw new LocalDataIntegrityException(
                    "An authenticated local rotation record exceeds the safe size limit.");
            }
            wrapper = await JsonSerializer.DeserializeAsync<AuthenticatedDocument>(
                stream,
                cancellationToken: cancellationToken);
        }
        catch (JsonException exception)
        {
            throw new LocalDataIntegrityException(
                "An authenticated local rotation record is malformed.",
                exception);
        }
        if (wrapper is null ||
            string.IsNullOrWhiteSpace(wrapper.Payload) ||
            string.IsNullOrWhiteSpace(wrapper.Mac))
        {
            throw new LocalDataIntegrityException(
                "An authenticated local rotation record is incomplete.");
        }

        byte[] payloadBytes;
        byte[] observedMac;
        try
        {
            payloadBytes = Convert.FromBase64String(wrapper.Payload);
            observedMac = Convert.FromBase64String(wrapper.Mac);
        }
        catch (FormatException exception)
        {
            throw new LocalDataIntegrityException(
                "An authenticated local rotation record has invalid encoding.",
                exception);
        }
        var expectedMac = HMACSHA256.HashData(key, payloadBytes);
        try
        {
            if (!CryptographicOperations.FixedTimeEquals(
                expectedMac,
                observedMac))
            {
                throw new LocalDataIntegrityException(
                    "An authenticated local rotation record failed integrity validation.");
            }
            return JsonSerializer.Deserialize<T>(payloadBytes)
                ?? throw new LocalDataIntegrityException(
                    "An authenticated local rotation record has no payload.");
        }
        catch (JsonException exception)
        {
            throw new LocalDataIntegrityException(
                "An authenticated local rotation payload is malformed.",
                exception);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(payloadBytes);
            CryptographicOperations.ZeroMemory(observedMac);
            CryptographicOperations.ZeroMemory(expectedMac);
        }
    }

    private async Task CheckpointAndValidateDatabaseAsync(
        byte[] key,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenDatabaseAsync(
            key,
            cancellationToken);
        await ExecuteNonQueryAsync(
            connection,
            "PRAGMA wal_checkpoint(TRUNCATE)",
            cancellationToken);
        await ValidateOpenDatabaseAsync(connection, cancellationToken);
    }

    private async Task RekeyDatabaseAsync(
        byte[] currentKey,
        byte[] replacementKey,
        CancellationToken cancellationToken)
    {
        await using (var connection = await OpenDatabaseAsync(
            currentKey,
            cancellationToken))
        {
            var replacement = Convert.ToBase64String(replacementKey);
            await ExecuteNonQueryAsync(
                connection,
                $"PRAGMA rekey = '{replacement}'",
                cancellationToken);
            await ExecuteNonQueryAsync(
                connection,
                "PRAGMA wal_checkpoint(TRUNCATE)",
                cancellationToken);
            await ValidateOpenDatabaseAsync(
                connection,
                cancellationToken);
        }
        await ValidateDatabaseAsync(
            replacementKey,
            cancellationToken);
    }

    private async Task ValidateDatabaseAsync(
        byte[] key,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenDatabaseAsync(
            key,
            cancellationToken);
        await ValidateOpenDatabaseAsync(connection, cancellationToken);
    }

    private async Task<SqliteConnection> OpenDatabaseAsync(
        byte[] key,
        CancellationToken cancellationToken)
    {
        SQLitePCL.Batteries_V2.Init();
        var connection = new SqliteConnection(
            new SqliteConnectionStringBuilder
            {
                DataSource = databasePath,
                Mode = SqliteOpenMode.ReadWrite,
                Password = Convert.ToBase64String(key),
                Pooling = false,
            }.ToString());
        try
        {
            await connection.OpenAsync(cancellationToken);
            return connection;
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }

    private static async Task ValidateOpenDatabaseAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "PRAGMA quick_check";
            var result = Convert.ToString(
                await command.ExecuteScalarAsync(cancellationToken),
                System.Globalization.CultureInfo.InvariantCulture);
            if (!string.Equals(result, "ok", StringComparison.OrdinalIgnoreCase))
                throw new LocalDataIntegrityException(
                    "The encrypted metadata database failed rotation integrity validation.");
        }
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "PRAGMA foreign_key_check";
            await using var reader = await command.ExecuteReaderAsync(
                cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
                throw new LocalDataIntegrityException(
                    "The encrypted metadata database failed rotation relationship validation.");
        }
    }

    private static async Task ExecuteNonQueryAsync(
        SqliteConnection connection,
        string commandText,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = commandText;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static List<ArchiveSourceFile> EnumerateArchiveFiles(
        string source)
    {
        var result = new List<ArchiveSourceFile>();
        var pending = new Stack<string>();
        pending.Push(source);
        while (pending.Count > 0)
        {
            var current = pending.Pop();
            foreach (var path in Directory.EnumerateFileSystemEntries(
                current))
            {
                var attributes = File.GetAttributes(path);
                if ((attributes & FileAttributes.ReparsePoint) != 0)
                    throw new LocalDataIntegrityException(
                        "Local encryption rotation does not follow reparse points.");
                if ((attributes & FileAttributes.Directory) != 0)
                {
                    pending.Push(path);
                    continue;
                }

                var info = new FileInfo(path);
                var relativePath = Path.GetRelativePath(source, path);
                if (string.Equals(
                    relativePath,
                    JournalFileName,
                    StringComparison.OrdinalIgnoreCase) ||
                    relativePath.EndsWith(
                        ".tmp",
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                result.Add(new ArchiveSourceFile(
                    path,
                    relativePath,
                    info.Length));
            }
        }
        if (result.Count == 0)
            throw new InvalidOperationException(
                "There is no local state to archive before rotation.");
        return result;
    }

    private static void EnsureFreeSpace(string path, long sourceBytes)
    {
        var root = Path.GetPathRoot(Path.GetFullPath(path))
            ?? throw new InvalidOperationException(
                "The recovery archive path has no volume root.");
        var available = new DriveInfo(root).AvailableFreeSpace;
        var required = checked(sourceBytes * 2 + 16 * 1024 * 1024);
        if (available < required)
            throw new IOException(
                "Insufficient free space for a verified pre-rotation archive.");
    }

    private static async Task<byte[]> HashFileAsync(
        string path,
        CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            81920,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        return await SHA256.HashDataAsync(stream, cancellationToken);
    }

    private string JournalPath() =>
        Path.Combine(dataDirectory, JournalFileName);

    private string RecoveryRoot()
    {
        var source = Path.GetFullPath(dataDirectory);
        var parent = Directory.GetParent(source)?.FullName
            ?? throw new InvalidOperationException(
                "The local data directory has no parent.");
        return Path.Combine(
            parent,
            $"{Path.GetFileName(source)}-Recovery");
    }

    private static void EnsureChildPath(
        string parent,
        string candidate)
    {
        var normalizedParent =
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(parent)) +
            Path.DirectorySeparatorChar;
        var normalizedCandidate = Path.GetFullPath(candidate);
        if (!normalizedCandidate.StartsWith(
            normalizedParent,
            StringComparison.OrdinalIgnoreCase))
        {
            throw new LocalDataIntegrityException(
                "A local rotation path escaped its recovery directory.");
        }
    }

    private static bool IsSafeArchiveName(string value) =>
        value.Length is >= 40 and <= 96 &&
        value.StartsWith("rotation-", StringComparison.Ordinal) &&
        value.All(character =>
            char.IsAsciiLetterOrDigit(character) ||
            character is '-');

    private sealed record RotationJournal(
        int Version,
        Guid RotationId,
        string ArchiveDirectoryName,
        string Stage);

    private sealed record ArchiveManifest(
        int Version,
        Guid RotationId,
        IReadOnlyList<ArchiveManifestEntry> Files);

    private sealed record ArchiveManifestEntry(
        string RelativePath,
        long Length,
        string Sha256);

    private sealed record AuthenticatedDocument(
        string Payload,
        string Mac);

    private sealed record ArchiveSourceFile(
        string FullPath,
        string RelativePath,
        long Length);

    private sealed class NoFaultInjector :
        ILocalEncryptionRotationFaultInjector
    {
        public void Reached(
            LocalEncryptionRotationCheckpoint checkpoint)
        {
        }
    }
}
