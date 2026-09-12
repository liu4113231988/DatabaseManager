using DatabaseInterpreter.Core;
using DatabaseInterpreter.Model;
using DatabaseManager.AppCore.Models;

namespace DatabaseManager.AppCore.Services;

public sealed record ForeignKeyChoices(TableForeignKey Key, QueryResult Values);

public static class ForeignKeyValueService
{
    public static async Task<IReadOnlyList<ForeignKeyChoices>> LoadAsync(ConnectionItem connection, DataTableInfo table, CancellationToken ct)
    {
        var info = ConnectionHelper.ToConnectionInfo(connection);
        info.Database = table.DatabaseName;
        var interpreter = DbInterpreterHelper.GetDbInterpreter(ConnectionHelper.ParseDatabaseType(connection.DatabaseType), info,
            new DbInterpreterOption { ThrowExceptionWhenErrorOccurs = true });
        using var db = interpreter.CreateConnection();
        await db.OpenAsync(ct);
        var keys = await interpreter.GetTableForeignKeysAsync(db, new SchemaInfoFilter { Schema = table.Schema, TableNames = new[] { table.Name } });
        var result = new List<ForeignKeyChoices>();
        foreach (var key in keys)
        {
            ct.ThrowIfCancellationRequested();
            if (key.Columns.Count == 0) continue;
            using var cmd = db.CreateCommand();
            cmd.CommandTimeout = 30;
            cmd.CommandText = $"SELECT {string.Join(",", key.Columns.Select(c => interpreter.GetQuotedString(c.ReferencedColumnName)))} FROM {interpreter.GetQuotedDbObjectNameWithSchema(key.ReferencedSchema, key.ReferencedTableName)}";
            using var reader = await cmd.ExecuteReaderAsync(ct);
            var rows = new List<IReadOnlyList<string>>();
            while (rows.Count < 200 && await reader.ReadAsync(ct))
                rows.Add(Enumerable.Range(0, reader.FieldCount).Select(i => reader.IsDBNull(i) ? "" : reader.GetValue(i).ToString() ?? "").ToArray());
            result.Add(new ForeignKeyChoices(key, new QueryResult { Columns = key.Columns.Select(c => c.ColumnName).ToArray(), Rows = rows, RowCount = rows.Count }));
        }
        return result;
    }
}
