using DatabaseManager.AppCore.Models;
using DatabaseManager.AppCore.Services;
using DatabaseManager.AppCore.ViewModels;

internal static class P0FeatureChecks
{
    public static async Task RunAsync()
    {
        var plan = ExecutionPlanParser.Parse(new QueryResult { Rows = new[] { new[] { "[{\"Plan\":{\"Node Type\":\"Hash Join\",\"Total Cost\":40,\"Plans\":[{\"Node Type\":\"Seq Scan\",\"Relation Name\":\"items\",\"Total Cost\":30,\"Actual Total Time\":2.5}]}}]" } } });
        Check(plan.Count == 1 && plan[0].Children[0].ObjectName == "items" && plan[0].Children[0].Milliseconds == 2.5 && plan[0].Children[0].IsHotspot, "Structured execution plan tree and metrics.");
        var sqlitePlan = ExecutionPlanParser.Parse(new QueryResult { Columns = new[] { "id", "parent", "detail" }, Rows = new[] { new[] { "1", "0", "ROOT" }, new[] { "2", "1", "SCAN items" } } });
        Check(sqlitePlan.Count == 1 && sqlitePlan[0].Children.Count == 1 && sqlitePlan[0].Cost is null, "SQLite hierarchy must not invent costs.");
        var invalid = new ConnectionItem { Server = "127.0.0.1", Port = "5432", Ssh = new() { Enabled = true } };
        Throws(() => ConnectionHelper.ToConnectionInfo(invalid), "SSH without a verified host fingerprint must fail closed.");
        Check(ConnectionHelper.ToConnectionInfo(new() { Server = "db", Port = "42", Database = "test" }).Server == "db", "Direct connections must be unchanged.");
        Throws(() => DefaultScheduleService.Validate(new() { ConnectionName = "test", TaskType = "typo" }), "Unknown schedule type must fail.");

        var folder = Path.Combine(Path.GetTempPath(), "dbm-p0-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        var connection = new ConnectionItem { Name = "p0", DatabaseType = "Sqlite", Database = Path.Combine(folder, "test.db") };
        var connections = new QueryExecutionChecks.Connections(connection);
        var query = new DefaultQueryService(connections);
        query.NotifyConnected(connection.Name);
        try
        {
            Check((await query.ExecuteAsync("p0", "CREATE TABLE items (id INTEGER PRIMARY KEY, name TEXT, payload BLOB); INSERT INTO items VALUES (1,'alpha',X'0102'),(2,'beta',X'0304');")).IsSuccess, "Create fixture.");
            var tab = new QueryTabViewModel(query, new DefaultDataEditService(connections)) { ConnectionName = "p0", DatabaseName = connection.Database, SqlText = "SELECT * FROM items", DangerousSqlConfirmationEnabled = false, AutoRefreshAfterSave = false };
            await tab.ExecuteAsync();
            Check(tab.IsResultEditable && tab.Rows.Count == 2, "Simple query editable metadata.");
            tab.PinResultCommand.Execute(null);
            var pinned = tab.ResultSnapshots.Single(s => s.IsPinned);
            int nameIndex = tab.Columns.IndexOf("name");
            Check(tab.ReplaceValues(nameIndex, "alpha", "gamma", false) == 1 && tab.Rows[0][nameIndex] == "alpha", "Replace preview must not mutate.");
            Check(tab.ReplaceValues(nameIndex, "alpha", "gamma", true) == 1 && tab.HasPendingChanges, "Replace marks edits dirty.");
            var displayed = tab.SelectedResultSnapshot;
            tab.SelectedResultSnapshot = pinned;
            Check(tab.SelectedResultSnapshot == displayed, "Unsaved edits must prevent result switch.");
            await tab.SaveEditsCommand.ExecuteAsync(null);
            Check(!tab.HasPendingChanges && (await query.ExecuteAsync("p0", "SELECT name FROM items WHERE id=1")).Rows[0][0] == "gamma", "Edited values saved once: " + tab.StatusMessage);
            Check(pinned.Result.Rows[0][nameIndex] == "alpha", "Pinned snapshot must be independent.");
            var row = tab.Rows[0];
            var values = Enumerable.Range(0, tab.Columns.Count).Select(i => row[i]).ToArray();
            values[tab.Columns.IndexOf("payload")] = "0xAABB";
            tab.ApplyFormValues(row, values);
            await tab.SaveEditsCommand.ExecuteAsync(null);
            Check((await query.ExecuteAsync("p0", "SELECT hex(payload) FROM items WHERE id=1")).Rows[0][0] == "AABB", "Binary editing must preserve bytes.");

            var tasks = new CaptureTasks();
            var schedules = new DefaultScheduleService(tasks, connections, query, new DefaultBackupService(), new DefaultExportImportService(connections), storageDirectory: folder);
            var definition = new ScheduleDefinition { Name = "batch", ConnectionName = "p0", Steps = new()
            {
                new() { Name="first", ConnectionName="p0", SqlText="UPDATE items SET name='first' WHERE id=1" },
                new() { Name="fail", ConnectionName="p0", SqlText="SELECT * FROM missing" },
                new() { Name="last", ConnectionName="p0", SqlText="UPDATE items SET name='last' WHERE id=1" },
            }};
            schedules.Save(definition);
            schedules.RunNow(definition);
            await tasks.ExecuteAsync();
            Check(tasks.Failed && (await query.ExecuteAsync("p0", "SELECT name FROM items WHERE id=1")).Rows[0][0] == "first", "Stop on failure.");
            definition.ContinueOnFailure = true;
            schedules.Save(definition); schedules.RunNow(definition);
            definition.Steps[2].SqlText = "UPDATE items SET name='unexpected' WHERE id=1";
            await tasks.ExecuteAsync();
            Check(tasks.Failed && (await query.ExecuteAsync("p0", "SELECT name FROM items WHERE id=1")).Rows[0][0] == "last", "Continue on failure, frozen run definition, final failure state.");
            Check(tasks.RunItem!.GetLogSnapshot().Any(l => l.Contains("步骤 3")), "Step logs retained before UI dispatch.");
            definition.Steps[2].SqlText = "UPDATE items SET name='cancelled' WHERE id=1";
            schedules.RunNow(definition);
            tasks.RunItem!.Cts.Cancel(); await tasks.ExecuteAsync();
            Check((await query.ExecuteAsync("p0", "SELECT name FROM items WHERE id=1")).Rows[0][0] == "last", "Cancelled batch must not execute steps.");
        }
        finally
        {
            query.CloseConnection(connection.Name);
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            try { Directory.Delete(folder, true); }
            catch (IOException) { Console.WriteLine("Temporary SQLite files are still held by the provider: " + folder); }
        }
        Console.WriteLine("P0 feature regression checks passed.");
    }
    private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    private static void Throws(Action action, string message) { try { action(); } catch (InvalidOperationException) { return; } throw new InvalidOperationException(message); }
    private sealed class CaptureTasks : ITaskCenterService
    {
        private Func<TaskRun,CancellationToken,Task>? _work;
        public TaskRun? RunItem;
        public bool Failed;
        public async Task ExecuteAsync() { Failed = false; try { await _work!(RunItem!, RunItem!.Cts.Token); } catch { Failed = true; } }
        public TaskRun Run(string title,string category,Func<TaskRun,CancellationToken,Task> work) { _work=work; return RunItem=new TaskRun(title,category); }
        public IReadOnlyList<TaskRun> Runs => Array.Empty<TaskRun>();
        public int RunningCount => 0;
        public bool HasRunning => false;
        public event Action<TaskRun>? TaskFinished { add {} remove {} }
        public event Action? RunsChanged { add {} remove {} }
        public TaskRun Register(string title,string category) => new(title,category);
        public void Cancel(string id) => RunItem?.Cts.Cancel();
        public IReadOnlyList<TaskHistoryEntry> GetHistory() => Array.Empty<TaskHistoryEntry>();
    }
}
