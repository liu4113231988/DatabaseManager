using DatabaseInterpreter.Model;
using DatabaseManager.AppCore.Models;
using DatabaseManager.AppCore.Services;
using DatabaseManager.AppCore.ViewModels;

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
        Console.WriteLine("Object prefetch checks passed.");
    }
    private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    private sealed class Schema : IDbSchemaService
    {
        public int ViewCalls;
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
        public Task<IReadOnlyList<DbObjectTreeNode>> GetObjectTreeAsync(string c, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<DbObjectTreeNode>> GetDatabaseChildrenAsync(string c, string d, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool> HasMultipleSchemasAsync(string c, string d, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<DbObjectTreeNode>> GetSchemasAsync(string c, string d, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<DbObjectTreeNode>> GetTableChildNodesAsync(string c, string d, DbObjectChildType child, DatabaseObject o, bool view = false, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<SearchResultItem>> SearchMetadataAsync(string c, string keyword, int limit = 100, string? d = null, string? s = null, CancellationToken ct = default) => throw new NotSupportedException();
    }
}
