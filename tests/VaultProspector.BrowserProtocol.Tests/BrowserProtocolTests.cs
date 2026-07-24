using System.Buffers.Binary;
using System.Text;
using VaultProspector.BrowserProtocol;

namespace VaultProspector.BrowserProtocol.Tests;

public sealed class BrowserProtocolTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 23, 16, 0, 0, TimeSpan.Zero);

    [Fact]
    public void CanonicalOriginNormalizesUnicodeHost()
    {
        var origin = CanonicalBrowserOrigin.Parse("https://xn--bcher-kva.example");

        Assert.Equal("https://xn--bcher-kva.example", origin.SerializedOrigin);
        Assert.Equal("bücher.example", origin.UnicodeHost);
    }

    [Theory]
    [InlineData("http://example.com")]
    [InlineData("https://user@example.com")]
    [InlineData("https://example.com/path")]
    [InlineData("https://example.com?query=1")]
    [InlineData("https://127.0.0.1")]
    [InlineData("https://localhost")]
    [InlineData("https://example.com.")]
    public void CanonicalOriginRejectsUnsafeForms(string value)
    {
        Assert.Throws<BrowserProtocolException>(() => CanonicalBrowserOrigin.Parse(value));
    }

    [Fact]
    public void RequestCodecAcceptsCanonicalCurrentRequest()
    {
        var request = ValidRequestJson(Now);

        var result = BrowserMessageCodec.ParseAndValidateRequest(Encoding.UTF8.GetBytes(request), Now);

        Assert.Equal("https://login.example.com", result.TopOrigin.SerializedOrigin);
        Assert.Equal(BrowserFieldPurpose.Password, result.Request.FieldPurpose);
    }

    [Theory]
    [InlineData("\"protocolVersion\":2", "Protocol version")]
    [InlineData("\"operation\":\"enumerate\"", "Operation")]
    [InlineData("\"tabId\":-1", "Tab and frame")]
    [InlineData("\"topOrigin\":\"http://login.example.com\"", "HTTPS")]
    [InlineData("\"topOrigin\":\"https://LOGIN.example.com\"", "canonical")]
    public void RequestCodecRejectsInvalidSecurityFields(string replacement, string expectedMessage)
    {
        var json = ValidRequestJson(Now);
        var property = replacement[..replacement.IndexOf(':')];
        var start = json.IndexOf(property, StringComparison.Ordinal);
        var end = json.IndexOf(',', start);
        var current = json[start..end];
        json = json.Replace(current, replacement, StringComparison.Ordinal);

        var exception = Assert.Throws<BrowserProtocolException>(
            () => BrowserMessageCodec.ParseAndValidateRequest(Encoding.UTF8.GetBytes(json), Now));

        Assert.Contains(expectedMessage, exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RequestCodecRejectsExpiredRequest()
    {
        var json = ValidRequestJson(Now - TimeSpan.FromSeconds(31));

        var exception = Assert.Throws<BrowserProtocolException>(
            () => BrowserMessageCodec.ParseAndValidateRequest(Encoding.UTF8.GetBytes(json), Now));

        Assert.Contains("expired", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RequestCodecRejectsFutureRequest()
    {
        var json = ValidRequestJson(Now + TimeSpan.FromSeconds(6));

        var exception = Assert.Throws<BrowserProtocolException>(
            () => BrowserMessageCodec.ParseAndValidateRequest(Encoding.UTF8.GetBytes(json), Now));

        Assert.Contains("future", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RequestCodecRejectsDuplicateProperty()
    {
        var json = ValidRequestJson(Now).Replace(
            "\"protocolVersion\":1,",
            "\"protocolVersion\":1,\"protocolVersion\":1,",
            StringComparison.Ordinal);

        var exception = Assert.Throws<BrowserProtocolException>(
            () => BrowserMessageCodec.ParseAndValidateRequest(Encoding.UTF8.GetBytes(json), Now));

        Assert.Contains("Duplicate", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RequestCodecRejectsUnknownProperty()
    {
        var json = ValidRequestJson(Now).Replace(
            "\"fieldToken\":\"field-token\"",
            "\"fieldToken\":\"field-token\",\"extra\":true",
            StringComparison.Ordinal);

        Assert.Throws<BrowserProtocolException>(
            () => BrowserMessageCodec.ParseAndValidateRequest(Encoding.UTF8.GetBytes(json), Now));
    }

    [Fact]
    public void FailureResponseRejectsSensitiveFields()
    {
        var response = new BrowserFillResponse(
            BrowserProtocolConstants.CurrentVersion,
            Guid.NewGuid(),
            BrowserFillResultCode.Denied,
            "nonce",
            Guid.NewGuid(),
            [1]);

        Assert.Throws<BrowserProtocolException>(() => BrowserMessageCodec.SerializeResponse(response));
    }

    [Fact]
    public void ApprovedResponseRoundTrips()
    {
        var response = new BrowserFillResponse(
            BrowserProtocolConstants.CurrentVersion,
            Guid.NewGuid(),
            BrowserFillResultCode.Approved,
            "transaction-nonce",
            Guid.NewGuid(),
            Encoding.UTF8.GetBytes("synthetic-value"));

        var payload = BrowserMessageCodec.SerializeResponse(response);
        var roundTrip = BrowserMessageCodec.ParseAndValidateResponse(payload);

        Assert.Equal(response.ProtocolVersion, roundTrip.ProtocolVersion);
        Assert.Equal(response.RequestId, roundTrip.RequestId);
        Assert.Equal(response.Result, roundTrip.Result);
        Assert.Equal(response.TransactionNonce, roundTrip.TransactionNonce);
        Assert.Equal(response.MappingId, roundTrip.MappingId);
        Assert.Equal(response.ValueUtf8, roundTrip.ValueUtf8);
    }

    [Fact]
    public async Task FramingRoundTripsPayload()
    {
        var payload = Encoding.UTF8.GetBytes("{\"safe\":true}");
        await using var stream = new MemoryStream();

        await NativeMessageFraming.WriteAsync(stream, payload, CancellationToken.None);
        stream.Position = 0;
        var result = await NativeMessageFraming.ReadAsync(stream, CancellationToken.None);

        Assert.Equal(payload, result);
    }

    [Fact]
    public async Task FramingRejectsOversizedLengthBeforeAllocation()
    {
        var header = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(
            header,
            BrowserProtocolConstants.MaximumNativeMessageBytes + 1u);
        await using var stream = new MemoryStream(header);

        await Assert.ThrowsAsync<BrowserProtocolException>(
            () => NativeMessageFraming.ReadAsync(stream, CancellationToken.None));
    }

    [Fact]
    public async Task FramingRejectsTruncatedPayload()
    {
        var bytes = new byte[6];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(0, 4), 5);
        await using var stream = new MemoryStream(bytes);

        await Assert.ThrowsAsync<BrowserProtocolException>(
            () => NativeMessageFraming.ReadAsync(stream, CancellationToken.None));
    }

    [Fact]
    public async Task FramingReturnsNullAtCleanEndOfStream()
    {
        await using var stream = new MemoryStream();

        Assert.Null(await NativeMessageFraming.ReadAsync(stream, CancellationToken.None));
    }

    private static string ValidRequestJson(DateTimeOffset createdAt) =>
        $$"""
        {
          "protocolVersion":1,
          "operation":"requestFill",
          "requestId":"5b75f934-2867-44eb-b53b-d909f9068353",
          "browserFamily":"chromium",
          "tabId":42,
          "frameId":0,
          "documentId":"document-token",
          "gestureNonce":"gesture-token",
          "createdAtUtc":"{{createdAt:O}}",
          "topOrigin":"https://login.example.com",
          "frameOrigin":"https://login.example.com",
          "fieldPurpose":"password",
          "fieldToken":"field-token"
        }
        """;
}
