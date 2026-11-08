using DatabaseInterpreter.Model;
using DatabaseManager.AppCore.Models;
using DatabaseManager.AppCore.Services;
using DatabaseManager.AppCore.ViewModels;

internal static class DatabaseDeletionChecks
{
    public static async Task RunAsync()
    {
        foreach (var (type, name, expected) in new[]
        {
            ("Postgres", "a.b\"; DROP DATABASE other;--", "\"a.b\"\"; DROP DATABASE other;--\""),
            ("MySql", "a.b`x", "`a.b``x`"),
            ("SqlServer", "a.b]x", "[a.b]]x]"),
        })
        {
            var item = new ConnectionItem { Name = "delete-test", DatabaseType = type };
            var ddl = new DefaultDdlService(new QueryExecutionChecks.Connections(item));
            Check(ddl.PreviewDropDatabase(item.Name, name).Script == $"DROP DATABASE {expected};",
                "Database names must be quoted as a single identifier, including dots and delimiters.");
            foreach (var invalid in new[] { "", " ", "a\0b", "a\nb" })
                Check(!(await ddl.DropDatabaseAsync(item.Name, invalid)).IsSuccess, "Invalid names must fail before connecting.");
            string[] system = type switch
            {
                "Postgres" => ["postgres", "template0", "template1"],
                "MySql" => ["mysql", "sys", "information_schema", "performance_schema"],
                _ => ["master", "model", "msdb", "tempdb"],
            };
            foreach (var database in system)
                Check(!(await ddl.DropDatabaseAsync(item.Name, database.ToUpperInvariant())).IsSuccess,
                    "System databases must be blocked by execution as well as the menu.");
            using var canceled = new CancellationTokenSource();
            canceled.Cancel();
            Check(!(await ddl.DropDatabaseAsync(item.Name, "user_db", canceled.Token)).IsSuccess,
                "Canceled deletion must fail before connecting.");
        }
        foreach (var type in new[] { "Oracle", "DM", "Sqlite", "DuckDB", "KingbaseES", "Unknown" })
        {
            var item = new ConnectionItem { Name = "unsupported", DatabaseType = type };
            var ddl = new DefaultDdlService(new QueryExecutionChecks.Connections(item));
            Check(!(await ddl.DropDatabaseAsync(item.Name, "user_db")).IsSuccess, "Unsupported engines must never delete files or schemas.");
            Check(!ddl.PreviewDropDatabase("missing", "user_db").IsSuccess, "Missing connections must fail safely.");
        }

        var connection = new ConnectionItem { Name = "server", DatabaseType = "Postgres", Database = "user_db" };
        var connections = new QueryExecutionChecks.Connections(connection);
        var query = new DefaultQueryService(connections);
        var schema = new DefaultDbSchemaService(connections);
        var explorer = new ObjectsExplorerViewModel(schema);
        explorer.LoadConnections([connection]);
        var parent = explorer.FindConnectionNode(connection.Name)!;
        var databaseNode = new DbObjectTreeNode { Name = "user_db", NodeType = DbObjectTreeNodeType.Database };
        parent.AddChild(databaseNode);
        parent.AddChild(new DbObjectTreeNode { Name = "other_db", NodeType = DbObjectTreeNodeType.Database });
        var vm = new MainWindowViewModel(schema, connections, query, new DefaultDataEditService(connections),
            explorer, new QueryEditorViewModel(query));
        vm.Connections.Add(connection);
        vm.SelectedConnection = connection;
        var deletedTab = vm.OpenSqlInNewTab(connection.Name, "SELECT * FROM valuable_data", "user_db");
        var otherTab = vm.OpenSqlInNewTab(connection.Name, "SELECT 2", "other_db");
        var stub = new DdlStub();
        deletedTab.IsExecuting = true;
        Check(!await vm.DeleteDatabaseAsync(databaseNode, stub) && stub.Calls == 0, "Running queries must block deletion.");
        deletedTab.IsExecuting = false;
        deletedTab.HasPendingChanges = true;
        Check(!await vm.DeleteDatabaseAsync(databaseNode, stub) && stub.Calls == 0, "Unsaved data edits must block deletion.");
        deletedTab.HasPendingChanges = false;
        Check(!await vm.DeleteDatabaseAsync(databaseNode, stub) && parent.Children.Contains(databaseNode)
            && deletedTab.ConnectionName == connection.Name && connection.Database == "user_db", "Failure must retain the tree and contexts.");
        stub.Succeed = true;
        stub.Pending = new TaskCompletionSource<DdlExecuteResult>();
        var deletion = vm.DeleteDatabaseAsync(databaseNode, stub);
        Check(!await vm.DeleteDatabaseAsync(databaseNode, stub) && stub.Calls == 2, "Duplicate clicks must not submit two deletions.");
        var lateTab = vm.OpenSqlInNewTab(connection.Name, "SELECT 3", "user_db");
        stub.Pending.SetResult(new DdlExecuteResult { IsSuccess = true });
        Check(await deletion, "Successful deletion must update the UI state.");
        Check(!await vm.DeleteDatabaseAsync(databaseNode, stub) && stub.Calls == 2, "A stale menu must not delete a recreated database.");
        Check(!parent.Children.Contains(databaseNode) && connection.Database == "postgres" && vm.CurrentDatabase == "postgres",
            "The deleted database must be removed and browser defaults must move to the maintenance database.");
        Check(deletedTab.ConnectionName == "" && deletedTab.DatabaseName == "" && deletedTab.SqlText == "SELECT * FROM valuable_data",
            "Preserve SQL while removing the deleted database's execution binding.");
        Check(lateTab.ConnectionName == "" && lateTab.DatabaseName == "" && lateTab.SqlText == "SELECT 3",
            "Tabs opened while deletion is pending must also lose the deleted database binding.");
        Check(otherTab.ConnectionName == connection.Name && otherTab.DatabaseName == "other_db"
            && !otherTab.AvailableDatabases.Contains("user_db"), "Other databases must keep their context and lose stale selector entries.");
        Console.WriteLine("Database deletion checks passed.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    internal sealed class DdlStub : IDdlService
    {
        public DdlScriptResult PreviewCreateDatabase(string c, string d) => throw new NotSupportedException();
        public int CreateCalls { get; private set; }
        public string? CreatedConnection { get; private set; }
        public string? CreatedDatabase { get; private set; }
        public Task<DdlExecuteResult> CreateDatabaseAsync(string c, string d, CancellationToken ct = default)
        {
            CreateCalls++;
            CreatedConnection = c;
            CreatedDatabase = d;
            return Pending?.Task ?? Task.FromResult(new DdlExecuteResult { IsSuccess = Succeed, ErrorMessage = Succeed ? null : "permission denied" });
        }
        public int Calls { get; private set; }
        public bool Succeed { get; set; }
        public TaskCompletionSource<DdlExecuteResult>? Pending { get; set; }
        public Task<DdlExecuteResult> DropDatabaseAsync(string connectionName, string databaseName, CancellationToken ct = default)
        {
            Calls++;
            Check(connectionName == "server" && databaseName == "user_db", "Delete must use the node's own connection and database.");
            return Pending?.Task ?? Task.FromResult(new DdlExecuteResult { IsSuccess = Succeed, ErrorMessage = Succeed ? null : "database is in use" });
        }
        public DdlScriptResult PreviewDropDatabase(string c, string d) => throw new NotSupportedException();
        public DdlScriptResult PreviewDrop(string c, string d, DatabaseObject o) => throw new NotSupportedException();
        public Task<DdlExecuteResult> DropAsync(string c, string d, DatabaseObject o, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<DdlExecuteResult> RenameTableAsync(string c, string d, Table t, string n, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<DdlExecuteResult> RenameTableColumnAsync(string c, string d, Table t, TableColumn col, string n, CancellationToken ct = default) => throw new NotSupportedException();
        public DdlScriptResult GetCreateTemplate(DatabaseObjectType t, string? s) => throw new NotSupportedException();
        public Task<DdlScriptResult> GetObjectDefinitionAsync(string c, string d, DatabaseObject o, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<DdlScriptResult> GenerateObjectScriptAsync(string c, string d, DatabaseObject o, ObjectScriptType t, CancellationToken ct = default) => throw new NotSupportedException();
        public DdlScriptResult GetAddColumnTemplate(string t, Table table) => throw new NotSupportedException();
    }
}
