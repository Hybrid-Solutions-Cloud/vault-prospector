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
}
