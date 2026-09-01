using System.Data;
using System.Diagnostics;
using System.Text;
using DatabaseInterpreter.Core;
using DatabaseInterpreter.Model;
using DatabaseManager.AppCore.Models;

namespace DatabaseManager.AppCore.Services;

/// <summary>
/// 查询性能剖析实现：单连接复用 + 分阶段（执行/取数）计时；
/// MySQL/PostgreSQL 额外执行一次 EXPLAIN ANALYZE 采集服务端实际耗时文本。
/// </summary>
public class DefaultQueryProfilerService : IQueryProfilerService
{
    public bool SupportsAnalyze(string databaseType)
    {
        var dbType = ParseDatabaseType(databaseType);
        return QueryProfilerSql.SupportsAnalyze(dbType);
    }

    public async Task<QueryProfileResult> ProfileAsync(
        ConnectionItem connection,
        string sql,
        int runs,
        bool includeAnalyze,
        CancellationToken cancellationToken = default)
    {
        var result = new QueryProfileResult();

        if (string.IsNullOrWhiteSpace(sql))
        {
            result.Error = "SQL 不能为空。";
            return result;
        }

        runs = Math.Clamp(runs, 1, 50);
        var dbType = ParseDatabaseType(connection.DatabaseType);
        if (dbType == DatabaseType.Unknown)
        {
            result.Error = $"不支持的数据库类型：{connection.DatabaseType}。";
            return result;
        }

        var statement = sql.Trim().TrimEnd(';').Trim();
        var safetyError = SqlSafety.ValidateProfilerStatement(statement);
        if (safetyError is not null)
        {
            result.Error = safetyError;
            return result;
        }

        try
        {
            var interpreter = CreateInterpreter(connection);
            await using var conn = interpreter.CreateConnection();

            var openSw = Stopwatch.StartNew();
            await conn.OpenAsync(cancellationToken);
            openSw.Stop();
            result.OpenMs = openSw.ElapsedMilliseconds;

            for (int i = 1; i <= runs; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var stat = new QueryProfileRunStat { Index = i };
                result.Runs.Add(stat);

                try
                {
                    await using var cmd = conn.CreateCommand();
                    cmd.CommandTimeout = 120;
                    cmd.CommandText = statement;

                    var execSw = Stopwatch.StartNew();
                    await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
                    execSw.Stop();
                    stat.ExecuteMs = execSw.ElapsedMilliseconds;

                    var fetchSw = Stopwatch.StartNew();
                    int rows = 0;
                    while (await reader.ReadAsync(cancellationToken))
                    {
                        rows++;
                    }

                    fetchSw.Stop();
                    stat.FetchMs = fetchSw.ElapsedMilliseconds;
                    stat.Rows = rows;
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    stat.Error = ex.Message;
                }
            }

            // 服务端真实耗时（EXPLAIN ANALYZE）。
            if (includeAnalyze && SupportsAnalyze(connection.DatabaseType))
            {
                try
                {
                    string analyzeSql = QueryProfilerSql.BuildAnalyzeSql(dbType, statement);

                    await using var cmd = conn.CreateCommand();
                    cmd.CommandTimeout = 120;
                    cmd.CommandText = analyzeSql;

                    await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
                    var text = new StringBuilder();

                    do
                    {
                        while (await reader.ReadAsync(cancellationToken))
                        {
                            var cells = Enumerable.Range(0, reader.FieldCount)
                                .Select(i => reader.IsDBNull(i) ? string.Empty : reader.GetValue(i)?.ToString() ?? string.Empty);
                            text.AppendLine(string.Join(" | ", cells));
                        }
                    }
                    while (await reader.NextResultAsync(cancellationToken));

                    result.AnalyzeText = text.Length > 0
                        ? text.ToString()
                        : "（服务端未返回可展示的计划文本；SQL Server 请确认驱动支持 STATISTICS XML，Oracle 请确认 DBMS_XPLAN 与 V$SQL 权限。）";
                }
                catch (Exception ex)
                {
                    result.AnalyzeText = $"（EXPLAIN ANALYZE 执行失败：{ex.Message}）";
                }
            }
        }
        catch (OperationCanceledException)
        {
            result.Error = "剖析已取消。";
        }
        catch (Exception ex)
        {
            result.Error = $"剖析失败：{ex.Message}";
        }

        return result;
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
