using System.Text.Json.Serialization;

namespace VaultProspector.BrowserProtocol;

public static class BrowserProtocolConstants
{
    public const int CurrentVersion = 1;
    public const string FillOperation = "requestFill";
    public const int MaximumNativeMessageBytes = 64 * 1024;
    public static readonly TimeSpan RequestLifetime = TimeSpan.FromSeconds(30);
    public static readonly TimeSpan MaximumClockSkew = TimeSpan.FromSeconds(5);
}

public enum BrowserFamily
{
    Chromium,
    Firefox,
}

public enum BrowserFieldPurpose
{
    Username,
    Password,
    OneTimeCode,
}

public enum BrowserFillResultCode
{
    Approved,
    Denied,
    Unavailable,
    InvalidRequest,
    Expired,
    ChangedContext,
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record BrowserFillRequest(
    int ProtocolVersion,
    string Operation,
    Guid RequestId,
    BrowserFamily BrowserFamily,
    long TabId,
    long FrameId,
    string DocumentId,
    string GestureNonce,
    DateTimeOffset CreatedAtUtc,
    string TopOrigin,
    string FrameOrigin,
    BrowserFieldPurpose FieldPurpose,
    string FieldToken);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record BrowserFillResponse(
    int ProtocolVersion,
    Guid RequestId,
    BrowserFillResultCode Result,
    string? TransactionNonce,
    Guid? MappingId,
    byte[]? ValueUtf8)
{
    public static BrowserFillResponse Failure(Guid requestId, BrowserFillResultCode result) =>
        new(BrowserProtocolConstants.CurrentVersion, requestId, result, null, null, null);
}

public sealed record ValidatedBrowserFillRequest(
    BrowserFillRequest Request,
    CanonicalBrowserOrigin TopOrigin,
    CanonicalBrowserOrigin FrameOrigin);

public sealed class BrowserProtocolException(string message) : Exception(message);
