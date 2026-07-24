using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using VaultProspector.Application;

namespace VaultProspector.Platform;

public sealed class WindowsDataProtectionKeyProvider(string keyDirectory) : IRotatableKeyMaterialProvider
{
    private static readonly HashSet<string> ReservedFileNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "con", "prn", "aux", "nul",
        "com1", "com2", "com3", "com4", "com5", "com6", "com7", "com8", "com9",
        "lpt1", "lpt2", "lpt3", "lpt4", "lpt5", "lpt6", "lpt7", "lpt8", "lpt9",
    };

    public bool IsAvailable => OperatingSystem.IsWindows();

    public async Task<byte[]> GetOrCreateKeyAsync(string purpose, CancellationToken cancellationToken)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Windows Data Protection API is unavailable.");
        ValidatePurpose(purpose);
        Directory.CreateDirectory(keyDirectory);
        var path = Path.Combine(keyDirectory, $"{purpose}.key");
        if (File.Exists(path))
            return await ReadAndUnprotectAsync(path, purpose, cancellationToken);

        var entropy = Entropy(purpose);
        var key = RandomNumberGenerator.GetBytes(32);
        var temporaryPath = $"{path}.{Guid.NewGuid():N}.tmp";
        byte[]? encrypted = null;
        var published = false;
        try
        {
            encrypted = ProtectedData.Protect(key, entropy, DataProtectionScope.CurrentUser);
            await File.WriteAllBytesAsync(temporaryPath, encrypted, cancellationToken);
            try
            {
                File.Move(temporaryPath, path, false);
                published = true;
                return key;
            }
            catch (IOException) when (File.Exists(path))
            {
                // Another process published a complete key first. Discard this candidate and
                // use the winner so concurrent starts cannot split encrypted state.
                var protectedKey = await ReadPublishedKeyAsync(path, cancellationToken);
                try
                {
                    return ProtectedData.Unprotect(protectedKey, entropy, DataProtectionScope.CurrentUser);
                }
                finally
                {
                    CryptographicOperations.ZeroMemory(protectedKey);
                }
            }
        }
        finally
        {
            if (encrypted is not null) CryptographicOperations.ZeroMemory(encrypted);
            CryptographicOperations.ZeroMemory(entropy);
            if (!published) CryptographicOperations.ZeroMemory(key);
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
        }
    }

    public async Task<byte[]> GetExistingKeyAsync(string purpose, CancellationToken cancellationToken)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Windows Data Protection API is unavailable.");
        ValidatePurpose(purpose);
        var path = Path.Combine(keyDirectory, $"{purpose}.key");
        if (!File.Exists(path))
            throw new ProtectedKeyUnavailableException("The required platform-protected key does not exist.");

        return await ReadAndUnprotectAsync(path, purpose, cancellationToken);
    }

    public bool HasKey(string purpose)
    {
        ValidatePurpose(purpose);
        return File.Exists(KeyPath(purpose));
    }

    public async Task<StagedKeyRotation> StageReplacementAsync(
        string purpose,
        Guid rotationId,
        CancellationToken cancellationToken)
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("Windows Data Protection API is unavailable.");
        ValidatePurpose(purpose);
        cancellationToken.ThrowIfCancellationRequested();
        if (!File.Exists(KeyPath(purpose)))
            throw new ProtectedKeyUnavailableException(
                "The key being rotated does not exist.");

        Directory.CreateDirectory(keyDirectory);
        var stagedPath = StagedPath(purpose, rotationId);
        if (File.Exists(stagedPath))
            throw new InvalidOperationException(
                "A staged key already exists for this rotation.");

        var key = RandomNumberGenerator.GetBytes(32);
        var entropy = Entropy(purpose);
        byte[]? protectedKey = null;
        var temporaryPath = $"{stagedPath}.{Guid.NewGuid():N}.tmp";
        try
        {
            protectedKey = ProtectedData.Protect(
                key,
                entropy,
                DataProtectionScope.CurrentUser);
            await File.WriteAllBytesAsync(
                temporaryPath,
                protectedKey,
                cancellationToken);
            File.Move(temporaryPath, stagedPath, false);
            return new StagedKeyRotation(purpose, rotationId, key);
        }
        catch
        {
            CryptographicOperations.ZeroMemory(key);
            throw;
        }
        finally
        {
            if (protectedKey is not null)
                CryptographicOperations.ZeroMemory(protectedKey);
            CryptographicOperations.ZeroMemory(entropy);
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }
    }

    public Task PublishReplacementAsync(
        StagedKeyRotation rotation,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidatePurpose(rotation.Purpose);
        var currentPath = KeyPath(rotation.Purpose);
        var stagedPath = StagedPath(rotation.Purpose, rotation.RotationId);
        var previousPath = PreviousPath(rotation.Purpose, rotation.RotationId);
        if (!File.Exists(currentPath))
            throw new ProtectedKeyUnavailableException(
                "The current key disappeared before replacement publication.");
        if (!File.Exists(stagedPath))
            throw new ProtectedKeyUnavailableException(
                "The staged replacement key disappeared before publication.");
        if (File.Exists(previousPath))
            throw new InvalidOperationException(
                "The previous-key slot is already occupied for this rotation.");

        File.Move(currentPath, previousPath, false);
        File.Move(stagedPath, currentPath, false);
        return Task.CompletedTask;
    }

    public Task CompleteReplacementAsync(
        string purpose,
        Guid rotationId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidatePurpose(purpose);
        DeleteIfPresent(PreviousPath(purpose, rotationId));
        DeleteIfPresent(StagedPath(purpose, rotationId));
        return Task.CompletedTask;
    }

    public Task AbortReplacementAsync(
        string purpose,
        Guid rotationId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidatePurpose(purpose);
        DeleteIfPresent(StagedPath(purpose, rotationId));
        return Task.CompletedTask;
    }

    private static async Task<byte[]> ReadAndUnprotectAsync(string path, string purpose, CancellationToken cancellationToken)
    {
        var entropy = Entropy(purpose);
        byte[]? protectedKey = null;
        try
        {
            protectedKey = await ReadPublishedKeyAsync(path, cancellationToken);
            return ProtectedData.Unprotect(protectedKey, entropy, DataProtectionScope.CurrentUser);
        }
        finally
        {
            if (protectedKey is not null) CryptographicOperations.ZeroMemory(protectedKey);
            CryptographicOperations.ZeroMemory(entropy);
        }
    }

    private static byte[] Entropy(string purpose) =>
        SHA256.HashData(Encoding.UTF8.GetBytes($"VaultProspector:{purpose}:v1"));

    private static async Task<byte[]> ReadPublishedKeyAsync(string path, CancellationToken cancellationToken)
    {
        const int maximumAttempts = 8;
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await File.ReadAllBytesAsync(path, cancellationToken);
            }
            catch (IOException) when (attempt < maximumAttempts && File.Exists(path))
            {
                // File.Move publishes the completed key atomically, but Windows can retain an
                // exclusive rename handle for a few milliseconds after the name is visible.
                await Task.Delay(TimeSpan.FromMilliseconds(attempt * 5), cancellationToken);
            }
        }
    }

    private string KeyPath(string purpose) =>
        Path.Combine(keyDirectory, $"{purpose}.key");

    private string StagedPath(string purpose, Guid rotationId) =>
        Path.Combine(keyDirectory, $"{purpose}.{rotationId:N}.next");

    private string PreviousPath(string purpose, Guid rotationId) =>
        Path.Combine(keyDirectory, $"{purpose}.{rotationId:N}.previous");

    private static void DeleteIfPresent(string path)
    {
        if (File.Exists(path))
            File.Delete(path);
    }

    private static void ValidatePurpose(string purpose)
    {
        if (string.IsNullOrWhiteSpace(purpose) || purpose.Length > 64 ||
            !IsLowercaseAsciiLetterOrDigit(purpose[0]) ||
            purpose.Any(character => !IsLowercaseAsciiLetterOrDigit(character) && character is not '-' and not '_') ||
            ReservedFileNames.Contains(purpose))
        {
            throw new ArgumentException("Key purpose must be 1-64 lowercase ASCII letters, digits, hyphens, or underscores, start alphanumerically, and not be a reserved Windows filename.", nameof(purpose));
        }
    }

    private static bool IsLowercaseAsciiLetterOrDigit(char character) =>
        character is >= 'a' and <= 'z' or >= '0' and <= '9';
}

