using System.Collections.Concurrent;
using System.Data.Common;
using System.Diagnostics;
using System.Text.RegularExpressions;
using DatabaseInterpreter.Core;
using DatabaseInterpreter.Model;
using DatabaseManager.AppCore.Common;
using DatabaseManager.AppCore.Models;

namespace DatabaseManager.AppCore.Services;

/// <summary>
/// 查询服务实现。阶段 2/3：接入 <see cref="DbInterpreter"/> 真正执行 SQL 并返回结果集，
/// 并支持事务生命周期（Commit / Rollback / Auto-commit）。
/// </summary>
public class DefaultQueryService : IQueryService
{
    private readonly IDbConnectionService _connectionService;

    /// <summary>每个连接名对应的活动事务状态（连接名 → 事务上下文）。</summary>
    private readonly ConcurrentDictionary<string, TransactionContext> _transactions = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, byte> _manualCommit = new(StringComparer.OrdinalIgnoreCase);
    // 锁保留至服务结束，避免等待中的操作与新操作取得不同的锁。
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _connectionLocks = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>对象浏览器已建立连接的集合（逻辑已连接状态）。</summary>
    private readonly HashSet<string> _connected = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _connectedLock = new();

    public DefaultQueryService(IDbConnectionService connectionService)
    {
        _connectionService = connectionService;
    }

    /// <summary>后台脚本使用独立自动提交连接，不复用交互查询的事务或连接状态。</summary>
    public Task<QueryResult> ExecuteStandaloneAsync(ConnectionItem connection, string sql,
        CancellationToken cancellationToken = default, int commandTimeoutSeconds = 600)
    {
        if (string.IsNullOrWhiteSpace(sql))
            return Task.FromResult(new QueryResult { ErrorMessage = "SQL 语句不能为空。" });
        return ExecuteAutoCommitAsync(connection.Name, CreateInterpreter(connection), sql,
            cancellationToken, commandTimeoutSeconds);
    }

