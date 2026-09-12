using DatabaseManager.AppCore.Models;
using DatabaseManager.AppCore.Services;
using Npgsql;

internal static class PostgresJobChecks
{
    public static async Task RunAsync()
    {
        var raw = Environment.GetEnvironmentVariable("DBSMOKE_CONNSTR");
        if (string.IsNullOrEmpty(raw)) return;
        var builder = new NpgsqlConnectionStringBuilder(raw) { Pooling = false, Timeout = 10, CommandTimeout = 30 };
        await using var admin = new NpgsqlConnection(builder.ConnectionString);
        await admin.OpenAsync();
        var names = new List<string>();
        string sourceName = "dbm_p0_src_" + Guid.NewGuid().ToString("N"), targetName = "dbm_p0_dst_" + Guid.NewGuid().ToString("N");
        try
        {
            foreach (var name in new[] { sourceName, targetName })
            {
                await using var cmd = new NpgsqlCommand("CREATE DATABASE " + name, admin);
                await cmd.ExecuteNonQueryAsync(); names.Add(name);
            }
            ConnectionItem Item(string name) => new() { Name=name, Database=name, DatabaseType="Postgres", Server=builder.Host!, Port=builder.Port.ToString(), UserId=builder.Username, Password=builder.Password };
            var source = Item(sourceName); var target = Item(targetName);
            var connections = new QueryExecutionChecks.Connections(source, target);
            var query = new DefaultQueryService(connections);
            var setup = await query.ExecuteStandaloneAsync(source, "CREATE TABLE public.sample(id integer PRIMARY KEY, name varchar(80)); INSERT INTO public.sample VALUES(1,'original');");
            Check(setup.IsSuccess, "Create migration fixture.");
            var operations = new ScheduledOperations(connections, new DefaultExportImportService(connections), new DefaultConvertService(), new DefaultCompareService(), new DefaultSyncScriptService());
            var step = new ScheduleDefinition { ConnectionName=sourceName, TargetConnectionName=targetName, TaskType=ScheduleTaskTypes.Migration, MigrationMode=ConvertMode.SchemaAndData, ExportTable="sample" };
            var run = new TaskRun("PG job checks", "test");
            try
            {
                await operations.RunAsync(step, run, CancellationToken.None);
                var migrated = await query.ExecuteStandaloneAsync(target, "SELECT name FROM public.sample WHERE id=1");
                Check(migrated.IsSuccess && migrated.Rows.Count == 1 && migrated.Rows[0][0] == "original", "Migration must copy schema and rows: " + migrated.ErrorMessage);
                Check((await query.ExecuteStandaloneAsync(source, "ALTER TABLE public.sample ADD COLUMN extra integer; UPDATE public.sample SET name='updated',extra=42 WHERE id=1")).IsSuccess, "Modify source fixture.");
                step.TaskType = ScheduleTaskTypes.SchemaSync;
                await operations.RunAsync(step, run, CancellationToken.None);
                Check((await query.ExecuteStandaloneAsync(target, "SELECT extra FROM public.sample")).IsSuccess, "Schema synchronization must add the column.");
                step.TaskType = ScheduleTaskTypes.DataSync;
                await operations.RunAsync(step, run, CancellationToken.None);
                var synced = await query.ExecuteStandaloneAsync(target, "SELECT name,extra FROM public.sample WHERE id=1");
                Check(synced.IsSuccess && synced.Rows[0][0] == "updated" && synced.Rows[0][1] == "42", "Data synchronization must update values.");
            }
            catch { Console.WriteLine(string.Join(Environment.NewLine, run.GetLogSnapshot())); throw; }
            Console.WriteLine("PostgreSQL scheduled migration, schema sync and data sync passed in isolated temporary databases.");
        }
        finally
        {
            foreach (var name in names)
            {
                await using var terminate = new NpgsqlCommand("SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname=@name AND pid<>pg_backend_pid()", admin);
                terminate.Parameters.AddWithValue("name", name); await terminate.ExecuteNonQueryAsync();
                await using var drop = new NpgsqlCommand("DROP DATABASE " + name, admin); await drop.ExecuteNonQueryAsync();
            }
        }
    }
    private static void Check(bool value,string message) { if (!value) throw new InvalidOperationException(message); }
}
