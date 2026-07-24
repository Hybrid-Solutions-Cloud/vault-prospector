using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace VaultProspector.BrowserProtocol;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AuthenticatedBrowserBrokerRequest(
    int ProtocolVersion,
    Guid BrokerRequestId,
    BrowserFamily BrowserFamily,
    string ExtensionId,
    string HostNonce,
    byte[] BrowserRequestUtf8,
    string AuthenticationTag);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AuthenticatedBrowserBrokerResponse(
    int ProtocolVersion,
    Guid BrokerRequestId,
    string DesktopNonce,
    byte[] BrowserResponseUtf8,
    string AuthenticationTag);

public static class BrowserBrokerAuthentication
{
    public const string KeyPurpose = "browser-broker-auth";

    public static AuthenticatedBrowserBrokerRequest CreateRequest(
        ReadOnlySpan<byte> key,
        BrowserFamily browserFamily,
        string extensionId,
        ReadOnlySpan<byte> browserRequestUtf8)
    {
        ValidateKey(key);
        ValidateExtensionId(browserFamily, extensionId);
        ValidateEmbeddedMessage(browserRequestUtf8);

        var brokerRequestId = Guid.NewGuid();
        var hostNonce = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var requestBytes = browserRequestUtf8.ToArray();
        var tag = ComputeRequestTag(
            key,
            brokerRequestId,
            browserFamily,
            extensionId,
            hostNonce,
            requestBytes);
        return new AuthenticatedBrowserBrokerRequest(
            BrowserProtocolConstants.CurrentVersion,
            brokerRequestId,
            browserFamily,
            extensionId,
            hostNonce,
            requestBytes,
            Convert.ToBase64String(tag));
    }

    public static void ValidateRequest(
        ReadOnlySpan<byte> key,
        AuthenticatedBrowserBrokerRequest request)
    {
        ValidateKey(key);
        if (request.ProtocolVersion != BrowserProtocolConstants.CurrentVersion ||
            request.BrokerRequestId == Guid.Empty)
        {
            throw new BrowserProtocolException("Broker request envelope is invalid.");
        }

        ValidateExtensionId(request.BrowserFamily, request.ExtensionId);
        ValidateNonce(request.HostNonce);
        ValidateEmbeddedMessage(request.BrowserRequestUtf8);
        var suppliedTag = DecodeTag(request.AuthenticationTag);
        var expectedTag = ComputeRequestTag(
            key,
            request.BrokerRequestId,
            request.BrowserFamily,
            request.ExtensionId,
            request.HostNonce,
            request.BrowserRequestUtf8);
        try
        {
            if (!CryptographicOperations.FixedTimeEquals(suppliedTag, expectedTag))
                throw new BrowserProtocolException("Broker request authentication failed.");
        }
        finally
        {
            CryptographicOperations.ZeroMemory(suppliedTag);
            CryptographicOperations.ZeroMemory(expectedTag);
        }
    }

    public static AuthenticatedBrowserBrokerResponse CreateResponse(
        ReadOnlySpan<byte> key,
        Guid brokerRequestId,
        ReadOnlySpan<byte> browserResponseUtf8)
    {
        ValidateKey(key);
        if (brokerRequestId == Guid.Empty)
            throw new BrowserProtocolException("Broker request identifier is missing.");
        ValidateEmbeddedMessage(browserResponseUtf8);

        var desktopNonce = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var responseBytes = browserResponseUtf8.ToArray();
        var tag = ComputeResponseTag(
            key,
            brokerRequestId,
            desktopNonce,
            responseBytes);
        return new AuthenticatedBrowserBrokerResponse(
            BrowserProtocolConstants.CurrentVersion,
            brokerRequestId,
            desktopNonce,
            responseBytes,
            Convert.ToBase64String(tag));
    }

