using System.Text.Json;
using System.Text.Json.Serialization;

namespace DatabaseManager.AppCore.Templating;

[JsonSourceGenerationOptions(WriteIndented = true, PropertyNameCaseInsensitive = true,
    AllowTrailingCommas = true, ReadCommentHandling = JsonCommentHandling.Skip)]
[JsonSerializable(typeof(TemplateManifest))]
[JsonSerializable(typeof(List<TemplateManifest>))]
[JsonSerializable(typeof(TemplateTransferEnvelope))]
internal partial class TemplateJsonContext : JsonSerializerContext;
