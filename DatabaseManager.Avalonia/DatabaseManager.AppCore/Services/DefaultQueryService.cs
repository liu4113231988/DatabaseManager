using System.Collections.Concurrent;
using System.Data.Common;
using System.Diagnostics;
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
    private readonly ConcurrentDictionary<string, TransactionContext> _transactions = new();

    /// <summary>对象浏览器已建立连接的集合（逻辑已连接状态）。</summary>
    private readonly HashSet<string> _connected = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _connectedLock = new();

    public DefaultQueryService(IDbConnectionService connectionService)
    {
        _connectionService = connectionService;
    }

    public async Task<QueryResult> ExecuteAsync(string connectionName, string sql, CancellationToken cancellationToken = default)
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
            return await ExecuteInTransactionAsync(ctx, interpreter, connectionName, sql, cancellationToken);
        }

        // 自动提交模式：直接执行。
        return await ExecuteAutoCommitAsync(connectionName, interpreter, sql, cancellationToken);
    }

    /// <summary>在活动事务上下文中执行 SQL（查询或非查询均在同一事务连接上执行）。</summary>
    private async Task<QueryResult> ExecuteInTransactionAsync(
        TransactionContext ctx,
        DbInterpreter interpreter,
        string connectionName,
        string sql,
        CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var dbConnection = ctx.Connection;
            var dbTransaction = ctx.Transaction;

            // 先尝试按查询执行（SELECT），获取结果集；失败则视为 DML/DDL，走事务内非查询执行。
            var dataTable = await interpreter.GetDataTableAsync(dbConnection, sql, ignoreSchema: true);

            sw.Stop();

            if (dataTable is not null && dataTable.Columns.Count > 0)
            {
                return QueryResult.FromDataTable(dataTable, sw.ElapsedMilliseconds);
            }

            // 无结果集：视为非查询语句（DML），在同一事务连接上执行。
            var result = await interpreter.ExecuteNonQueryAsync(
                new CommandInfo
                {
                    CommandText = sql,
                    Transaction = dbTransaction,
                    CancellationToken = cancellationToken,
                });

            if (result is not null && result.HasError)
            {
                return new QueryResult
                {
                    ErrorMessage = result.Message,
                    IsNonQuery = true,
                    RowCount = result.NumberOfRowsAffected,
                    ElapsedMilliseconds = sw.ElapsedMilliseconds,
                };
            }

            return new QueryResult
            {
                IsNonQuery = true,
                RowCount = result?.NumberOfRowsAffected ?? 0,
                ElapsedMilliseconds = sw.ElapsedMilliseconds,
            };
        }
        catch (Exception ex)
        {
            sw.Stop();
            return new QueryResult
            {
                ErrorMessage = ex.Message,
                ElapsedMilliseconds = sw.ElapsedMilliseconds,
            };
        }
    }

    /// <summary>自动提交模式下的单条 SQL 执行。</summary>
    private async Task<QueryResult> ExecuteAutoCommitAsync(
        string connectionName,
        DbInterpreter interpreter,
        string sql,
        CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();

        try
        {
            using var dbConnection = interpreter.CreateConnection();

            // 先尝试按查询执行，获取结果集。
            var dataTable = await interpreter.GetDataTableAsync(dbConnection, sql, ignoreSchema: true);

            sw.Stop();

            if (dataTable is null || dataTable.Columns.Count == 0)
            {
                // 无结果集：视为非查询语句。
                return new QueryResult
                {
                    IsNonQuery = true,
                    RowCount = dataTable?.Rows?.Count ?? 0,
                    ElapsedMilliseconds = sw.ElapsedMilliseconds,
                };
            }

            return QueryResult.FromDataTable(dataTable, sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            sw.Stop();
            return new QueryResult
            {
                ErrorMessage = ex.Message,
                ElapsedMilliseconds = sw.ElapsedMilliseconds,
            };
        }
    }

    public async Task<bool> BeginTransactionAsync(string connectionName, CancellationToken cancellationToken = default)
    {
        // 已有活动事务，直接返回 false。
        if (_transactions.ContainsKey(connectionName))
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

            _transactions[connectionName] = new TransactionContext
            {
                Connection = dbConnection,
                Transaction = dbTransaction,
            };

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

        var connectionInfo = new ConnectionInfo
        {
            Server = connection.Server,
            Port = connection.Port,
            ServerVersion = connection.ServerVersion,
            Database = connection.Database,
            IntegratedSecurity = connection.IntegratedSecurity,
            UserId = connection.UserId,
            Password = connection.Password,
            IsDba = connection.IsDba,
            UseSsl = connection.UseSsl,
        };

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

    /// <summary>单个连接的事务上下文（连接 + 事务）。</summary>
    private sealed class TransactionContext
    {
        public DbConnection Connection { get; set; } = null!;

        public DbTransaction Transaction { get; set; } = null!;
    }
}
