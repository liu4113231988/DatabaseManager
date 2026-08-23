using System;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DatabaseInterpreter.Core;
using DatabaseInterpreter.Model;
using DatabaseManager.AppCore.Models;

namespace DatabaseManager.AppCore.Services;

/// <summary>
/// 基于 <see cref="DbInterpreter"/> / <see cref="DbScriptGenerator"/> 的 DDL 执行服务实现。
/// 在事务内执行单条 Drop/Rename，失败自动回滚。
/// </summary>
public class DefaultDdlService : IDdlService
{
    private readonly IDbConnectionService _connectionService;

    public DefaultDdlService(IDbConnectionService connectionService)
    {
        _connectionService = connectionService;
    }

    public DdlScriptResult PreviewDrop(string connectionName, string databaseName, DatabaseObject dbObject)
    {
        try
        {
            var connection = FindConnection(connectionName);
            if (connection is null) return Error<DdlScriptResult>($"未找到连接 '{connectionName}'。");

            var interpreter = CreateInterpreter(connection, databaseName);
            var scriptGenerator = DbScriptGeneratorHelper.GetDbScriptGenerator(interpreter);
            var script = scriptGenerator.Drop(dbObject);
            return new DdlScriptResult { IsSuccess = true, Script = script?.Content };
        }
        catch (Exception ex)
        {
            return Error<DdlScriptResult>(ex.Message);
        }
    }

    public async Task<DdlExecuteResult> DropAsync(string connectionName, string databaseName, DatabaseObject dbObject, CancellationToken ct = default)
    {
        var preview = PreviewDrop(connectionName, databaseName, dbObject);
        if (!preview.IsSuccess)
            return new DdlExecuteResult { IsSuccess = false, ErrorMessage = preview.ErrorMessage };
        if (string.IsNullOrWhiteSpace(preview.Script))
            return new DdlExecuteResult { IsSuccess = false, ErrorMessage = $"不支持删除 {dbObject.GetType().Name}。" };

        return await ExecuteSingleAsync(connectionName, databaseName, preview.Script!, ct);
    }

    public async Task<DdlExecuteResult> RenameTableAsync(string connectionName, string databaseName, Table table, string newName, CancellationToken ct = default)
    {
        try
        {
            var connection = FindConnection(connectionName);
            if (connection is null) return ErrorExecute($"未找到连接 '{connectionName}'。");

            var interpreter = CreateInterpreter(connection, databaseName);
            var scriptGenerator = DbScriptGeneratorHelper.GetDbScriptGenerator(interpreter);
            var script = scriptGenerator.RenameTable(table, newName);
            if (string.IsNullOrWhiteSpace(script?.Content))
                return ErrorExecute($"当前数据库类型暂不支持重命名表。");

            return await ExecuteSingleAsync(connectionName, databaseName, script.Content!, ct);
        }
        catch (Exception ex)
        {
            return ErrorExecute(ex.Message);
        }
    }

    public async Task<DdlExecuteResult> RenameTableColumnAsync(string connectionName, string databaseName, Table table, TableColumn column, string newName, CancellationToken ct = default)
    {
        try
        {
            var connection = FindConnection(connectionName);
            if (connection is null) return ErrorExecute($"未找到连接 '{connectionName}'。");

            var interpreter = CreateInterpreter(connection, databaseName);
            var scriptGenerator = DbScriptGeneratorHelper.GetDbScriptGenerator(interpreter);
            var script = scriptGenerator.RenameTableColumn(table, column, newName);
            if (string.IsNullOrWhiteSpace(script?.Content))
                return ErrorExecute($"当前数据库类型暂不支持重命名列。");

            return await ExecuteSingleAsync(connectionName, databaseName, script.Content!, ct);
        }
        catch (Exception ex)
        {
            return ErrorExecute(ex.Message);
        }
    }

    private async Task<DdlExecuteResult> ExecuteSingleAsync(string connectionName, string databaseName, string sql, CancellationToken ct)
    {
        var connection = FindConnection(connectionName);
        if (connection is null) return ErrorExecute($"未找到连接 '{connectionName}'。");

        var interpreter = CreateInterpreter(connection, databaseName);
        using var dbConnection = interpreter.CreateConnection();
        if (dbConnection.State != ConnectionState.Open)
            await dbConnection.OpenAsync(ct);

        using var transaction = await dbConnection.BeginTransactionAsync(ct);
        try
        {
            var commandInfo = new CommandInfo
            {
                CommandText = sql.Trim().TrimEnd(';'),
                Transaction = transaction,
                CancellationToken = ct,
            };

            var exec = await interpreter.ExecuteNonQueryAsync(dbConnection, commandInfo);
            if (exec is not null && exec.HasError)
                throw new Exception(exec.Message);

            await transaction.CommitAsync(ct);
            return new DdlExecuteResult
            {
                IsSuccess = true,
                Script = sql,
                AffectedCount = 0,
            };
        }
        catch
        {
            try { await transaction.RollbackAsync(ct); } catch { /* 忽略回滚失败 */ }
            throw;
        }
    }

    private ConnectionItem? FindConnection(string connectionName)
        => _connectionService.GetConnections().FirstOrDefault(c =>
            string.Equals(c.Name, connectionName, StringComparison.OrdinalIgnoreCase));