public static class VaultProspectorPaths
{
    public static string DataDirectory
    {
        get
        {
            var root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(root, "VaultProspector");
        }
    }

    public static string DatabasePath => Path.Combine(DataDirectory, "vault-prospector.db");
    public static string KeyDirectory => Path.Combine(DataDirectory, "keys");
    public static string CacheDirectory => Path.Combine(DataDirectory, "protected-values");
    public static string LogPath => Path.Combine(DataDirectory, "logs", "vault-prospector.log");
    public static string TokenCacheDirectory => Path.Combine(DataDirectory, "identity-cache");
}

public sealed class FileSystemLocalDataResetter(string dataDirectory) : ILocalDataResetter
{
    public Task<LocalDataArchive> ArchiveForResetAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var source = Path.GetFullPath(dataDirectory);
        if (!Directory.Exists(source) || !Directory.EnumerateFileSystemEntries(source).Any())
        {
            Directory.CreateDirectory(source);
            return Task.FromResult(new LocalDataArchive(string.Empty, false));
        }

        var parent = Directory.GetParent(source)?.FullName
            ?? throw new InvalidOperationException("The local data directory has no parent.");
        var recoveryRoot = Path.Combine(parent, $"{Path.GetFileName(source)}-Recovery");
        Directory.CreateDirectory(recoveryRoot);
        var archivePath = Path.Combine(
            recoveryRoot,
            $"{DateTimeOffset.UtcNow:yyyyMMdd-HHmmssfff}-{Guid.NewGuid():N}");

