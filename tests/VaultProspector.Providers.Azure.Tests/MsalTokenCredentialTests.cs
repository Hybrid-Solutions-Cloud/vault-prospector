using Azure.Core;
using Azure.Identity;
using Microsoft.Identity.Client;
using VaultProspector.Providers.Azure;

namespace VaultProspector.Providers.Azure.Tests;

public sealed class MsalTokenCredentialTests
{
    private static readonly TokenRequestContext GuestVaultRequest = new(
        ["https://vault.azure.net/.default"],
        tenantId: "22222222-2222-2222-2222-222222222222");

    [Fact]
    public async Task SilentCredentialDoesNotLaunchInteractiveAuthentication()
    {
        var interactiveCalls = 0;
        var credential = new MsalIdentityProvider.MsalTokenCredential(
            (_, _) => Task.FromException<AccessToken>(UiRequired()),
            (_, _) =>
            {
                interactiveCalls++;
                return Task.FromResult(Token());
            },
            allowInteractiveAuthentication: false);

        await Assert.ThrowsAsync<AuthenticationFailedException>(() =>
            credential.GetTokenAsync(
                GuestVaultRequest,
                TestContext.Current.CancellationToken).AsTask());

        Assert.Equal(0, interactiveCalls);
    }

    [Fact]
    public async Task InteractiveCredentialRecoversOnceAndThenUsesSilentCache()
    {
        var authorized = false;
        var silentCalls = 0;
        var interactiveCalls = 0;
        var credential = new MsalIdentityProvider.MsalTokenCredential(
            (_, _) =>
            {
                silentCalls++;
                return authorized
                    ? Task.FromResult(Token())
                    : Task.FromException<AccessToken>(UiRequired());
            },
            (_, _) =>
            {
                interactiveCalls++;
                authorized = true;
                return Task.FromResult(Token());
            },
            allowInteractiveAuthentication: true);

        await credential.GetTokenAsync(
            GuestVaultRequest,
            TestContext.Current.CancellationToken);
        await credential.GetTokenAsync(
            GuestVaultRequest,
            TestContext.Current.CancellationToken);

        Assert.Equal(1, interactiveCalls);
        Assert.Equal(3, silentCalls);
    }

    [Fact]
    public async Task InteractiveFailureIsNotPromptedAgainForSameTenantAndResource()
    {
        var interactiveCalls = 0;
        var credential = new MsalIdentityProvider.MsalTokenCredential(
            (_, _) => Task.FromException<AccessToken>(UiRequired()),
            (_, _) =>
            {
                interactiveCalls++;
                return Task.FromException<AccessToken>(UiRequired());
            },
            allowInteractiveAuthentication: true);

        await Assert.ThrowsAsync<AuthenticationFailedException>(() =>
            credential.GetTokenAsync(
                GuestVaultRequest,
                TestContext.Current.CancellationToken).AsTask());
        await Assert.ThrowsAsync<AuthenticationFailedException>(() =>
            credential.GetTokenAsync(
                GuestVaultRequest,
                TestContext.Current.CancellationToken).AsTask());

        Assert.Equal(1, interactiveCalls);
    }

    private static MsalUiRequiredException UiRequired() =>
        new("interaction_required", "Interactive authentication is required.");

    private static AccessToken Token() =>
        new("token", DateTimeOffset.UtcNow.AddHours(1));
}
