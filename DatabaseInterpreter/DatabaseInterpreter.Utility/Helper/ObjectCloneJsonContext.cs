using System.Text.Json;
using System.Text.Json.Serialization;
using DatabaseInterpreter.Model;

namespace DatabaseInterpreter.Utility
{
    [JsonSourceGenerationOptions(IncludeFields = true, PropertyNameCaseInsensitive = true)]
    [JsonSerializable(typeof(DatabaseObject))]
    [JsonSerializable(typeof(Database))]
    [JsonSerializable(typeof(DatabaseSchema))]
    [JsonSerializable(typeof(Table))]
    [JsonSerializable(typeof(View))]
    [JsonSerializable(typeof(Function))]
    [JsonSerializable(typeof(Procedure))]
    [JsonSerializable(typeof(Sequence))]
    [JsonSerializable(typeof(ScriptDbObject))]
    [JsonSerializable(typeof(UserDefinedType))]
    [JsonSerializable(typeof(UserDefinedTypeAttribute))]
    [JsonSerializable(typeof(RoutineParameter))]
    [JsonSerializable(typeof(TableChild))]
    [JsonSerializable(typeof(TableColumn))]
    [JsonSerializable(typeof(TablePrimaryKey))]
    [JsonSerializable(typeof(TableForeignKey))]
    [JsonSerializable(typeof(ForeignKeyColumn))]
    [JsonSerializable(typeof(TableIndex))]
    [JsonSerializable(typeof(IndexColumn))]
    [JsonSerializable(typeof(TableConstraint))]
    [JsonSerializable(typeof(TableTrigger))]
    [JsonSerializable(typeof(TableColumnChild))]
    [JsonSerializable(typeof(TableDefaultValueConstraint))]
    [JsonSerializable(typeof(ConnectionInfo))]
    internal partial class ObjectCloneJsonContext : JsonSerializerContext
    {
    }
}
