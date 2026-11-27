using DatabaseManager.AppCore.Models;
using DatabaseManager.AppCore.Services;
using DatabaseManager.AppCore.ViewModels;

/// <summary>验证属性面板查询执行使用应用设置中的命令超时。</summary>
internal static class QueryEditorTimeoutChecks
{
    public static async Task RunAsync()
    {
        var settings = new StubSettings();
        var query = new CapturingQueryService();
        var editor = new QueryEditorViewModel(query, settings)
        {
            ConnectionName = "fixture",
            SqlText = "SELECT 1",
        };

        settings.Settings.QueryCommandTimeoutSeconds = 0;
        await editor.ExecuteAsync();
        Check(query.CapturedTimeout == 0, "A zero timeout must pass through so the driver default is used.");

        settings.Settings.QueryCommandTimeoutSeconds = 120;
        await editor.ExecuteAsync();
        Check(query.CapturedTimeout == 120, "Configured command timeout must be forwarded to the query service.");

        var fallbackQuery = new CapturingQueryService();
        var fallbackEditor = new QueryEditorViewModel(fallbackQuery)
        {
            ConnectionName = "fixture",
            SqlText = "SELECT 1",
        };
        await fallbackEditor.ExecuteAsync();
        Check(fallbackQuery.CapturedTimeout == 60, "Without settings, the editor must keep the 60-second default.");

        Console.WriteLine("Query editor timeout checks passed: setting value and default forwarding.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private sealed class StubSettings : IAppSettingsService
    {
        public AppSettings Settings { get; set; } = new();
        public void Save() { }
    }

    private sealed class CapturingQueryService : IQueryService
    {
        public int CapturedTimeout { get; private set; } = -1;

        public Task<QueryResult> ExecuteAsync(string connectionName, string sql,
            CancellationToken cancellationToken = default, int commandTimeoutSeconds = 60, string? databaseName = null)
        {
            CapturedTimeout = commandTimeoutSeconds;
            return Task.FromResult(new QueryResult { ErrorMessage = "captured" });
        }

        public Task<QueryResult> ExecuteStandaloneAsync(ConnectionItem connection, string sql,
            CancellationToken cancellationToken = default, int commandTimeoutSeconds = 600)
            => Task.FromResult(new QueryResult());
        public Task<bool> BeginTransactionAsync(string connectionName, CancellationToken cancellationToken = default, string? databaseName = null)
            => Task.FromResult(false);
        public Task<bool> CommitAsync(string connectionName, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<bool> RollbackAsync(string connectionName, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public bool IsTransactionActive(string connectionName) => false;
        public string? GetTransactionDatabase(string connectionName) => null;
        public Task<bool> SetAutoCommitAsync(string connectionName, bool enabled, CancellationToken cancellationToken = default,
            bool commitActiveTransaction = true) => Task.FromResult(false);
        public bool IsAutoCommit(string connectionName) => true;
        public Task CloseConnectionAsync(string connectionName) => Task.CompletedTask;
        public bool IsConnected(string connectionName) => true;
        public void NotifyConnected(string connectionName) { }
    }
}
