using System.Security.Cryptography;
using System.Text;
using VaultProspector.Application;

namespace VaultProspector.Platform;

public sealed class WindowsDataProtectionKeyProvider(string keyDirectory) : IKeyMaterialProvider
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
        var entropy = SHA256.HashData(Encoding.UTF8.GetBytes($"VaultProspector:{purpose}:v1"));
        if (File.Exists(path))
        {
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