    private static DbInterpreter CreateInterpreter(ConnectionItem connection, string? databaseOverride = null)
    {
        var dbType = ParseDatabaseType(connection.DatabaseType);

        var connectionInfo = new ConnectionInfo
        {
            Server = connection.Server,
            Port = connection.Port,
            ServerVersion = connection.ServerVersion,
            Database = string.IsNullOrEmpty(databaseOverride) ? connection.Database : databaseOverride,
            IntegratedSecurity = connection.IntegratedSecurity,
            UserId = connection.UserId,
            Password = connection.Password,
            IsDba = connection.IsDba,
            UseSsl = connection.UseSsl,
        };

        var option = new DbInterpreterOption
        {
            ObjectFetchMode = DatabaseObjectFetchMode.Details,
            ShowTextForGeometry = true,
        };

        return DbInterpreterHelper.GetDbInterpreter(dbType, connectionInfo, option);
    }

    private static DatabaseType ParseDatabaseType(string databaseType)
    {
        if (Enum.TryParse<DatabaseType>(databaseType, true, out var type))
            return type;
        return DatabaseType.Unknown;
    }

    public DdlScriptResult GetCreateTemplate(DatabaseObjectType objectType, string? schema)
    {
        var prefix = string.IsNullOrEmpty(schema) ? string.Empty : $"[{schema}].";
        string body = objectType switch
        {
            DatabaseObjectType.View => $"CREATE VIEW {prefix}[ViewName] AS{Environment.NewLine}    SELECT /* TODO: 请编辑列名和 WHERE 条件 */ 1 AS Col1;",
            DatabaseObjectType.Procedure => $"CREATE PROCEDURE {prefix}[ProcedureName]{Environment.NewLine}    /* @Param1 INT = 0, @Param2 NVARCHAR(50) */{Environment.NewLine}AS{Environment.NewLine}BEGIN{Environment.NewLine}    SET NOCOUNT ON;{Environment.NewLine}    /* TODO: 编写存储过程逻辑 */{Environment.NewLine}    SELECT 1;{Environment.NewLine}END;",
            DatabaseObjectType.Function => $"CREATE FUNCTION {prefix}[FunctionName](/* @Param1 INT */){Environment.NewLine}RETURNS /* TABLE / INT / NVARCHAR(MAX) */{Environment.NewLine}AS{Environment.NewLine}BEGIN{Environment.NewLine}    /* TODO: 编写函数逻辑 */{Environment.NewLine}    RETURN /* value / SELECT */ 1;{Environment.NewLine}END;",
            _ => throw new NotSupportedException($"暂不支持生成 {objectType} 对象模板。")
        };

        var comment = "-- 注意：此为通用 CREATE 模板，不同数据库（SQL Server / MySQL / Oracle / Postgres / SQLite）语法有差异，请按实际数据库方言调整后执行。"
            + Environment.NewLine;
        return new DdlScriptResult { IsSuccess = true, Script = comment + body };
    }

    public async Task<DdlScriptResult> GetObjectDefinitionAsync(string connectionName, string databaseName, DatabaseObject dbObject, CancellationToken ct = default)
    {
        try
        {
            var connection = FindConnection(connectionName);
            if (connection is null) return Error<DdlScriptResult>($"未找到连接 '{connectionName}'。");

            // 先尝试使用已有 Definition（如果 ObjectFetchMode.Details 模式已经填充）。
            if (dbObject is ScriptDbObject scriptObj && !string.IsNullOrWhiteSpace(scriptObj.Definition))
            {
                return new DdlScriptResult { IsSuccess = true, Script = scriptObj.Definition };
            }

            // 否则以 Details 模式重新读取该对象的 SchemaInfo（含 Definition）。
            var interpreter = CreateInterpreter(connection, databaseName);

            var filter = new SchemaInfoFilter
            {
                Schema = dbObject.Schema,
                TableNames = dbObject is TableTrigger trigger ? new[] { trigger.TableName } : null,
            };

            if (dbObject is View) filter.DatabaseObjectType = DatabaseObjectType.View;
            else if (dbObject is Function) filter.DatabaseObjectType = DatabaseObjectType.Function;
            else if (dbObject is Procedure) filter.DatabaseObjectType = DatabaseObjectType.Procedure;
            else if (dbObject is TableTrigger) filter.DatabaseObjectType = DatabaseObjectType.Trigger;
            else throw new NotSupportedException($"暂不支持读取 {dbObject.GetType().Name} 类型的定义。");

            var schemaInfo = await interpreter.GetSchemaInfoAsync(filter);

            string? definition = null;
            if (dbObject is View)
                definition = schemaInfo.Views.FirstOrDefault(v => SameObject(v, dbObject))?.Definition;
            else if (dbObject is Function)
                definition = schemaInfo.Functions.FirstOrDefault(v => SameObject(v, dbObject))?.Definition;
            else if (dbObject is Procedure)
                definition = schemaInfo.Procedures.FirstOrDefault(v => SameObject(v, dbObject))?.Definition;
            else if (dbObject is TableTrigger)
                definition = schemaInfo.TableTriggers.FirstOrDefault(v => SameObject(v, dbObject))?.Definition;

            if (string.IsNullOrWhiteSpace(definition))
                return Error<DdlScriptResult>($"无法从数据库读取 {dbObject.GetType().Name}「{dbObject.Name}」的定义（可能需要数据库支持或权限不足）。");

            return new DdlScriptResult { IsSuccess = true, Script = definition };
        }
        catch (Exception ex)
        {
            return Error<DdlScriptResult>(ex.Message);
        }
    }

    private static bool SameObject(DatabaseObject a, DatabaseObject b)
    {
        return string.Equals(a.Name, b.Name, StringComparison.OrdinalIgnoreCase)
            && string.Equals(a.Schema ?? string.Empty, b.Schema ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    private static T Error<T>(string msg) where T : DdlScriptResult, new()
        => new() { IsSuccess = false, ErrorMessage = msg };
    private static DdlExecuteResult ErrorExecute(string msg)
        => new() { IsSuccess = false, ErrorMessage = msg };
}
