using VaultProspector.Application;
using VaultProspector.Domain;
using VaultProspector.Platform;

namespace VaultProspector.Platform.Tests;

public sealed class WindowsRegistryEnterprisePolicyTests
{
    private const string AllowedTenant =
        "11111111-1111-1111-1111-111111111111";
    private static readonly string[] AllowedTenantValues =
        [$"{{{AllowedTenant.ToUpperInvariant()}}}"];
    private static readonly string[] AllowedProviderValues =
        ["AzureKeyVault"];
    private static readonly string[] AllowedIdentityTypeValues =
        ["InteractiveUser", "ServicePrincipal"];
    private static readonly string[] InvalidTenantValues =
        ["not-a-guid"];
    private static readonly string[] InvalidProviderValues =
        ["UnknownProvider"];
    private static readonly string[] InvalidIdentityTypeValues =
        ["ClientSecret"];

    [Fact]
    public void ValidManagedPolicyNormalizesAndConstrainsEveryBoundary()
    {
        var policy = WindowsRegistryEnterprisePolicy.Parse(
            new Dictionary<string, object?>
            {
                ["PolicyVersion"] = 1,
                ["Enabled"] = 1,
                ["AllowedTenantIds"] = AllowedTenantValues,
                ["AllowedProviders"] = AllowedProviderValues,
                ["AllowedIdentityTypes"] = AllowedIdentityTypeValues,
                ["DisableClipboard"] = 1,
                ["DisableOfflineCache"] = 0,
                ["DisableRemoteCredentialVerification"] = 1,
                ["MaximumOfflineCacheMinutes"] = 90,
            });

        Assert.True(policy.IsManaged);
        Assert.True(policy.IsValid);
        Assert.Contains(AllowedTenant, policy.AllowedTenantIds);
        Assert.Equal(
            [EnterpriseProvider.AzureKeyVault],
            policy.AllowedProviders);
        Assert.Equal(
            [IdentityType.InteractiveUser, IdentityType.ServicePrincipal],
            policy.AllowedIdentityTypes);
        Assert.False(policy.AllowClipboard);
        Assert.True(policy.AllowOfflineCache);
        Assert.False(policy.AllowRemoteCredentialVerification);
        Assert.Equal(
            TimeSpan.FromMinutes(90),
            policy.MaximumOfflineCacheLifetime);
        Assert.DoesNotContain(
            AllowedTenant,
            policy.SafeStatus,
            StringComparison.OrdinalIgnoreCase);

        var constrained = policy.Constrain(
            new CachePolicy(
                true,
                TimeSpan.FromHours(8),
                false,
                true));
        Assert.True(constrained.IsEnabled);
        Assert.Equal(TimeSpan.FromMinutes(90), constrained.MaximumLifetime);
        Assert.True(constrained.RequireLocalUnlock);
        Assert.False(constrained.AllowClipboard);
    }

    [Fact]
    public void DisabledPolicyIsUnmanaged()
    {
        var policy = WindowsRegistryEnterprisePolicy.Parse(
            new Dictionary<string, object?>
            {
                ["Enabled"] = 0,
            });

        Assert.False(policy.IsManaged);
        Assert.True(policy.AllowClipboard);
        Assert.True(policy.AllowOfflineCache);
        Assert.True(policy.AllowRemoteCredentialVerification);
    }

    [Theory]
    [MemberData(nameof(InvalidPolicies))]
    public void InvalidPolicyFailsClosed(
        IReadOnlyDictionary<string, object?> values)
    {
        var policy = WindowsRegistryEnterprisePolicy.Parse(values);

        Assert.True(policy.IsManaged);
        Assert.False(policy.IsValid);
        Assert.Empty(policy.AllowedProviders);
        Assert.Empty(policy.AllowedIdentityTypes);
        Assert.False(policy.AllowClipboard);
        Assert.False(policy.AllowOfflineCache);
        Assert.False(policy.AllowRemoteCredentialVerification);
        Assert.Throws<EnterprisePolicyDeniedException>(
            () => policy.EnsureProviderAllowed(
                EnterpriseProvider.AzureKeyVault));
    }

    public static TheoryData<IReadOnlyDictionary<string, object?>>
        InvalidPolicies() =>
        new()
        {
            new Dictionary<string, object?>
            {
                ["Enabled"] = 1,
            },
            new Dictionary<string, object?>
            {
                ["PolicyVersion"] = 2,
                ["Enabled"] = 1,
            },
            new Dictionary<string, object?>
            {
                ["PolicyVersion"] = 1,
            },
            new Dictionary<string, object?>
            {
                ["PolicyVersion"] = 1,
                ["Enabled"] = 2,
            },
            new Dictionary<string, object?>
            {
                ["PolicyVersion"] = 1,
                ["Enabled"] = 1,
                ["AllowedTenantIds"] = InvalidTenantValues,
            },
            new Dictionary<string, object?>
            {
                ["PolicyVersion"] = 1,
                ["Enabled"] = 1,
                ["AllowedProviders"] = InvalidProviderValues,
            },
            new Dictionary<string, object?>
            {
                ["PolicyVersion"] = 1,
                ["Enabled"] = 1,
                ["AllowedIdentityTypes"] = InvalidIdentityTypeValues,
            },
            new Dictionary<string, object?>
            {
                ["PolicyVersion"] = 1,
                ["Enabled"] = 1,
                ["DisableClipboard"] = 7,
            },
            new Dictionary<string, object?>
            {
                ["PolicyVersion"] = 1,
                ["Enabled"] = 1,
                ["DisableRemoteCredentialVerification"] = 7,
            },
            new Dictionary<string, object?>
            {
                ["PolicyVersion"] = 1,
                ["Enabled"] = 1,
                ["MaximumOfflineCacheMinutes"] = 10_081,
            },
        };
}
