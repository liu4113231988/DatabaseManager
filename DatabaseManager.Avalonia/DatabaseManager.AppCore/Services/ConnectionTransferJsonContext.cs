using System.Text.Json;
using System.Text.Json.Serialization;
using DatabaseManager.AppCore.Models;

namespace DatabaseManager.AppCore.Services;

[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true, AllowTrailingCommas = true,
    ReadCommentHandling = JsonCommentHandling.Skip, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(ConnectionItem))]
[JsonSerializable(typeof(List<ConnectionTransferEntry>))]
[JsonSerializable(typeof(ConnectionTransferEntry[]))]
internal partial class ConnectionTransferPayloadContext : JsonSerializerContext;

[JsonSourceGenerationOptions(WriteIndented = true, PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(ConnectionTransferEnvelope))]
internal partial class ConnectionTransferEnvelopeContext : JsonSerializerContext;
