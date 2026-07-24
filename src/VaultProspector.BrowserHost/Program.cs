using System.Security.Cryptography;
using VaultProspector.Application;
using VaultProspector.BrowserProtocol;
using VaultProspector.Platform;

namespace VaultProspector.BrowserHost;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        byte[]? browserRequest = null;
        byte[]? browserResponse = null;
        byte[]? authenticationKey = null;
        var requestId = Guid.NewGuid();
        try
        {
            var applicationDirectory = AppContext.BaseDirectory;
            var configuration = BrowserHostConfiguration.Load(applicationDirectory);
            var caller = NativeHostCallerParser.Parse(args, applicationDirectory, configuration);

            browserRequest = await NativeMessageFraming.ReadAsync(
                Console.OpenStandardInput(),
                CancellationToken.None).ConfigureAwait(false);
            if (browserRequest is null)
                return 0;

            var validatedRequest = BrowserMessageCodec.ParseAndValidateRequest(
                browserRequest,
                DateTimeOffset.UtcNow);
            requestId = validatedRequest.Request.RequestId;
            if (validatedRequest.Request.BrowserFamily != caller.BrowserFamily)
                throw new BrowserProtocolException("Request browser family does not match the caller.");

            var keyProvider = new WindowsDataProtectionKeyProvider(VaultProspectorPaths.KeyDirectory);
            authenticationKey = await keyProvider.GetExistingKeyAsync(
                BrowserBrokerAuthentication.KeyPurpose,
                CancellationToken.None).ConfigureAwait(false);

            var broker = new BrowserDesktopBrokerClient(configuration.PipeName);
            browserResponse = await broker.SendAsync(
                authenticationKey,
                caller,
                browserRequest,
                CancellationToken.None).ConfigureAwait(false);
            var validatedResponse = BrowserMessageCodec.ParseAndValidateResponse(browserResponse);
            if (validatedResponse.RequestId != requestId)
                throw new BrowserProtocolException("Response request identifier does not match.");

            await NativeMessageFraming.WriteAsync(
                Console.OpenStandardOutput(),
                browserResponse,
                CancellationToken.None).ConfigureAwait(false);
            return 0;
        }
        catch (Exception exception) when (
            exception is BrowserProtocolException or
            IOException or
            UnauthorizedAccessException or
            OperationCanceledException or
            ProtectedKeyUnavailableException)
        {
            var failure = BrowserMessageCodec.SerializeResponse(
                BrowserFillResponse.Failure(requestId, BrowserFillResultCode.Unavailable));
            try
            {
                await NativeMessageFraming.WriteAsync(
                    Console.OpenStandardOutput(),
                    failure,
                    CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception writeException) when (
                writeException is IOException or
                BrowserProtocolException)
            {
                return 2;
            }
            finally
            {
                CryptographicOperations.ZeroMemory(failure);
            }

            return 1;
        }
        finally
        {
            if (browserRequest is not null)
                CryptographicOperations.ZeroMemory(browserRequest);
            if (browserResponse is not null)
                CryptographicOperations.ZeroMemory(browserResponse);
            if (authenticationKey is not null)
                CryptographicOperations.ZeroMemory(authenticationKey);
        }
    }
}
