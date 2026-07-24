using System.Text.Json.Serialization;

namespace VaultProspector.Infrastructure;

[JsonSerializable(typeof(Dictionary<string, string>))]
internal sealed partial class InfrastructureJsonContext : JsonSerializerContext;
