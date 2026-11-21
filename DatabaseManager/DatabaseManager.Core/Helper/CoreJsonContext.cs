using System.Text.Json;
using System.Text.Json.Serialization;
using DatabaseInterpreter.Model;
using DatabaseManager.Core.Model;

namespace DatabaseManager.Core
{
    [JsonSourceGenerationOptions(
        IncludeFields = true,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        WriteIndented = true)]
    [JsonSerializable(typeof(Setting))]
    [JsonSerializable(typeof(DbInterpreterSetting))]
    [JsonSerializable(typeof(TextEditorOption))]
    [JsonSerializable(typeof(ThemeOption))]
    [JsonSerializable(typeof(ProfilingInfo))]
    [JsonSerializable(typeof(Table))]
    [JsonSerializable(typeof(TableDesignerInfo))]
    [JsonSerializable(typeof(TableColumn))]
    [JsonSerializable(typeof(TableColumnDesingerInfo))]
    [JsonSerializable(typeof(TableForeignKey))]
    [JsonSerializable(typeof(TableForeignKeyDesignerInfo))]
    [JsonSerializable(typeof(TableConstraint))]
    [JsonSerializable(typeof(TableConstraintDesignerInfo))]
    [JsonSerializable(typeof(DataTypeSpecification))]
    [JsonSerializable(typeof(DataTypeDesignerInfo))]
    internal partial class CoreJsonContext : JsonSerializerContext
    {
    }
}
