using System.Text.Json;
using System.Text.Json.Serialization;
using DatabaseInterpreter.Model;

namespace DatabaseInterpreter.Core;

[JsonSourceGenerationOptions(IncludeFields = true, PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(SchemaInfo))]
internal partial class SchemaInfoJsonContext : JsonSerializerContext;
