using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using DatabaseConverter.Core;
using DatabaseInterpreter.Core;
using DatabaseInterpreter.Model;
using DatabaseManager.AppCore.Models;
using DatabaseManager.AppCore.Services;
using DatabaseManager.AppCore.ViewModels;
using DatabaseManager.Avalonia.Views;
using DatabaseManager.Avalonia.Views.Workbench;
using Microsoft.Extensions.DependencyInjection;
using SchemaCompareDifferenceType = DatabaseManager.Core.Model.SchemaCompareDifferenceType;

namespace DatabaseManager.Avalonia.Smoke;

public static class P2SmokeHarness
{
    public static async Task RunAsync()
    {
        var output = Path.GetFullPath("artifacts/p2-ui"); Directory.CreateDirectory(output); File.Delete(Path.Combine(output, "passed.txt"));
        var folder = Path.Combine(Path.GetTempPath(), "dbm-p2-ui-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(folder);
        var item = new ConnectionItem { Name = "P2 UI 测试", DatabaseType = "Sqlite", Database = Path.Combine(folder, "fixture.db") };
        var connections = new FixtureConnections(item); var query = new DefaultQueryService(connections);
        try
        {
            File.WriteAllBytes(item.Database, []);
            var created = await query.ExecuteStandaloneAsync(item, "CREATE TABLE sample(id INTEGER PRIMARY KEY, phone TEXT, amount INTEGER); INSERT INTO sample VALUES(1,'13812345678',12),(2,'13912345678',25);");
            if (!created.IsSuccess) throw new Exception(created.ErrorMessage);
            await VerifyDateFunctionConversionAsync(item, query);
            await VerifySchemaCompareAsync(item, query, folder);
            var source = await query.ExecuteStandaloneAsync(item, "SELECT * FROM sample");
            var services = new ServiceCollection().AddSingleton<IDbConnectionService>(connections).AddSingleton<IExportImportService>(new DefaultExportImportService(connections)).AddSingleton<IDataEditService>(new DefaultDataEditService(connections)).BuildServiceProvider();
            string sent = "";
            var queryWindow = new QueryBuilderWindow(services, (_, sql) => sent = sql, item.Name, item.Database); queryWindow.Show(); await Task.Delay(250);
            await Click(queryWindow, "加载对象");
            if (queryWindow.FindControl<ListBox>("AvailableTables")?.ItemCount != 1)
                throw new InvalidOperationException("Builder did not load the fixture table: " + Status(queryWindow));
            await Click(queryWindow, "加入表");
            if (queryWindow.FindControl<ComboBox>("FieldBox")?.ItemCount != 3)
                throw new InvalidOperationException("Builder did not load the fixture columns: " + Status(queryWindow));
            await Click(queryWindow, "添加输出"); await Click(queryWindow, "预览 SQL"); await Click(queryWindow, "送入新查询标签");
            if (!sent.StartsWith("SELECT")) throw new Exception("Builder UI did not produce SQL."); await Capture(queryWindow, "query"); queryWindow.Close();
            var generator = new TestDataWindow(services, item.Name, item.Database); generator.Show(); await Task.Delay(150);
            await Click(generator, "加载对象"); await Click(generator, "读取字段规则"); await Click(generator, "生成并预览");
            if (!Output(generator).Contains("已生成")) throw new Exception("Generator UI: " + Status(generator)); await Capture(generator, "generator"); generator.Close();
            var quality = new QualityWindow(services, item.Name, item.Database); quality.Show(); await Task.Delay(150);
            await Click(quality, "加载对象"); await Click(quality, "剖析选中表");
            if (!Output(quality).Contains("已读取全表")) throw new Exception("Quality UI: " + Status(quality)); await Capture(quality, "quality"); quality.Close();
            var dictionary = new DictionaryWindow(services, item.Name, item.Database); dictionary.Show(); await Task.Delay(150);
            await Click(dictionary, "加载对象"); await Click(dictionary, "生成预览");
            if (!Output(dictionary).Contains("sample")) throw new Exception("Dictionary UI: " + Status(dictionary)); await Capture(dictionary, "dictionary"); dictionary.Close();
            var informative = new Window[] { new AiSqlWindow(services, item.Name, item.Database), new ConnectionTransferWindow(services, item.Name, item.Database), new ExternalImportWindow(services, item.Name, item.Database) };
            var informativeNames = new[] { "ai", "transfer", "external" };
            for (int i = 0; i < informative.Length; i++) { informative[i].Show(); await Task.Delay(150); await Capture(informative[i], informativeNames[i]); informative[i].Close(); }
            var mask = new MaskWindow(services, item.Name, item.Database, source); mask.Show(); await Task.Delay(150);
            await Click(mask, "加载对象");
            var maskPanel = ((ScrollViewer)((Grid)mask.Content!).Children[1]).Content as Control;
            var column = maskPanel!.GetVisualDescendants().OfType<TextBox>().First(t => t.Width == 170); column.Text = "phone";
            await Click(mask, "添加 / 更新规则"); await Click(mask, "脱敏预览");
            var grid = mask.GetVisualDescendants().OfType<DataGrid>().FirstOrDefault(g => g.Name == "PreviewGrid");
            // The result grid may not be realized until its tab is selected.
            var resultTabs = mask.GetVisualDescendants().OfType<TabControl>().Single(); resultTabs.SelectedIndex = 1; await Task.Delay(100);
            grid = mask.GetVisualDescendants().OfType<DataGrid>().Single(g => g.Name == "PreviewGrid");
            if (grid.ItemsSource.Cast<PreviewRow>().First().Values[1] != "138****5678") throw new Exception("Mask UI values not masked."); await Capture(mask, "mask"); mask.Close();
            var dashboards = new FixtureDashboard(); dashboards.Save(new DashboardChart { Name = "金额分析", ConnectionName = item.Name, Database = item.Database, Sql = "SELECT phone, amount FROM sample", XColumn = "phone", YColumns = new() { "gross" }, CalculatedFields = new() { new() { Name = "gross", Expression = "[amount]*1.2" } } });
            dashboards.Save(new DashboardChart { Name = "联动分析", ConnectionName = item.Name, Sql = "SELECT phone, amount FROM sample", XColumn = "phone", YColumns = new() { "amount" } });
            dashboards.Save(new DashboardChart { Name = "第二页图表", Page = "第二页", ConnectionName = item.Name, Sql = "SELECT phone, amount FROM sample", XColumn = "phone", YColumns = new() { "amount" } });
            var dashboard = new DashboardWindow(dashboards, query, connections); dashboard.Show(); await Task.Delay(600); await Capture(dashboard, "dashboard");
            var link = dashboard.GetVisualDescendants().OfType<ComboBox>().First(c => c.Tag is DashboardCard); link.SelectedIndex = 0; await Task.Delay(100);
            var charts = dashboard.GetVisualDescendants().OfType<Controls.ChartRenderControl>().ToArray();
            if (charts.Length != 2 || charts.Any(c => c.Chart?.Labels.Count != 1)) throw new Exception("Dashboard linked filtering failed.");
            await dashboard.ExportPageAsync(Path.Combine(output, "dashboard.html"));
            if (!File.ReadAllText(Path.Combine(output, "dashboard.html")).Contains("data:image/png;base64,")) throw new Exception("Dashboard HTML did not embed charts.");
            var pages = dashboard.GetVisualDescendants().OfType<ComboBox>().Single(c => c.Name == "PageSelector"); pages.SelectedItem = "第二页"; await Task.Delay(400);
            if (dashboard.GetVisualDescendants().OfType<Controls.ChartRenderControl>().Single().Chart?.Title != "第二页图表") throw new Exception("Dashboard page selection failed.");
            await Capture(dashboard, "dashboard-page2"); dashboard.Close();
            await VerifySqliteDesignerAndFilesAsync(item, connections, query, folder, Capture);

            var encrypted = new ConnectionItem
            {
                Name = "P2 SQLCipher fixture",
                DatabaseType = "Sqlite",
                Database = Path.Combine(folder, "encrypted.db"),
                Password = "p2-fixture-only",
            };
            File.WriteAllBytes(encrypted.Database, []);
            var encryptedConnections = new FixtureConnections(encrypted);
            var encryptedQuery = new DefaultQueryService(encryptedConnections);
            await VerifySqliteDesignerAndFilesAsync(encrypted, encryptedConnections, encryptedQuery, folder, Capture);
            File.WriteAllText(Path.Combine(output, "passed.txt"), "P2 UI flows passed, including SQLite and SQLCipher table design and file round trips.");
            string Output(Window window) => window.GetVisualDescendants().OfType<TextBox>().Single(t => t.Name == "OperationOutput").Text ?? "";
            string Status(Window window) => window.GetVisualDescendants().OfType<TextBlock>().Single(t => t.Name == "OperationStatus").Text ?? "";
            async Task Click(Window window, string name)
            {
                var button = window.GetVisualDescendants().OfType<Button>().First(b => b.Content?.ToString() == name);
                button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                for (int tries = 0; !button.IsEnabled && tries < 300; tries++) await Task.Delay(50);
                if (!button.IsEnabled) throw new TimeoutException(name); await Task.Delay(100);
                if (Status(window) == "正在处理…") throw new Exception("UI action did not finish: " + name);
            }
        }
        finally { await query.CloseConnectionAsync(item.Name); Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools(); }
        async Task Capture(Window window, string name)
        {
            await Task.Delay(150); using var bitmap = new RenderTargetBitmap(new global::Avalonia.PixelSize((int)window.Bounds.Width, (int)window.Bounds.Height), new global::Avalonia.Vector(96, 96)); bitmap.Render(window); bitmap.Save(Path.Combine(output, name + ".png"), new PngBitmapEncoderOptions());
        }
    }

    private static async Task VerifySchemaCompareAsync(ConnectionItem source, IQueryService query, string folder)
    {
        var target = new ConnectionItem
        {
            Name = "P2 schema comparison target",
            DatabaseType = source.DatabaseType,
            Database = Path.Combine(folder, "schema-target.db"),
        };
        File.WriteAllBytes(target.Database, []);

        var created = await query.ExecuteStandaloneAsync(target,
            "CREATE TABLE sample(id INTEGER PRIMARY KEY, phone TEXT, amount INTEGER);");
        if (!created.IsSuccess) throw new InvalidOperationException(created.ErrorMessage);

        var comparer = new DefaultCompareService();
        var equal = await comparer.CompareSchemaAsync(source, target, DatabaseObjectType.Table);
        if (equal.ErrorMessage is not null || equal.HasDifferences)
            throw new InvalidOperationException(equal.ErrorMessage ?? "Identical SQLite schemas reported differences.");

        foreach (var (connection, indexSql) in new[]
        {
            (source, "CREATE INDEX idx_sample ON sample(phone);"),
            (target, "CREATE INDEX idx_sample ON sample(phone, amount);")
        })
        {
            var result = await query.ExecuteStandaloneAsync(connection, indexSql);
            if (!result.IsSuccess) throw new InvalidOperationException(result.ErrorMessage);
        }

        var changed = await comparer.CompareSchemaAsync(source, target, DatabaseObjectType.Table);
        if (changed.ErrorMessage is not null || !changed.HasDifferences
            || !changed.Differences.SelectMany(d => d.SubDifferences).Any(d => d.DatabaseObjectType == DatabaseObjectType.Index
                && d.DifferenceType == SchemaCompareDifferenceType.Modified))
            throw new InvalidOperationException(changed.ErrorMessage ?? "SQLite index column differences were missed.");
    }

    private static async Task VerifyDateFunctionConversionAsync(ConnectionItem item, IQueryService query)
    {
        var info = new ConnectionInfo { Database = item.Database };
        using var translator = new FunctionTranslator(
            DbInterpreterHelper.GetDbInterpreter(DatabaseType.SqlServer, info, new DbInterpreterOption()),
            DbInterpreterHelper.GetDbInterpreter(DatabaseType.Sqlite, info, new DbInterpreterOption()));
        translator.LoadMappings();
        translator.LoadFunctionSpecifications();
        foreach (var (expression, expected) in new[]
        {
            ("DATEADD(DAY,1,'2024-02-28')", "'2024-02-29'"),
            ("DATEDIFF(DAY,'2024-02-28','2024-03-01')", "2"),
            ("DATEPART(YEAR,'2024-02-29')", "'2024'"),
        })
        {
            var sql = translator.GetMappedFunction(expression);
            var result = await query.ExecuteStandaloneAsync(item, $"SELECT ({sql}) = {expected}");
            if (!result.IsSuccess || result.Rows.Count != 1 || result.Rows[0][0] != "1")
                throw new InvalidOperationException($"Date conversion failed for {sql}. Expected {expected}. {result.ErrorMessage}");
        }
    }

    private static async Task VerifySqliteDesignerAndFilesAsync(ConnectionItem item, IDbConnectionService connections,
        IQueryService query, string folder, Func<Window, string, Task> capture)
    {
        const string table = "aot_source";
        const string target = "aot_target";
        var vm = new TableDesignerViewModel(new DefaultTableDesignService(connections));
        Require(await vm.LoadAsync(item.Name, item.Database, table, null, isNew: true), vm.StatusMessage);
        vm.Columns.Add(new TableDesignColumn { Name = "id", DataType = "INTEGER", IsNullable = false });
        vm.Columns.Add(new TableDesignColumn { Name = "value", DataType = "TEXT", IsNullable = false });
        vm.PrimaryKey = new TableDesignKey { Name = "pk_source", Columns = new() { "id" } };

        var window = new TableDesignerWindow(vm);
        window.Show();
        try
        {
            await vm.GeneratePreviewCommand.ExecuteAsync(null);
            Require(vm.PreviewScript.Contains("CREATE TABLE", StringComparison.OrdinalIgnoreCase), vm.StatusMessage);
            await vm.SaveCommand.ExecuteAsync(null);
            Require(!vm.IsNew && vm.PrimaryKey?.Columns.SequenceEqual(new[] { "id" }) == true && vm.Columns.Count == 2, vm.StatusMessage);
            await RunSqlAsync("INSERT INTO aot_source VALUES (1, '中文, ''quoted'''), (2, 'second');");

            vm.Columns.Add(new TableDesignColumn { Name = "note", DataType = "TEXT", DefaultValue = "'added'" });
            await vm.GeneratePreviewCommand.ExecuteAsync(null);
            Require(vm.PreviewScript.Contains("ALTER TABLE", StringComparison.OrdinalIgnoreCase), vm.StatusMessage);
            await vm.SaveCommand.ExecuteAsync(null);
            Require(vm.Columns.Any(c => c.Name == "note"), vm.StatusMessage);
            await capture(window, string.IsNullOrEmpty(item.Password) ? "sqlite-table-designer" : "sqlcipher-table-designer");
        }
        finally
        {
            window.Close();
        }

        await VerifyRowsAsync(table);
        await RunSqlAsync("CREATE TABLE aot_target(id INTEGER PRIMARY KEY, value TEXT NOT NULL, note TEXT);");
        await RunSqlAsync("CREATE VIEW aot_source_view AS SELECT id, value, note FROM aot_source;");

        var files = new DefaultExportImportService(connections);
        foreach (var (format, extension) in new[] { ("CSV", "csv"), ("Excel", "xlsx"), ("JSON", "json"), ("XML", "xml"), ("SQL", "sql") })
        {
            bool isView = format == "CSV";
            string path = Path.Combine(folder, Path.GetFileNameWithoutExtension(item.Database) + "." + extension);
            var export = await files.ExportDataAsync(item, isView ? "aot_source_view" : table, null, isView, format, path);
            Require(export.IsSuccess, $"{format} export failed: {export.Message}");

            if (format == "SQL")
            {
                await RunSqlAsync("DELETE FROM aot_source;");
                await RunSqlAsync(await File.ReadAllTextAsync(path));
                await VerifyRowsAsync(table);
                continue;
            }

            var preview = await files.PreviewFileAsync(path);
            Require(preview.IsSuccess && preview.Columns.SequenceEqual(new[] { "id", "value", "note" }) && preview.TotalRows == 2,
                $"{format} preview failed: {preview.Message}");
            await RunSqlAsync("DELETE FROM aot_target;");
            var import = await files.ImportDataAsync(item, target, null, path);
            Require(import.IsSuccess, $"{format} import failed: {import.Message}");
            await VerifyRowsAsync(target);
        }

        if (!string.IsNullOrEmpty(item.Password))
        {
            using var databaseFile = new FileStream(item.Database, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            var header = new byte[16];
            databaseFile.ReadExactly(header);
            Require(!header.AsSpan().SequenceEqual("SQLite format 3\0"u8), "Encrypted fixture must retain the SQLCipher file format.");
        }

        async Task<QueryResult> RunSqlAsync(string sql)
        {
            var result = await query.ExecuteStandaloneAsync(item, sql);
            Require(result.IsSuccess, result.ErrorMessage ?? "SQLite execution failed.");
            return result;
        }

        async Task VerifyRowsAsync(string name)
        {
            var result = await RunSqlAsync($"SELECT id, value, note FROM {name} ORDER BY id;");
            Require(result.Rows.Count == 2 && result.Rows[0].SequenceEqual(new[] { "1", "中文, 'quoted'", "added" })
                && result.Rows[1].SequenceEqual(new[] { "2", "second", "added" }), $"{name} did not preserve its rows.");
        }

        void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException($"{item.Name}: {message}");
        }
    }

    private sealed class FixtureConnections(ConnectionItem item) : IDbConnectionService
    {
        public IReadOnlyList<ConnectionItem> GetConnections() => new[] { item };
        public IReadOnlyList<ConnectionItem> GetConnections(string type) => GetConnections();
        public ConnectionItem? GetConnectionById(string id) => item;
        public Task<IReadOnlyList<ConnectionItem>> GetConnectionsAsync(CancellationToken cancellationToken = default) => Task.FromResult(GetConnections());
        public Task<ConnectionItem?> GetConnectionByIdAsync(string id, CancellationToken cancellationToken = default) => Task.FromResult(GetConnectionById(id));
        public Task<IReadOnlyList<string>> TestConnectionAsync(ConnectionItem c, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<string>>(new[] { c.Database });
        public Task<string?> SaveAsync(ConnectionItem c, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool> DeleteAsync(IEnumerable<string> ids, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool> IsNameExistedAsync(bool add, string? account, string name, string? id, CancellationToken ct = default) => Task.FromResult(false);
    }
    private sealed class FixtureDashboard : IDashboardService
    {
        private readonly List<DashboardChart> _items = new();
        public IReadOnlyList<DashboardChart> GetAll() => _items;
        public void Save(DashboardChart chart) => _items.Add(chart);
        public void Delete(string id) => _items.RemoveAll(c => c.Id == id);
    }
}
