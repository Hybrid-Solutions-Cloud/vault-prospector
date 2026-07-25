using System.Security;
using Microsoft.Win32;
using VaultProspector.Application;
using VaultProspector.Domain;

namespace VaultProspector.Platform;

public sealed class WindowsRegistryEnterprisePolicy : IEnterprisePolicy
{
    public const string RegistryPath =
        @"SOFTWARE\Policies\Hybrid Solutions Cloud\Vault Prospector";

    public EnterprisePolicySnapshot GetSnapshot()
    {
        if (!OperatingSystem.IsWindows())
        {
            return EnterprisePolicySnapshot.Invalid(
                "the Windows machine-policy store is unavailable");
        }

        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(
                RegistryPath,
                writable: false);
            if (key is null)
                return EnterprisePolicySnapshot.Unmanaged;

            var values = new Dictionary<string, object?>(
                StringComparer.OrdinalIgnoreCase);
            foreach (var name in key.GetValueNames())
                values[name] = key.GetValue(name);
            return Parse(values);
        }
        catch (Exception exception) when (
            exception is SecurityException or
                UnauthorizedAccessException or
                IOException)
        {
            return EnterprisePolicySnapshot.Invalid(
                "the Windows machine-policy store could not be read");
        }
    }

    public static EnterprisePolicySnapshot Parse(
        IReadOnlyDictionary<string, object?> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        var normalized = values.ToDictionary(
            pair => pair.Key,
            pair => pair.Value,
            StringComparer.OrdinalIgnoreCase);

        try
        {
            var enabled = ReadRequiredSwitch(normalized, "Enabled");
            if (!enabled)
                return EnterprisePolicySnapshot.Unmanaged;

            var version = ReadRequiredDword(normalized, "PolicyVersion");
            if (version != 1)
                return EnterprisePolicySnapshot.Invalid(
                    "PolicyVersion must be 1");

            var allowedTenants = ReadOptionalMultiString(
                    normalized,
                    "AllowedTenantIds")
                ?.Select(NormalizeTenantId)
                .ToArray();
            var allowedProviders = ParseEnumSet<EnterpriseProvider>(
                normalized,
                "AllowedProviders");
            var allowedIdentityTypes = ParseEnumSet<IdentityType>(
                normalized,
                "AllowedIdentityTypes");
            var disableClipboard = ReadOptionalSwitch(
                normalized,
                "DisableClipboard",
                false);
            var disableOfflineCache = ReadOptionalSwitch(
                normalized,
                "DisableOfflineCache",
                false);
            var disableRemoteCredentialVerification = ReadOptionalSwitch(
                normalized,
                "DisableRemoteCredentialVerification",
                false);
            var maximumCacheMinutes = ReadOptionalDword(
                normalized,
                "MaximumOfflineCacheMinutes");
            if (maximumCacheMinutes is < 1 or > 10_080)
            {
                return EnterprisePolicySnapshot.Invalid(
                    "MaximumOfflineCacheMinutes must be between 1 and 10080");
            }
            var maximumRevealGraceSeconds = ReadOptionalDword(
                normalized,
                "MaximumRevealVerificationGraceSeconds");
            if (maximumRevealGraceSeconds is > 120)
            {
                return EnterprisePolicySnapshot.Invalid(
                    "MaximumRevealVerificationGraceSeconds must be between 0 and 120");
            }

            var providerCount = allowedProviders?.Count ??
                Enum.GetValues<EnterpriseProvider>().Length;
            var identityTypeCount = allowedIdentityTypes?.Count ??
                Enum.GetValues<IdentityType>().Length;
            var tenantScope = allowedTenants is null
                ? "all tenants"
                : $"{allowedTenants.Length} allowed tenant(s)";
            var cacheStatus = disableOfflineCache
                ? "offline values disabled"
                : maximumCacheMinutes is { } minutes
                    ? $"offline values capped at {minutes} minute(s)"
                    : "offline values use the user/workspace limit";
            var clipboardStatus = disableClipboard
                ? "clipboard disabled"
                : "clipboard permitted subject to workspace policy";
            var remoteVerificationStatus =
                disableRemoteCredentialVerification
                    ? "remote Windows credential verification disabled"
                    : "remote Windows credential verification permitted";
            var revealGraceStatus =
                maximumRevealGraceSeconds is { } revealSeconds
                    ? $"reveal verification grace capped at {revealSeconds} second(s)"
                    : "reveal verification grace uses the user setting";

            return new EnterprisePolicySnapshot(
                true,
                allowedTenants,
                allowedProviders,
                allowedIdentityTypes,
                allowClipboard: !disableClipboard,
                allowOfflineCache: !disableOfflineCache,
                maximumOfflineCacheLifetime:
                    maximumCacheMinutes is { } maximum
                        ? TimeSpan.FromMinutes(maximum)
                        : null,
                allowRemoteCredentialVerification:
                    !disableRemoteCredentialVerification,
                maximumRevealVerificationGracePeriod:
                    maximumRevealGraceSeconds is { } maximumReveal
                        ? TimeSpan.FromSeconds(maximumReveal)
                        : null,
                safeStatus:
                    $"Machine-managed policy is active: {tenantScope}; " +
                    $"{providerCount} provider(s); {identityTypeCount} identity type(s); " +
                    $"{clipboardStatus}; {cacheStatus}; " +
                    $"{remoteVerificationStatus}; {revealGraceStatus}.");
        }
        catch (EnterprisePolicyFormatException exception)
        {
            return EnterprisePolicySnapshot.Invalid(exception.Message);
        }
        catch (ArgumentException exception)
        {
            return EnterprisePolicySnapshot.Invalid(exception.Message);
        }
    }

    private static int ReadRequiredDword(
        IReadOnlyDictionary<string, object?> values,
        string name) =>
        ReadOptionalDword(values, name) ??
        throw new EnterprisePolicyFormatException(
            $"{name} is required");

    private static int? ReadOptionalDword(
        IReadOnlyDictionary<string, object?> values,
        string name)
    {
        if (!values.TryGetValue(name, out var value))
            return null;
        return value is int result && result >= 0
            ? result
            : throw new EnterprisePolicyFormatException(
                $"{name} must be a non-negative registry DWORD");
    }

    private static bool ReadRequiredSwitch(
        IReadOnlyDictionary<string, object?> values,
        string name) =>
        ReadSwitchValue(ReadRequiredDword(values, name), name);

    private static bool ReadOptionalSwitch(
        IReadOnlyDictionary<string, object?> values,
        string name,
        bool defaultValue)
    {
        var value = ReadOptionalDword(values, name);
        return value is null
            ? defaultValue
            : ReadSwitchValue(value.Value, name);
    }

    private static bool ReadSwitchValue(int value, string name) =>
        value switch
        {
            0 => false,
            1 => true,
            _ => throw new EnterprisePolicyFormatException(
                $"{name} must be 0 or 1"),
        };

    private static string[]? ReadOptionalMultiString(
        IReadOnlyDictionary<string, object?> values,
        string name)
    {
        if (!values.TryGetValue(name, out var value))
            return null;
        if (value is not string[] entries)
        {
            throw new EnterprisePolicyFormatException(
                $"{name} must be a registry multi-string");
        }

        if (entries.Any(string.IsNullOrWhiteSpace))
        {
            throw new EnterprisePolicyFormatException(
                $"{name} cannot contain blank entries");
        }

        return entries
            .Select(entry => entry.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static List<T>? ParseEnumSet<T>(
        IReadOnlyDictionary<string, object?> values,
        string name)
        where T : struct, Enum
    {
        var entries = ReadOptionalMultiString(values, name);
        if (entries is null)
            return null;
        var result = new List<T>();
        foreach (var entry in entries)
        {
            if (!Enum.TryParse<T>(entry, ignoreCase: true, out var parsed) ||
                !Enum.IsDefined(parsed))
            {
                throw new EnterprisePolicyFormatException(
                    $"{name} contains an unsupported value");
            }
            if (!result.Contains(parsed))
                result.Add(parsed);
        }
        return result;
    }

    private static string NormalizeTenantId(string value)
    {
        if (!Guid.TryParse(value, out var tenantId))
        {
            throw new EnterprisePolicyFormatException(
                "AllowedTenantIds contains a non-GUID value");
        }
        return tenantId.ToString("D");
    }

    private sealed class EnterprisePolicyFormatException(string message)
        : Exception(message);
}
