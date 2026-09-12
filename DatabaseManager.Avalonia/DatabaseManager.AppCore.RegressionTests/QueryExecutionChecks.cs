using DatabaseManager.AppCore.Models;
using DatabaseManager.AppCore.Services;

internal static class QueryExecutionChecks
{
    public static async Task RunAsync()
    {
        var path = Path.Combine(Path.GetTempPath(), $"database-manager-query-{Guid.NewGuid():N}.db");
        var connection = new ConnectionItem { Name = "Regression", DatabaseType = "Sqlite", Database = path };
        var service = new DefaultQueryService(new Connections(connection));
        try
        {
            Check(!await service.BeginTransactionAsync(connection.Name), "Disconnected connections cannot begin transactions.");
            service.NotifyConnected(connection.Name);
            await Execute("CREATE TABLE items (id INTEGER PRIMARY KEY, value INTEGER)");
            var inserted = await Execute("INSERT INTO items VALUES (1, 10), (2, 20)");
            Check(inserted.IsNonQuery && inserted.RowCount == 2, "INSERT must report two affected rows.");
            Check(await service.BeginTransactionAsync(connection.Name), "Transaction should begin.");
            Check(service.IsTransactionActive("REGRESSION"), "Transaction names must be case insensitive.");
            await Execute("UPDATE items SET value = value + 1 WHERE id = 1");
            Check((await Execute("SELECT value FROM items WHERE id = 1")).Rows[0][0] == "11", "UPDATE must execute exactly once in its transaction.");
            var independent = await service.ExecuteStandaloneAsync(connection, "SELECT value FROM items WHERE id = 1");
            Check(independent.IsSuccess && independent.Rows[0][0] == "10", "Background SQL must not share uncommitted interactive changes.");
            Check(await service.RollbackAsync("REGRESSION"), "Rollback should succeed.");
            Check((await Execute("SELECT value FROM items WHERE id = 1")).Rows[0][0] == "10", "Rollback must restore original data.");
            Check(await service.BeginTransactionAsync(connection.Name), "Second transaction should begin.");
            await Execute("INSERT INTO items VALUES (3, 30)");
            Check(await service.CommitAsync(connection.Name), "Commit should succeed.");
            Check((await Execute("SELECT COUNT(*) FROM items")).Rows[0][0] == "3", "Committed insert must persist exactly once.");
            var batch = await Execute("SELECT value FROM items WHERE id = 1; UPDATE items SET value = 40 WHERE id = 1;");
            Check(batch.Rows[0][0] == "10", "Batch should preserve its first result.");
            var multiple = await Execute("SELECT 1 AS first; SELECT 2 AS second; SELECT 3 WHERE 0;");
            Check(multiple.ResultSets.Count == 3 && multiple.ResultSets[1].Rows[0][0] == "2" && multiple.ResultSets[2].Rows.Count == 0, "All result sets, including empty sets, must be retained.");
            var limited = await Execute("WITH RECURSIVE n(x) AS (VALUES(1) UNION ALL SELECT x+1 FROM n WHERE x<100005) SELECT x FROM n; UPDATE items SET value=40 WHERE id=1;");
            Check(limited.IsTruncated && limited.Rows.Count == 100000 && limited.WarningMessage is not null, "Large results must be bounded with a warning.");
            Check((await Execute("SELECT value FROM items WHERE id = 1")).Rows[0][0] == "40", "Statements after a result set must execute.");
            var failure = await service.ExecuteAsync(connection.Name, "SELECT 1; SELECT * FROM missing_table;");
            Check(!failure.IsSuccess, "Errors in later statements must not be hidden.");
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            Check(!(await service.ExecuteAsync(connection.Name, "DELETE FROM items", cancellation.Token)).IsSuccess, "Cancelled execution must fail.");
            Check((await Execute("SELECT COUNT(*) FROM items")).Rows[0][0] == "3", "Cancelled execution must not modify data.");
            Check(await service.BeginTransactionAsync(connection.Name), "Close test transaction should begin.");
            await Execute("DELETE FROM items");
            service.CloseConnection("REGRESSION");
            var standalone = await service.ExecuteStandaloneAsync(connection, "SELECT COUNT(*) FROM items");
            Check(standalone.IsSuccess && standalone.Rows[0][0] == "3", "Background SQL must work without an object-tree connection.");
            service.NotifyConnected(connection.Name);
            Check((await Execute("SELECT COUNT(*) FROM items")).Rows[0][0] == "3", "Disconnect must roll back pending changes.");
        }
        finally
        {
            service.CloseConnection(connection.Name);
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            File.Delete(path);
        }

        var tasks = new DefaultTaskCenterService();
        var first = tasks.Register("Keep running", "Regression");
        for (int i = 0; i < 100; i++) tasks.Register($"Running {i}", "Regression");
        Check(tasks.RunningCount == 101 && tasks.Runs.Contains(first), "Task retention must not hide running work.");
        tasks.Cancel(first.Id);
        Check(first.Cts.IsCancellationRequested, "Oldest running task must remain cancellable.");
        foreach (var task in tasks.Runs) task.Cts.Dispose();

        async Task<QueryResult> Execute(string sql)
        {
            var result = await service.ExecuteAsync(connection.Name, sql);
            Check(result.IsSuccess, result.ErrorMessage ?? sql);
            return result;
        }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    internal sealed class Connections(params ConnectionItem[] connections) : IDbConnectionService
    {
        public IReadOnlyList<ConnectionItem> GetConnections() => connections;
        public IReadOnlyList<ConnectionItem> GetConnections(string databaseType) => GetConnections();
        public ConnectionItem? GetConnectionById(string id) => connections.FirstOrDefault(c => c.Id == id);
        public Task<IReadOnlyList<string>> TestConnectionAsync(ConnectionItem item, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<string?> SaveAsync(ConnectionItem item, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> DeleteAsync(IEnumerable<string> ids, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> IsNameExistedAsync(bool isAdd, string? accountId, string name, string? id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
