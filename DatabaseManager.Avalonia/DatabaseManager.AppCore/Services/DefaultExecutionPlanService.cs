using System.Data;
using System.Diagnostics;
using DatabaseInterpreter.Core;
using DatabaseInterpreter.Model;
using DatabaseManager.AppCore.Models;

namespace DatabaseManager.AppCore.Services;

/// <summary>
/// 执行计划服务实现：
/// - MySQL / PostgreSQL / SQLite：EXPLAIN（可选 ANALYZE）前缀，直接读取计划结果集；
/// - SQL Server：SET SHOWPLAN_ALL ON → 查询 → SET SHOWPLAN_ALL OFF（同一连接）；
/// - Oracle：EXPLAIN PLAN FOR → 查询 DBMS_XPLAN.DISPLAY。
/// </summary>
public class DefaultExecutionPlanService : IExecutionPlanService
{
    public async Task<QueryResult> ExplainAsync(
        ConnectionItem connection,
        string sql,
        bool analyze = false,
        CancellationToken cancellationToken = default)
    {
        var dbType = ParseDatabaseType(connection.DatabaseType);
        if (dbType == DatabaseType.Unknown)
        {
            return ErrorResult("连接的数据库类型无效。");
        }

        if (string.IsNullOrWhiteSpace(sql))
        {
            return ErrorResult("请先输入要分析的 SQL。");
        }

        var statement = sql.Trim().TrimEnd(';').Trim();

        try
        {
            var interpreter = CreateInterpreter(connection);
            var sw = Stopwatch.StartNew();

            await using var conn = interpreter.CreateConnection();
            await conn.OpenAsync(cancellationToken);

            switch (dbType)
            {
                case DatabaseType.MySql:
                    return await ReadPlanAsync(conn, $"{(analyze ? "EXPLAIN ANALYZE " : "EXPLAIN ")}{statement}", sw, 60, cancellationToken);

                case DatabaseType.Postgres:
                    return await ReadPlanAsync(conn, $"EXPLAIN {(analyze ? "ANALYZE " : string.Empty)}{statement}", sw, 60, cancellationToken);

                case DatabaseType.Sqlite:
                    // SQLite 的 EXPLAIN ANALYZE 输出面向引擎行，这里统一用 QUERY PLAN。
                    return await ReadPlanAsync(conn, $"EXPLAIN QUERY PLAN {statement}", sw, 60, cancellationToken);

                case DatabaseType.SqlServer:
                {
                    await ExecuteAsync(conn, "SET SHOWPLAN_ALL ON", cancellationToken);
                    var result = await ReadPlanAsync(conn, statement, sw, 60, cancellationToken);
                    await ExecuteAsync(conn, "SET SHOWPLAN_ALL OFF", cancellationToken);
                    return result;
                }

                case DatabaseType.Oracle:
                    await ExecuteAsync(conn, $"EXPLAIN PLAN FOR {statement}", cancellationToken);
                    return await ReadPlanAsync(conn, "SELECT * FROM TABLE(DBMS_XPLAN.DISPLAY)", sw, 60, cancellationToken);

                default:
                    return ErrorResult($"暂不支持该数据库类型的执行计划分析：{dbType}。");
            }
        }
        catch (Exception ex)
        {
            return ErrorResult($"获取执行计划失败：{ex.Message}");
        }
    }

    private static async Task<QueryResult> ReadPlanAsync(
        System.Data.Common.DbConnection connection,
        string statement,
        Stopwatch sw,
        int commandTimeoutSeconds,
        CancellationToken cancellationToken)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandTimeout = commandTimeoutSeconds;
        cmd.CommandText = statement;

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        var table = new DataTable();
        table.Load(reader);

        sw.Stop();
        return QueryResult.FromDataTable(table, sw.ElapsedMilliseconds);
    }

    private static async Task ExecuteAsync(
        System.Data.Common.DbConnection connection,
        string statement,
        CancellationToken cancellationToken)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandTimeout = 60;
        cmd.CommandText = statement;
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    private static QueryResult ErrorResult(string message)
        => new() { ErrorMessage = message };

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

        var option = new DbInterpreterOption
        {
            ObjectFetchMode = DatabaseObjectFetchMode.Simple,
        };

        return DbInterpreterHelper.GetDbInterpreter(
            Enum.TryParse<DatabaseType>(connection.DatabaseType, true, out var t) ? t : DatabaseType.Unknown,
            connectionInfo, option);
    }

    private static DatabaseType ParseDatabaseType(string databaseType)
        => Enum.TryParse<DatabaseType>(databaseType, true, out var type) ? type : DatabaseType.Unknown;
}
