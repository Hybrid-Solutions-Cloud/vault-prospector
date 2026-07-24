using VaultProspector.BrowserProtocol;
using VaultProspector.Domain;
using VaultProspector.Mobile.Autofill;
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
    [InlineData("https://example.test/path")]
    [InlineData("https://example.test?query=1")]
    [InlineData("https://127.0.0.1")]
    [InlineData("https://localhost")]
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

    [Fact]
    public void NativeRequestRequiresOneExactHttpsOriginAndExplicitHints()
    {
        var request = new MobileAutofillNativeRequest(
            "https",
            "Login.Example.Test",
            [
                new MobileAutofillFieldDescriptor("user", ["username"]),
                new MobileAutofillFieldDescriptor("pass", ["password"]),
                new MobileAutofillFieldDescriptor("email", ["emailAddress"]),
            ]);

        Assert.True(MobileAutofillRequestAnalyzer.TryAnalyze(
            request,
            out var candidate));
        Assert.NotNull(candidate);
        Assert.Equal("https://login.example.test/", candidate.Origin.AbsoluteUri);
        Assert.Collection(
            candidate.Fields,
            field =>
            {
                Assert.Equal("user", field.Id);
                Assert.Equal(
                    BrowserMappingFieldPurpose.Username,
                    field.Purpose);
            },
            field =>
            {
                Assert.Equal("pass", field.Id);
                Assert.Equal(
                    BrowserMappingFieldPurpose.Password,
                    field.Purpose);
            });
    }

    [Theory]
    [InlineData("http", "example.test")]
    [InlineData("https", "example.test:8443")]
    [InlineData("https", "user@example.test")]
    [InlineData("https", "example.test/path")]
    [InlineData("https", "example.test evil.test")]
    [InlineData("https", "127.0.0.1")]
    [InlineData("https", "localhost")]
    [InlineData("https", "example.test.")]
    public void NativeRequestRejectsUnsafeOrigin(
        string scheme,
        string domain)
    {
        Assert.False(MobileAutofillRequestAnalyzer.TryAnalyze(
            new MobileAutofillNativeRequest(
                scheme,
                domain,
                [new MobileAutofillFieldDescriptor("pass", ["password"])]),
            out _));
    }

    [Fact]
    public void NativeRequestRejectsAmbiguousOrDuplicateCredentialFields()
    {
        Assert.False(MobileAutofillRequestAnalyzer.TryAnalyze(
            new MobileAutofillNativeRequest(
                "https",
                "example.test",
                [
                    new MobileAutofillFieldDescriptor(
                        "ambiguous",
                        ["username", "password"]),
                ]),
            out _));

        Assert.False(MobileAutofillRequestAnalyzer.TryAnalyze(
            new MobileAutofillNativeRequest(
                "https",
                "example.test",
                [
                    new MobileAutofillFieldDescriptor("first", ["password"]),
                    new MobileAutofillFieldDescriptor("second", ["password"]),
                ]),
            out _));
    }

    [Theory]
    [InlineData("example.test", true)]
    [InlineData("https://example.test", false)]
    public void AppleServiceIdentifierNormalizesExactHttpsOrigin(
        string identifier,
        bool isDomainIdentifier)
    {
        Assert.True(MobileAutofillRequestAnalyzer.TryCreateAppleServiceOrigin(
            identifier,
            isDomainIdentifier,
            out var origin));
        Assert.Equal("https://example.test/", origin!.AbsoluteUri);
    }

    [Fact]
    public void NativeRequestNormalizesInternationalizedDomain()
    {
        Assert.True(MobileAutofillRequestAnalyzer.TryAnalyze(
            new MobileAutofillNativeRequest(
                "https",
                "bücher.example",
                [new MobileAutofillFieldDescriptor("pass", ["password"])]),
            out var candidate));
        Assert.Equal(
            "https://xn--bcher-kva.example/",
            candidate!.Origin.AbsoluteUri);
    }

    [Theory]
    [InlineData("http://example.test")]
    [InlineData("https://example.test:8443")]
    [InlineData("https://example.test/path")]
    [InlineData("https://user@example.test")]
    public void AppleUrlServiceIdentifierRejectsNonOriginValue(
        string identifier)
    {
        Assert.False(MobileAutofillRequestAnalyzer.TryCreateAppleServiceOrigin(
            identifier,
            isDomainIdentifier: false,
            out _));
    }

    private static MobileAutofillRequest Allowed() => new(
        new Uri("https://example.test"),
        BrowserMappingFieldPurpose.Password,
        VaultObjectType.Secret,
        HasExactMapping: true,
        IsForegroundInvocation: true,
        HasFreshUserVerification: true);
}