    public static void ValidateResponse(
        ReadOnlySpan<byte> key,
        AuthenticatedBrowserBrokerResponse response,
        Guid expectedBrokerRequestId)
    {
        ValidateKey(key);
        if (response.ProtocolVersion != BrowserProtocolConstants.CurrentVersion ||
            response.BrokerRequestId == Guid.Empty ||
            response.BrokerRequestId != expectedBrokerRequestId)
        {
            throw new BrowserProtocolException("Broker response envelope is invalid.");
        }

        ValidateNonce(response.DesktopNonce);
        ValidateEmbeddedMessage(response.BrowserResponseUtf8);
        var suppliedTag = DecodeTag(response.AuthenticationTag);
        var expectedTag = ComputeResponseTag(
            key,
            response.BrokerRequestId,
            response.DesktopNonce,
            response.BrowserResponseUtf8);
        try
        {
            if (!CryptographicOperations.FixedTimeEquals(suppliedTag, expectedTag))
                throw new BrowserProtocolException("Broker response authentication failed.");
        }
        finally
        {
            CryptographicOperations.ZeroMemory(suppliedTag);
            CryptographicOperations.ZeroMemory(expectedTag);
        }
    }

    public static byte[] SerializeRequest(AuthenticatedBrowserBrokerRequest request)
    {
        var payload = JsonSerializer.SerializeToUtf8Bytes(
            request,
            BrowserProtocolJsonContext.Default.AuthenticatedBrowserBrokerRequest);
        EnsureSerializedLimit(payload);
        return payload;
    }

    public static AuthenticatedBrowserBrokerRequest ParseRequest(ReadOnlySpan<byte> payload) =>
        Parse(
            payload,
            "request",
            BrowserProtocolJsonContext.Default.AuthenticatedBrowserBrokerRequest);

    public static byte[] SerializeResponse(AuthenticatedBrowserBrokerResponse response)
    {
        var payload = JsonSerializer.SerializeToUtf8Bytes(
            response,
            BrowserProtocolJsonContext.Default.AuthenticatedBrowserBrokerResponse);
        EnsureSerializedLimit(payload);
        return payload;
    }

    public static AuthenticatedBrowserBrokerResponse ParseResponse(ReadOnlySpan<byte> payload) =>
        Parse(
            payload,
            "response",
            BrowserProtocolJsonContext.Default.AuthenticatedBrowserBrokerResponse);

    public static void ValidateExtensionId(BrowserFamily browserFamily, string extensionId)
    {
        if (!Enum.IsDefined(browserFamily) ||
            string.IsNullOrWhiteSpace(extensionId) ||
            extensionId.Length > 128 ||
            extensionId.Any(character => character < 0x21 || character > 0x7e))
        {
            throw new BrowserProtocolException("Extension identity is invalid.");
        }

        if (browserFamily == BrowserFamily.Chromium &&
            (extensionId.Length != 32 ||
             extensionId.Any(character => character is < 'a' or > 'p')))
        {
            throw new BrowserProtocolException("Chromium extension identity is invalid.");
        }

        if (browserFamily == BrowserFamily.Firefox &&
            !IsFirefoxExtensionId(extensionId))
        {
            throw new BrowserProtocolException("Firefox extension identity is invalid.");
        }
    }

    private static bool IsFirefoxExtensionId(string extensionId)
    {
        if (extensionId is ['{', .., '}'])
            return Guid.TryParse(extensionId[1..^1], out _);

        var separator = extensionId.IndexOf('@', StringComparison.Ordinal);
        return separator > 0 &&
               separator == extensionId.LastIndexOf('@') &&
               separator < extensionId.Length - 1 &&
               extensionId[(separator + 1)..].Contains('.', StringComparison.Ordinal);
    }

    private static T Parse<T>(
        ReadOnlySpan<byte> payload,
        string description,
        JsonTypeInfo<T> typeInfo)
    {
        if (payload.Length is < 2 or > BrowserProtocolConstants.MaximumNativeMessageBytes)
            throw new BrowserProtocolException($"Broker {description} size is outside the protocol limit.");

        BrowserMessageCodec.ValidateJsonObject(payload);
        try
        {
            return JsonSerializer.Deserialize(payload, typeInfo)
                ?? throw new BrowserProtocolException($"Broker {description} is empty.");
        }
        catch (JsonException exception)
        {
            throw new BrowserProtocolException(
                $"Broker {description} JSON does not match the protocol: {exception.Message}");
        }
    }

