using System.Diagnostics;
using DatabaseInterpreter.Core;
using DatabaseInterpreter.Model;
using DatabaseManager.AppCore.Models;

namespace DatabaseManager.AppCore.Services;

/// <summary>
/// 查询服务实现。阶段 2：接入 <see cref="DbInterpreter"/> 真正执行 SQL 并返回结果集。
/// </summary>
public class DefaultQueryService : IQueryService
{
    private readonly IDbConnectionService _connectionService;

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

        var interpreter = CreateInterpreter(connection);

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
}
