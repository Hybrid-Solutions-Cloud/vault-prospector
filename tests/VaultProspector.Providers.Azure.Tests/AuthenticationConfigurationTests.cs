using Microsoft.Identity.Client;
using VaultProspector.Domain;
using VaultProspector.Providers.Azure;

namespace VaultProspector.Providers.Azure.Tests;

public sealed class AuthenticationConfigurationTests
{
    [Fact]
    public void InteractiveSignInUsesOneResourceAudience()
    {
        Assert.Equal([AzureAuthenticationScopes.ArmDelegated], AzureAuthenticationScopes.InteractiveSignIn);
        Assert.DoesNotContain(AzureAuthenticationScopes.KeyVaultDelegated, AzureAuthenticationScopes.InteractiveSignIn);
        Assert.Equal([AzureAuthenticationScopes.KeyVaultDelegated], AzureAuthenticationScopes.AdditionalConsent);
    }

    [Fact]
    public async Task CredentialAcquisitionUsesIdentityClientAndFailsForMissingAccount()
    {
        var provider = new MsalIdentityProvider(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));
        var identity = new ConnectedIdentity(Guid.NewGuid(), "11111111-1111-1111-1111-111111111111", "account", "user@example.invalid", "Test", "tenant", AuthenticationState.Ready, DateTimeOffset.UtcNow);

        if (!OperatingSystem.IsWindows())
        {
            await Assert.ThrowsAsync<PlatformNotSupportedException>(() => provider.GetCredentialAsync(identity, TestContext.Current.CancellationToken));
            return;
        }

        await Assert.ThrowsAsync<MsalUiRequiredException>(() => provider.GetCredentialAsync(identity, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task InvalidStoredClientIdIsRejectedBeforeCachePathConstruction()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"vault-prospector-msal-{Guid.NewGuid():N}");
        var provider = new MsalIdentityProvider(directory);
        var identity = new ConnectedIdentity(Guid.NewGuid(), "../../outside-cache", "account", "user@example.invalid", "Test", "tenant", AuthenticationState.Ready, DateTimeOffset.UtcNow);

        await Assert.ThrowsAsync<ArgumentException>(() => provider.GetCredentialAsync(identity, TestContext.Current.CancellationToken));

        Assert.False(Directory.Exists(directory));
    }
}
