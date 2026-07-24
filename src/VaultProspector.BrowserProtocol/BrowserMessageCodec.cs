using System.Text;
using System.Text.Json;

namespace VaultProspector.BrowserProtocol;

public static class BrowserMessageCodec
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    public static ValidatedBrowserFillRequest ParseAndValidateRequest(
        ReadOnlySpan<byte> payload,
        DateTimeOffset now)
    {
        if (payload.Length is < 2 or > BrowserProtocolConstants.MaximumNativeMessageBytes)
            throw new BrowserProtocolException("Native message size is outside the protocol limit.");

        ValidateJsonObject(payload);

        BrowserFillRequest request;
        try
        {
            var json = StrictUtf8.GetString(payload);
            request = JsonSerializer.Deserialize(
                json,
                BrowserProtocolJsonContext.Default.BrowserFillRequest)
                ?? throw new BrowserProtocolException("Request is empty.");
        }
        catch (DecoderFallbackException exception)
        {
            throw new BrowserProtocolException($"Request is not valid UTF-8: {exception.Message}");
        }
        catch (JsonException exception)
        {
            throw new BrowserProtocolException($"Request JSON does not match the protocol: {exception.Message}");
        }

        if (request.ProtocolVersion != BrowserProtocolConstants.CurrentVersion)
            throw new BrowserProtocolException("Protocol version is not supported.");

        if (!string.Equals(request.Operation, BrowserProtocolConstants.FillOperation, StringComparison.Ordinal))
            throw new BrowserProtocolException("Operation is not supported.");

        if (request.RequestId == Guid.Empty)
            throw new BrowserProtocolException("Request identifier is missing.");

        if (!Enum.IsDefined(request.BrowserFamily) || !Enum.IsDefined(request.FieldPurpose))
            throw new BrowserProtocolException("Request contains an unsupported enum value.");

        if (request.TabId < 0 || request.FrameId < 0)
            throw new BrowserProtocolException("Tab and frame identifiers must be non-negative.");

        ValidateOpaqueToken(request.DocumentId, nameof(request.DocumentId), 128);
        ValidateOpaqueToken(request.GestureNonce, nameof(request.GestureNonce), 128);
        ValidateOpaqueToken(request.FieldToken, nameof(request.FieldToken), 128);

        if (request.CreatedAtUtc > now + BrowserProtocolConstants.MaximumClockSkew)
            throw new BrowserProtocolException("Request timestamp is in the future.");

        if (now - request.CreatedAtUtc > BrowserProtocolConstants.RequestLifetime)
            throw new BrowserProtocolException("Request has expired.");

        var topOrigin = CanonicalBrowserOrigin.Parse(request.TopOrigin);
        var frameOrigin = CanonicalBrowserOrigin.Parse(request.FrameOrigin);

        if (!string.Equals(request.TopOrigin, topOrigin.SerializedOrigin, StringComparison.Ordinal) ||
            !string.Equals(request.FrameOrigin, frameOrigin.SerializedOrigin, StringComparison.Ordinal))
        {
            throw new BrowserProtocolException("Origin is not in canonical serialized form.");
        }

        return new ValidatedBrowserFillRequest(request, topOrigin, frameOrigin);
    }

    public static byte[] SerializeResponse(BrowserFillResponse response)
    {
        ValidateResponse(response);
        var payload = JsonSerializer.SerializeToUtf8Bytes(
            response,
            BrowserProtocolJsonContext.Default.BrowserFillResponse);
        if (payload.Length > BrowserProtocolConstants.MaximumNativeMessageBytes)
            throw new BrowserProtocolException("Response exceeds the protocol limit.");
        return payload;
    }

    public static BrowserFillResponse ParseAndValidateResponse(ReadOnlySpan<byte> payload)
    {
        if (payload.Length is < 2 or > BrowserProtocolConstants.MaximumNativeMessageBytes)
            throw new BrowserProtocolException("Native message size is outside the protocol limit.");

        ValidateJsonObject(payload);

        BrowserFillResponse response;
        try
        {
            var json = StrictUtf8.GetString(payload);
            response = JsonSerializer.Deserialize(
                json,
                BrowserProtocolJsonContext.Default.BrowserFillResponse)
                ?? throw new BrowserProtocolException("Response is empty.");
        }
        catch (DecoderFallbackException exception)
        {
            throw new BrowserProtocolException($"Response is not valid UTF-8: {exception.Message}");
        }
        catch (JsonException exception)
        {
            throw new BrowserProtocolException($"Response JSON does not match the protocol: {exception.Message}");
        }

        ValidateResponse(response);
        return response;
    }

    private static void ValidateResponse(BrowserFillResponse response)
    {
        if (response.ProtocolVersion != BrowserProtocolConstants.CurrentVersion ||
            response.RequestId == Guid.Empty ||
            !Enum.IsDefined(response.Result))
        {
            throw new BrowserProtocolException("Response envelope is invalid.");
        }

        var approved = response.Result == BrowserFillResultCode.Approved;
        if (approved)
        {
            ValidateOpaqueToken(response.TransactionNonce, nameof(response.TransactionNonce), 128);
            if (response.MappingId is null || response.MappingId == Guid.Empty ||
                response.ValueUtf8 is null or { Length: 0 } ||
                response.ValueUtf8.Length > BrowserProtocolConstants.MaximumNativeMessageBytes / 2)
            {
                throw new BrowserProtocolException("Approved response is incomplete or exceeds the value limit.");
            }
        }
        else if (response.TransactionNonce is not null ||
                 response.MappingId is not null ||
                 response.ValueUtf8 is not null)
        {
            throw new BrowserProtocolException("Failure response must not contain sensitive fields.");
        }
    }

    private static void ValidateOpaqueToken(string? value, string name, int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            value.Length > maximumLength ||
            value.Any(character => character < 0x21 || character > 0x7e))
        {
            throw new BrowserProtocolException($"{name} is missing or invalid.");
        }
    }

    public static void ValidateJsonObject(ReadOnlySpan<byte> payload)
    {
        try
        {
            var reader = new Utf8JsonReader(payload, new JsonReaderOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow,
                MaxDepth = 16,
            });
            var objectProperties = new Stack<HashSet<string>>();

            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.StartObject)
                {
                    objectProperties.Push(new HashSet<string>(StringComparer.Ordinal));
                }
                else if (reader.TokenType == JsonTokenType.EndObject)
                {
                    objectProperties.Pop();
                }
                else if (reader.TokenType == JsonTokenType.PropertyName)
                {
                    if (objectProperties.Count == 0)
                        throw new BrowserProtocolException("Property appears outside an object.");

                    var propertyName = reader.GetString()
                        ?? throw new BrowserProtocolException("Property name is invalid.");
                    if (!objectProperties.Peek().Add(propertyName))
                        throw new BrowserProtocolException($"Duplicate JSON property '{propertyName}' is not allowed.");
                }
            }

            if (reader.TokenType is not JsonTokenType.EndObject || objectProperties.Count != 0)
                throw new BrowserProtocolException("Native message must contain one complete JSON object.");
        }
        catch (JsonException exception)
        {
            throw new BrowserProtocolException($"Native message is invalid JSON: {exception.Message}");
        }
    }
}
