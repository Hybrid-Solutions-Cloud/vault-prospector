using System.Diagnostics;
using System.IO.Pipes;
using System.Security.Cryptography;
using System.Text;
using VaultProspector.BrowserProtocol;
using VaultProspector.Platform;

namespace VaultProspector.Platform.Tests;

public sealed class BrowserBrokerServerTests
{
    [Fact]
    public void ProcessVerifierRequiresExactNonReparseExecutable()
    {
        using var process = Process.GetCurrentProcess();
        var executable = process.MainModule?.FileName
            ?? throw new InvalidOperationException("Current executable path is unavailable.");

        Assert.True(
            new WindowsBrowserHostProcessVerifier(executable)
                .IsAllowed(process.Id));
        Assert.False(
            new WindowsBrowserHostProcessVerifier(
                Path.Combine(Path.GetDirectoryName(executable)!, "other.exe"))
                .IsAllowed(process.Id));
    }

    [Fact]
    public async Task AuthenticatedCurrentUserRequestRoundTripsWithoutSecretPersistence()
    {
        var pipeName = $"VaultProspector.BrowserBroker.Tests.{Guid.NewGuid():N}";
        var key = RandomNumberGenerator.GetBytes(32);
        var requestId = Guid.NewGuid();
        var handlerCalls = 0;
        await using var server = new BrowserBrokerServer(
            pipeName,
            key,
            BrowserKnownIdentities.IsAllowed,
            _ => true,
            (request, _) =>
            {
                handlerCalls++;
                return Task.FromResult(
                    BrowserFillResponse.Failure(
                        request.Request.RequestId,
                        BrowserFillResultCode.Denied));
            });
        server.Start();

        var response = await SendAsync(
            pipeName,
            key,
            CreateBrokerRequest(key, requestId),
            TestContext.Current.CancellationToken);

        Assert.Equal(requestId, response.RequestId);
        Assert.Equal(BrowserFillResultCode.Denied, response.Result);
        Assert.Equal(1, handlerCalls);
    }

    [Fact]
    public async Task ReplayedAuthenticatedEnvelopeIsRejectedBeforeHandler()
    {
        var pipeName = $"VaultProspector.BrowserBroker.Tests.{Guid.NewGuid():N}";
        var key = RandomNumberGenerator.GetBytes(32);
        var handlerCalls = 0;
        await using var server = new BrowserBrokerServer(
            pipeName,
            key,
            BrowserKnownIdentities.IsAllowed,
            _ => true,
            (request, _) =>
            {
                handlerCalls++;
                return Task.FromResult(
                    BrowserFillResponse.Failure(
                        request.Request.RequestId,
                        BrowserFillResultCode.Denied));
            });
        server.Start();
        var brokerRequest = CreateBrokerRequest(key, Guid.NewGuid());

        _ = await SendAsync(
            pipeName,
            key,
            brokerRequest,
            TestContext.Current.CancellationToken);
        await Assert.ThrowsAnyAsync<Exception>(
            () => SendAsync(
                pipeName,
                key,
                brokerRequest,
                TestContext.Current.CancellationToken));

        Assert.Equal(1, handlerCalls);
    }

    [Fact]
    public async Task AuthenticatedButUnlistedExtensionIsRejected()
    {
        var pipeName = $"VaultProspector.BrowserBroker.Tests.{Guid.NewGuid():N}";
        var key = RandomNumberGenerator.GetBytes(32);
        var handlerCalls = 0;
        await using var server = new BrowserBrokerServer(
            pipeName,
            key,
            BrowserKnownIdentities.IsAllowed,
            _ => true,
            (_, _) =>
            {
                handlerCalls++;
                throw new InvalidOperationException("Handler must not run.");
            });
        server.Start();
        var browserRequest = RequestJson(Guid.NewGuid());
        var brokerRequest = BrowserBrokerAuthentication.CreateRequest(
            key,
            BrowserFamily.Chromium,
            "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
            browserRequest);

        await Assert.ThrowsAnyAsync<Exception>(
            () => SendAsync(
                pipeName,
                key,
                brokerRequest,
                TestContext.Current.CancellationToken));

        Assert.Equal(0, handlerCalls);
    }

    [Fact]
    public async Task UnapprovedClientProcessIsRejectedBeforeEnvelopeParsing()
    {
        var pipeName = $"VaultProspector.BrowserBroker.Tests.{Guid.NewGuid():N}";
        var key = RandomNumberGenerator.GetBytes(32);
        var handlerCalls = 0;
        await using var server = new BrowserBrokerServer(
            pipeName,
            key,
            BrowserKnownIdentities.IsAllowed,
            _ => false,
            (_, _) =>
            {
                handlerCalls++;
                throw new InvalidOperationException("Handler must not run.");
            });
        server.Start();

        await Assert.ThrowsAnyAsync<Exception>(
            () => SendAsync(
                pipeName,
                key,
                CreateBrokerRequest(key, Guid.NewGuid()),
                TestContext.Current.CancellationToken));

        Assert.Equal(0, handlerCalls);
    }

    private static AuthenticatedBrowserBrokerRequest CreateBrokerRequest(
        byte[] key,
        Guid requestId) =>
        BrowserBrokerAuthentication.CreateRequest(
            key,
            BrowserFamily.Chromium,
            BrowserKnownIdentities.ChromiumDevelopment,
            RequestJson(requestId));

    private static byte[] RequestJson(Guid requestId) =>
        Encoding.UTF8.GetBytes(
            $$"""
            {
              "protocolVersion":1,
              "operation":"requestFill",
              "requestId":"{{requestId:D}}",
              "browserFamily":"chromium",
              "tabId":42,
              "frameId":0,
              "documentId":"document-token",
              "gestureNonce":"gesture-token",
              "createdAtUtc":"{{DateTimeOffset.UtcNow:O}}",
              "topOrigin":"https://login.example.com",
              "frameOrigin":"https://login.example.com",
              "fieldPurpose":"password",
              "fieldToken":"field-token"
            }
            """);

    private static async Task<BrowserFillResponse> SendAsync(
        string pipeName,
        byte[] key,
        AuthenticatedBrowserBrokerRequest brokerRequest,
        CancellationToken cancellationToken)
    {
        await using var client = new NamedPipeClientStream(
            ".",
            pipeName,
            PipeDirection.InOut,
            PipeOptions.Asynchronous | PipeOptions.WriteThrough);
        await client.ConnectAsync(cancellationToken);
        var requestEnvelope =
            BrowserBrokerAuthentication.SerializeRequest(brokerRequest);
        await NativeMessageFraming.WriteAsync(
            client,
            requestEnvelope,
            cancellationToken);
        var responseEnvelope = await NativeMessageFraming.ReadAsync(
            client,
            cancellationToken)
            ?? throw new IOException("Broker closed without a response.");
        var brokerResponse =
            BrowserBrokerAuthentication.ParseResponse(responseEnvelope);
        BrowserBrokerAuthentication.ValidateResponse(
            key,
            brokerResponse,
            brokerRequest.BrokerRequestId);
        return BrowserMessageCodec.ParseAndValidateResponse(
            brokerResponse.BrowserResponseUtf8);
    }
}
