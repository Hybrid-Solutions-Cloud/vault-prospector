using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Microsoft.Win32.SafeHandles;
using VaultProspector.BrowserProtocol;

namespace VaultProspector.Platform;

public sealed class BrowserBrokerServer : IAsyncDisposable
{
    private static readonly TimeSpan ReplayRetention = TimeSpan.FromMinutes(2);

    private readonly string _pipeName;
    private readonly byte[] _authenticationKey;
    private readonly Func<BrowserFamily, string, bool> _extensionAllowed;
    private readonly Func<int, bool> _clientProcessAllowed;
    private readonly Func<
        ValidatedBrowserFillRequest,
        CancellationToken,
        Task<BrowserFillResponse>> _handler;
    private readonly CancellationTokenSource _shutdown = new();
    private readonly Dictionary<Guid, DateTimeOffset> _recentRequests = [];
    private Task? _runTask;
    private bool _disposed;

    public BrowserBrokerServer(
        string pipeName,
        ReadOnlySpan<byte> authenticationKey,
        Func<BrowserFamily, string, bool> extensionAllowed,
        Func<int, bool> clientProcessAllowed,
        Func<
            ValidatedBrowserFillRequest,
            CancellationToken,
            Task<BrowserFillResponse>> handler)
    {
        if (string.IsNullOrWhiteSpace(pipeName) ||
            pipeName.Length > 128 ||
            pipeName.Any(character =>
                character is not (>= 'A' and <= 'Z') and
                not (>= 'a' and <= 'z') and
                not (>= '0' and <= '9') and
                not '.' and
                not '-'))
        {
            throw new ArgumentException("Browser broker pipe name is invalid.", nameof(pipeName));
        }
        if (authenticationKey.Length != 32)
            throw new ArgumentException("Browser broker authentication key is invalid.", nameof(authenticationKey));

        _pipeName = pipeName;
        _authenticationKey = authenticationKey.ToArray();
        _extensionAllowed = extensionAllowed ?? throw new ArgumentNullException(nameof(extensionAllowed));
        _clientProcessAllowed =
            clientProcessAllowed ?? throw new ArgumentNullException(nameof(clientProcessAllowed));
        _handler = handler ?? throw new ArgumentNullException(nameof(handler));
    }

    public void Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_runTask is not null)
            throw new InvalidOperationException("Browser broker server is already running.");
        _runTask = RunAsync(_shutdown.Token);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;
        _disposed = true;
        await _shutdown.CancelAsync().ConfigureAwait(false);
        if (_runTask is not null)
        {
            try
            {
                await _runTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (_shutdown.IsCancellationRequested)
            {
            }
        }
        _shutdown.Dispose();
        CryptographicOperations.ZeroMemory(_authenticationKey);
        _recentRequests.Clear();
        GC.SuppressFinalize(this);
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            await using var pipe = new NamedPipeServerStream(
                _pipeName,
                PipeDirection.InOut,
                1,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous |
                PipeOptions.WriteThrough |
                PipeOptions.CurrentUserOnly);
            await pipe.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);
            if (!TryGetClientProcessId(pipe.SafePipeHandle, out var clientProcessId) ||
                clientProcessId > int.MaxValue ||
                !_clientProcessAllowed((int)clientProcessId))
            {
                continue;
            }
            await HandleConnectionAsync(pipe, cancellationToken).ConfigureAwait(false);
        }
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetNamedPipeClientProcessId(
        SafePipeHandle pipe,
        out uint clientProcessId);

    private static bool TryGetClientProcessId(
        SafePipeHandle pipe,
        out uint clientProcessId)
    {
        if (!OperatingSystem.IsWindows())
        {
            clientProcessId = 0;
            return false;
        }

        return GetNamedPipeClientProcessId(pipe, out clientProcessId);
    }

    private async Task HandleConnectionAsync(
        Stream pipe,
        CancellationToken cancellationToken)
    {
        byte[]? requestEnvelopeBytes = null;
        byte[]? responseEnvelopeBytes = null;
        byte[]? browserResponseBytes = null;
        AuthenticatedBrowserBrokerRequest? brokerRequest = null;
        try
        {
            requestEnvelopeBytes = await NativeMessageFraming.ReadAsync(pipe, cancellationToken)
                .ConfigureAwait(false);
            if (requestEnvelopeBytes is null)
                return;
            brokerRequest = BrowserBrokerAuthentication.ParseRequest(requestEnvelopeBytes);
            BrowserBrokerAuthentication.ValidateRequest(_authenticationKey, brokerRequest);
            if (!_extensionAllowed(brokerRequest.BrowserFamily, brokerRequest.ExtensionId))
                throw new BrowserProtocolException("Browser extension identity is not allowed.");
            RejectReplay(brokerRequest.BrokerRequestId);

            var request = BrowserMessageCodec.ParseAndValidateRequest(
                brokerRequest.BrowserRequestUtf8,
                DateTimeOffset.UtcNow);
            if (request.Request.BrowserFamily != brokerRequest.BrowserFamily)
                throw new BrowserProtocolException("Browser family does not match the authenticated caller.");

            BrowserFillResponse response;
            try
            {
                response = await _handler(request, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (
                exception is UnauthorizedAccessException or
                InvalidOperationException or
                KeyNotFoundException or
                OperationCanceledException)
            {
                response = BrowserFillResponse.Failure(
                    request.Request.RequestId,
                    BrowserFillResultCode.Denied);
            }

            browserResponseBytes = BrowserMessageCodec.SerializeResponse(response);
            var brokerResponse = BrowserBrokerAuthentication.CreateResponse(
                _authenticationKey,
                brokerRequest.BrokerRequestId,
                browserResponseBytes);
            responseEnvelopeBytes = BrowserBrokerAuthentication.SerializeResponse(brokerResponse);
            await NativeMessageFraming.WriteAsync(
                pipe,
                responseEnvelopeBytes,
                cancellationToken).ConfigureAwait(false);
            if (response.ValueUtf8 is not null)
                CryptographicOperations.ZeroMemory(response.ValueUtf8);
            CryptographicOperations.ZeroMemory(brokerResponse.BrowserResponseUtf8);
        }
        catch (Exception exception) when (
            exception is BrowserProtocolException or
            IOException or
            UnauthorizedAccessException or
            OperationCanceledException)
        {
            // Closing the authenticated local connection is the only safe response when the
            // envelope itself cannot be trusted. The browser host returns a generic failure.
        }
        finally
        {
            if (brokerRequest is not null)
                CryptographicOperations.ZeroMemory(brokerRequest.BrowserRequestUtf8);
            if (requestEnvelopeBytes is not null)
                CryptographicOperations.ZeroMemory(requestEnvelopeBytes);
            if (responseEnvelopeBytes is not null)
                CryptographicOperations.ZeroMemory(responseEnvelopeBytes);
            if (browserResponseBytes is not null)
                CryptographicOperations.ZeroMemory(browserResponseBytes);
        }
    }

    private void RejectReplay(Guid requestId)
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var expired in _recentRequests
                     .Where(entry => now - entry.Value > ReplayRetention)
                     .Select(entry => entry.Key)
                     .ToArray())
        {
            _recentRequests.Remove(expired);
        }

        if (!_recentRequests.TryAdd(requestId, now))
            throw new BrowserProtocolException("Browser broker request was replayed.");
        if (_recentRequests.Count > 256)
        {
            var oldest = _recentRequests.MinBy(entry => entry.Value);
            _recentRequests.Remove(oldest.Key);
        }
    }
}
