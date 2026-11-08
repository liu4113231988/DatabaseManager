using DatabaseInterpreter.Model;
using DatabaseManager.AppCore.Models;
using DatabaseManager.AppCore.Services;
using DatabaseManager.AppCore.ViewModels;

internal static class DatabaseManagementChecks
{
    public static async Task RunAsync()
    {
        foreach (var (type, name, quoted) in new[]
        {
            ("Postgres", "a.b\";--", "\"a.b\"\";--\""),
            ("MySql", "a.b`x", "`a.b``x`"),
            ("SqlServer", "a.b]x", "[a.b]]x]"),
        })
        {
            var connection = new ConnectionItem { Name = "server", DatabaseType = type };
            var ddl = new DefaultDdlService(new QueryExecutionChecks.Connections(connection));
            Check(ddl.PreviewCreateDatabase("server", name).Script == $"CREATE DATABASE {quoted};", "Create names must remain a single quoted identifier.");
            foreach (var invalid in new[] { "", " ", " db", "db ", "db\0name", new string('x', 129) })
                Check(!(await ddl.CreateDatabaseAsync("server", invalid)).IsSuccess, "Invalid names must fail before any connection is opened.");
            Check(!ddl.PreviewCreateDatabase("missing", name).IsSuccess, "Missing connections must fail safely.");
            var boundary = type == "Postgres" ? new string('中', 21) : new string('x', type == "MySql" ? 64 : 128);
            Check(ddl.PreviewCreateDatabase("server", boundary).IsSuccess && !ddl.PreviewCreateDatabase("server", boundary + "x").IsSuccess,
                "Name limits must prevent silent PostgreSQL identifier truncation, including multibyte text.");
            using var canceled = new CancellationTokenSource();
            canceled.Cancel();
            Check(!(await ddl.CreateDatabaseAsync("server", "new_db", canceled.Token)).IsSuccess, "Canceled creation must not connect.");
            Check(DatabasePropertiesService.BuildSql(ConnectionHelper.ParseDatabaseType(type))!.Contains("@database"),
                "Property queries must bind the target database as a parameter.");
        }
        foreach (var type in new[] { "Oracle", "DM", "Sqlite", "DuckDB", "KingbaseES" })
        {
            var ddl = new DefaultDdlService(new QueryExecutionChecks.Connections(new ConnectionItem { Name = "server", DatabaseType = type }));
            Check(!(await ddl.CreateDatabaseAsync("server", "new_db")).IsSuccess, "Unsupported creation must not create schemas or files.");
        }
        await VerifyCreationStateAsync();
        await VerifyBackupTargetAsync();
        Console.WriteLine("Database management checks passed.");
    }

    private static async Task VerifyCreationStateAsync()
    {
        var connection = new ConnectionItem { Name = "server", DatabaseType = "Postgres", Database = "original" };
        var connections = new QueryExecutionChecks.Connections(connection);
        var query = new DefaultQueryService(connections);
        var schema = new SchemaFixture();
        var explorer = new ObjectsExplorerViewModel(schema);
        explorer.LoadConnections([connection]);
        var root = explorer.FindConnectionNode(connection.Name)!;
        var vm = new MainWindowViewModel(schema, connections, query, new DefaultDataEditService(connections), explorer, new QueryEditorViewModel(query));
        vm.Connections.Add(connection);
        vm.SelectedConnection = connection;
        var tab = vm.OpenSqlInNewTab(connection.Name, "SELECT 1", "original");
        var ddl = new DatabaseDeletionChecks.DdlStub();
        Check(!await vm.CreateDatabaseAsync(root, "new_db", ddl) && schema.RefreshCalls == 0, "Failed creation must not change the tree.");
        ddl.Pending = new TaskCompletionSource<DdlExecuteResult>();
        var pending = vm.CreateDatabaseAsync(root, "new_db", ddl);
        Check(!await vm.CreateDatabaseAsync(root, "new_db", ddl) && ddl.CreateCalls == 2, "Pending creation must reject duplicate clicks.");
        ddl.Pending.SetResult(new DdlExecuteResult { IsSuccess = true });
        Check(await pending && root.Children.Any(n => n.Name == "new_db") && tab.AvailableDatabases.Contains("new_db"), "Successful creation must refresh the tree and database selectors.");
        Check(ddl.CreatedConnection == "server" && ddl.CreatedDatabase == "new_db" && vm.IsConnected && query.IsConnected("server"), "Creation must target its own connection and update connection state.");
        Check(tab.DatabaseName == "original" && tab.SqlText == "SELECT 1" && connection.Database == "original", "Creating a database must preserve query and profile targets.");
        schema.FailRefresh = true;
        ddl.Pending = null;
        ddl.Succeed = true;
        Check(await vm.CreateDatabaseAsync(root, "another_db", ddl) && vm.QueryEditor.StatusMessage.Contains("已创建，但刷新"), "A metadata refresh failure must not report a successful CREATE as failed.");
        Check(root.IsConnectionActive && query.IsConnected("server"), "A metadata refresh failure must retain the existing connection state.");
    }

