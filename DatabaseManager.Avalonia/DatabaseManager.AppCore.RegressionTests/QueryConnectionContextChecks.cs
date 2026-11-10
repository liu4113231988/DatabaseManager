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
                File.WriteAllBytes(connection.Database, []);
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
            await vm.QueryEditor.SetAutoCommitAsync(false);
            Check(!vm.QueryEditor.AutoCommit && !query.IsAutoCommit(a.Name) && !query.IsTransactionActive(a.Name),
                "Manual mode must persist before its first SQL statement.");
            tabA.SqlText = "UPDATE marker SET value='manual'";
            await tabA.ExecuteAsync();
            Check(query.IsTransactionActive(a.Name) && !vm.QueryEditor.AutoCommit,
                "The normal tab execution path must start a manual transaction without resetting its checkbox.");
            Check(draft.IsSharedTransaction && tabA.IsSharedTransaction && !tabB.IsSharedTransaction,
                "Shared-transaction indicator must mark every tab sharing the active manual-transaction connection.");
            Check((await query.ExecuteStandaloneAsync(a, "SELECT value FROM marker")).Rows.Single()[0] == a.Name,
                "Uncommitted manual-mode writes must remain invisible to standalone SQL.");
            await vm.QueryEditor.RollbackTransactionAsync();
            Check(!query.IsTransactionActive(a.Name) && !vm.QueryEditor.AutoCommit,
                "Rollback ends the transaction but preserves manual mode.");
            Check(!draft.IsSharedTransaction && !tabA.IsSharedTransaction,
                "Rollback must clear the shared-transaction indicator on every associated tab.");
            var sibling = vm.OpenSqlInNewTab(a.Name, "SELECT value FROM marker", a.Database);
            await tabA.ExecuteAsync();
            Check(tabA.IsSharedTransaction && draft.IsSharedTransaction && sibling.IsSharedTransaction,
                "A background tab execution must refresh the shared-transaction indicator without a tab switch.");
            await vm.QueryEditor.CommitTransactionAsync();
            Check(!query.IsTransactionActive(a.Name) && !vm.QueryEditor.AutoCommit && !tabA.IsSharedTransaction && !sibling.IsSharedTransaction
                && (await query.ExecuteStandaloneAsync(a, "SELECT value FROM marker")).Rows.Single()[0] == "manual",
                "A background sibling tab starts a fresh transaction which the selected tab can commit without a tab switch.");
            await tabA.ExecuteAsync();

            var gates = (System.Collections.Concurrent.ConcurrentDictionary<string, SemaphoreSlim>)typeof(DefaultQueryService)
                .GetField("_connectionLocks", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(query)!;
            var gate = gates[a.Name];
            await gate.WaitAsync();
            Task modeChange;
            try
            {
                modeChange = vm.QueryEditor.SetAutoCommitAsync(true);
                Check(!modeChange.IsCompleted && vm.QueryEditor.IsTransactionBusy && !vm.QueryEditor.CanChangeTransaction,
                    "Switching to auto commit must asynchronously wait for the current connection operation.");
                vm.SelectedQueryTab = tabB;
                await tabB.ExecuteAsync();
                Check(tabB.Rows.Single()[0] == b.Name && vm.QueryEditor.AutoCommit,
                    "A pending mode change must not block B or copy A's transaction display to B.");
            }
            finally { gate.Release(); }
            await modeChange.WaitAsync(TimeSpan.FromSeconds(10));
            Check(query.IsAutoCommit(a.Name) && !query.IsTransactionActive(a.Name) && vm.QueryEditor.AutoCommit,
                "Async mode change must commit A and leave selected B's state intact.");
            await query.ExecuteAsync(a.Name, $"UPDATE marker SET value='{a.Name}'");
            tabA.SqlText = "SELECT value FROM marker";
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
            await query.ExecuteAsync(a.Name, "UPDATE marker SET value='closing'");
            await gate.WaitAsync();
            Task disconnect;
            Task<QueryResult> queued;
            try
            {
                disconnect = vm.DisconnectConnectionNodeAsync(explorer.FindConnectionNode(a.Name)!);
                Check(!disconnect.IsCompleted && !query.IsConnected(a.Name),
                    "Disconnect must reject new work immediately and asynchronously wait for in-flight work.");
                queued = query.ExecuteAsync(a.Name, "UPDATE marker SET value='must not execute'");
                using var canceled = new CancellationTokenSource();
                var waiting = query.ExecuteAsync(a.Name, "DELETE FROM marker", canceled.Token);
                canceled.Cancel();
                Check(!(await waiting).IsSuccess, "Queued queries must be cancellable without releasing another operation's lock.");
                Check((await query.ExecuteAsync(b.Name, "SELECT value FROM marker")).IsSuccess,
                    "Disconnecting A must not block queries on B.");
            }
            finally { gate.Release(); }
            await disconnect.WaitAsync(TimeSpan.FromSeconds(10));
            Check(!(await queued).IsSuccess
                && (await query.ExecuteStandaloneAsync(a, "SELECT value FROM marker")).Rows.Single()[0] == a.Name,
                "Disconnect must roll back pending changes and reject queued writes.");
            Check(query.IsConnected(b.Name) && vm.IsConnected && tabA.ConnectionName == a.Name,
                "Disconnecting A must retain A's binding and leave B's browser connection active.");

            var group = new DbObjectTreeNode { Name = "group", NodeType = DbObjectTreeNodeType.Folder };
            var bNode = explorer.FindConnectionNode(b.Name)!;
            explorer.RootNodes.Remove(bNode);
            group.AddChild(bNode);
            explorer.RootNodes.Add(group);
            await vm.ConnectConnectionNodeAsync(explorer.FindConnectionNode(a.Name)!);
            Check(vm.CanDisconnectAll, "Grouped active connections must enable Disconnect All.");
            await vm.DisconnectAllAsync();
            Check(!query.IsConnected(a.Name) && !query.IsConnected(b.Name) && !bNode.IsConnectionActive && !vm.CanDisconnectAll,
                "Disconnect All must include both root-level and grouped connections.");
            await vm.QueryEditor.SetAutoCommitAsync(false);
            Check(vm.QueryEditor.AutoCommit && !vm.QueryEditor.IsTransactionBusy && vm.SelectedQueryTab!.StatusMessage.Contains("失败"),
                "Failed mode changes on a disconnected connection must restore the displayed mode and report failure.");
        }
        finally
        {
            await query.CloseConnectionAsync(a.Name);
            await query.CloseConnectionAsync(b.Name);
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
