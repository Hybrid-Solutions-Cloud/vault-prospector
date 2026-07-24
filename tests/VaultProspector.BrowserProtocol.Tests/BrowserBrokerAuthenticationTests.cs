using System.Security.Cryptography;
using System.Text;
using VaultProspector.BrowserProtocol;

namespace VaultProspector.BrowserProtocol.Tests;

public sealed class BrowserBrokerAuthenticationTests
{
    private const string ChromiumId = "abcdefghijklmnopabcdefghijklmnop";

    [Fact]
    public void RequestAuthenticationRoundTrips()
    {
        var key = RandomNumberGenerator.GetBytes(32);
        var message = Encoding.UTF8.GetBytes("{\"safe\":true}");
        var request = BrowserBrokerAuthentication.CreateRequest(
            key,
            BrowserFamily.Chromium,
            ChromiumId,
            message);

        var payload = BrowserBrokerAuthentication.SerializeRequest(request);
        var parsed = BrowserBrokerAuthentication.ParseRequest(payload);
        BrowserBrokerAuthentication.ValidateRequest(key, parsed);

        Assert.Equal(message, parsed.BrowserRequestUtf8);
    }

    [Fact]
    public void RequestAuthenticationRejectsPayloadTampering()
    {
        var key = RandomNumberGenerator.GetBytes(32);
        var request = BrowserBrokerAuthentication.CreateRequest(
            key,
            BrowserFamily.Chromium,
            ChromiumId,
            Encoding.UTF8.GetBytes("{\"safe\":true}"));
        request.BrowserRequestUtf8[2] ^= 0x01;

        Assert.Throws<BrowserProtocolException>(
            () => BrowserBrokerAuthentication.ValidateRequest(key, request));
    }

    [Fact]
    public void RequestAuthenticationRejectsWrongKey()
    {
        var request = BrowserBrokerAuthentication.CreateRequest(
            RandomNumberGenerator.GetBytes(32),
            BrowserFamily.Chromium,
            ChromiumId,
            Encoding.UTF8.GetBytes("{\"safe\":true}"));

        Assert.Throws<BrowserProtocolException>(
            () => BrowserBrokerAuthentication.ValidateRequest(
                RandomNumberGenerator.GetBytes(32),
                request));
    }

    [Fact]
    public void ResponseAuthenticationBindsBrokerRequest()
    {
        var key = RandomNumberGenerator.GetBytes(32);
        var requestId = Guid.NewGuid();
        var response = BrowserBrokerAuthentication.CreateResponse(
            key,
            requestId,
            Encoding.UTF8.GetBytes("{\"safe\":true}"));

        BrowserBrokerAuthentication.ValidateResponse(key, response, requestId);
        Assert.Throws<BrowserProtocolException>(
            () => BrowserBrokerAuthentication.ValidateResponse(key, response, Guid.NewGuid()));
    }

    [Theory]
    [InlineData(BrowserFamily.Chromium, "abcdefghijklmnopabcdefghijklmnop")]
    [InlineData(BrowserFamily.Firefox, "vault-prospector@hybrid-solutions.cloud")]
    [InlineData(BrowserFamily.Firefox, "{5b75f934-2867-44eb-b53b-d909f9068353}")]
    public void ExtensionIdentityAcceptsSupportedForms(BrowserFamily family, string extensionId)
    {
        BrowserBrokerAuthentication.ValidateExtensionId(family, extensionId);
    }

    [Theory]
    [InlineData(BrowserFamily.Chromium, "abcdefghijklmnop")]
    [InlineData(BrowserFamily.Chromium, "zzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzz")]
    [InlineData(BrowserFamily.Firefox, "not-an-id")]
    [InlineData(BrowserFamily.Firefox, "local@host")]
    public void ExtensionIdentityRejectsUnsupportedForms(BrowserFamily family, string extensionId)
    {
        Assert.Throws<BrowserProtocolException>(
            () => BrowserBrokerAuthentication.ValidateExtensionId(family, extensionId));
    }
}
