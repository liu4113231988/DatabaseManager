using System.Net;
using System.Text;
using DatabaseInterpreter.Model;
using DatabaseManager.AppCore.Models;
using DatabaseManager.AppCore.Services;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

internal static class P2FeatureChecks
{
    public static async Task RunAsync()
    {
        var design = new QueryDesign
        {
            Tables = new() { new("orders", "public", "o"), new("customers", "public", "c") },
            Fields = new() { new("c", "name"), new("o", "amount", "SUM", "total") },
            Joins = new() { new("LEFT", "o", "customer_id", "c", "id") },
            Where = new() { Logic = "OR", Children = new() { new() { TableAlias = "c", Column = "name", Value = "O'Brien\\'" }, new() { TableAlias = "o", Column = "amount", Operator = ">", Value = "10" } } },
            OrderBy = new() { new("o", "amount", "SUM") }, Descending = true,
        };
        var sql = VisualQueryBuilder.Build(design, DatabaseType.Postgres, "test_view", "public");
        Check(sql.Contains("CREATE VIEW") && sql.Contains("LEFT JOIN") && sql.Contains("GROUP BY") && sql.Contains(" OR ") && sql.Contains("ORDER BY SUM"), "Query builder grouping, joins, view and sort.");
        Check(VisualQueryBuilder.Build(design, DatabaseType.MySql).Contains("CONVERT(X'"), "MySQL values use SQL-mode independent encoding.");
        design.Joins.Clear(); Throws(() => VisualQueryBuilder.Build(design, DatabaseType.Postgres), "Disconnected join rejected.");
        var quoted = new QueryDesign { Tables = new() { new("a\"b", null, "t") }, Fields = new() { new("t", "x\"y") } };
        Check(VisualQueryBuilder.Build(quoted, DatabaseType.Postgres).Contains("\"a\"\"b\""), "Quoted identifiers escaped.");

        var columns = new[] { "phone", "number", "name" };
        IReadOnlyList<IReadOnlyList<string?>> rows = new[] { new string?[] { "13812345678", "1", null }, new[] { "13812345678", "1", "" }, new[] { "13912345678", "2", "a" }, new[] { "13712345678", "2", "b" }, new[] { "13612345678", "100", "c" } };
        var quality = DataQualityProfiler.Analyze(columns, rows, true);
        Check(quality.Columns[0].Format == "手机号", "Phone format takes precedence over numeric classification.");
        Check(!DataQualityProfiler.Analyze(new[] { "x" }, new[] { new[] { "1" }, new[] { "2" } }, false).Findings.Any(f => f.Kind.StartsWith("异常")), "Small-sample quartiles interpolate rather than flagging the second value.");
        Check(quality.IsSample && quality.Columns[2].Nulls == 1 && quality.Columns[0].Duplicates == 1 && quality.Findings.Any(f => f.Kind.StartsWith("异常")), "Quality null/empty, duplicates and numeric outlier.");
        var source = new TableSample(columns, rows, true).ToResult();
        var masked = DataMasker.Apply(source, new[] { new MaskRule { Column = "phone" } });
        Check(masked.Rows[0][0] == "138****5678" && source.Rows[0][0] == "13812345678" && masked.IsTruncated, "Masking is an independent copy.");
        Check(DataMasker.Mask("123", new()) == "***" && DataMasker.Mask(null, new()) is null, "Short and NULL mask behavior.");
        Throws(() => DataMasker.Apply(source, new[] { new MaskRule { Column = "missing" } }), "Missing mask column rejected.");
        Check(DataMasker.Mask("abc123", new() { Kind = "自定义", Pattern = "[0-9]+", Replacement = "X" }) == "abcX", "Custom mask rule.");

        var table = new DataTableInfo { Name = "sample", Columns = new[] { new DataColumnInfo { Name = "id", DataType = "int", IsNullable = false }, new DataColumnInfo { Name = "code", DataType = "text" } }, PrimaryKeyColumns = new[] { "id" } };
        var rules = new[] { new GenerationRule { Column = "id", Minimum = 1, Maximum = 1000, Unique = true }, new GenerationRule { Column = "code", Kind = "正则", Pattern = "^[A-Z]{2}\\d{4}$" } };
        var generated = TestDataGenerator.Generate(table, rules, 100, 42); var repeated = TestDataGenerator.Generate(table, rules, 100, 42);
        Check(generated.Select(r => r["id"]).Distinct().Count() == 100 && generated.Select(r => r["code"]).SequenceEqual(repeated.Select(r => r["code"])), "Seeded unique generation.");
        rules[0].Maximum = 1; Throws(() => TestDataGenerator.Generate(table, rules, 2, 42), "Exhausted uniqueness fails.");
        Throws(() => TestDataGenerator.GeneratePattern("(a+)+", new Random(1)), "Unsupported regex rejected.");

        var connection = new ConnectionItem { Name = "test", DatabaseType = "Postgres", Password = "fake-password-only", Ssh = new() { Secret = "fake-ssh-secret", EncryptedSecret = "old-cipher" } };
        string plain = ConnectionTransferService.Export(new[] { connection }, false, "");
        var clean = ConnectionTransferService.Preview(plain, "")[0];
        Check(clean.Password is null && clean.Ssh!.Secret == "" && clean.Ssh.EncryptedSecret == "", "Password-free export.");
        const string passphrase = "test-only-passphrase";
        var encrypted = ConnectionTransferService.Export(new[] { connection }, true, passphrase);
        Check(!encrypted.Contains(connection.Password!), "No plaintext password in protected export.");
        var decrypted = ConnectionTransferService.Preview(encrypted, passphrase)[0];
        Check(decrypted.Password == connection.Password && decrypted.Ssh!.Secret == connection.Ssh.Secret, "Encrypted transfer roundtrip.");
        Throws(() => ConnectionTransferService.Preview(encrypted, "wrong"), "Wrong passphrase rejected.");
        var tamper = JObject.Parse(encrypted); var cipher = Convert.FromBase64String(tamper["Payload"]!.Value<string>()!); cipher[0] ^= 1; tamper["Payload"] = Convert.ToBase64String(cipher);
        Throws(() => ConnectionTransferService.Preview(tamper.ToString(), passphrase), "Tampered transfer rejected.");
        var store = new MemoryConnections(); store.Items.Add(connection);
        Check(await ConnectionTransferService.ImportAsync(store, new[] { clean }, false, default) == 0, "Conflict skip.");
        Check(await ConnectionTransferService.ImportAsync(store, new[] { clean }, true, default) == 1 && store.Items.Select(c => c.Name).Distinct().Count() == 2, "Conflict rename.");
        store.FailAfter = store.Items.Count + 1;
        try { await ConnectionTransferService.ImportAsync(store, new[] { clean, clean }, true, default); throw new Exception("Expected failed import."); } catch (InvalidOperationException) { Check(store.Items.Count == 2, "Import rollback."); }
        store.FailAfter = int.MaxValue; store.FailAfterWriting = true;
        try { await ConnectionTransferService.ImportAsync(store, new[] { clean }, true, default); throw new Exception("Expected sidecar failure."); } catch (InvalidOperationException) { Check(store.Items.Count == 2, "Import cleans the profile committed before a sidecar failure."); }

        var calc = DashboardTransform.Apply(new QueryResult { Columns = new[] { "category", "amount" }, Rows = new[] { new[] { "A", "10" }, new[] { "B", "20" } } }, new[] { new CalculatedField { Name = "gross", Expression = "([amount] + 2) * 1.5" } }, "category", "B");
        Check(calc.Rows.Count == 1 && calc.Rows[0][2] == "33.0", "Dashboard calculation and shared filter.");
        Throws(() => DashboardTransform.Evaluate("1/0", _ => 0), "Division by zero rejected.");
        Throws(() => DashboardTransform.Evaluate("Process.Start(1)", _ => 0), "Expression cannot invoke code.");

        var folder = Path.Combine(Path.GetTempPath(), "dbm-p2-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(folder);
        try
        {
            var dbf = Path.Combine(folder, "input.dbf"); WriteDbf(dbf);
            var csv = Path.Combine(folder, "snapshot.csv");
            Check(await ExternalImportSource.SnapshotDbfAsync(dbf, csv, "UTF-8", default) == 2, "DBF snapshot row count.");
            var content = await File.ReadAllTextAsync(csv); Check(content.Contains("alpha") && content.Contains("beta"), "DBF values preserved.");
            if (Environment.GetEnvironmentVariable("DBM_TEST_ODBC") == "1")
            {
                var odbc = new System.Data.Odbc.OdbcConnectionStringBuilder { Driver = "Microsoft Access dBASE Driver (*.dbf, *.ndx, *.mdx)" }; odbc["Dbq"] = folder;
                int odbcRows = await ExternalImportSource.SnapshotOdbcAsync(odbc.ConnectionString, "SELECT * FROM [input] ORDER BY [id]", Path.Combine(folder, "odbc.csv"), default);
                Check(odbcRows == 2, "Real dBASE ODBC snapshot.");
                string accessPath = Environment.GetEnvironmentVariable("DBM_ACCESS_FIXTURE") ?? throw new InvalidOperationException("DBM_ACCESS_FIXTURE missing.");
                var access = new System.Data.Odbc.OdbcConnectionStringBuilder(ExternalImportSource.AccessConnectionString(accessPath, "Microsoft Access Driver (*.mdb, *.accdb)")); access["ReadOnly"] = "0";
                string accessTable = "test_" + Guid.NewGuid().ToString("N");
                using (var accessDb = new System.Data.Odbc.OdbcConnection(access.ConnectionString))
                {
                    await accessDb.OpenAsync();
                    using var command = accessDb.CreateCommand();
                    command.CommandText = $"CREATE TABLE {accessTable} (id INTEGER, title VARCHAR(50))"; await command.ExecuteNonQueryAsync();
                    command.CommandText = $"INSERT INTO {accessTable} VALUES (1,'access-fixture')"; await command.ExecuteNonQueryAsync();
                }
                try
                {
                    Check(await ExternalImportSource.SnapshotOdbcAsync(ExternalImportSource.AccessConnectionString(accessPath, "Microsoft Access Driver (*.mdb, *.accdb)"), $"SELECT * FROM {accessTable} ORDER BY id", Path.Combine(folder, "access.csv"), default) == 1, "Real Access source snapshot.");
                }
                finally
                {
                    using var cleanup = new System.Data.Odbc.OdbcConnection(access.ConnectionString); await cleanup.OpenAsync();
                    using var command = cleanup.CreateCommand(); command.CommandText = $"DROP TABLE {accessTable}"; await command.ExecuteNonQueryAsync();
                }
                Console.WriteLine("P2 real ODBC dBASE and Access source checks passed.");
            }
            using var canceled = new CancellationTokenSource(); canceled.Cancel();
            try { await ExternalImportSource.SnapshotDbfAsync(dbf, Path.Combine(folder, "cancel.csv"), "UTF-8", canceled.Token); throw new Exception("Expected cancellation."); } catch (OperationCanceledException) { Check(!File.Exists(Path.Combine(folder, "cancel.csv")), "Canceled snapshot not published."); }
            var schema = new SchemaInfo { Tables = new() { new Table { Name = "sample", Schema = "public", Comment = "<script>alert(1)</script>" } }, TableColumns = new() { new TableColumn { Name = "title", TableName = "sample", Schema = "public", DataType = "text", Comment = "中文描述", IsNullable = true } } };
            var doc = DataDictionaryService.Build("test", schema, new DictionaryOptions());
            var dashboardStore = new DefaultDashboardService(folder);
            var persistedChart = new DashboardChart { Name = "持久化测试", Page = "第二页", Position = 2, Sql = "SELECT 1", CardWidth = 500, CalculatedFields = new() { new() { Name = "x", Expression = "1+2" } } };
            dashboardStore.Save(persistedChart); persistedChart.Page = "未保存修改";
            Check(new DefaultDashboardService(folder).GetAll()[0].Page == "第二页" && dashboardStore.GetAll()[0].Page == "第二页", "Dashboard persists layout without caller mutation.");
            Directory.CreateDirectory(Path.Combine(folder, "dashboard-charts.json.tmp"));
            try { dashboardStore.Save(persistedChart); throw new Exception("Expected disk failure."); } catch (UnauthorizedAccessException) { Check(dashboardStore.GetAll()[0].Page == "第二页", "Failed dashboard save preserves memory state."); }
            Directory.Delete(Path.Combine(folder, "dashboard-charts.json.tmp"));
            await DataDictionaryService.SaveAsync(doc, Path.Combine(folder, "doc.html"), default);
            Check((await File.ReadAllTextAsync(Path.Combine(folder, "doc.html"))).Contains("&lt;script&gt;"), "Dictionary HTML escaping.");
            var pdf = SimpleDocumentPdf.Create(Enumerable.Range(0, 120).Select(i => "中文字段说明 " + i));
            Check(Encoding.ASCII.GetString(pdf).StartsWith("%PDF-1.4") && Encoding.ASCII.GetString(pdf).Contains("/Count 3"), "PDF pagination.");
            var artifact = Path.GetFullPath("artifacts/p2-tests"); Directory.CreateDirectory(artifact); await File.WriteAllBytesAsync(Path.Combine(artifact, "dictionary.pdf"), pdf);
        }
        finally { Directory.Delete(folder, true); }
        var handler = new FakeAiHandler(); var answer = await AiSqlAssistant.AskAsync(new() { Model = "test" }, "生成", "按部门计数", "", "table departments(id int)", default, handler);
        Check(answer == "SELECT 1;" && handler.Body.Contains("departments") && !handler.Body.Contains("fake-password"), "AI request/response preview.");
        handler.Status = HttpStatusCode.Unauthorized;
        try { await AiSqlAssistant.AskAsync(new() { Model = "test" }, "生成", "test", "", "", default, handler); throw new Exception("Expected AI error."); } catch (InvalidOperationException ex) { Check(ex.Message.Contains("401"), "AI HTTP error is actionable."); }
        Console.WriteLine("P2 core checks passed: builder, generation, quality, masks, encrypted transfer, DBF snapshot, dictionary/PDF, dashboard and AI contract.");
    }
    private static void WriteDbf(string path)
    {
        using var stream = File.Create(path); using var writer = new BinaryWriter(stream);
        var header = new byte[32]; header[0] = 3; BitConverter.GetBytes(2).CopyTo(header, 4); BitConverter.GetBytes((ushort)97).CopyTo(header, 8); BitConverter.GetBytes((ushort)17).CopyTo(header, 10); writer.Write(header);
        foreach (var f in new[] { ("name", 'C', 12), ("id", 'N', 4) }) { var field = new byte[32]; Encoding.ASCII.GetBytes(f.Item1).CopyTo(field, 0); field[11] = (byte)f.Item2; field[16] = (byte)f.Item3; writer.Write(field); }
        writer.Write((byte)13); writer.Write(Encoding.ASCII.GetBytes(" " + "alpha".PadRight(12) + "1".PadLeft(4))); writer.Write(Encoding.ASCII.GetBytes(" " + "beta".PadRight(12) + "2".PadLeft(4))); writer.Write((byte)26);
    }
    private sealed class FakeAiHandler : HttpMessageHandler
    {
        public string Body = ""; public HttpStatusCode Status = HttpStatusCode.OK;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) { Body = await request.Content!.ReadAsStringAsync(ct); return new(Status) { Content = new StringContent("{\"choices\":[{\"message\":{\"content\":\"SELECT 1;\"}}]}") }; }
    }
    private sealed class MemoryConnections : IDbConnectionService
    {
        public List<ConnectionItem> Items { get; } = new(); public int FailAfter = int.MaxValue; public bool FailAfterWriting;
        public IReadOnlyList<ConnectionItem> GetConnections() => Items;
        public IReadOnlyList<ConnectionItem> GetConnections(string type) => Items;
        public ConnectionItem? GetConnectionById(string id) => Items.FirstOrDefault(c => c.Id == id);
        public Task<IReadOnlyList<string>> TestConnectionAsync(ConnectionItem item, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<string?> SaveAsync(ConnectionItem item, CancellationToken ct = default) { if (Items.Count >= FailAfter) throw new InvalidOperationException("Simulated persistence failure."); item.Id = Guid.NewGuid().ToString(); Items.Add(item); if (FailAfterWriting) throw new InvalidOperationException("Simulated sidecar failure."); return Task.FromResult<string?>(item.Id); }
        public Task<bool> DeleteAsync(IEnumerable<string> ids, CancellationToken ct = default) { Items.RemoveAll(c => ids.Contains(c.Id)); return Task.FromResult(true); }
        public Task<bool> IsNameExistedAsync(bool add, string? account, string name, string? id, CancellationToken ct = default) => Task.FromResult(Items.Any(c => c.Name == name));
    }
    private static void Throws(Action action, string message) { try { action(); } catch (ArgumentException) { return; } catch (InvalidOperationException) { return; } throw new Exception(message); }
    private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
}
