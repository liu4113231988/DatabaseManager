using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using DatabaseManager.AppCore.Models;
using DatabaseManager.AppCore.Services;
using DatabaseManager.Avalonia.Views;
using Microsoft.Extensions.DependencyInjection;

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
            var created = await query.ExecuteStandaloneAsync(item, "CREATE TABLE sample(id INTEGER PRIMARY KEY, phone TEXT, amount INTEGER); INSERT INTO sample VALUES(1,'13812345678',12),(2,'13912345678',25);");
            if (!created.IsSuccess) throw new Exception(created.ErrorMessage);
            var source = await query.ExecuteStandaloneAsync(item, "SELECT * FROM sample");
            var services = new ServiceCollection().AddSingleton<IDbConnectionService>(connections).AddSingleton<IExportImportService>(new DefaultExportImportService(connections)).AddSingleton<IDataEditService>(new DefaultDataEditService(connections)).BuildServiceProvider();
            string sent = ""; var window = new P2WorkbenchWindow(services, (_, sql) => sent = sql, item.Name, item.Database, source); window.Show(); await Task.Delay(250);
            var tabs = window.GetVisualDescendants().OfType<TabControl>().Single(t => t.Name == "WorkbenchTabs");
            await Click("加载对象"); await Click("加入表"); await Click("添加输出"); await Click("预览 SQL"); await Click("送入新查询标签");
            if (!sent.StartsWith("SELECT")) throw new Exception("Builder UI did not produce SQL."); await Capture(window, "query");
            tabs.SelectedIndex = 1; await Task.Delay(100); await Click("读取字段规则"); await Click("生成并预览");
            if (!Output().Contains("已生成")) throw new Exception("Generator UI: " + Status()); await Capture(window, "generator");
            tabs.SelectedIndex = 2; await Task.Delay(100); await Click("剖析选中表"); if (!Output().Contains("已读取全表")) throw new Exception("Quality UI: " + Status()); await Capture(window, "quality");
            tabs.SelectedIndex = 3; await Task.Delay(100); await Click("生成预览"); if (!Output().Contains("sample")) throw new Exception("Dictionary UI: " + Status()); await Capture(window, "dictionary");
            for (int i = 4; i <= 6; i++) { tabs.SelectedIndex = i; await Task.Delay(100); await Capture(window, new[] { "ai", "transfer", "external" }[i - 4]); }
            tabs.SelectedIndex = 7; await Task.Delay(100);
            var maskPanel = ((ScrollViewer)((TabItem)tabs.SelectedItem!).Content!).Content as Control;
            var column = maskPanel!.GetVisualDescendants().OfType<TextBox>().First(t => t.Width == 170); column.Text = "phone";
            await Click("添加 / 更新规则"); await Click("脱敏预览");
            var grid = window.GetVisualDescendants().OfType<DataGrid>().FirstOrDefault(g => g.Name == "PreviewGrid");
            // The result grid may not be realized until its tab is selected.
            var resultTabs = window.GetVisualDescendants().OfType<TabControl>().First(t => t != tabs); resultTabs.SelectedIndex = 1; await Task.Delay(100);
            grid = window.GetVisualDescendants().OfType<DataGrid>().Single(g => g.Name == "PreviewGrid");
            if (grid.ItemsSource.Cast<P2WorkbenchWindow.PreviewRow>().First().Values[1] != "138****5678") throw new Exception("Mask UI values not masked."); await Capture(window, "mask"); window.Close();
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
            File.WriteAllText(Path.Combine(output, "passed.txt"), "P2 UI flows passed: builder, generator, quality, dictionary, masking and all pages rendered.");
            string Output() => window.GetVisualDescendants().OfType<TextBox>().Single(t => t.Name == "OperationOutput").Text ?? "";
            string Status() => window.GetVisualDescendants().OfType<TextBlock>().Single(t => t.Name == "OperationStatus").Text ?? "";
            async Task Click(string name)
            {
                var button = window.GetVisualDescendants().OfType<Button>().First(b => b.Content?.ToString() == name);
                button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                for (int tries = 0; !button.IsEnabled && tries < 300; tries++) await Task.Delay(50);
                if (!button.IsEnabled) throw new TimeoutException(name); await Task.Delay(100);
                if (Status() == "正在处理…") throw new Exception("UI action did not finish: " + name);
            }
        }
        finally { query.CloseConnection(item.Name); Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools(); }
        async Task Capture(Window window, string name)
        {
            await Task.Delay(150); using var bitmap = new RenderTargetBitmap(new global::Avalonia.PixelSize((int)window.Bounds.Width, (int)window.Bounds.Height), new global::Avalonia.Vector(96, 96)); bitmap.Render(window); bitmap.Save(Path.Combine(output, name + ".png"));
        }
    }
    private sealed class FixtureConnections(ConnectionItem item) : IDbConnectionService
    {
        public IReadOnlyList<ConnectionItem> GetConnections() => new[] { item };
        public IReadOnlyList<ConnectionItem> GetConnections(string type) => GetConnections();
        public ConnectionItem? GetConnectionById(string id) => item;
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
