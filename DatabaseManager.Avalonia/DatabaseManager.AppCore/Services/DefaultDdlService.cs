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
/// 对象 Drop/Rename 使用事务；整库删除通过维护库自动提交。
/// </summary>
public class DefaultDdlService : IDdlService
{
    private readonly IDbConnectionService _connectionService;

    public DefaultDdlService(IDbConnectionService connectionService)
    {
        _connectionService = connectionService;
    }

    public DdlScriptResult PreviewDropDatabase(string connectionName, string databaseName)
        => PreviewDatabaseCommand(connectionName, databaseName, create: false);

    public DdlScriptResult PreviewCreateDatabase(string connectionName, string databaseName)
        => PreviewDatabaseCommand(connectionName, databaseName, create: true);

    private DdlScriptResult PreviewDatabaseCommand(string connectionName, string databaseName, bool create)
    {
        var connection = FindConnection(connectionName);
        if (connection is null) return Error<DdlScriptResult>($"未找到连接 '{connectionName}'。");
        var type = ParseDatabaseType(connection.DatabaseType);
        if (type is not (DatabaseType.Postgres or DatabaseType.MySql or DatabaseType.SqlServer))
            return Error<DdlScriptResult>(create ? "当前数据库类型不支持新建数据库。" : "当前数据库类型不支持整库删除。");
        if (string.IsNullOrWhiteSpace(databaseName) || databaseName.Any(char.IsControl))
            return Error<DdlScriptResult>("数据库名称不能为空或包含控制字符。");
        if (create && (databaseName != databaseName.Trim()
            || (type == DatabaseType.Postgres ? System.Text.Encoding.UTF8.GetByteCount(databaseName) > 63
                : databaseName.Length > (type == DatabaseType.MySql ? 64 : 128))))
            return Error<DdlScriptResult>("数据库名称首尾不能有空白，长度上限为 PostgreSQL 63 个 UTF-8 字节、MySQL 64 个字符、SQL Server 128 个字符。");
        string[] systemDatabases = type switch
        {
            DatabaseType.Postgres => ["postgres", "template0", "template1"],
            DatabaseType.MySql => ["mysql", "sys", "information_schema", "performance_schema"],
            _ => ["master", "model", "msdb", "tempdb"],
        };
        if (systemDatabases.Contains(databaseName, StringComparer.OrdinalIgnoreCase))
            return Error<DdlScriptResult>(create ? "不能使用系统数据库名称。" : "不允许删除系统数据库。");
        return new DdlScriptResult
        {
            IsSuccess = true,
            Script = $"{(create ? "CREATE" : "DROP")} DATABASE {SqlDialectHelper.QuoteIdentifier(type, databaseName)};",
        };
    }

    internal static string GetMaintenanceDatabase(string databaseType) => ParseDatabaseType(databaseType) switch
    {
        DatabaseType.Postgres => "postgres",
        DatabaseType.MySql => "information_schema",
        DatabaseType.SqlServer => "master",
        _ => throw new NotSupportedException("当前数据库类型不支持整库删除。"),
    };

    public async Task<DdlExecuteResult> CreateDatabaseAsync(string connectionName, string databaseName, CancellationToken ct = default)
    {
        var preview = PreviewCreateDatabase(connectionName, databaseName);
        if (!preview.IsSuccess) return ErrorExecute(preview.ErrorMessage!);
        try
        {
            ct.ThrowIfCancellationRequested();
            return await ExecuteDatabaseCommandAsync(FindConnection(connectionName)!, preview.Script!, ct);
        }
        catch (Exception ex)
        {
            return ErrorExecute(ex.Message);
        }
    }

    public async Task<DdlExecuteResult> DropDatabaseAsync(string connectionName, string databaseName, CancellationToken ct = default)
    {
        var preview = PreviewDropDatabase(connectionName, databaseName);
        if (!preview.IsSuccess) return ErrorExecute(preview.ErrorMessage!);
        try
        {
            ct.ThrowIfCancellationRequested();
            var connection = FindConnection(connectionName)!;
            // 只清理目标库的空闲池连接；活动会话由服务器拒绝删除，不强制终止或回滚。
            using (var target = CreateInterpreter(connection, databaseName).CreateConnection())
            {
                if (target is Npgsql.NpgsqlConnection pg) Npgsql.NpgsqlConnection.ClearPool(pg);
                else if (target is Microsoft.Data.SqlClient.SqlConnection sql) Microsoft.Data.SqlClient.SqlConnection.ClearPool(sql);
                else if (target is MySqlConnector.MySqlConnection mysql) await MySqlConnector.MySqlConnection.ClearPoolAsync(mysql, ct);
            }
            return await ExecuteDatabaseCommandAsync(connection, preview.Script!, ct);
        }
        catch (Exception ex)
        {
            return ErrorExecute(ex.Message);
        }
    }

