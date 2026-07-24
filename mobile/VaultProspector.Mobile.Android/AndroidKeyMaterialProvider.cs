using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Android.Security.Keystore;
using Java.Security;
using Javax.Crypto;
using Javax.Crypto.Spec;
using VaultProspector.Application;

namespace VaultProspector.Mobile.Android;

public sealed class AndroidKeyMaterialProvider(string keyDirectory)
    : IKeyMaterialProvider
{
    private const int KeySizeBytes = 32;
    private const int MaximumEnvelopeBytes = 4096;
    private const string AndroidKeyStoreName = "AndroidKeyStore";

    private sealed record Envelope(
        int Version,
        string InitializationVector,
        string Ciphertext);

    public bool IsAvailable =>
        OperatingSystem.IsAndroidVersionAtLeast(31);

    public async Task<byte[]> GetOrCreateKeyAsync(
        string purpose,
        CancellationToken cancellationToken)
    {
        EnsureAvailable();
        var path = GetPath(purpose);
        if (File.Exists(path))
            return await ReadAsync(purpose, path, cancellationToken);

        Directory.CreateDirectory(keyDirectory);
        var plaintext = RandomNumberGenerator.GetBytes(KeySizeBytes);
        try
        {
            var envelope = Encrypt(GetOrCreateWrappingKey(purpose), plaintext);
            var serialized = JsonSerializer.SerializeToUtf8Bytes(envelope);
            if (serialized.Length > MaximumEnvelopeBytes)
                throw new CryptographicException(
                    "The Android key envelope exceeded its size limit.");

            var temporaryPath = $"{path}.{Guid.NewGuid():N}.tmp";
            try
            {
                await File.WriteAllBytesAsync(
                    temporaryPath,
                    serialized,
                    cancellationToken);
                try
                {
                    File.Move(temporaryPath, path, false);
                }
                catch (IOException) when (File.Exists(path))
                {
                    return await ReadAsync(
                        purpose,
                        path,
                        cancellationToken);
                }
            }
            finally
            {
                if (File.Exists(temporaryPath))
                    File.Delete(temporaryPath);
                CryptographicOperations.ZeroMemory(serialized);
            }

            return plaintext.ToArray();
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintext);
        }
    }

    public Task<byte[]> GetExistingKeyAsync(
        string purpose,
        CancellationToken cancellationToken)
    {
        EnsureAvailable();
        var path = GetPath(purpose);
        if (!File.Exists(path))
            throw new KeyNotFoundException(
                "The device-bound Android key is unavailable.");
        return ReadAsync(purpose, path, cancellationToken);
    }

    private static async Task<byte[]> ReadAsync(
        string purpose,
        string path,
        CancellationToken cancellationToken)
    {
        var file = new FileInfo(path);
        if (file.Length is <= 0 or > MaximumEnvelopeBytes)
            throw new CryptographicException(
                "The Android key envelope is invalid.");

        var serialized = await File.ReadAllBytesAsync(path, cancellationToken);
        try
        {
            var envelope = JsonSerializer.Deserialize<Envelope>(serialized)
                ?? throw new CryptographicException(
                    "The Android key envelope is invalid.");
            if (envelope.Version != 1)
                throw new CryptographicException(
                    "The Android key envelope version is unsupported.");
            return Decrypt(GetExistingWrappingKey(purpose), envelope);
        }
        catch (JsonException exception)
        {
            throw new CryptographicException(
                "The Android key envelope is invalid.",
                exception);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(serialized);
        }
    }

    private static Envelope Encrypt(IKey key, byte[] plaintext)
    {
        using var cipher = Cipher.GetInstance("AES/GCM/NoPadding")
            ?? throw new CryptographicException(
                "Android AES-GCM is unavailable.");
        cipher.Init(Javax.Crypto.CipherMode.EncryptMode, key);
        var ciphertext = cipher.DoFinal(plaintext)
            ?? throw new CryptographicException(
                "Android Keystore encryption failed.");
        var initializationVector = cipher.GetIV()
            ?? throw new CryptographicException(
                "Android Keystore did not return an initialization vector.");
        try
        {
            return new Envelope(
                1,
                Convert.ToBase64String(initializationVector),
                Convert.ToBase64String(ciphertext));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(ciphertext);
            CryptographicOperations.ZeroMemory(initializationVector);
        }
    }

    private static byte[] Decrypt(IKey key, Envelope envelope)
    {
        byte[] initializationVector;
        byte[] ciphertext;
        try
        {
            initializationVector = Convert.FromBase64String(
                envelope.InitializationVector);
            ciphertext = Convert.FromBase64String(envelope.Ciphertext);
        }
        catch (FormatException exception)
        {
            throw new CryptographicException(
                "The Android key envelope is invalid.",
                exception);
        }

        try
        {
            if (initializationVector.Length is < 12 or > 16 ||
                ciphertext.Length < 16)
            {
                throw new CryptographicException(
                    "The Android key envelope is invalid.");
            }

            using var cipher = Cipher.GetInstance("AES/GCM/NoPadding")
                ?? throw new CryptographicException(
                    "Android AES-GCM is unavailable.");
            using var parameters = new GCMParameterSpec(
                128,
                initializationVector);
            cipher.Init(Javax.Crypto.CipherMode.DecryptMode, key, parameters);
            var plaintext = cipher.DoFinal(ciphertext)
                ?? throw new CryptographicException(
                    "Android Keystore decryption failed.");
            if (plaintext.Length != KeySizeBytes)
            {
                CryptographicOperations.ZeroMemory(plaintext);
                throw new CryptographicException(
                    "The Android protected key length is invalid.");
            }

            return plaintext;
        }
        catch (Java.Lang.Exception exception)
        {
            throw new CryptographicException(
                "The device-bound Android key could not be used. Reauthentication or local reset is required.",
                exception);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(initializationVector);
            CryptographicOperations.ZeroMemory(ciphertext);
        }
    }

    private static IKey GetOrCreateWrappingKey(string purpose)
    {
        var alias = Alias(purpose);
        using var keyStore = KeyStore.GetInstance(AndroidKeyStoreName)
            ?? throw new CryptographicException(
                "Android Keystore is unavailable.");
        keyStore.Load(null);
        if (keyStore.ContainsAlias(alias))
            return keyStore.GetKey(alias, null)
                ?? throw new CryptographicException(
                    "The Android wrapping key is unavailable.");

        using var generator = KeyGenerator.GetInstance(
            KeyProperties.KeyAlgorithmAes,
            AndroidKeyStoreName)
            ?? throw new CryptographicException(
                "Android Keystore AES generation is unavailable.");
        using var specification = new KeyGenParameterSpec.Builder(
                alias,
                KeyStorePurpose.Encrypt | KeyStorePurpose.Decrypt)
            .SetBlockModes(KeyProperties.BlockModeGcm)
            .SetEncryptionPaddings(KeyProperties.EncryptionPaddingNone)
            .SetKeySize(256)
            .SetRandomizedEncryptionRequired(true)
            .SetUserAuthenticationRequired(true)
            .SetUserAuthenticationParameters(
                30,
                (int)(KeyPropertiesAuthType.BiometricStrong |
                      KeyPropertiesAuthType.DeviceCredential))
            .SetInvalidatedByBiometricEnrollment(true)
            .Build();
        generator.Init(specification);
        return generator.GenerateKey()
            ?? throw new CryptographicException(
                "Android Keystore did not generate a wrapping key.");
    }

    private static IKey GetExistingWrappingKey(string purpose)
    {
        using var keyStore = KeyStore.GetInstance(AndroidKeyStoreName)
            ?? throw new CryptographicException(
                "Android Keystore is unavailable.");
        keyStore.Load(null);
        return keyStore.GetKey(Alias(purpose), null)
            ?? throw new CryptographicException(
                "The device-bound Android key is unavailable.");
    }

    private string GetPath(string purpose)
    {
        ValidatePurpose(purpose);
        return Path.Combine(keyDirectory, $"{Alias(purpose)}.json");
    }

    private static string Alias(string purpose)
    {
        ValidatePurpose(purpose);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(purpose));
        return $"vault-prospector.{Convert.ToHexString(hash)[..24].ToLowerInvariant()}";
    }

    private static void ValidatePurpose(string purpose)
    {
        if (string.IsNullOrWhiteSpace(purpose) ||
            purpose.Length > 64 ||
            !char.IsAsciiLetterOrDigit(purpose[0]) ||
            purpose.Any(character =>
                !(char.IsAsciiLetterOrDigit(character) ||
                  character is '-' or '_')))
        {
            throw new ArgumentException(
                "Key purpose must be 1-64 ASCII letters, digits, hyphens, or underscores and start alphanumerically.",
                nameof(purpose));
        }
    }

    private static void EnsureAvailable()
    {
        if (!OperatingSystem.IsAndroidVersionAtLeast(31))
            throw new PlatformNotSupportedException(
                "Android 12/API 31 or later is required.");
    }
}