    public async Task<QueryResult> ExecuteAsync(
        string connectionName, string sql, CancellationToken cancellationToken = default,
        int commandTimeoutSeconds = 60, string? databaseName = null)
    {
        try
        {
            return await InConnectionAsync(connectionName,
                () => ExecuteCoreAsync(connectionName, sql, cancellationToken, commandTimeoutSeconds, databaseName), cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return new QueryResult { ErrorMessage = "查询已取消。" };
        }
    }

    private async Task<QueryResult> ExecuteCoreAsync(
        string connectionName,
        string sql,
        CancellationToken cancellationToken = default,
        int commandTimeoutSeconds = 60,
        string? databaseName = null)
    {
        var connection = FindConnection(connectionName);
        if (connection is null)
        {
            return new QueryResult
            {
                ErrorMessage = $"未找到连接 '{connectionName}'。",
            };
        }

        if (string.IsNullOrWhiteSpace(sql))
        {
            return new QueryResult
            {
                ErrorMessage = "SQL 语句不能为空。",
            };
        }

        if (!IsConnected(connectionName))
        {
            return new QueryResult
            {
                ErrorMessage = $"连接 '{connectionName}' 已断开，请先在对象浏览器中重新连接后再执行。",
            };
        }

        var interpreter = CreateInterpreter(connection, databaseName);

        if (!IsAutoCommit(connectionName) && !_transactions.ContainsKey(connectionName)
            && !await BeginTransactionCoreAsync(connectionName, cancellationToken, databaseName))
            return new QueryResult { ErrorMessage = "无法开启手动事务，请检查连接。" };

        // 若当前处于手动事务中，则在该事务连接上执行（使语句纳入同一事务）。
        if (_transactions.TryGetValue(connectionName, out var ctx) && ctx is not null)
        {
            var targetDatabase = string.IsNullOrWhiteSpace(databaseName) ? connection.Database : databaseName;
            if (connection.DatabaseType is DatabaseTypeNames.SqlServer or DatabaseTypeNames.MySql or DatabaseTypeNames.Postgres or DatabaseTypeNames.KingbaseES
                && !string.IsNullOrWhiteSpace(targetDatabase)
                && !string.Equals(ctx.Connection.Database, targetDatabase, StringComparison.Ordinal))
                return new QueryResult { ErrorMessage = "当前事务属于其他数据库，请先提交或回滚事务后再执行。" };
            return await ExecuteInTransactionAsync(ctx, interpreter, connectionName, sql, cancellationToken, commandTimeoutSeconds);
        }

        // 自动提交模式：直接执行。
        return await ExecuteAutoCommitAsync(connectionName, interpreter, sql, cancellationToken, commandTimeoutSeconds);
    }

    /// <summary>在活动事务上下文中执行 SQL（查询或非查询均在同一事务连接上执行）。</summary>
    private async Task<QueryResult> ExecuteInTransactionAsync(
        TransactionContext ctx,
        DbInterpreter interpreter,
        string connectionName,
        string sql,
        CancellationToken cancellationToken,
        int commandTimeoutSeconds)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            return await ExecuteCommandAsync(ctx.Connection, ctx.Transaction, sql,
                cancellationToken, commandTimeoutSeconds, sw);
        }
        catch (OperationCanceledException)
        {
            sw.Stop();
            return new QueryResult { ErrorMessage = "查询已取消。", ElapsedMilliseconds = sw.ElapsedMilliseconds };
        }
        catch (Exception ex)
        {
            sw.Stop();
            return CreateErrorResult(ex.Message, sw.ElapsedMilliseconds);
        }
    }

    /// <summary>自动提交模式下的单条 SQL 执行。</summary>
    private async Task<QueryResult> ExecuteAutoCommitAsync(
        string connectionName,
        DbInterpreter interpreter,
        string sql,
        CancellationToken cancellationToken,
        int commandTimeoutSeconds)
    {
        var sw = Stopwatch.StartNew();

        try
        {
            using var dbConnection = interpreter.CreateConnection();

            await dbConnection.OpenAsync(cancellationToken);
            return await ExecuteCommandAsync(dbConnection, null, sql,
                cancellationToken, commandTimeoutSeconds, sw);
        }
        catch (OperationCanceledException)
        {
            sw.Stop();
            return new QueryResult { ErrorMessage = "查询已取消。", ElapsedMilliseconds = sw.ElapsedMilliseconds };
        }
        catch (Exception ex)
        {
            sw.Stop();
            return CreateErrorResult(ex.Message, sw.ElapsedMilliseconds);
        }
    }

    // ExecuteReader also executes DML/DDL. Never retry a command just because it has no columns.
    private static async Task<QueryResult> ExecuteCommandAsync(
        DbConnection connection, DbTransaction? transaction, string sql,
        CancellationToken cancellationToken, int timeoutSeconds, Stopwatch stopwatch)
    {
        if (connection is Oracle.ManagedDataAccess.Client.OracleConnection)
            DatabaseInterpreter.Geometry.GeometryUtility.Hook();

        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Transaction = transaction;
        command.CommandTimeout = timeoutSeconds > 0 ? timeoutSeconds : DbInterpreter.Setting.CommandTimeout;
        using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var sets = new List<QueryResult>();
        long retainedCharacters = 0;
        int retainedRows = 0;
        bool truncated = false;
        const int maxRows = 100000;
        const long maxCharacters = 16 * 1024 * 1024;
        do
        {
            bool capture = reader.FieldCount > 0 && sets.Count < 64;
            var columns = capture ? Enumerable.Range(0, reader.FieldCount).Select(reader.GetName).ToList() : new List<string>();
            var rows = new List<IReadOnlyList<string>>();
            bool setTruncated = reader.FieldCount > 0 && !capture;
            while (await reader.ReadAsync(cancellationToken))
            {
                if (!capture) continue;
                if (retainedRows >= maxRows || retainedCharacters >= maxCharacters)
                {
                    setTruncated = true;
                    continue;
                }
                var values = new string[reader.FieldCount];
                for (int i = 0; i < values.Length; i++)
                    values[i] = reader.IsDBNull(i) ? string.Empty : reader.GetValue(i) is byte[] bytes ? "0x" + Convert.ToHexString(bytes) : reader.GetValue(i).ToString() ?? string.Empty;
                long size = values.Sum(v => (long)v.Length + 24);
                if (size > maxCharacters - retainedCharacters)
                {
                    setTruncated = true;
                    continue;
                }
                retainedCharacters += size;
                retainedRows++;
                rows.Add(values);
            }
            truncated |= setTruncated;
            if (capture) sets.Add(new QueryResult { Columns = columns, Rows = rows, RowCount = rows.Count, IsTruncated = setTruncated });
        } while (await reader.NextResultAsync(cancellationToken));

        stopwatch.Stop();
        return new QueryResult
        {
            Columns = sets.FirstOrDefault()?.Columns ?? Array.Empty<string>(),
            Rows = sets.FirstOrDefault()?.Rows ?? Array.Empty<IReadOnlyList<string>>(),
            ResultSets = sets,
            IsTruncated = truncated,
            WarningMessage = truncated ? "结果已截断：单次最多保留 64 个结果集、100000 行和约 32 MiB 文本；后续语句仍已执行。" : null,
            IsNonQuery = sets.Count == 0,
            RowCount = sets.Count == 0 ? Math.Max(0, reader.RecordsAffected) : sets[0].RowCount,
            ElapsedMilliseconds = stopwatch.ElapsedMilliseconds,
        };
    }

    public Task<bool> BeginTransactionAsync(string connectionName, CancellationToken cancellationToken = default, string? databaseName = null)
        => InConnectionAsync(connectionName, () => BeginTransactionCoreAsync(connectionName, cancellationToken, databaseName), cancellationToken);

    private async Task<bool> BeginTransactionCoreAsync(string connectionName, CancellationToken cancellationToken, string? databaseName)
    {
        // 已有活动事务，直接返回 false。
        if (!IsConnected(connectionName) || _transactions.ContainsKey(connectionName))
            return false;

        var connection = FindConnection(connectionName);
        if (connection is null)
            return false;

        var interpreter = CreateInterpreter(connection, databaseName);
        var dbConnection = interpreter.CreateConnection();

        try
        {
            await dbConnection.OpenAsync(cancellationToken);
            var dbTransaction = await dbConnection.BeginTransactionAsync(cancellationToken);

            if (!IsConnected(connectionName))
            {
                await dbTransaction.DisposeAsync();
                await dbConnection.DisposeAsync();
                return false;
            }

            var context = new TransactionContext
            {
                Connection = dbConnection,
                Transaction = dbTransaction,
            };

            if (!_transactions.TryAdd(connectionName, context))
            {
                await dbTransaction.DisposeAsync();
                await dbConnection.DisposeAsync();
                return false;
            }

            _manualCommit[connectionName] = 0;
            return true;
        }
        catch
        {
            try { await dbConnection.DisposeAsync(); } catch { /* 忽略 */ }
            return false;
        }
    }

    public Task<bool> CommitAsync(string connectionName, CancellationToken cancellationToken = default)
        => InConnectionAsync(connectionName, () => CommitCoreAsync(connectionName, cancellationToken), cancellationToken);

    private async Task<bool> CommitCoreAsync(string connectionName, CancellationToken cancellationToken)
    {
        if (!_transactions.TryRemove(connectionName, out var ctx) || ctx is null)
            return false;

        try
        {
            if (ctx.Transaction?.Connection is not null)
            {
                await ctx.Transaction.CommitAsync(cancellationToken);
            }

            return true;
        }
        catch
        {
            // 提交失败则尝试回滚。
            try { await ctx.Transaction.RollbackAsync(CancellationToken.None); } catch { /* 忽略 */ }
            return false;
        }
        finally
        {
            try { await ctx.Transaction.DisposeAsync(); } catch { /* 忽略 */ }
            try { await ctx.Connection.DisposeAsync(); } catch { /* 忽略 */ }
        }
    }

    public Task<bool> RollbackAsync(string connectionName, CancellationToken cancellationToken = default)
        => InConnectionAsync(connectionName, () => RollbackCoreAsync(connectionName, cancellationToken), cancellationToken);

    private async Task<bool> RollbackCoreAsync(string connectionName, CancellationToken cancellationToken)
    {
        if (!_transactions.TryRemove(connectionName, out var ctx) || ctx is null)
            return false;

        try
        {
            if (ctx.Transaction?.Connection is not null)
            {
                await ctx.Transaction.RollbackAsync(cancellationToken);
            }

            return true;
        }
        catch
        {
            return false;
        }
        finally
        {
            try { await ctx.Transaction.DisposeAsync(); } catch { /* 忽略 */ }
            try { await ctx.Connection.DisposeAsync(); } catch { /* 忽略 */ }
        }
    }

    public bool IsTransactionActive(string connectionName)
        => _transactions.ContainsKey(connectionName);

    public Task<bool> SetAutoCommitAsync(string connectionName, bool enabled, CancellationToken cancellationToken = default)
        => InConnectionAsync(connectionName, async () =>
        {
            if (!IsConnected(connectionName)) return false;
            if (!enabled)
            {
                _manualCommit[connectionName] = 0;
                return true;
            }
            if (_transactions.ContainsKey(connectionName) && !await CommitCoreAsync(connectionName, cancellationToken))
                return false;
            _manualCommit.TryRemove(connectionName, out _);
            return true;
        }, cancellationToken);

    public bool IsAutoCommit(string connectionName)
        => !_manualCommit.ContainsKey(connectionName);

    public async Task CloseConnectionAsync(string connectionName)
    {
        lock (_connectedLock) _connected.Remove(connectionName);
        await InConnectionAsync(connectionName, async () =>
        {
            if (_transactions.ContainsKey(connectionName)) await RollbackCoreAsync(connectionName, CancellationToken.None);
            _manualCommit.TryRemove(connectionName, out _);
            SshTunnelManager.Close(connectionName);
            return true;
        }, CancellationToken.None);
    }

    private async Task<T> InConnectionAsync<T>(string connectionName, Func<Task<T>> action, CancellationToken cancellationToken)
    {
        var gate = _connectionLocks.GetOrAdd(connectionName, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        try { return await action(); }
        finally { gate.Release(); }
    }

    public bool IsConnected(string connectionName)
    {
        if (string.IsNullOrWhiteSpace(connectionName)) return false;
        lock (_connectedLock) return _connected.Contains(connectionName);
    }

    public void NotifyConnected(string connectionName)
    {
        if (string.IsNullOrWhiteSpace(connectionName)) return;
        lock (_connectedLock) _connected.Add(connectionName);
    }

    private DbInterpreter CreateInterpreter(ConnectionItem connection, string? databaseName = null)
    {
        var dbType = ParseDatabaseType(connection.DatabaseType);

        var connectionInfo = ConnectionHelper.ToConnectionInfo(connection);

        // 查询标签带有的数据库上下文（对象树选中的库）优先于连接配置的默认库；
        // Oracle 覆盖 Database 会破坏服务名连接串，SQLite 的 Database 是文件路径，二者不覆盖。
        if (!string.IsNullOrWhiteSpace(databaseName)
            && dbType is not (DatabaseType.Oracle or DatabaseType.Sqlite))
        {
            connectionInfo.Database = databaseName;
        }

        var option = new DbInterpreterOption
        {
            ThrowExceptionWhenErrorOccurs = false,
        };

        return DbInterpreterHelper.GetDbInterpreter(dbType, connectionInfo, option);
    }

    private ConnectionItem? FindConnection(string connectionName)
        => _connectionService.GetConnections().FirstOrDefault(c =>
            string.Equals(c.Name, connectionName, StringComparison.OrdinalIgnoreCase));

    private static DatabaseType ParseDatabaseType(string databaseType)
    {
        if (Enum.TryParse<DatabaseType>(databaseType, true, out var type))
            return type;
        return DatabaseType.Unknown;
    }

    private static QueryResult CreateErrorResult(string message, long elapsedMilliseconds)
    {
        // 常见驱动错误：PostgreSQL "LINE 3:"、MySQL "at line 3"、SQL Server "Line 3"。
        var match = Regex.Match(message ?? string.Empty, @"\b(?:LINE|Line|line)\s*(?<line>\d+)\b", RegexOptions.CultureInvariant);
        return new QueryResult
        {
            ErrorMessage = message,
            ElapsedMilliseconds = elapsedMilliseconds,
            ErrorLine = match.Success && int.TryParse(match.Groups["line"].Value, out var line) ? line : null,
        };
    }

    /// <summary>单个连接的事务上下文（连接 + 事务）。</summary>
    private sealed class TransactionContext
    {
        public DbConnection Connection { get; set; } = null!;

        public DbTransaction Transaction { get; set; } = null!;
    }
}
