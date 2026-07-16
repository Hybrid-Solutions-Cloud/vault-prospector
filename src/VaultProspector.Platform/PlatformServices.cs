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
            var protectedKey = await File.ReadAllBytesAsync(path, cancellationToken);
            return ProtectedData.Unprotect(protectedKey, entropy, DataProtectionScope.CurrentUser);
        }

        var key = RandomNumberGenerator.GetBytes(32);
        var encrypted = ProtectedData.Protect(key, entropy, DataProtectionScope.CurrentUser);
        await File.WriteAllBytesAsync(path, encrypted, cancellationToken);
        return key;
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
