using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using VaultProspector.Application;
using VaultProspector.Domain;

namespace VaultProspector.Infrastructure;

public sealed partial class EncryptedFileValueStore(string directory, IKeyMaterialProvider keyMaterial, IClock clock) : IProtectedValueStore
{
    private const int CurrentKeyVersion = 2;
    private const int MaximumEnvelopeBytes = 16 * 1024 * 1024;
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
            await File.WriteAllTextAsync(
                temporaryPath,
                JsonSerializer.Serialize(
                    envelope,
                    EncryptedFileJsonContext.Default.Envelope),
                cancellationToken);
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
        Envelope envelope;
        try
        {
            envelope = await ReadEnvelopeAsync(path, cancellationToken);
        }
        catch (CryptographicException)
        {
            File.Delete(path);
            throw;
        }

        if (envelope.KeyVersion < CurrentKeyVersion)
        {
            File.Delete(path);
            return null;
        }
        if (envelope.KeyVersion > CurrentKeyVersion) throw new CryptographicException("Unsupported protected-value key version.");

        byte[] plaintext;
        try
        {
            plaintext = await DecryptAsync(envelope, vaultItemId, cancellationToken);
        }
        catch (CryptographicException)
        {
            File.Delete(path);
            throw;
        }

        try
        {
            if (envelope.Descriptor.VaultItemId != vaultItemId)
            {
                File.Delete(path);
                throw new CryptographicException("Protected-value envelope does not match the requested item.");
            }
            if (envelope.Descriptor.ExpiresAt <= now ||
                (expectedFingerprint is not null && !string.Equals(envelope.Descriptor.SourceMetadataFingerprint, expectedFingerprint, StringComparison.Ordinal)))
            {
                File.Delete(path);
                return null;
            }

            return new SensitiveValue(Encoding.UTF8.GetString(plaintext));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintext);
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

    internal async Task<int> RotateEncryptionKeyAsync(
        byte[] currentKey,
        byte[] replacementKey,
        CancellationToken cancellationToken)
    {
        if (currentKey.Length != 32 || replacementKey.Length != 32)
            throw new ArgumentException(
                "Offline-value rotation requires 256-bit current and replacement keys.");
        if (!Directory.Exists(directory))
            return 0;

        var paths = Directory.EnumerateFiles(directory, "*.vpcache")
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();
        var rotated = 0;
        foreach (var path in paths)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!Guid.TryParse(Path.GetFileNameWithoutExtension(path), out var itemId))
                throw new LocalDataIntegrityException(
                    "An offline-value file has an invalid item identifier.");

            Envelope envelope;
            byte[] plaintext;
            try
            {
                envelope = await ReadEnvelopeAsync(path, cancellationToken);
                if (envelope.KeyVersion != CurrentKeyVersion)
                    throw new CryptographicException(
                        "An offline-value envelope has an unsupported key version.");
                plaintext = DecryptWithKey(
                    envelope,
                    itemId,
                    currentKey);
                if (envelope.Descriptor.VaultItemId != itemId)
                    throw new CryptographicException(
                        "An offline-value envelope does not match its item path.");
            }
            catch (CryptographicException exception)
            {
                throw new LocalDataIntegrityException(
                    "An offline value failed validation before key rotation.",
                    exception);
            }

