using System.Collections.Concurrent;
using System.Data.Common;
using System.Diagnostics;
using System.Text.RegularExpressions;
using DatabaseInterpreter.Core;
using DatabaseInterpreter.Model;
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
        string connectionName,
        string sql,
        CancellationToken cancellationToken = default,
        int commandTimeoutSeconds = 60)
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

        var interpreter = CreateInterpreter(connection);

        // 若当前处于手动事务中，则在该事务连接上执行（使语句纳入同一事务）。
        if (_transactions.TryGetValue(connectionName, out var ctx) && ctx is not null)
        {
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

    public async Task<bool> BeginTransactionAsync(string connectionName, CancellationToken cancellationToken = default)
    {
        // 已有活动事务，直接返回 false。
        if (!IsConnected(connectionName) || _transactions.ContainsKey(connectionName))
            return false;

        var connection = FindConnection(connectionName);
        if (connection is null)
            return false;

        var interpreter = CreateInterpreter(connection);
        var dbConnection = interpreter.CreateConnection();

        try
        {
            await dbConnection.OpenAsync(cancellationToken);
            var dbTransaction = await dbConnection.BeginTransactionAsync(cancellationToken);

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

            return true;
        }
        catch
        {
            try { dbConnection.Dispose(); } catch { /* 忽略 */ }
            return false;
        }
    }

    public async Task<bool> CommitAsync(string connectionName, CancellationToken cancellationToken = default)
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
            try { ctx.Transaction?.Rollback(); } catch { /* 忽略 */ }
            return false;
        }
        finally
        {
            try { ctx.Connection?.Dispose(); } catch { /* 忽略 */ }
        }
    }

    public async Task<bool> RollbackAsync(string connectionName, CancellationToken cancellationToken = default)
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
            try { ctx.Connection?.Dispose(); } catch { /* 忽略 */ }
        }
    }

    public bool IsTransactionActive(string connectionName)
        => _transactions.ContainsKey(connectionName);

    public void SetAutoCommit(string connectionName, bool enabled)
    {
        if (enabled)
        {
            // 切回自动提交：若存在未提交事务，先提交。
            _ = CommitAsync(connectionName).GetAwaiter().GetResult();
        }
    }

    public bool IsAutoCommit(string connectionName)
        => !_transactions.ContainsKey(connectionName);

    public void CloseConnection(string connectionName)
    {
        lock (_connectedLock) _connected.Remove(connectionName);
        if (_transactions.TryRemove(connectionName, out var ctx))
        {
            try { ctx.Transaction?.Rollback(); } catch { /* 忽略 */ }
            try { ctx.Connection?.Dispose(); } catch { /* 忽略 */ }
        }
        SshTunnelManager.Close(connectionName);
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

    private DbInterpreter CreateInterpreter(ConnectionItem connection)
    {
        var dbType = ParseDatabaseType(connection.DatabaseType);

        var connectionInfo = ConnectionHelper.ToConnectionInfo(connection);

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