        Directory.Move(source, archivePath);
        try
        {
            Directory.CreateDirectory(source);
        }
        catch
        {
            Directory.Move(archivePath, source);
            throw;
        }

        return Task.FromResult(new LocalDataArchive(archivePath, true));
    }
}

public sealed class FileSystemLocalRecoveryArchiveStore(
    string dataDirectory) : ILocalRecoveryArchiveStore
{
    private const string ActiveRotationJournalFileName =
        "local-encryption.rotation";

    public Task<IReadOnlyList<LocalRecoveryArchive>> ListAsync(
        CancellationToken cancellationToken) =>
        Task.Run<IReadOnlyList<LocalRecoveryArchive>>(
            () => List(cancellationToken),
            cancellationToken);

    public Task DeleteAsync(
        string archiveId,
        CancellationToken cancellationToken) =>
        Task.Run(
            () => Delete(archiveId, cancellationToken),
            cancellationToken);

    private LocalRecoveryArchive[] List(
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var recoveryRoot = RecoveryRoot();
        if (!Directory.Exists(recoveryRoot))
            return [];

        EnsureOrdinaryDirectory(recoveryRoot);
        var archives = new List<LocalRecoveryArchive>();
        foreach (var directory in Directory.EnumerateDirectories(
                     recoveryRoot,
                     "*",
                     SearchOption.TopDirectoryOnly))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var archiveId = Path.GetFileName(directory);
            if (!TryParseGeneratedArchive(
                    archiveId,
                    out var kind,
                    out var createdAtUtc))
            {
                continue;
            }

            var archivePath = ResolveArchivePath(
                recoveryRoot,
                archiveId);
            EnsureOrdinaryDirectory(archivePath);
            archives.Add(new LocalRecoveryArchive(
                archiveId,
                kind,
                createdAtUtc,
                MeasureTree(archivePath, cancellationToken)));
        }

        return archives
            .OrderByDescending(archive => archive.CreatedAtUtc)
            .ThenBy(archive => archive.Id, StringComparer.Ordinal)
            .ToArray();
    }

    private void Delete(
        string archiveId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!TryParseGeneratedArchive(
                archiveId,
                out _,
                out _))
        {
            throw new LocalRecoveryArchiveValidationException(
                "The selected recovery archive identifier is invalid.");
        }

        if (File.Exists(Path.Combine(
                Path.GetFullPath(dataDirectory),
                ActiveRotationJournalFileName)))
        {
            throw new LocalRecoveryArchiveValidationException(
                "A recovery archive cannot be deleted while local encryption recovery is pending.");
        }

        var recoveryRoot = RecoveryRoot();
        EnsureOrdinaryDirectory(recoveryRoot);
        var archivePath = ResolveArchivePath(
            recoveryRoot,
            archiveId);
        if (!Directory.Exists(archivePath))
        {
            throw new DirectoryNotFoundException(
                "The selected recovery archive no longer exists.");
        }

        EnsureOrdinaryDirectory(archivePath);
        _ = MeasureTree(archivePath, cancellationToken);

        var quarantinePath = Path.Combine(
            recoveryRoot,
            $".deleting-{Guid.NewGuid():N}");
        EnsureDirectChild(recoveryRoot, quarantinePath);
        Directory.Move(archivePath, quarantinePath);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            EnsureOrdinaryDirectory(quarantinePath);
            _ = MeasureTree(quarantinePath, cancellationToken);
            Directory.Delete(quarantinePath, true);
        }
        catch
        {
            if (Directory.Exists(quarantinePath) &&
                !Directory.Exists(archivePath))
            {
                Directory.Move(quarantinePath, archivePath);
            }

            throw;
        }
    }

    private static long MeasureTree(
        string root,
        CancellationToken cancellationToken)
    {
        long total = 0;
        var pending = new Stack<DirectoryInfo>();
        pending.Push(new DirectoryInfo(root));
        while (pending.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var current = pending.Pop();
            EnsureOrdinaryFileSystemEntry(current);
            foreach (var entry in current.EnumerateFileSystemInfos())
            {
                cancellationToken.ThrowIfCancellationRequested();
                EnsureOrdinaryFileSystemEntry(entry);
                if (entry is DirectoryInfo directory)
                {
                    pending.Push(directory);
                    continue;
                }

                if (entry is FileInfo file)
                    total = checked(total + file.Length);
            }
        }

        return total;
    }

    private static void EnsureOrdinaryDirectory(string path)
    {
        if (!Directory.Exists(path))
            return;
        EnsureOrdinaryFileSystemEntry(new DirectoryInfo(path));
    }

    private static void EnsureOrdinaryFileSystemEntry(
        FileSystemInfo entry)
    {
        entry.Refresh();
        if ((entry.Attributes & FileAttributes.ReparsePoint) != 0)
        {
            throw new LocalRecoveryArchiveValidationException(
                "A recovery archive contains an unsupported reparse point.");
        }
    }

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

    private static string ResolveArchivePath(
        string recoveryRoot,
        string archiveId)
    {
        var path = Path.GetFullPath(
            Path.Combine(recoveryRoot, archiveId));
        EnsureDirectChild(recoveryRoot, path);
        return path;
    }

    private static void EnsureDirectChild(
        string recoveryRoot,
        string candidate)
    {
        var normalizedRoot =
            Path.TrimEndingDirectorySeparator(
                Path.GetFullPath(recoveryRoot));
        var normalizedCandidate =
            Path.TrimEndingDirectorySeparator(
                Path.GetFullPath(candidate));
        if (!string.Equals(
                Path.GetDirectoryName(normalizedCandidate),
                normalizedRoot,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new LocalRecoveryArchiveValidationException(
                "A recovery archive path escaped its recovery directory.");
        }
    }

    private static bool TryParseGeneratedArchive(
        string archiveId,
        out LocalRecoveryArchiveKind kind,
        out DateTimeOffset createdAtUtc)
    {
        kind = LocalRecoveryArchiveKind.Reset;
        createdAtUtc = default;
        var remainder = archiveId;
        if (archiveId.StartsWith(
                "failed-rotation-",
                StringComparison.Ordinal))
        {
            kind = LocalRecoveryArchiveKind.FailedRotation;
            remainder = archiveId["failed-rotation-".Length..];
        }
        else if (archiveId.StartsWith(
                     "rotation-",
                     StringComparison.Ordinal))
        {
            kind = LocalRecoveryArchiveKind.Rotation;
            remainder = archiveId["rotation-".Length..];
        }

        var components = remainder.Split(
            '-',
            StringSplitOptions.RemoveEmptyEntries);
        if (components.Length != 3 ||
            components[0].Length != 8 ||
            components[1].Length != 9 ||
            components[2].Length != 32 ||
            !Guid.TryParseExact(components[2], "N", out _))
        {
            return false;
        }

        return DateTimeOffset.TryParseExact(
            $"{components[0]}-{components[1]}",
            "yyyyMMdd-HHmmssfff",
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal |
            DateTimeStyles.AdjustToUniversal,
            out createdAtUtc);
    }
}
