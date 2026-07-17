using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using VaultProspector.Application;
using VaultProspector.Domain;

namespace VaultProspector.Infrastructure;

public sealed class EncryptedFileValueStore(string directory, IKeyMaterialProvider keyMaterial, IClock clock) : IProtectedValueStore
{
    private const int CurrentKeyVersion = 2;
    private sealed record Envelope(int KeyVersion, CachedSecretDescriptor Descriptor, string Nonce, string Tag, string Ciphertext);
    private sealed record AuthenticatedMetadata(int KeyVersion, Guid RequestedVaultItemId, CachedSecretDescriptor Descriptor);

    public async Task<CachedSecretDescriptor> StoreAsync(Guid vaultItemId, Guid vaultId, Guid? workspaceId, SensitiveValue value, string fingerprint, DateTimeOffset expiresAt, CancellationToken cancellationToken)
    {
        EnsureAvailable();
        if (expiresAt <= clock.UtcNow) throw new ArgumentOutOfRangeException(nameof(expiresAt), "Protected values must expire in the future.");
        if (string.IsNullOrWhiteSpace(fingerprint)) throw new ArgumentException("A source metadata fingerprint is required.", nameof(fingerprint));
        Directory.CreateDirectory(directory);
        var descriptor = new CachedSecretDescriptor(Guid.NewGuid(), vaultItemId, vaultId, workspaceId, clock.UtcNow, expiresAt, null, fingerprint);
        var key = await keyMaterial.GetOrCreateKeyAsync(KeyPurpose(CurrentKeyVersion), cancellationToken);
        var nonce = RandomNumberGenerator.GetBytes(12);
        var tag = new byte[16];
        var plaintext = Encoding.UTF8.GetBytes(value.Reveal());
        var ciphertext = new byte[plaintext.Length];
        var associatedData = AssociatedData(CurrentKeyVersion, vaultItemId, descriptor);
        try
        {
            using var aes = new AesGcm(key, tag.Length);
            aes.Encrypt(nonce, plaintext, ciphertext, tag, associatedData);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintext);
            CryptographicOperations.ZeroMemory(key);
            CryptographicOperations.ZeroMemory(associatedData);
        }

        var envelope = new Envelope(CurrentKeyVersion, descriptor, Convert.ToBase64String(nonce), Convert.ToBase64String(tag), Convert.ToBase64String(ciphertext));
        var path = PathFor(vaultItemId);
        var temporaryPath = $"{path}.{Guid.NewGuid():N}.tmp";
        try
        {
            await File.WriteAllTextAsync(temporaryPath, JsonSerializer.Serialize(envelope), cancellationToken);
            File.Move(temporaryPath, path, true);
        }
        finally
        {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
        }
        return descriptor;
    }

    public async Task<SensitiveValue?> RetrieveAsync(Guid vaultItemId, DateTimeOffset now, string? expectedFingerprint, CancellationToken cancellationToken)
    {
        EnsureAvailable();
        var path = PathFor(vaultItemId);
        if (!File.Exists(path)) return null;
        var envelope = JsonSerializer.Deserialize<Envelope>(await File.ReadAllTextAsync(path, cancellationToken))
            ?? throw new CryptographicException("Invalid protected-value envelope.");
        if (envelope.KeyVersion < CurrentKeyVersion)
        {
            File.Delete(path);
            return null;
        }
        if (envelope.KeyVersion > CurrentKeyVersion) throw new CryptographicException("Unsupported protected-value key version.");
        if (envelope.Descriptor.VaultItemId != vaultItemId)
            throw new CryptographicException("Protected-value envelope does not match the requested item.");
        if (envelope.Descriptor.ExpiresAt <= now ||
            (expectedFingerprint is not null && !string.Equals(envelope.Descriptor.SourceMetadataFingerprint, expectedFingerprint, StringComparison.Ordinal)))
        {
            File.Delete(path);
            return null;
        }

        var key = await keyMaterial.GetOrCreateKeyAsync(KeyPurpose(envelope.KeyVersion), cancellationToken);
        var ciphertext = Convert.FromBase64String(envelope.Ciphertext);
        var plaintext = new byte[ciphertext.Length];
        var associatedData = AssociatedData(envelope.KeyVersion, vaultItemId, envelope.Descriptor);
        try
        {
            using var aes = new AesGcm(key, 16);
            aes.Decrypt(Convert.FromBase64String(envelope.Nonce), ciphertext, Convert.FromBase64String(envelope.Tag), plaintext, associatedData);
            return new SensitiveValue(Encoding.UTF8.GetString(plaintext));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintext);
            CryptographicOperations.ZeroMemory(key);
            CryptographicOperations.ZeroMemory(associatedData);
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
    private static byte[] AssociatedData(int keyVersion, Guid requestedVaultItemId, CachedSecretDescriptor descriptor) =>
        JsonSerializer.SerializeToUtf8Bytes(new AuthenticatedMetadata(keyVersion, requestedVaultItemId, descriptor));
    private void EnsureAvailable()
    {
        if (!keyMaterial.IsAvailable) throw new PlatformNotSupportedException("Platform-protected key storage is required for offline values.");
    }
}
