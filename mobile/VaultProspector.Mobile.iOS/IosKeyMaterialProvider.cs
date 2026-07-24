using System.Security.Cryptography;
using Foundation;
using LocalAuthentication;
using Security;
using VaultProspector.Application;

namespace VaultProspector.Mobile.iOS;

public sealed class IosKeyMaterialProvider : IKeyMaterialProvider
{
    private const int KeySizeBytes = 32;
    private const string Service =
        "cloud.hybridsolutions.vaultprospector.keys";

    public bool IsAvailable =>
        OperatingSystem.IsIOSVersionAtLeast(18);

    public static void PrepareForCurrentInstall(string dataDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataDirectory);
        Directory.CreateDirectory(dataDirectory);
        var marker = Path.Combine(
            dataDirectory,
            ".installation-bound-keychain-v1");
        if (File.Exists(marker))
            return;

        using var query = new SecRecord(SecKind.GenericPassword)
        {
            Service = Service,
        };
        var status = SecKeyChain.Remove(query);
        if (status is not
            (SecStatusCode.Success or SecStatusCode.ItemNotFound))
        {
            throw new CryptographicException(
                $"iOS could not remove stale install-bound keys ({status}).");
        }

        var temporaryMarker = $"{marker}.{Guid.NewGuid():N}.tmp";
        try
        {
            File.WriteAllText(temporaryMarker, "1");
            File.Move(temporaryMarker, marker, false);
        }
        catch (IOException) when (File.Exists(marker))
        {
        }
        finally
        {
            if (File.Exists(temporaryMarker))
                File.Delete(temporaryMarker);
        }
    }

    public Task<byte[]> GetOrCreateKeyAsync(
        string purpose,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureAvailable();
        var existing = Query(purpose, allowNotFound: true);
        if (existing is not null)
            return Task.FromResult(existing);

        var key = RandomNumberGenerator.GetBytes(KeySizeBytes);
        try
        {
            using var data = NSData.FromArray(key);
            using var accessControl = new SecAccessControl(
                SecAccessible.WhenPasscodeSetThisDeviceOnly,
                SecAccessControlCreateFlags.BiometryCurrentSet);
            using var record = new SecRecord(SecKind.GenericPassword)
            {
                Service = Service,
                Account = NormalizePurpose(purpose),
                ValueData = data,
                AccessControl = accessControl,
            };
            var status = SecKeyChain.Add(record);
            if (status == SecStatusCode.DuplicateItem)
                return Task.FromResult(
                    Query(purpose, allowNotFound: false)!);
            if (status != SecStatusCode.Success)
            {
                throw new CryptographicException(
                    $"iOS Keychain rejected protected key creation ({status}).");
            }

            return Task.FromResult(key.ToArray());
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
        }
    }

    public Task<byte[]> GetExistingKeyAsync(
        string purpose,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureAvailable();
        return Task.FromResult(Query(purpose, allowNotFound: false)!);
    }

    private static byte[]? Query(string purpose, bool allowNotFound)
    {
        using var authenticationContext = new LAContext
        {
            LocalizedReason =
                "Unlock Vault Prospector encrypted local data",
            TouchIdAuthenticationAllowableReuseDuration = 30,
        };
        using var query = new SecRecord(SecKind.GenericPassword)
        {
            Service = Service,
            Account = NormalizePurpose(purpose),
            AuthenticationContext = authenticationContext,
        };
        using var data = SecKeyChain.QueryAsData(
            query,
            false,
            out var status);
        if (status == SecStatusCode.ItemNotFound && allowNotFound)
            return null;
        if (status != SecStatusCode.Success || data is null)
        {
            throw new CryptographicException(
                $"The device-bound iOS key is unavailable ({status}). Reauthentication or local reset is required.");
        }

        var key = data.ToArray();
        if (key.Length != KeySizeBytes)
        {
            CryptographicOperations.ZeroMemory(key);
            throw new CryptographicException(
                "The iOS protected key length is invalid.");
        }

        return key;
    }

    private static string NormalizePurpose(string purpose)
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

        return purpose;
    }

    private static void EnsureAvailable()
    {
        if (!OperatingSystem.IsIOSVersionAtLeast(18))
            throw new PlatformNotSupportedException(
                "iOS 18 or later is required.");
    }
}
