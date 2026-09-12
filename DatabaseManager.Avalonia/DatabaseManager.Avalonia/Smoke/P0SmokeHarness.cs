using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using DatabaseManager.AppCore.Models;
using DatabaseManager.AppCore.Services;
using DatabaseManager.AppCore.ViewModels;
using DatabaseManager.Avalonia.Views;

namespace DatabaseManager.Avalonia.Smoke;

/// <summary>Isolated UI fixtures: no saved database connections or scheduled jobs are executed.</summary>
public static class P0SmokeHarness
{
    public static async Task RunAsync()
    {
        var output = Path.GetFullPath(Path.Combine("artifacts", "p0-ui"));
        Directory.CreateDirectory(output);
        File.Delete(Path.Combine(output, "passed.txt"));
        var folder = Path.Combine(Path.GetTempPath(), "dbm-ui-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        var item = new ConnectionItem { Name = "测试连接", DatabaseType = "Sqlite", Database = Path.Combine(folder, "test.db") };
        var connections = new FixtureConnections(item);
        var query = new DefaultQueryService(connections);
        query.NotifyConnected(item.Name);
        try
        {
            await query.ExecuteAsync(item.Name, "CREATE TABLE sample(id INTEGER PRIMARY KEY, title TEXT, payload BLOB); INSERT INTO sample VALUES(1,'测试文本',X'0102');");
            var tab = new QueryTabViewModel(query, new DefaultDataEditService(connections)) { ConnectionName = item.Name, DatabaseName = item.Database, SqlText = "SELECT * FROM sample" };
            await tab.ExecuteAsync(); tab.PinResultCommand.Execute(null);
            await Capture(new RecordEditorWindow(tab, tab.Rows[0], item), "record");
            await Capture(new ResultCompareWindow(tab.ResultSnapshots.ToArray()), "compare");
            var vm = new ExecutionPlanViewModel(new FixturePlan()) { Connection = item, SqlText = "SELECT * FROM sample" };
            await Capture(new ExecutionPlanWindow(vm), "plan");
            var connect = new ConnectWindow(new ConnectionManagerViewModel(connections), new ConnectionItem { DatabaseType = "Postgres", Name = "SSH 测试", Server = "db.internal", Port = "5432", Ssh = new() { Enabled = true, Host = "bastion.example", UserName = "developer" } });
            connect.Height = 980;
            await Capture(connect, "ssh", () => { foreach (var expander in connect.GetVisualDescendants().OfType<Expander>()) expander.IsExpanded = true; });
            var schedule = new DefaultScheduleService(new DefaultTaskCenterService(), connections, query, new DefaultBackupService(), new DefaultExportImportService(connections), storageDirectory: folder);
            var window = new ScheduleWindow(schedule, connections);
            await Capture(window, "schedule", () => window.GetVisualDescendants().OfType<Button>().First(b => b.Content?.ToString() == "新建计划").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)));
            File.WriteAllText(Path.Combine(output, "passed.txt"), "P0 UI fixtures opened and rendered successfully.");
        }
        finally
        {
            query.CloseConnection(item.Name);
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        }
        async Task Capture(Window window, string name, Action? arrange = null)
        {
            window.Show(); await Task.Delay(200); arrange?.Invoke(); await Task.Delay(200);
            using var bitmap = new RenderTargetBitmap(new global::Avalonia.PixelSize((int)window.Bounds.Width, (int)window.Bounds.Height), new global::Avalonia.Vector(96,96));
            bitmap.Render(window);
            bitmap.Save(Path.Combine(output, name + ".png"));
            window.Close();
        }
    }
    private sealed class FixturePlan : IExecutionPlanService
    {
        public Task<QueryResult> ExplainAsync(ConnectionItem connection,string sql,bool analyze=false,CancellationToken cancellationToken=default) => Task.FromResult(new QueryResult
        {
            Columns = new[] { "QUERY PLAN" }, Rows = new[] { new[] { "[{\"Plan\":{\"Node Type\":\"Index Scan\",\"Relation Name\":\"sample\",\"Total Cost\":8.2,\"Actual Total Time\":0.03}}]" } },
        });
    }
    private sealed class FixtureConnections(ConnectionItem item) : IDbConnectionService
    {
        public IReadOnlyList<ConnectionItem> GetConnections() => new[] { item };
        public IReadOnlyList<ConnectionItem> GetConnections(string type) => GetConnections();
        public ConnectionItem? GetConnectionById(string id) => item;
        public Task<IReadOnlyList<string>> TestConnectionAsync(ConnectionItem c,CancellationToken ct=default) => Task.FromResult<IReadOnlyList<string>>(new[] { c.Database });
        public Task<string?> SaveAsync(ConnectionItem c,CancellationToken ct=default) => throw new NotSupportedException();
        public Task<bool> DeleteAsync(IEnumerable<string> ids,CancellationToken ct=default) => throw new NotSupportedException();
        public Task<bool> IsNameExistedAsync(bool add,string? account,string name,string? id,CancellationToken ct=default) => Task.FromResult(false);
    }
}
