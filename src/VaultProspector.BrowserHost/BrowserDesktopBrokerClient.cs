using System.IO.Pipes;
using System.Security.Cryptography;
using VaultProspector.BrowserProtocol;

namespace VaultProspector.BrowserHost;

public sealed class BrowserDesktopBrokerClient(string pipeName)
{
    private static readonly TimeSpan ConnectionTimeout = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(40);

    public async Task<byte[]> SendAsync(
        ReadOnlyMemory<byte> authenticationKey,
        NativeHostCaller caller,
        ReadOnlyMemory<byte> browserRequestUtf8,
        CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(RequestTimeout);
        await using var pipe = new NamedPipeClientStream(
            ".",
            pipeName,
            PipeDirection.InOut,
            PipeOptions.Asynchronous | PipeOptions.WriteThrough);

        using (var connection = CancellationTokenSource.CreateLinkedTokenSource(timeout.Token))
        {
            connection.CancelAfter(ConnectionTimeout);
            await pipe.ConnectAsync(connection.Token).ConfigureAwait(false);
        }

        var brokerRequest = BrowserBrokerAuthentication.CreateRequest(
            authenticationKey.Span,
            caller.BrowserFamily,
            caller.ExtensionId,
            browserRequestUtf8.Span);
        var requestEnvelope = BrowserBrokerAuthentication.SerializeRequest(brokerRequest);
        try
        {
            await NativeMessageFraming.WriteAsync(pipe, requestEnvelope, timeout.Token)
                .ConfigureAwait(false);
            var responseEnvelope = await NativeMessageFraming.ReadAsync(pipe, timeout.Token)
                .ConfigureAwait(false)
                ?? throw new BrowserProtocolException("Desktop broker closed without a response.");
            try
            {
                var brokerResponse = BrowserBrokerAuthentication.ParseResponse(responseEnvelope);
                BrowserBrokerAuthentication.ValidateResponse(
                    authenticationKey.Span,
                    brokerResponse,
                    brokerRequest.BrokerRequestId);
                return brokerResponse.BrowserResponseUtf8;
            }
            finally
            {
                CryptographicOperations.ZeroMemory(responseEnvelope);
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(requestEnvelope);
            CryptographicOperations.ZeroMemory(brokerRequest.BrowserRequestUtf8);
        }
    }
}