    private static async Task VerifyBackupTargetAsync()
    {
        var saved = new ConnectionItem { Id = "saved-id", Name = "server", DatabaseType = "Postgres", Database = "default_db", Password = "not-for-display" };
        var root = new DbObjectTreeNode { Name = saved.Name, NodeType = DbObjectTreeNodeType.Connection, Connection = saved };
        var node = new DbObjectTreeNode { Name = "report_db", NodeType = DbObjectTreeNodeType.Database };
        root.AddChild(node);
        var target = node.GetConnectionSnapshot()!;
        Check(target.Database == "report_db" && saved.Database == "default_db" && !ReferenceEquals(target, saved), "Node tools must use a cloned target rather than the saved default database.");
        var connections = new QueryExecutionChecks.Connections(saved);
        var backup = new BackupFixture();
        var vm = new BackupViewModel(connections, backup, new DefaultTaskCenterService());
        vm.SetTargetConnection(target);
        target.Database = "mutated";
        vm.RefreshConnections();
        Check(vm.SelectedConnection!.Database == "report_db" && vm.Connections.Count == 1 && vm.CanBackup && vm.CanRestore,
            "Opening and refreshing the window must preserve its isolated target.");
        await vm.BackupCommand.ExecuteAsync(null);
        Check(backup.BackupTarget?.Database == "report_db", "The backup service must receive the right-click database.");
        var file = Path.GetTempFileName();
        try
        {
            vm.RestoreFilePath = file;
            await vm.RestoreCommand.ExecuteAsync(null);
            Check(backup.RestoreTarget?.Database == "report_db" && saved.Database == "default_db", "Restore must target the selected node without modifying saved profiles.");
        }
        finally { File.Delete(file); }
        foreach (var type in new[] { "Oracle", "DM", "Sqlite", "DuckDB" })
        {
            saved.DatabaseType = type;
            Check(node.GetConnectionSnapshot()!.Database == "default_db", "File paths and Oracle/DM service identifiers must not become tree node names.");
        }
        vm.SetTargetConnection(new ConnectionItem { DatabaseType = "Sqlite", Database = ":memory:", Password = saved.Password });
        Check(!vm.CanBackup && vm.CanRestore, "SQLite has a restore adapter but no backup adapter.");
        var properties = await new DatabasePropertiesService().GetAsync(vm.SelectedConnection!, "main");
        Check(properties.Any(p => p.Name == "数据源" && p.Value == ":memory:") && properties.Any(p => p.Name == "信息范围"), "File database properties must preserve their data source and state their scope.");
        Check(!properties.Any(p => p.Value == "not-for-display"), "Property values must not expose connection passwords.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private sealed class BackupFixture : IBackupService
    {
        public ConnectionItem? BackupTarget { get; private set; }
        public ConnectionItem? RestoreTarget { get; private set; }
        public Task<BackupResultItem> BackupAsync(ConnectionItem c, string folder, string? tool, bool zip, Action<string>? feedback = null, CancellationToken ct = default)
        {
            BackupTarget = c.Clone();
            return Task.FromResult(new BackupResultItem(true, "", "fixture.backup"));
        }
        public Task<BackupResultItem> RestoreAsync(ConnectionItem c, string path, string? tool, Action<string>? feedback = null, CancellationToken ct = default)
        {
            RestoreTarget = c.Clone();
            return Task.FromResult(new BackupResultItem(true, "", path));
        }
    }

    private sealed class SchemaFixture : IDbSchemaService
    {
        public int RefreshCalls { get; private set; }
        public bool FailRefresh { get; set; }
        public Task<IReadOnlyList<DbObjectTreeNode>> GetObjectTreeAsync(string c, CancellationToken ct = default)
        {
            RefreshCalls++;
            if (FailRefresh) throw new InvalidOperationException("fixture refresh failure");
            return Task.FromResult<IReadOnlyList<DbObjectTreeNode>>([new() { Name = "original", NodeType = DbObjectTreeNodeType.Database }, new() { Name = "new_db", NodeType = DbObjectTreeNodeType.Database }]);
        }
        public IReadOnlyList<string> GetSupportedDatabaseTypes() => ["Postgres"];
        public Task<IReadOnlyList<DbObjectTreeNode>> GetDatabaseChildrenAsync(string c, string d, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<DbObjectTreeNode>> GetDbObjectNodesAsync(string c, string d, DatabaseObjectType t, string? s = null, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool> HasMultipleSchemasAsync(string c, string d, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<DbObjectTreeNode>> GetSchemasAsync(string c, string d, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<DbObjectTreeNode>> GetTableChildNodesAsync(string c, string d, DbObjectChildType child, DatabaseObject o, bool view = false, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<SearchResultItem>> SearchMetadataAsync(string c, string keyword, int limit = 100, string? d = null, string? s = null, CancellationToken ct = default) => throw new NotSupportedException();
    }
}
