using System.Security.Cryptography;
using System.Text;
using VaultProspector.Application;
using VaultProspector.Domain;

namespace VaultProspector.Platform;

public sealed class WindowsCyberArkCredentialStore(string credentialDirectory)
    : ICyberArkCredentialStore
{
    private const int MaximumCredentialBytes = 64 * 1024;

    public async Task StoreAsync(
        Guid profileId,
        SensitiveValue credential,
        CancellationToken cancellationToken)
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException(
                "Windows Data Protection API is required for CyberArk credentials.");

        ArgumentNullException.ThrowIfNull(credential);
        cancellationToken.ThrowIfCancellationRequested();
        Directory.CreateDirectory(CanonicalDirectory());

        var plaintext = credential.CopyUtf8Bytes();
        if (plaintext.Length is 0 or > MaximumCredentialBytes)
        {
            CryptographicOperations.ZeroMemory(plaintext);
            throw new CyberArkConfigurationException(
                "The CyberArk credential must contain between 1 and 65,536 UTF-8 bytes.",
                nameof(credential));
        }

        var entropy = Entropy(profileId);
        byte[]? protectedBytes = null;
        var path = CredentialPath(profileId);
        var temporaryPath = $"{path}.{Guid.NewGuid():N}.tmp";
        try
        {
            protectedBytes = ProtectedData.Protect(
                plaintext,
                entropy,
                DataProtectionScope.CurrentUser);
            await File.WriteAllBytesAsync(
                temporaryPath,
                protectedBytes,
                cancellationToken);
            File.Move(temporaryPath, path, true);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintext);
            CryptographicOperations.ZeroMemory(entropy);
            if (protectedBytes is not null)
                CryptographicOperations.ZeroMemory(protectedBytes);
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }
    }

    public async Task<SensitiveValue> RetrieveAsync(
        Guid profileId,
        CancellationToken cancellationToken)
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException(
                "Windows Data Protection API is required for CyberArk credentials.");

        var path = CredentialPath(profileId);
        if (!File.Exists(path))
            throw new ProtectedKeyUnavailableException(
                "The CyberArk credential is not available on this Windows account.");

        var file = new FileInfo(path);
        if (file.Length is <= 0 or > MaximumCredentialBytes * 2)
            throw new LocalDataIntegrityException(
                "The protected CyberArk credential has an invalid size.");

        var protectedBytes = await File.ReadAllBytesAsync(path, cancellationToken);
        var entropy = Entropy(profileId);
        byte[]? plaintext = null;
        try
        {
            plaintext = ProtectedData.Unprotect(
                protectedBytes,
                entropy,
                DataProtectionScope.CurrentUser);
            if (plaintext.Length is 0 or > MaximumCredentialBytes)
                throw new LocalDataIntegrityException(
                    "The protected CyberArk credential has an invalid plaintext size.");

            var characters = Encoding.UTF8.GetChars(plaintext);
            try
            {
                return new SensitiveValue(characters);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(
                    System.Runtime.InteropServices.MemoryMarshal.AsBytes(
                        characters.AsSpan()));
            }
        }
        catch (CryptographicException exception)
        {
            throw new LocalDataIntegrityException(
                "The CyberArk credential could not be unprotected for this Windows account.",
                exception);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(protectedBytes);
            CryptographicOperations.ZeroMemory(entropy);
            if (plaintext is not null)
                CryptographicOperations.ZeroMemory(plaintext);
        }
    }

    public Task RemoveAsync(Guid profileId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var path = CredentialPath(profileId);
        if (File.Exists(path))
            File.Delete(path);
        return Task.CompletedTask;
    }

    private string CredentialPath(Guid profileId) =>
        Path.Combine(CanonicalDirectory(), $"{profileId:D}.credential");

    private string CanonicalDirectory()
    {
        var directory = Path.GetFullPath(credentialDirectory);
        var parent = Directory.GetParent(directory)?.FullName
            ?? throw new InvalidOperationException(
                "The CyberArk credential directory must have a parent.");
        var expected = Path.GetFullPath(Path.Combine(parent, Path.GetFileName(directory)));
        if (!string.Equals(directory, expected, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                "The CyberArk credential directory is not canonical.");
        if (Directory.Exists(directory) &&
            (File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0)
            throw new LocalDataIntegrityException(
                "The CyberArk credential directory cannot be a reparse point.");
        return directory;
    }

    private static byte[] Entropy(Guid profileId) =>
        Encoding.UTF8.GetBytes($"VaultProspector.CyberArkCredential.v1:{profileId:D}");
}