    private static async Task<DdlExecuteResult> ExecuteDatabaseCommandAsync(ConnectionItem connection, string script, CancellationToken ct)
    {
        var interpreter = CreateInterpreter(connection, GetMaintenanceDatabase(connection.DatabaseType));
        using var dbConnection = interpreter.CreateConnection();
        await dbConnection.OpenAsync(ct);
        // CREATE/DROP DATABASE 不能复用 ExecuteSingleAsync 的事务。
        var result = await interpreter.ExecuteNonQueryAsync(dbConnection, new CommandInfo
        {
            CommandText = script,
            CancellationToken = ct,
            CommandTimeoutSeconds = 30,
        });
        return result?.HasError == true
            ? ErrorExecute(result.Message)
            : new DdlExecuteResult { IsSuccess = true, Script = script };
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

        var connectionInfo = ConnectionHelper.ToConnectionInfo(connection);
        connectionInfo.Database = string.IsNullOrEmpty(databaseOverride) ? connection.Database : databaseOverride;

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

    public async Task<DdlScriptResult> GenerateObjectScriptAsync(
        string connectionName,
        string databaseName,
        DatabaseObject dbObject,
        ObjectScriptType scriptType,
        CancellationToken ct = default)
    {
        try
        {
            var connection = FindConnection(connectionName);
            if (connection is null) return Error<DdlScriptResult>($"未找到连接 '{connectionName}'。");

            if (dbObject is not (Table or View))
                return Error<DdlScriptResult>("仅支持为表或视图生成脚本。");

            bool isTable = dbObject is Table;
            if (!isTable && scriptType is not (ObjectScriptType.Select or ObjectScriptType.SelectTopN))
                return Error<DdlScriptResult>("视图仅支持生成 SELECT / SELECT TOP N 脚本，DML 请针对基表操作。");

            var interpreter = CreateInterpreter(connection, databaseName);
            var dbType = ParseDatabaseType(connection.DatabaseType);
            string quotedName = interpreter.GetQuotedDbObjectNameWithSchema(dbObject);

            switch (scriptType)
            {
                case ObjectScriptType.Select:
                    return Ok($"SELECT * FROM {quotedName};");

                case ObjectScriptType.SelectTopN:
                    return Ok(BuildSelectTopNScript(dbType, quotedName, topN: 100));

                case ObjectScriptType.Insert:
                case ObjectScriptType.Update:
                case ObjectScriptType.Delete:
                case ObjectScriptType.CreateTable:
                    break;

                default:
                    return Error<DdlScriptResult>($"暂不支持生成 {scriptType} 类型脚本。");
            }

            // 以下脚本需要真实列结构（仅表）。
            var table = (Table)dbObject;
            var filter = new SchemaInfoFilter
            {
                Schema = table.Schema,
                TableNames = new[] { table.Name },
            };

            List<TableColumn> columns = await interpreter.GetTableColumnsAsync(filter);
            if (columns.Count == 0)
                return Error<DdlScriptResult>($"未能读取表「{table.Name}」的列结构，请确认表存在且连接有效。");

            switch (scriptType)
            {
                case ObjectScriptType.Insert:
                    return Ok(BuildInsertScript(quotedName, columns));
                case ObjectScriptType.Update:
                {
                    List<TablePrimaryKey> pks = await interpreter.GetTablePrimaryKeysAsync(filter);
                    return Ok(BuildUpdateScript(quotedName, columns, pks));
                }
                case ObjectScriptType.Delete:
                {
                    List<TablePrimaryKey> pks = await interpreter.GetTablePrimaryKeysAsync(filter);
                    return Ok(BuildDeleteScript(quotedName, pks.FirstOrDefault(p => p.TableName == table.Name)));
                }
                case ObjectScriptType.CreateTable:
                    return await BuildCreateTableScriptAsync(interpreter, connection, databaseName, table, columns);

                default:
                    return Error<DdlScriptResult>($"暂不支持生成 {scriptType} 类型脚本。");
            }
        }
        catch (Exception ex)
        {
            return Error<DdlScriptResult>(ex.Message);
        }
    }

    /// <summary>按方言生成 SELECT TOP N 查询。</summary>
    private static string BuildSelectTopNScript(DatabaseType dbType, string quotedName, int topN)
    {
        return dbType switch
        {
            DatabaseType.SqlServer => $"SELECT TOP ({topN}) * FROM {quotedName};",
            DatabaseType.Oracle => $"SELECT * FROM {quotedName} FETCH FIRST {topN} ROWS ONLY;",
            _ => $"SELECT * FROM {quotedName} LIMIT {topN};",
        };
    }

    /// <summary>基于真实列结构生成 INSERT 模板（排除自增列与计算列）。</summary>
    private static string BuildInsertScript(string quotedName, IEnumerable<TableColumn> columns)
    {
        var insertableColumns = columns
            .Where(c => !c.IsIdentity && !c.IsComputed)
            .ToList();

        if (insertableColumns.Count == 0)
            insertableColumns = columns.ToList();

        string columnList = string.Join($",{Environment.NewLine}    ", insertableColumns.Select(c => c.Name));
        string valueList = string.Join($",{Environment.NewLine}    ", insertableColumns.Select(c =>
            string.IsNullOrWhiteSpace(c.DefaultValue)
                ? $"/* {c.Name} */ ?"
                : $"/* {c.Name}，默认值: {c.DefaultValue} */ ?"));

        return $"INSERT INTO {quotedName}{Environment.NewLine}({Environment.NewLine}    {columnList}{Environment.NewLine}){Environment.NewLine}VALUES{Environment.NewLine}({Environment.NewLine}    {valueList}{Environment.NewLine});";
    }

    /// <summary>基于真实列结构与主键生成 UPDATE 模板。</summary>
    private static string BuildUpdateScript(string quotedName, IEnumerable<TableColumn> columns, IEnumerable<TablePrimaryKey> primaryKeys)
    {
        var pkColumnNames = primaryKeys
            .SelectMany(pk => pk.Columns.OrderBy(c => c.Order).Select(c => c.ColumnName))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var setColumns = columns.Where(c => !pkColumnNames.Contains(c.Name) && !c.IsComputed).ToList();
        if (setColumns.Count == 0)
            setColumns = columns.ToList();

        string setClause = string.Join($",{Environment.NewLine}    ", setColumns.Select(c =>
            string.IsNullOrWhiteSpace(c.DefaultValue)
                ? $"{c.Name} = /* {c.Name} */ ?"
                : $"{c.Name} = /* {c.Name}，默认值: {c.DefaultValue} */ ?"));

        string whereClause = pkColumnNames.Count > 0
            ? string.Join($"{Environment.NewLine}  AND ", pkColumnNames.Select(c => $"{c} = /* 原值 */ ?"))
            : "1 = 0 -- 未识别到主键，请手动指定 WHERE 条件";

        return $"UPDATE {quotedName}{Environment.NewLine}SET {setClause}{Environment.NewLine}WHERE {whereClause};";
    }

    /// <summary>基于主键生成 DELETE 模板。</summary>
    private static string BuildDeleteScript(string quotedName, TablePrimaryKey? primaryKey)
    {
        if (primaryKey is null || primaryKey.Columns.Count == 0)
        {
            return $"DELETE FROM {quotedName}{Environment.NewLine}WHERE 1 = 0 -- 未识别到主键，请手动指定 WHERE 条件;";
        }

        string whereClause = string.Join(
            $"{Environment.NewLine}  AND ",
            primaryKey.Columns.OrderBy(c => c.Order).Select(c => $"{c.ColumnName} = /* 原值 */ ?"));

        return $"DELETE FROM {quotedName}{Environment.NewLine}WHERE {whereClause};";
    }

    /// <summary>使用方言脚本生成器产出完整 CREATE TABLE 脚本。</summary>
    private async Task<DdlScriptResult> BuildCreateTableScriptAsync(
        DbInterpreter interpreter,
        ConnectionItem connection,
        string databaseName,
        Table table,
        List<TableColumn> columns)
    {
        var filter = new SchemaInfoFilter
        {
            Schema = table.Schema,
            TableNames = new[] { table.Name },
        };

        List<TablePrimaryKey> primaryKeys = await interpreter.GetTablePrimaryKeysAsync(filter);
        var scriptGenerator = DbScriptGeneratorHelper.GetDbScriptGenerator(interpreter);
        var builder = scriptGenerator.CreateTable(
            table,
            columns,
            primaryKeys.FirstOrDefault(p => p.TableName == table.Name),
            Array.Empty<TableForeignKey>(),
            Array.Empty<TableIndex>(),
            Array.Empty<TableConstraint>());

        string script = builder.ToString();
        if (string.IsNullOrWhiteSpace(script))
            return Error<DdlScriptResult>($"当前数据库类型暂不支持生成表「{table.Name}」的建表脚本。");

        return Ok(script);
    }

    public DdlScriptResult GetAddColumnTemplate(string databaseType, Table table)
    {
        var dbType = ParseDatabaseType(databaseType);
        string tableName = GetPlainTableName(table);

        // 方言差异：SQL Server 无 COLUMN 关键字；Oracle 需括号包裹列定义。
        string body = dbType switch
        {
            DatabaseType.SqlServer => $"ALTER TABLE {tableName}{Environment.NewLine}ADD [ColumnName] [INT] NULL;",
            DatabaseType.Oracle => $"ALTER TABLE {tableName}{Environment.NewLine}ADD ([ColumnName] [INT] NULL);",
            _ => $"ALTER TABLE {tableName}{Environment.NewLine}ADD COLUMN [ColumnName] [INT] NULL;",
        };

        string comment = "-- 注意：请将 [ColumnName] / [INT] / NULL 替换为实际列名、类型与可空性后执行。"
            + Environment.NewLine;
        return new DdlScriptResult { IsSuccess = true, Script = comment + body };
    }

    private static string GetPlainTableName(Table table)
        => string.IsNullOrEmpty(table.Schema) ? table.Name : $"{table.Schema}.{table.Name}";

    private static DdlScriptResult Ok(string script) => new() { IsSuccess = true, Script = script };

    private static T Error<T>(string msg) where T : DdlScriptResult, new()
        => new() { IsSuccess = false, ErrorMessage = msg };
    private static DdlExecuteResult ErrorExecute(string msg)
        => new() { IsSuccess = false, ErrorMessage = msg };
}
