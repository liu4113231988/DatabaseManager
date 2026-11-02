using DatabaseManager.AppCore.Models;
using DatabaseManager.AppCore.Services;
using DatabaseManager.AppCore.ViewModels;

internal static class QueryConnectionContextChecks
{
    public static async Task RunAsync()
    {
        var folder = Path.Combine(Path.GetTempPath(), $"dbm-tab-context-{Guid.NewGuid():N}");
        Directory.CreateDirectory(folder);
        var a = new ConnectionItem { Name = "Context A", DatabaseType = "Sqlite", Database = Path.Combine(folder, "a.db") };
        var b = new ConnectionItem { Name = "Context B", DatabaseType = "Sqlite", Database = Path.Combine(folder, "b.db") };
        var connections = new QueryExecutionChecks.Connections(a, b);
        var query = new DefaultQueryService(connections);
        var schema = new DefaultDbSchemaService(connections);
        var explorer = new ObjectsExplorerViewModel(schema);
        var vm = new MainWindowViewModel(schema, connections, query, new DefaultDataEditService(connections),
            explorer, new QueryEditorViewModel(query));
        try
        {
            foreach (var connection in new[] { a, b })
            {
                var result = await query.ExecuteStandaloneAsync(connection,
                    $"CREATE TABLE marker (value TEXT); INSERT INTO marker VALUES ('{connection.Name}');");
                Check(result.IsSuccess, result.ErrorMessage ?? "Create marker database failed.");
            }
            vm.Initialize();
            var draft = vm.SelectedQueryTab!;
            await vm.ConnectConnectionNodeAsync(explorer.FindConnectionNode(a.Name)!);
            Check(draft.ConnectionName == a.Name && draft.DatabaseName == a.Database,
                "First successful connection must bind the unbound draft.");
            var tabA = vm.OpenSqlInNewTab(a.Name, "SELECT value FROM marker", a.Database);
            await vm.ConnectConnectionNodeAsync(explorer.FindConnectionNode(b.Name)!);
            Check(draft.ConnectionName == a.Name && tabA.ConnectionName == a.Name && tabA.DatabaseName == a.Database,
                "Connecting B must preserve every existing A tab.");
            Check(vm.QueryEditor.ConnectionName == a.Name, "Transaction toolbar must follow the selected tab, not the browser.");
            await tabA.ExecuteAsync();
            Check(tabA.Rows.Single()[0] == a.Name, "A tab must still execute against database A after connecting B.");

            vm.NewQuery();
            var tabB = vm.SelectedQueryTab!;
            Check(tabB.ConnectionName == b.Name && tabB.DatabaseName == b.Database,
                "Only a new tab inherits the browser connection B.");
            tabB.SqlText = "SELECT value FROM marker";
            await tabB.ExecuteAsync();
            Check(tabB.Rows.Single()[0] == b.Name, "B tab must execute against database B.");

            vm.SelectedQueryTab = tabA;
            await vm.QueryEditor.BeginTransactionAsync();
            await query.ExecuteAsync(a.Name, "UPDATE marker SET value='pending'");
            vm.SelectedQueryTab = tabB;
            Check(vm.QueryEditor.ConnectionName == b.Name && !vm.QueryEditor.IsTransactionActive && vm.QueryEditor.AutoCommit,
                "B must not display A's transaction state.");
            Check(query.IsTransactionActive(a.Name), "Switching tabs must never commit A's transaction.");
            await vm.QueryEditor.BeginTransactionAsync();
            await vm.QueryEditor.RollbackTransactionAsync();
            Check(query.IsTransactionActive(a.Name) && !query.IsTransactionActive(b.Name),
                "B transaction commands must leave A's transaction intact.");
            vm.SelectedQueryTab = tabA;
            Check(vm.QueryEditor.IsTransactionActive && !vm.QueryEditor.AutoCommit,
                "Returning to A must restore its transaction display without side effects.");
            await vm.QueryEditor.RollbackTransactionAsync();
            Check((await query.ExecuteAsync(a.Name, "SELECT value FROM marker")).Rows.Single()[0] == a.Name,
                "A's pending update must have been rolled back, never implicitly committed by tab switches.");

            var snapshot = vm.GetSelectedQueryConnection()!;
            Check(snapshot.Name == a.Name && snapshot.Database == a.Database && !ReferenceEquals(snapshot, a),
                "Execution plan context must snapshot the selected A tab while the browser is on B.");
            snapshot.Database = "other";
            Check(a.Database != "other", "Tool snapshots must not mutate saved connection profiles.");
            Check(vm.OpenSqlInNewTab(a.Name, "SELECT 1").DatabaseName == a.Database,
                "Explicit A tools must not inherit B's database when no override is supplied.");
            vm.SelectedConnection = a;
            vm.CurrentDatabase = "browser-only";
            vm.SelectedConnection = b;
            vm.NewQuery();
            Check(vm.SelectedQueryTab!.DatabaseName == b.Database, "Selecting B must discard A's browser database context.");
            vm.DisconnectConnectionNode(explorer.FindConnectionNode(a.Name)!);
            Check(query.IsConnected(b.Name) && vm.IsConnected && tabA.ConnectionName == a.Name,
                "Disconnecting A must retain A's binding and leave B's browser connection active.");
        }
        finally
        {
            query.CloseConnection(a.Name);
            query.CloseConnection(b.Name);
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            try { Directory.Delete(folder, true); }
            catch (IOException) { Console.WriteLine("Temporary SQLite files are still held by the provider: " + folder); }
        }
        Console.WriteLine("Query connection context checks passed.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
