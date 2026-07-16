using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using VaultProspector.Application;
using VaultProspector.Domain;

namespace VaultProspector.Infrastructure;

public sealed class EncryptedFileValueStore(string directory, IKeyMaterialProvider keyMaterial, IClock clock) : IProtectedValueStore
{
    private const int CurrentKeyVersion = 1;
    private sealed record Envelope(int KeyVersion, CachedSecretDescriptor Descriptor, string Nonce, string Tag, string Ciphertext);

    public async Task<CachedSecretDescriptor> StoreAsync(Guid vaultItemId, Guid vaultId, Guid? workspaceId, SensitiveValue value, string fingerprint, DateTimeOffset expiresAt, CancellationToken cancellationToken)
    {
        EnsureAvailable();
        Directory.CreateDirectory(directory);
        var key = await keyMaterial.GetOrCreateKeyAsync(KeyPurpose(CurrentKeyVersion), cancellationToken);
        var nonce = RandomNumberGenerator.GetBytes(12);
        var tag = new byte[16];
        var plaintext = Encoding.UTF8.GetBytes(value.Reveal());
        var ciphertext = new byte[plaintext.Length];
        try
        {
            using var aes = new AesGcm(key, tag.Length);
            aes.Encrypt(nonce, plaintext, ciphertext, tag, Encoding.UTF8.GetBytes(vaultItemId.ToString("D")));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintext);
            CryptographicOperations.ZeroMemory(key);
        }

        var descriptor = new CachedSecretDescriptor(Guid.NewGuid(), vaultItemId, vaultId, workspaceId, clock.UtcNow, expiresAt, null, fingerprint);
        var envelope = new Envelope(CurrentKeyVersion, descriptor, Convert.ToBase64String(nonce), Convert.ToBase64String(tag), Convert.ToBase64String(ciphertext));
        await File.WriteAllTextAsync(PathFor(vaultItemId), JsonSerializer.Serialize(envelope), cancellationToken);
        return descriptor;
    }

    public async Task<SensitiveValue?> RetrieveAsync(Guid vaultItemId, DateTimeOffset now, string? expectedFingerprint, CancellationToken cancellationToken)
    {
        EnsureAvailable();
        var path = PathFor(vaultItemId);
        if (!File.Exists(path)) return null;
        var envelope = JsonSerializer.Deserialize<Envelope>(await File.ReadAllTextAsync(path, cancellationToken))
            ?? throw new CryptographicException("Invalid protected-value envelope.");
        if (envelope.KeyVersion is < 1 or > CurrentKeyVersion) throw new CryptographicException("Unsupported protected-value key version.");
        if (envelope.Descriptor.ExpiresAt <= now ||
            (expectedFingerprint is not null && !string.Equals(envelope.Descriptor.SourceMetadataFingerprint, expectedFingerprint, StringComparison.Ordinal)))
        {
            File.Delete(path);
            return null;
        }

        var key = await keyMaterial.GetOrCreateKeyAsync(KeyPurpose(envelope.KeyVersion), cancellationToken);
        var ciphertext = Convert.FromBase64String(envelope.Ciphertext);
        var plaintext = new byte[ciphertext.Length];
        try
        {
            using var aes = new AesGcm(key, 16);
            aes.Decrypt(Convert.FromBase64String(envelope.Nonce), ciphertext, Convert.FromBase64String(envelope.Tag), plaintext, Encoding.UTF8.GetBytes(vaultItemId.ToString("D")));
            return new SensitiveValue(Encoding.UTF8.GetString(plaintext));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintext);
            CryptographicOperations.ZeroMemory(key);
        }
    }

    public Task PurgeItemAsync(Guid vaultItemId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var path = PathFor(vaultItemId);
        if (File.Exists(path)) File.Delete(path);
        return Task.CompletedTask;
    }

    public Task PurgeVaultAsync(Guid vaultId, CancellationToken cancellationToken) => PurgeWhereAsync(x => x.Descriptor.VaultId == vaultId, cancellationToken);
    public Task PurgeWorkspaceAsync(Guid workspaceId, CancellationToken cancellationToken) => PurgeWhereAsync(x => x.Descriptor.WorkspaceId == workspaceId, cancellationToken);

    public Task PurgeAllAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (Directory.Exists(directory)) Directory.Delete(directory, true);
        return Task.CompletedTask;
    }

    private async Task PurgeWhereAsync(Func<Envelope, bool> predicate, CancellationToken cancellationToken)
    {
        if (!Directory.Exists(directory)) return;
        foreach (var path in Directory.EnumerateFiles(directory, "*.vpcache"))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var envelope = JsonSerializer.Deserialize<Envelope>(await File.ReadAllTextAsync(path, cancellationToken));
            if (envelope is not null && predicate(envelope)) File.Delete(path);
        }
    }

    private string PathFor(Guid itemId) => Path.Combine(directory, $"{itemId:D}.vpcache");
    private static string KeyPurpose(int keyVersion) => $"offline-values-v{keyVersion}";
    private void EnsureAvailable()
    {
        if (!keyMaterial.IsAvailable) throw new PlatformNotSupportedException("Platform-protected key storage is required for offline values.");
    }
}