    private static byte[] ComputeRequestTag(
        ReadOnlySpan<byte> key,
        Guid requestId,
        BrowserFamily browserFamily,
        string extensionId,
        string nonce,
        ReadOnlySpan<byte> payload) =>
        ComputeTag(
            key,
            "VaultProspector.BrowserBroker.Request.v1",
            requestId,
            (int)browserFamily,
            extensionId,
            nonce,
            payload);

    private static byte[] ComputeResponseTag(
        ReadOnlySpan<byte> key,
        Guid requestId,
        string nonce,
        ReadOnlySpan<byte> payload) =>
        ComputeTag(
            key,
            "VaultProspector.BrowserBroker.Response.v1",
            requestId,
            0,
            string.Empty,
            nonce,
            payload);

    private static byte[] ComputeTag(
        ReadOnlySpan<byte> key,
        string domain,
        Guid requestId,
        int discriminator,
        string identity,
        string nonce,
        ReadOnlySpan<byte> payload)
    {
        using var stream = new MemoryStream();
        WriteLengthPrefixed(stream, Encoding.UTF8.GetBytes(domain));
        Span<byte> requestIdBytes = stackalloc byte[16];
        requestId.TryWriteBytes(requestIdBytes, true, out _);
        stream.Write(requestIdBytes);
        Span<byte> discriminatorBytes = stackalloc byte[sizeof(int)];
        BinaryPrimitives.WriteInt32LittleEndian(discriminatorBytes, discriminator);
        stream.Write(discriminatorBytes);
        WriteLengthPrefixed(stream, Encoding.UTF8.GetBytes(identity));
        WriteLengthPrefixed(stream, Encoding.UTF8.GetBytes(nonce));
        WriteLengthPrefixed(stream, payload);
        return HMACSHA256.HashData(key, stream.GetBuffer().AsSpan(0, checked((int)stream.Length)));
    }

    private static void WriteLengthPrefixed(Stream stream, ReadOnlySpan<byte> value)
    {
        Span<byte> length = stackalloc byte[sizeof(int)];
        BinaryPrimitives.WriteInt32LittleEndian(length, value.Length);
        stream.Write(length);
        stream.Write(value);
    }

    private static byte[] DecodeTag(string tag)
    {
        try
        {
            var bytes = Convert.FromBase64String(tag);
            if (bytes.Length != 32)
            {
                CryptographicOperations.ZeroMemory(bytes);
                throw new BrowserProtocolException("Broker authentication tag is invalid.");
            }

            return bytes;
        }
        catch (FormatException)
        {
            throw new BrowserProtocolException("Broker authentication tag is invalid.");
        }
    }

    private static void ValidateKey(ReadOnlySpan<byte> key)
    {
        if (key.Length != 32)
            throw new BrowserProtocolException("Broker authentication key is invalid.");
    }

    private static void ValidateNonce(string nonce)
    {
        try
        {
            var bytes = Convert.FromBase64String(nonce);
            try
            {
                if (bytes.Length != 32)
                    throw new BrowserProtocolException("Broker nonce is invalid.");
            }
            finally
            {
                CryptographicOperations.ZeroMemory(bytes);
            }
        }
        catch (FormatException)
        {
            throw new BrowserProtocolException("Broker nonce is invalid.");
        }
    }

    private static void ValidateEmbeddedMessage(ReadOnlySpan<byte> payload)
    {
        if (payload.Length is < 2 or > BrowserProtocolConstants.MaximumNativeMessageBytes / 2)
            throw new BrowserProtocolException("Embedded browser message is outside the broker limit.");
    }

    private static void EnsureSerializedLimit(ReadOnlySpan<byte> payload)
    {
        if (payload.Length > BrowserProtocolConstants.MaximumNativeMessageBytes)
            throw new BrowserProtocolException("Broker envelope exceeds the protocol limit.");
    }
}
