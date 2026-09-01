using System.Data;
using DatabaseInterpreter.Core;
using DatabaseInterpreter.Model;
using DatabaseManager.AppCore.Models;

namespace DatabaseManager.AppCore.Services;

/// <summary>
/// 会话与锁监控实现（方言 SQL）：
/// - MySQL: information_schema.processlist / innodb_lock_waits，KILL {id}；
/// - PostgreSQL: pg_stat_activity / pg_blocking_pids，pg_terminate_backend；
/// - SQL Server: sys.dm_exec_sessions + dm_exec_requests，KILL {id}；
/// - Oracle: v$session，ALTER SYSTEM KILL SESSION。
/// </summary>
public class DefaultDbSessionService : IDbSessionService
{
    public bool IsSupported(string databaseType)
        => ParseDatabaseType(databaseType) != DatabaseType.Unknown
           && ParseDatabaseType(databaseType) != DatabaseType.Sqlite;

    public async Task<DbSessionSnapshot> GetSnapshotAsync(ConnectionItem connection, CancellationToken cancellationToken = default)
    {
        var snapshot = new DbSessionSnapshot();
        var dbType = ParseDatabaseType(connection.DatabaseType);

        if (dbType == DatabaseType.KingbaseES &&
            KingbaseCompatibilityModes.GetConnectionBlockReason(connection.KingbaseCompatibilityMode) is { } compatibilityReason)
        {
            snapshot.Error = compatibilityReason;
            return snapshot;
        }

        if (!IsSupported(connection.DatabaseType))
        {
            snapshot.Error = "该数据库类型不支持会话监控（SQLite 无服务端会话概念）。";
            return snapshot;
        }

        try
        {
            var interpreter = CreateInterpreter(connection);
            await using var conn = interpreter.CreateConnection();
            await conn.OpenAsync(cancellationToken);

            // 会话列表。
            var sessionsSql = DbSessionSql.BuildSessionsSql(dbType);

            if (sessionsSql.Length > 0)
            {
                var table = await ExecuteQueryAsync(conn, sessionsSql, 15, cancellationToken);
                foreach (var row in table.Rows.Cast<DataRow>())
                {
                    snapshot.Sessions.Add(new DbSessionInfo
                    {
                        SessionId = row[0]?.ToString() ?? string.Empty,
                        User = row[1]?.ToString() ?? string.Empty,
                        Client = row[2]?.ToString() ?? string.Empty,
                        Database = row[3]?.ToString() ?? string.Empty,
                        State = row[4]?.ToString() ?? string.Empty,
                        WaitInfo = row[5]?.ToString() ?? string.Empty,
                        Duration = row[6]?.ToString() ?? string.Empty,
                        CurrentSql = row[7]?.ToString() ?? string.Empty,
                    });
                }
            }

            // 锁/阻塞（失败不影响会话列表）。
            try
            {
                var locksSql = DbSessionSql.BuildLocksSql(dbType);

                if (locksSql.Length > 0)
                {
                    var lockTable = await ExecuteQueryAsync(conn, locksSql, 10, cancellationToken);
                    foreach (var row in lockTable.Rows.Cast<DataRow>())
                    {
                        snapshot.Locks.Add(new DbLockInfo
                        {
                            BlockedSession = row[0]?.ToString() ?? string.Empty,
                            BlockingSession = row[1]?.ToString() ?? string.Empty,
                            WaitResource = row[2]?.ToString() ?? string.Empty,
                            WaitTime = row[3]?.ToString() ?? string.Empty,
                        });
                    }
                }
            }
            catch (Exception lockEx)
            {
                snapshot.Error = $"锁信息读取失败（不影响会话列表）：{lockEx.Message}";
            }
        }
        catch (Exception ex)
        {
            snapshot.Error = $"会话读取失败：{ex.Message}{Environment.NewLine}权限提示：{DbAdminGuidance.GetSessionPermissionHint(dbType)}";
        }

        return snapshot;
    }

    public async Task<(bool Success, string? Error)> KillSessionAsync(ConnectionItem connection, string sessionId, CancellationToken cancellationToken = default)
    {
        var dbType = ParseDatabaseType(connection.DatabaseType);

        if (dbType == DatabaseType.KingbaseES &&
            KingbaseCompatibilityModes.GetConnectionBlockReason(connection.KingbaseCompatibilityMode) is { } compatibilityReason)
        {
            return (false, compatibilityReason);
        }

        string? sql = dbType == DatabaseType.Oracle
            ? System.Text.RegularExpressions.Regex.IsMatch(sessionId, @"^[\w,]+$")
                ? $"ALTER SYSTEM KILL SESSION '{sessionId}' IMMEDIATE"
                : null
            : DbSessionSql.BuildTerminateSessionSql(dbType, sessionId);

        if (sql is null)
        {
            return (false, "会话标识无效或数据库类型不支持。");
        }

        try
        {
            var interpreter = CreateInterpreter(connection);
            await using var conn = interpreter.CreateConnection();
            await conn.OpenAsync(cancellationToken);

            await using var cmd = conn.CreateCommand();
            cmd.CommandTimeout = 15;
            cmd.CommandText = sql;
            await cmd.ExecuteNonQueryAsync(cancellationToken);
            return (true, null);
        }
        catch (Exception ex)
        {
            return (false, $"{ex.Message}{Environment.NewLine}权限提示：{DbAdminGuidance.GetSessionPermissionHint(dbType)}");
        }
    }

    private static async Task<DataTable> ExecuteQueryAsync(
        System.Data.Common.DbConnection conn, string sql, int timeoutSeconds, CancellationToken ct)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandTimeout = Math.Clamp(timeoutSeconds, 1, 120);
        cmd.CommandText = sql;

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var table = new DataTable();
        table.Load(reader);
        return table;
    }

    private static DbInterpreter CreateInterpreter(ConnectionItem connection)
    {
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

        return DbInterpreterHelper.GetDbInterpreter(
            ParseDatabaseType(connection.DatabaseType), connectionInfo, new DbInterpreterOption());
    }

    private static DatabaseType ParseDatabaseType(string databaseType)
        => Enum.TryParse<DatabaseType>(databaseType, true, out var type) ? type : DatabaseType.Unknown;
}
