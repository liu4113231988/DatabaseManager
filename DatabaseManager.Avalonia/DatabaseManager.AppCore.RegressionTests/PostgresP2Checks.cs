using DatabaseInterpreter.Model;
using DatabaseManager.AppCore.Models;
using DatabaseManager.AppCore.Services;
using Npgsql;

internal static class PostgresP2Checks
{
    public static async Task RunAsync()
    {
        string? cs = Environment.GetEnvironmentVariable("DBSMOKE_CONNSTR");
        if (string.IsNullOrWhiteSpace(cs)) { Console.WriteLine("P2 PostgreSQL checks skipped: DBSMOKE_CONNSTR unset."); return; }
        var settings = new NpgsqlConnectionStringBuilder(cs) { Timeout = 10, CommandTimeout = 30 };
        var item = new ConnectionItem { Name = "p2-pg", DatabaseType = "Postgres", Server = settings.Host!, Port = settings.Port.ToString(), Database = settings.Database!, UserId = settings.Username, Password = settings.Password };
        var connections = new QueryExecutionChecks.Connections(item); var edit = new DefaultDataEditService(connections);
        await using var db = new NpgsqlConnection(settings.ConnectionString); await db.OpenAsync();
        string schema = "dbm_p2_" + Guid.NewGuid().ToString("N");
        var folder = Path.GetFullPath("artifacts/p2-tests"); Directory.CreateDirectory(folder);
        bool created = false;
        try
        {
            await Run($"CREATE SCHEMA {schema}"); created = true;
            await Run($"CREATE TABLE {schema}.parents(id integer PRIMARY KEY); INSERT INTO {schema}.parents VALUES(10),(20); CREATE TABLE {schema}.items(id integer PRIMARY KEY, parent_id integer REFERENCES {schema}.parents(id), phone varchar(20), amount numeric); INSERT INTO {schema}.items VALUES(1,10,'13812345678',NULL),(2,20,'13912345678',0); COMMENT ON TABLE {schema}.items IS 'P2 测试数据字典'; COMMENT ON COLUMN {schema}.items.phone IS '手机号码';");
            var metadata = await edit.GetTableMetadataAsync(item.Name, item.Database, "items", schema);
            Check(metadata.IsSuccess, "P2 metadata: " + metadata.ErrorMessage);
            var keys = await ForeignKeyValueService.LoadAsync(item, metadata.TableInfo, default);
            var rules = new[] { new GenerationRule { Column = "id", Minimum = 100, Maximum = 100000, Unique = true }, new GenerationRule { Column = "phone", Kind = "正则", Pattern = "138[0-9]{8}" } };
            var data = TestDataGenerator.Generate(metadata.TableInfo, rules, 30, 123, keys);
            var saved = await edit.SaveChangesAsync(item.Name, item.Database, "items", schema, data, Array.Empty<DataEditRow>(), Array.Empty<DataEditRow>());
            Check(saved.IsSuccess && saved.RowCount == 30, "Generated data saved with FK constraints: " + saved.ErrorMessage);
            var design = new QueryDesign { Tables = new() { new("items", schema, "i"), new("parents", schema, "p") }, Fields = new() { new("p", "id"), new("i", "amount", "SUM", "total") }, Joins = new() { new("INNER", "i", "parent_id", "p", "id") }, Where = new() { TableAlias = "i", Column = "id", Operator = ">=", Value = "100" } };
            await Run(VisualQueryBuilder.Build(design, DatabaseType.Postgres, "totals", schema));
            Check(Convert.ToInt32(await Scalar($"SELECT COUNT(*) FROM {schema}.totals")) == 2, "Generated grouped view executes.");
            var sample = await P2TableReader.ReadAsync(item, "items", schema, 100, default);
            Check(!sample.IsSample && sample.Rows.Count == 32 && sample.Rows.Count(r => r[3] is null) == 1, "Actual NULL vs numeric zero preserved.");
            var quality = DataQualityProfiler.Analyze(sample.Columns, sample.Rows, sample.IsSample);
            Check(quality.Columns.Single(c => c.Column == "amount").Nulls == 1, "Real data quality metrics.");
            var masked = DataMasker.Apply(sample.ToResult(), new[] { new MaskRule { Column = "phone" } });
            Check(masked.Rows.All(r => r[2].Contains("****")), "Real result masking.");
            Check((string)(await Scalar($"SELECT phone FROM {schema}.items WHERE id=1"))! == "13812345678", "Masking never updates source.");
            var limited = await P2TableReader.ReadAsync(item, "items", schema, 5, default); Check(limited.IsSample && limited.Rows.Count == 5, "Sampling limit explicit.");
            var options = new DictionaryOptions { Objects = new() { schema + ".items", schema + ".totals" } };
            var doc = await DataDictionaryService.ReadAsync(item, options, default);
            Check(doc.Lines.Any(l => l.Contains("手机号码")) && doc.Lines.Any(l => l.Contains("外键")) && doc.Lines.Any(l => l.Contains("total")), "Dictionary columns, comments, views and FK metadata.");
            await DataDictionaryService.SaveAsync(doc, Path.Combine(folder, "postgres-dictionary.pdf"), default);
            var operations = new ScheduledOperations(connections, new DefaultExportImportService(connections), null, null, null);
            var job = new ScheduleDefinition { Name = "P2 doc", TaskType = ScheduleTaskTypes.Documentation, ConnectionName = item.Name, Dictionary = options, ExportFilePath = Path.Combine(folder, "scheduled-dictionary.html") };
            DefaultScheduleService.Validate(job);
            await operations.RunAsync(job, new TaskRun("P2 dictionary", "Documentation"), default);
            Check(File.Exists(job.ExportFilePath), "Scheduled document created.");
            // Existing CSV mapping/skipRows pipeline remains the target for external-source snapshots.
            await Run($"CREATE TABLE {schema}.imported(id integer PRIMARY KEY, name text)");
            var csv = Path.Combine(folder, "external-snapshot.csv"); await File.WriteAllTextAsync(csv, "id,name\n1,alpha\n2,beta\n3,gamma\n");
            var imported = await new DefaultExportImportService(connections).ImportDataAsync(item, "imported", schema, csv, skipRows: 1);
            Check(imported.IsSuccess && Convert.ToInt32(await Scalar($"SELECT COUNT(*) FROM {schema}.imported")) == 2, "Snapshot continuation uses existing import pipeline: " + imported.Message);
            Console.WriteLine("P2 PostgreSQL integration passed: generated FK rows, visual grouped view, quality/NULL sampling, masking, dictionary and scheduled PDF/HTML, snapshot resume.");
        }
        finally { if (created) await Run($"DROP SCHEMA {schema} CASCADE"); }
        async Task Run(string sql) { await using var command = new NpgsqlCommand(sql, db); await command.ExecuteNonQueryAsync(); }
        async Task<object?> Scalar(string sql) { await using var command = new NpgsqlCommand(sql, db); return await command.ExecuteScalarAsync(); }
    }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
