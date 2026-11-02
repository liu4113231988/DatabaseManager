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
            await SqlCompletionSmokeChecks.RunAsync(output);
            await query.ExecuteAsync(item.Name, "CREATE TABLE sample(id INTEGER PRIMARY KEY, title TEXT, payload BLOB); INSERT INTO sample VALUES(1,'测试文本',X'0102');");
            var tab = new QueryTabViewModel(query, new DefaultDataEditService(connections)) { ConnectionName = item.Name, DatabaseName = item.Database, SqlText = "SELECT * FROM sample" };
            await tab.ExecuteAsync(); tab.PinResultCommand.Execute(null);
            await Capture(new RecordEditorWindow(tab, tab.Rows[0], item), "record");
            await Capture(new ResultCompareWindow(tab.ResultSnapshots.ToArray()), "compare");
            var server = new ConnectionItem { Name = "数据库切换测试", DatabaseType = "Postgres", Database = "app_db" };
            var serverConnections = new FixtureConnections(server);
            var serverQuery = new DefaultQueryService(serverConnections);
            var schema = new DefaultDbSchemaService(serverConnections);
            var explorer = new ObjectsExplorerViewModel(schema);
            var mainVm = new MainWindowViewModel(schema, serverConnections, serverQuery,
                new DefaultDataEditService(serverConnections), explorer, new QueryEditorViewModel(serverQuery));
            mainVm.Initialize();
            var shell = new MainWindow { DataContext = mainVm };
            var content = shell.Content;
            shell.Content = null;
            var databaseWindow = new Window { DataContext = mainVm, Content = content, Width = 1280, Height = 800 };
            await Capture(databaseWindow, "query-database", () =>
            {
                var node = explorer.FindConnectionNode(server.Name)!;
                foreach (var name in new[] { "app_db", "report_db" })
                    node.Children.Add(new DbObjectTreeNode { Name = name, NodeType = DbObjectTreeNodeType.Database });
                var queryTab = mainVm.OpenSqlInNewTab(server.Name, "SELECT current_database();", server.Database);
                if (queryTab.AvailableDatabases.Count != 2) throw new InvalidOperationException("Database selector fixture failed.");
            }, async () =>
            {
                var queryTab = mainVm.SelectedQueryTab!;
                var selector = databaseWindow.GetVisualDescendants().OfType<AtomUI.Desktop.Controls.ComboBox>()
                    .Single(c => ReferenceEquals(c.ItemsSource, queryTab.AvailableDatabases));
                if (!selector.IsVisible || !Equals(selector.SelectedItem, "app_db"))
                    throw new InvalidOperationException("Database selector must display its initial context.");
                selector.SelectedItem = "report_db";
                await Task.Delay(100);
                if (queryTab.DatabaseName != "report_db") throw new InvalidOperationException("Database selection binding failed.");
                queryTab.IsExecuting = true;
                selector.SelectedItem = "app_db";
                await Task.Delay(100);
                if (queryTab.DatabaseName != "report_db" || !Equals(selector.SelectedItem, "report_db"))
                    throw new InvalidOperationException("Rejected database selection must restore the displayed context.");
                queryTab.IsExecuting = false;
            });
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
        async Task Capture(Window window, string name, Action? arrange = null, Func<Task>? verify = null)
        {
            window.Show(); await Task.Delay(200); arrange?.Invoke(); await Task.Delay(200);
            if (verify is not null) await verify();
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
