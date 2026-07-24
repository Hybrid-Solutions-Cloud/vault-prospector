using System.Text.Json;
using System.Text.Json.Serialization;

namespace VaultProspector.BrowserProtocol;

[JsonSourceGenerationOptions(
    JsonSerializerDefaults.Web,
    AllowTrailingCommas = false,
    GenerationMode = JsonSourceGenerationMode.Metadata,
    MaxDepth = 16,
    NumberHandling = JsonNumberHandling.Strict,
    PropertyNameCaseInsensitive = false,
    ReadCommentHandling = JsonCommentHandling.Disallow,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    UseStringEnumConverter = true)]
[JsonSerializable(typeof(AuthenticatedBrowserBrokerRequest))]
[JsonSerializable(typeof(AuthenticatedBrowserBrokerResponse))]
[JsonSerializable(typeof(BrowserFillRequest))]
[JsonSerializable(typeof(BrowserFillResponse))]
internal sealed partial class BrowserProtocolJsonContext : JsonSerializerContext;
