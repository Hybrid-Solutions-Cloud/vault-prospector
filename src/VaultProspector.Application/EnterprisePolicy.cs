using VaultProspector.Domain;

namespace VaultProspector.Application;

public enum EnterpriseProvider
{
    AzureKeyVault,
    CyberArkPrivilegeCloud,
}

public sealed class EnterprisePolicyDeniedException(
    string policyName,
    string message)
    : UnauthorizedAccessException(message)
{
    public string PolicyName { get; } = policyName;
}

public sealed class EnterprisePolicySnapshot
{
    private static readonly EnterpriseProvider[] AllProviders =
        Enum.GetValues<EnterpriseProvider>();
    private static readonly IdentityType[] AllIdentityTypes =
        Enum.GetValues<IdentityType>();
    private readonly HashSet<string> _allowedTenantIds;
    private readonly HashSet<EnterpriseProvider> _allowedProviders;
    private readonly HashSet<IdentityType> _allowedIdentityTypes;

    public EnterprisePolicySnapshot(
        bool isManaged,
        IEnumerable<string>? allowedTenantIds = null,
        IEnumerable<EnterpriseProvider>? allowedProviders = null,
        IEnumerable<IdentityType>? allowedIdentityTypes = null,
        bool allowClipboard = true,
        bool allowOfflineCache = true,
        TimeSpan? maximumOfflineCacheLifetime = null,
        bool isValid = true,
        string safeStatus = "No machine-managed enterprise policy is configured.")
    {
        if (maximumOfflineCacheLifetime is { } lifetime &&
            lifetime <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(
                nameof(maximumOfflineCacheLifetime),
                "The enterprise offline-cache lifetime must be positive.");

        IsManaged = isManaged;
        IsValid = isValid;
        AllowClipboard = allowClipboard;
        AllowOfflineCache = allowOfflineCache;
        MaximumOfflineCacheLifetime = maximumOfflineCacheLifetime;
        SafeStatus = string.IsNullOrWhiteSpace(safeStatus)
            ? "Enterprise policy status is unavailable."
            : safeStatus.Trim();
        _allowedTenantIds = new HashSet<string>(
            (allowedTenantIds ?? [])
                .Select(NormalizeTenantId),
            StringComparer.OrdinalIgnoreCase);
        _allowedProviders = new HashSet<EnterpriseProvider>(
            allowedProviders ?? AllProviders);
        _allowedIdentityTypes = new HashSet<IdentityType>(
            allowedIdentityTypes ?? AllIdentityTypes);
    }

    public static EnterprisePolicySnapshot Unmanaged { get; } = new(false);

    public bool IsManaged { get; }
    public bool IsValid { get; }
    public bool AllowClipboard { get; }
    public bool AllowOfflineCache { get; }
    public TimeSpan? MaximumOfflineCacheLifetime { get; }
    public string SafeStatus { get; }
    public IReadOnlySet<string> AllowedTenantIds => _allowedTenantIds;
    public IReadOnlySet<EnterpriseProvider> AllowedProviders => _allowedProviders;
    public IReadOnlySet<IdentityType> AllowedIdentityTypes => _allowedIdentityTypes;
    public bool RestrictsTenants => _allowedTenantIds.Count > 0;

    public static EnterprisePolicySnapshot Invalid(string safeReason) =>
        new(
            true,
            allowedProviders: [],
            allowedIdentityTypes: [],
            allowClipboard: false,
            allowOfflineCache: false,
            isValid: false,
            safeStatus:
                $"Machine-managed enterprise policy is invalid and access is denied: {safeReason}");

    public void EnsureProviderAllowed(EnterpriseProvider provider)
    {
        if (!_allowedProviders.Contains(provider))
        {
            throw new EnterprisePolicyDeniedException(
                "AllowedProviders",
                $"{provider} is disabled by machine-managed enterprise policy.");
        }
    }

    public void EnsureIdentityAllowed(ConnectedIdentity identity)
    {
        ArgumentNullException.ThrowIfNull(identity);
        EnsureProviderAllowed(EnterpriseProvider.AzureKeyVault);
        EnsureIdentityTypeAllowed(identity.Type);
        EnsureTenantAllowed(identity.HomeTenantId);
    }

    public void EnsureIdentityTypeAllowed(IdentityType identityType)
    {
        if (!_allowedIdentityTypes.Contains(identityType))
        {
            throw new EnterprisePolicyDeniedException(
                "AllowedIdentityTypes",
                $"{identityType} connections are disabled by machine-managed enterprise policy.");
        }
    }

    public void EnsureTenantAllowed(string tenantId)
    {
        if (!RestrictsTenants)
            return;

        string normalized;
        try
        {
            normalized = NormalizeTenantId(tenantId);
        }
        catch (ArgumentException)
        {
            throw new EnterprisePolicyDeniedException(
                "AllowedTenantIds",
                "The selected Azure tenant cannot be established under machine-managed enterprise policy.");
        }

        if (!_allowedTenantIds.Contains(normalized))
        {
            throw new EnterprisePolicyDeniedException(
                "AllowedTenantIds",
                "The selected Azure tenant is not permitted by machine-managed enterprise policy.");
        }
    }

    public void EnsureClipboardAllowed()
    {
        if (!AllowClipboard)
        {
            throw new EnterprisePolicyDeniedException(
                "DisableClipboard",
                "Clipboard use is disabled by machine-managed enterprise policy.");
        }
    }

    public void EnsureOfflineCacheAllowed()
    {
        if (!AllowOfflineCache)
        {
            throw new EnterprisePolicyDeniedException(
                "DisableOfflineCache",
                "Offline value storage is disabled by machine-managed enterprise policy.");
        }
    }

    public CachePolicy Constrain(CachePolicy requested)
    {
        ArgumentNullException.ThrowIfNull(requested);
        var maximumLifetime = MaximumOfflineCacheLifetime is { } enterpriseMaximum
            ? TimeSpan.FromTicks(Math.Min(
                requested.MaximumLifetime.Ticks,
                enterpriseMaximum.Ticks))
            : requested.MaximumLifetime;
        return requested with
        {
            IsEnabled = requested.IsEnabled && AllowOfflineCache,
            MaximumLifetime = maximumLifetime,
            RequireLocalUnlock = true,
            AllowClipboard = requested.AllowClipboard && AllowClipboard,
        };
    }

    private static string NormalizeTenantId(string value)
    {
        if (!Guid.TryParse(value?.Trim(), out var tenantId))
            throw new ArgumentException(
                "Enterprise tenant identifiers must be GUIDs.",
                nameof(value));
        return tenantId.ToString("D");
    }
}

public interface IEnterprisePolicy
{
    EnterprisePolicySnapshot GetSnapshot();
}

public sealed class UnmanagedEnterprisePolicy : IEnterprisePolicy
{
    public static UnmanagedEnterprisePolicy Instance { get; } = new();

    private UnmanagedEnterprisePolicy()
    {
    }

    public EnterprisePolicySnapshot GetSnapshot() =>
        EnterprisePolicySnapshot.Unmanaged;
}
