using VaultProspector.BrowserProtocol;
using VaultProspector.Domain;
using VaultProspector.Mobile.Core;

namespace VaultProspector.Mobile.Tests;

public sealed class MobileAutofillPolicyTests
{
    [Fact]
    public void AllowsExactForegroundVerifiedHttpsSecretMapping()
    {
        Assert.True(MobileAutofillPolicy.CanOffer(Allowed()));
    }

    [Theory]
    [InlineData("http://example.test")]
    [InlineData("https://user@example.test")]
    [InlineData("https://example.test:8443")]
    public void RejectsUnsafeOrigin(string origin)
    {
        Assert.False(MobileAutofillPolicy.CanOffer(
            Allowed() with { Origin = new Uri(origin) }));
    }

    [Fact]
    public void RejectsArbitraryOrUnverifiedValue()
    {
        Assert.False(MobileAutofillPolicy.CanOffer(
            Allowed() with { HasExactMapping = false }));
        Assert.False(MobileAutofillPolicy.CanOffer(
            Allowed() with { HasFreshUserVerification = false }));
        Assert.False(MobileAutofillPolicy.CanOffer(
            Allowed() with { ObjectType = VaultObjectType.Key }));
    }

    private static MobileAutofillRequest Allowed() => new(
        new Uri("https://example.test"),
        BrowserMappingFieldPurpose.Password,
        VaultObjectType.Secret,
        HasExactMapping: true,
        IsForegroundInvocation: true,
        HasFreshUserVerification: true);
}