            try
            {
                await WriteEnvelopeWithKeyAsync(
                    path,
                    itemId,
                    envelope.Descriptor,
                    plaintext,
                    replacementKey,
                    cancellationToken);
                rotated++;
            }
            finally
            {
                CryptographicOperations.ZeroMemory(plaintext);
            }
        }

        return rotated;
    }

    internal async Task ValidateAllWithKeyAsync(
        byte[] key,
        CancellationToken cancellationToken)
    {
        if (key.Length != 32)
            throw new ArgumentException(
                "Offline-value validation requires a 256-bit key.",
                nameof(key));
        if (!Directory.Exists(directory))
            return;

        foreach (var path in Directory.EnumerateFiles(directory, "*.vpcache")
            .OrderBy(path => path, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!Guid.TryParse(Path.GetFileNameWithoutExtension(path), out var itemId))
                throw new LocalDataIntegrityException(
                    "An offline-value file has an invalid item identifier.");
            try
            {
                var envelope = await ReadEnvelopeAsync(path, cancellationToken);
                if (envelope.KeyVersion != CurrentKeyVersion ||
                    envelope.Descriptor.VaultItemId != itemId)
                {
                    throw new CryptographicException(
                        "An offline-value envelope has invalid rotation metadata.");
                }

                var plaintext = DecryptWithKey(envelope, itemId, key);
                CryptographicOperations.ZeroMemory(plaintext);
            }
            catch (CryptographicException exception)
            {
                throw new LocalDataIntegrityException(
                    "An offline value failed post-rotation validation.",
                    exception);
            }
        }
    }

    private async Task PurgeWhereAsync(Func<Envelope, bool> predicate, CancellationToken cancellationToken)
    {
        if (!Directory.Exists(directory)) return;
        foreach (var path in Directory.EnumerateFiles(directory, "*.vpcache"))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!Guid.TryParse(Path.GetFileNameWithoutExtension(path), out var requestedVaultItemId))
            {
                File.Delete(path);
                continue;
            }

            Envelope envelope;
            try
            {
                envelope = await ReadEnvelopeAsync(path, cancellationToken);
                if (envelope.KeyVersion != CurrentKeyVersion)
                {
                    File.Delete(path);
                    continue;
                }

                var plaintext = await DecryptAsync(envelope, requestedVaultItemId, cancellationToken);
                CryptographicOperations.ZeroMemory(plaintext);
                if (envelope.Descriptor.VaultItemId != requestedVaultItemId)
                    throw new CryptographicException("Protected-value envelope does not match its published item path.");
            }
            catch (CryptographicException)
            {
                File.Delete(path);
                continue;
            }

            if (predicate(envelope)) File.Delete(path);
        }
    }

    private static async Task<Envelope> ReadEnvelopeAsync(string path, CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                4096,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            if (stream.Length > MaximumEnvelopeBytes)
                throw new CryptographicException(
                    "The protected-value envelope exceeds the safe size limit.");
            var envelope = await JsonSerializer.DeserializeAsync(
                stream,
                EncryptedFileJsonContext.Default.Envelope,
                cancellationToken: cancellationToken);
            if (envelope?.Descriptor is null ||
                string.IsNullOrWhiteSpace(envelope.Nonce) ||
                string.IsNullOrWhiteSpace(envelope.Tag) ||
                envelope.Ciphertext is null)
            {
                throw new CryptographicException("Invalid protected-value envelope.");
            }

            return envelope;
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            throw new CryptographicException("Invalid protected-value envelope.", ex);
        }
    }

    private async Task<byte[]> DecryptAsync(Envelope envelope, Guid requestedVaultItemId, CancellationToken cancellationToken)
    {
        var key = await keyMaterial.GetExistingKeyAsync(KeyPurpose(envelope.KeyVersion), cancellationToken);
        try
        {
            return DecryptWithKey(envelope, requestedVaultItemId, key);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
        }
    }

    private static byte[] DecryptWithKey(
        Envelope envelope,
        Guid requestedVaultItemId,
        byte[] key)
    {
        byte[] nonce;
        byte[] tag;
        byte[] ciphertext;
        try
        {
            nonce = Convert.FromBase64String(envelope.Nonce);
            tag = Convert.FromBase64String(envelope.Tag);
            ciphertext = Convert.FromBase64String(envelope.Ciphertext);
        }
        catch (FormatException ex)
        {
            throw new CryptographicException("Invalid protected-value encoding.", ex);
        }

        if (nonce.Length != 12 || tag.Length != 16)
            throw new CryptographicException("Invalid protected-value nonce or authentication tag length.");

        var plaintext = new byte[ciphertext.Length];
        var associatedData = AssociatedData(envelope.KeyVersion, requestedVaultItemId, envelope.Descriptor);
        var authenticated = false;
        try
        {
            using var aes = new AesGcm(key, tag.Length);
            aes.Decrypt(nonce, ciphertext, tag, plaintext, associatedData);
            authenticated = true;
            return plaintext;
        }
        finally
        {
            if (!authenticated) CryptographicOperations.ZeroMemory(plaintext);
            CryptographicOperations.ZeroMemory(associatedData);
            CryptographicOperations.ZeroMemory(nonce);
            CryptographicOperations.ZeroMemory(tag);
            CryptographicOperations.ZeroMemory(ciphertext);
        }
    }

    private static async Task WriteEnvelopeWithKeyAsync(
        string path,
        Guid itemId,
        CachedSecretDescriptor descriptor,
        byte[] plaintext,
        byte[] key,
        CancellationToken cancellationToken)
    {
        var nonce = RandomNumberGenerator.GetBytes(12);
        var tag = new byte[16];
        var ciphertext = new byte[plaintext.Length];
        var associatedData = AssociatedData(
            CurrentKeyVersion,
            itemId,
            descriptor);
        try
        {
            using var aes = new AesGcm(key, tag.Length);
            aes.Encrypt(
                nonce,
                plaintext,
                ciphertext,
                tag,
                associatedData);
            var envelope = new Envelope(
                CurrentKeyVersion,
                descriptor,
                Convert.ToBase64String(nonce),
                Convert.ToBase64String(tag),
                Convert.ToBase64String(ciphertext));
            var temporaryPath = $"{path}.{Guid.NewGuid():N}.tmp";
            try
            {
                await File.WriteAllTextAsync(
                    temporaryPath,
                    JsonSerializer.Serialize(
                        envelope,
                        EncryptedFileJsonContext.Default.Envelope),
                    cancellationToken);
                File.Move(temporaryPath, path, true);
            }
            finally
            {
                if (File.Exists(temporaryPath))
                    File.Delete(temporaryPath);
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(nonce);
            CryptographicOperations.ZeroMemory(tag);
            CryptographicOperations.ZeroMemory(ciphertext);
            CryptographicOperations.ZeroMemory(associatedData);
        }
    }

    private string PathFor(Guid itemId) => Path.Combine(directory, $"{itemId:D}.vpcache");
    private static string KeyPurpose(int keyVersion) => $"offline-values-v{keyVersion}";
    private static byte[] AssociatedData(int keyVersion, Guid requestedVaultItemId, CachedSecretDescriptor descriptor) =>
        JsonSerializer.SerializeToUtf8Bytes(
            new AuthenticatedMetadata(
                keyVersion,
                requestedVaultItemId,
                descriptor),
            EncryptedFileJsonContext.Default.AuthenticatedMetadata);

    [JsonSerializable(typeof(Envelope))]
    [JsonSerializable(typeof(AuthenticatedMetadata))]
    private sealed partial class EncryptedFileJsonContext :
        JsonSerializerContext;

    private void EnsureAvailable()
    {
        if (!keyMaterial.IsAvailable) throw new PlatformNotSupportedException("Platform-protected key storage is required for offline values.");
    }
}
