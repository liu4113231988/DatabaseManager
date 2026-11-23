using DatabaseInterpreter.Model;
using DatabaseManager.AppCore.Models;
using DatabaseManager.AppCore.Services;
using DatabaseManager.AppCore.ViewModels;
using Microsoft.Data.Sqlite;

internal static class ObjectPrefetchChecks
{
    public static async Task RunAsync()
    {
        var schema = new Schema();
        var vm = new ObjectsExplorerViewModel(schema);
        var connection = new DbObjectTreeNode { Name = "server", NodeType = DbObjectTreeNodeType.Connection };
        var database = new DbObjectTreeNode { Name = "db", NodeType = DbObjectTreeNodeType.Database };
        connection.AddChild(database);
        var tables = new DbObjectTreeNode { NodeType = DbObjectTreeNodeType.Folder, DatabaseObjectType = DatabaseObjectType.Table };
        var views = new DbObjectTreeNode { NodeType = DbObjectTreeNodeType.Folder, DatabaseObjectType = DatabaseObjectType.View };
        database.AddChild(tables);
        database.AddChild(views);
        await vm.LoadFolderChildrenAsync(tables, "server");
        await schema.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var loading = vm.LoadFolderChildrenAsync(views, "server");
        Check(schema.ViewCalls == 1, "Expanding a prefetched folder must reuse the pending request.");
        schema.Pending.SetResult([new() { Name = "old" }]);
        await loading;
        Check(views.Children.Single().Name == "old", "Shared request must populate the folder.");

        views.IsLoaded = false;
        tables.IsLoaded = false;
        schema.Pending = new(TaskCreationOptions.RunContinuationsAsynchronously);
        schema.Started = new(TaskCreationOptions.RunContinuationsAsynchronously);
        await vm.LoadFolderChildrenAsync(tables, "server");
        await schema.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var oldRequest = schema.Pending;
        vm.Disconnect(connection);
        schema.Pending = new(TaskCreationOptions.RunContinuationsAsynchronously);
        schema.Started = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var refreshed = vm.LoadFolderChildrenAsync(views, "server");
        await schema.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        oldRequest.SetResult([new() { Name = "stale" }]);
        schema.Pending.SetResult([new() { Name = "fresh" }]);
        await refreshed;
        Check(schema.ViewCalls == 3 && views.Children.Single().Name == "fresh", "Old session requests must not supply refreshed metadata.");
        await VerifyPrincipalLoadingAsync(schema, vm);
        await VerifyAdminCatalogLoadingAsync(schema, vm);
        await VerifySqlServerCatalogsAsync();
        Console.WriteLine("Object prefetch checks passed.");
    }

