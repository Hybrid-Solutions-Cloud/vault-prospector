using System.Security.Cryptography;
using System.Text;
using VaultProspector.Application;

namespace VaultProspector.Platform;

public sealed class WindowsDataProtectionKeyProvider(string keyDirectory) : IKeyMaterialProvider
{
    public bool IsAvailable => OperatingSystem.IsWindows();

    public async Task<byte[]> GetOrCreateKeyAsync(string purpose, CancellationToken cancellationToken)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Windows Data Protection API is unavailable.");
        Directory.CreateDirectory(keyDirectory);
        var path = Path.Combine(keyDirectory, $"{Sanitize(purpose)}.key");
        var entropy = SHA256.HashData(Encoding.UTF8.GetBytes($"VaultProspector:{purpose}:v1"));
        if (File.Exists(path))
        {
            byte[]? protectedKey = null;
            try
            {
                protectedKey = await File.ReadAllBytesAsync(path, cancellationToken);
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
                var protectedKey = await File.ReadAllBytesAsync(path, cancellationToken);
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

    private static string Sanitize(string value) => string.Concat(value.Where(x => char.IsLetterOrDigit(x) || x is '-' or '_'));
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
