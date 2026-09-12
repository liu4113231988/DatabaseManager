using DatabaseManager.AppCore.Models;
using DatabaseManager.AppCore.Services;
using DatabaseManager.AppCore.ViewModels;
using Npgsql;

internal static class PostgresP0Checks
{
    public static async Task RunAsync()
    {
        var connectionString = Environment.GetEnvironmentVariable("DBSMOKE_CONNSTR");
        if (string.IsNullOrWhiteSpace(connectionString)) { Console.WriteLine("PostgreSQL integration skipped: DBSMOKE_CONNSTR is unset."); return; }
        var builder = new NpgsqlConnectionStringBuilder(connectionString) { Timeout = 10, CommandTimeout = 30 };
        var connection = new ConnectionItem { Name = "P0-PG-Test", DatabaseType = "Postgres", Server = builder.Host!, Port = builder.Port.ToString(), Database = builder.Database!, UserId = builder.Username, Password = builder.Password };
        var connections = new QueryExecutionChecks.Connections(connection);
        var query = new DefaultQueryService(connections);
        query.NotifyConnected(connection.Name);
        string schema = "dbm_p0_" + Guid.NewGuid().ToString("N");
        await using var db = new NpgsqlConnection(builder.ConnectionString);
        await db.OpenAsync();
        bool created = false;
        try
        {
            await Run($"CREATE SCHEMA {schema}"); created = true;
            await Run($"CREATE TABLE {schema}.parents(id integer PRIMARY KEY); INSERT INTO {schema}.parents VALUES(10),(20); CREATE TABLE {schema}.items(id integer PRIMARY KEY, parent_id integer REFERENCES {schema}.parents(id), name text, payload bytea); INSERT INTO {schema}.items VALUES(1,10,'alpha',decode('0102','hex'))");
            var multi = await query.ExecuteAsync(connection.Name, "SELECT 1 AS first; SELECT 2 AS second; SELECT 3 WHERE false;");
            Check(multi.IsSuccess && multi.ResultSets.Count == 3 && multi.ResultSets[1].Rows[0][0] == "2", "PG multiple result sets.");
            Check(await query.BeginTransactionAsync(connection.Name), "PG begin transaction.");
            await query.ExecuteAsync(connection.Name, $"UPDATE {schema}.items SET name='uncommitted' WHERE id=1");
            Check((await query.ExecuteStandaloneAsync(connection, $"SELECT name FROM {schema}.items")).Rows[0][0] == "alpha", "PG standalone isolation.");
            Check(await query.RollbackAsync(connection.Name), "PG rollback.");
            var planResult = await new DefaultExecutionPlanService().ExplainAsync(connection, $"SELECT * FROM {schema}.items WHERE id=1", true);
            var plan = ExecutionPlanParser.Parse(planResult);
            Check(planResult.IsSuccess && plan.Count > 0 && plan[0].Milliseconds.HasValue, "PG real execution plan metrics.");
            var edit = new DefaultDataEditService(connections);
            var metadata = await edit.GetTableMetadataAsync(connection.Name, connection.Database, "items", schema);
            Check(metadata.IsSuccess, "PG metadata: " + metadata.ErrorMessage);
            var choices = await ForeignKeyValueService.LoadAsync(connection, metadata.TableInfo, CancellationToken.None);
            Check(choices.Count == 1 && choices[0].Values.Rows.Count == 2, "PG foreign key choices.");
            var tab = new QueryTabViewModel(query, edit) { ConnectionName = connection.Name, DatabaseName = connection.Database, SqlText = $"SELECT * FROM {schema}.items", DangerousSqlConfirmationEnabled = false, AutoRefreshAfterSave = false };
            await tab.ExecuteAsync();
            Check(tab.IsResultEditable, "PG result editable: " + tab.EditReadOnlyReason);
            var values = tab.Columns.Select((_,i) => tab.Rows[0][i]).ToArray();
            values[tab.Columns.IndexOf("payload")] = "0xAABB";
            values[tab.Columns.IndexOf("parent_id")] = "20";
            tab.ApplyFormValues(tab.Rows[0], values);
            await tab.SaveEditsCommand.ExecuteAsync(null);
            Check(!tab.HasPendingChanges, "PG form save: " + tab.StatusMessage);
            var saved = await query.ExecuteAsync(connection.Name, $"SELECT encode(payload,'hex'),parent_id FROM {schema}.items");
            Check(saved.Rows[0][0] == "aabb" && saved.Rows[0][1] == "20", "PG binary and foreign key roundtrip.");
            using var canceled = new CancellationTokenSource(); canceled.Cancel();
            Check(!(await query.ExecuteAsync(connection.Name, $"DELETE FROM {schema}.items", canceled.Token)).IsSuccess, "PG cancellation.");
            var fileFolder = Path.Combine(Path.GetTempPath(), "dbm-pg-files-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(fileFolder);
            try
            {
                await Run($"CREATE TABLE {schema}.imports(id integer PRIMARY KEY, name text)");
                var input = Path.Combine(fileFolder, "input.csv");
                await File.WriteAllTextAsync(input, "id,name\r\n1,imported\r\n");
                var files = new DefaultExportImportService(connections);
                var operations = new ScheduledOperations(connections, files, null, null, null);
                await operations.RunAsync(new ScheduleDefinition { TaskType = ScheduleTaskTypes.Import, ConnectionName = connection.Name, ExportSchema = schema, ExportTable = "imports", ExportFilePath = input }, new TaskRun("import", "test"), CancellationToken.None);
                Check((await query.ExecuteAsync(connection.Name, $"SELECT name FROM {schema}.imports")).Rows[0][0] == "imported", "PG scheduled import.");
                var exported = await files.ExportDataAsync(connection, "imports", schema, false, "Csv", Path.Combine(fileFolder, "output.csv"));
                Check(exported.IsSuccess && File.Exists(exported.FilePath), "PG export: " + exported.Message);
            }
            finally { Directory.Delete(fileFolder, true); }
            Console.WriteLine("PostgreSQL P0 integration passed: multi-results, transaction isolation, plan metrics, foreign keys, binary editing, cancellation.");
        }
        finally
        {
            query.CloseConnection(connection.Name);
            if (created) await Run($"DROP SCHEMA {schema} CASCADE");
        }
        async Task Run(string sql) { await using var command = new NpgsqlCommand(sql, db); await command.ExecuteNonQueryAsync(); }
    }
    private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
}