    private static async Task VerifySqlServerCatalogsAsync()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            ATTACH DATABASE ':memory:' AS sys;
            CREATE TABLE sys.server_principals (name TEXT, type TEXT);
            CREATE TABLE sys.database_principals (name TEXT, type TEXT);
            INSERT INTO sys.server_principals VALUES ('service_login', 'S'), ('server_support', 'R'), ('domain_group', 'G');
            INSERT INTO sys.database_principals VALUES ('app_reader', 'S'), ('app_support', 'R'), ('app_session', 'A');
            """;
        await command.ExecuteNonQueryAsync();

        Check((await Read(DbPrincipalKind.Login, false)).SequenceEqual(new[] { "domain_group", "service_login" })
            && (await Read(DbPrincipalKind.Role, false)).SequenceEqual(new[] { "server_support" })
            && (await Read(DbPrincipalKind.User, true)).SequenceEqual(new[] { "app_reader" })
            && (await Read(DbPrincipalKind.Role, true)).SequenceEqual(new[] { "app_session", "app_support" }),
            "SQL Server login, user, server role and database role catalogs must preserve their scopes.");

        async Task<List<string>> Read(DbPrincipalKind kind, bool databaseScope)
        {
            command.CommandText = DbPrincipalCatalog.GetSql(DatabaseType.SqlServer, kind, databaseScope);
            await using var reader = await command.ExecuteReaderAsync();
            var names = new List<string>();
            while (await reader.ReadAsync())
                names.Add(reader.GetString(0));
            return names;
        }
    }

    private static async Task VerifyPrincipalLoadingAsync(Schema schema, ObjectsExplorerViewModel vm)
    {
        var profile = new ConnectionItem { Name = "fixture", DatabaseType = nameof(DatabaseType.SqlServer) };
        vm.LoadConnections([profile]);
        var connection = vm.FindConnectionNode(profile.Name)!;
        await vm.ConnectAsync(connection);
        var serverRole = connection.Children.Single(node => node.PrincipalKind == DbPrincipalKind.Role);
        Check(schema.PrincipalCalls == 0 && serverRole.Children.Single().IsPlaceholder, "Principal folders must not run a query at connect time.");

        var database = connection.Children.Single(node => node.NodeType == DbObjectTreeNodeType.Database);
        var databaseRole = DbPrincipalCatalog.CreateFolders(profile, database.Name).Single(node => node.PrincipalKind == DbPrincipalKind.Role);
        database.AddChild(databaseRole);
        schema.Principals = (_, _, _) => Task.FromException<IReadOnlyList<DbObjectTreeNode>>(new InvalidOperationException("access denied"));
        await vm.LoadPrincipalFolderAsync(serverRole);
        Check(!serverRole.IsLoaded && serverRole.Children.Single().Text.Contains("权限") && database.Children.Contains(databaseRole),
            "An unreadable catalog must leave the other database objects available and allow a retry.");

        schema.Principals = (_, db, _) => Task.FromResult<IReadOnlyList<DbObjectTreeNode>>(
            [DbPrincipalCatalog.CreatePrincipalNode(db ?? "server", null, db, DbPrincipalKind.Role)]);
        await vm.LoadPrincipalFolderAsync(serverRole);
        await vm.LoadPrincipalFolderAsync(databaseRole);
        await vm.LoadPrincipalFolderAsync(databaseRole);
        Check(serverRole.Children.Single().Name == "server" && databaseRole.Children.Single().Name == database.Name && schema.PrincipalCalls == 3,
            "Retry must succeed, the database scope must propagate, and expansion must cache each successful result.");

        var connections = new QueryExecutionChecks.Connections(profile);
        var query = new DefaultQueryService(connections);
        var main = new MainWindowViewModel(schema, connections, query, new DefaultDataEditService(connections), vm, new QueryEditorViewModel(query));
        schema.Principals = async (_, _, ct) =>
        {
            await Task.Delay(Timeout.Infinite, ct);
            return [];
        };
        var loading = main.RefreshNodeAsync(databaseRole);
        await main.RefreshNodeAsync(databaseRole);
        databaseRole.LoadCts!.Cancel();
        await loading.WaitAsync(TimeSpan.FromSeconds(5));
        Check(schema.PrincipalCalls == 4 && !databaseRole.IsLoading && !databaseRole.IsLoaded && databaseRole.Children.Single().Text.Contains("取消"),
            "Overlapping expansion must use one request and cancellation must allow another attempt.");

        schema.Principals = (_, _, _) => Task.FromResult<IReadOnlyList<DbObjectTreeNode>>([]);
        await main.RefreshNodeAsync(databaseRole.Children.Single());
        Check(databaseRole.IsLoaded && databaseRole.Children.Single().Text == "（空）", "A successful empty catalog must be distinct from a failed query.");

        var mysqlAccount = DbPrincipalCatalog.CreatePrincipalNode("o'neal", "localhost", null, DbPrincipalKind.Account);
        Check(mysqlAccount.Text == "'o''neal'@'localhost'" &&
            DbPrincipalCatalog.CreatePrincipalNode("", "localhost", null, DbPrincipalKind.Account).Text == "''@'localhost'",
            "MySQL accounts must retain both the host and anonymous account identity.");
        Check(!DbPrincipalCatalog.CreateFolders(new ConnectionItem { DatabaseType = "Sqlite" }).Any()
            && !DbPrincipalCatalog.CreateFolders(new ConnectionItem { DatabaseType = "KingbaseES", KingbaseCompatibilityMode = KingbaseCompatibilityModes.Oracle }).Any(),
            "Unsupported engines and compatibility modes must hide principal folders.");
    }

    private static async Task VerifyAdminCatalogLoadingAsync(Schema schema, ObjectsExplorerViewModel vm)
    {
        var profile = new ConnectionItem { Name = "admin", DatabaseType = nameof(DatabaseType.Postgres), Database = "postgres" };
        vm.LoadConnections([profile]);
        var connection = vm.FindConnectionNode(profile.Name)!;
        await vm.ConnectAsync(connection);
        var database = connection.Children.Single(node => node.NodeType == DbObjectTreeNodeType.Database);
        var folder = DbAdminCatalog.CreateFolders(profile, database.Name).Single(f => f.CatalogKind == DbCatalogKind.Extension);
        database.AddChild(folder);

        try
        {
            await DbAdminCatalog.ReadAsync(profile, database.Name, DbCatalogKind.Extension, new CancellationToken(true));
            throw new InvalidOperationException("A canceled directory query must propagate cancellation before connecting.");
        }
        catch (OperationCanceledException)
        {
        }

        schema.Catalog = async (_, _, ct) =>
        {
            await Task.Delay(Timeout.Infinite, ct);
            return [];
        };
        var pending = vm.LoadCatalogFolderAsync(folder);
        folder.LoadCts!.Cancel();
        await pending.WaitAsync(TimeSpan.FromSeconds(5));
        Check(!folder.IsLoaded && folder.Children.Single().Text.Contains("取消"), "Canceled catalogs must allow a retry and report cancellation.");

        schema.Catalog = (_, _, _) => Task.FromException<IReadOnlyList<DbObjectTreeNode>>(new InvalidOperationException("权限提示：pg_extension"));
        await vm.LoadCatalogFolderAsync(folder);
        Check(!folder.IsLoaded && folder.Children.Single().Text.Contains("权限提示：pg_extension"), "Failed catalogs must show the database-specific hint.");

        schema.Catalog = (kind, db, _) => Task.FromResult<IReadOnlyList<DbObjectTreeNode>>(
            kind == DbCatalogKind.Extension && db == database.Name ? [new() { Name = "pgcrypto" }] : []);
        await vm.LoadCatalogFolderAsync(folder);
        Check(folder.IsLoaded && folder.Children.Single().Name == "pgcrypto", "The extension catalog must query the expanded database.");
    }

    private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    private sealed class Schema : IDbSchemaService
    {
        public int ViewCalls;
        public int PrincipalCalls;
        public Func<DbPrincipalKind, string?, CancellationToken, Task<IReadOnlyList<DbObjectTreeNode>>> Principals { get; set; } =
            (_, _, _) => throw new NotSupportedException();
        public Func<DbCatalogKind, string?, CancellationToken, Task<IReadOnlyList<DbObjectTreeNode>>> Catalog { get; set; } =
            (_, _, _) => throw new NotSupportedException();
        public TaskCompletionSource<IReadOnlyList<DbObjectTreeNode>> Pending = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Started = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task<IReadOnlyList<DbObjectTreeNode>> GetDbObjectNodesAsync(string c, string d, DatabaseObjectType t, string? s = null, CancellationToken ct = default)
        {
            if (t != DatabaseObjectType.View) return Task.FromResult<IReadOnlyList<DbObjectTreeNode>>([]);
            Interlocked.Increment(ref ViewCalls);
            var task = Pending.Task;
            Started.TrySetResult();
            return task;
        }
        public IReadOnlyList<string> GetSupportedDatabaseTypes() => [];
        public Task<IReadOnlyList<DbObjectTreeNode>> GetObjectTreeAsync(string c, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<DbObjectTreeNode>>([new() { Name = "catalog", NodeType = DbObjectTreeNodeType.Database }]);
        public Task<IReadOnlyList<DbObjectTreeNode>> GetPrincipalNodesAsync(string c, DbPrincipalKind kind, string? d, CancellationToken ct = default)
        {
            PrincipalCalls++;
            return Principals(kind, d, ct);
        }
        public Task<IReadOnlyList<DbObjectTreeNode>> GetCatalogNodesAsync(string c, DbCatalogKind kind, string? d, CancellationToken ct = default)
            => Catalog(kind, d, ct);
        public Task<IReadOnlyList<DbObjectTreeNode>> GetDatabaseChildrenAsync(string c, string d, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool> HasMultipleSchemasAsync(string c, string d, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<DbObjectTreeNode>> GetSchemasAsync(string c, string d, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<DbObjectTreeNode>> GetTableChildNodesAsync(string c, string d, DbObjectChildType child, DatabaseObject o, bool view = false, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<SearchResultItem>> SearchMetadataAsync(string c, string keyword, int limit = 100, string? d = null, string? s = null, CancellationToken ct = default) => throw new NotSupportedException();
    }
}
