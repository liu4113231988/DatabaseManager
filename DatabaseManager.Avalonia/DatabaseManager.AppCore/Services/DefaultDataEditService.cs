using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DatabaseInterpreter.Core;
using DatabaseInterpreter.Model;
using DatabaseManager.AppCore.Models;

namespace DatabaseManager.AppCore.Services;

/// <summary>
/// 基于 <see cref="DbInterpreter"/> 的数据编辑服务实现。
/// 数据加载：读取表列/主键元数据 + 分页数据；数据保存：生成 INSERT/UPDATE/DELETE 脚本并在事务内执行。
/// </summary>
public class DefaultDataEditService : IDataEditService
{
    private readonly IDbConnectionService _connectionService;

    public DefaultDataEditService(IDbConnectionService connectionService)
    {
        _connectionService = connectionService;
    }

    public async Task<DataLoadResult> LoadDataAsync(
        string connectionName,
        string databaseName,
        string tableName,
        string? schema,
        bool isView,
        int pageSize,
        long pageNumber,
        CancellationToken cancellationToken = default)
    {
        var connection = FindConnection(connectionName);
        if (connection is null)
        {
            return new DataLoadResult { ErrorMessage = $"未找到连接 '{connectionName}'。" };
        }

        var interpreter = CreateInterpreter(connection, databaseName);

        try
        {
            // 1. 读取列/主键/标识列元数据。
            var filter = new SchemaInfoFilter
            {
                Schema = schema,
                TableNames = new[] { tableName },
                DatabaseObjectType = DatabaseObjectType.Column | DatabaseObjectType.PrimaryKey,
            };

            if (isView)
            {
                filter.ColumnType = ColumnType.ViewColumn;
                filter.IsForView = true;
            }

            var schemaInfo = await interpreter.GetSchemaInfoAsync(filter);

            var tableColumns = schemaInfo.TableColumns
                .OrderBy(c => c.Order)
                .ToList();

            if (tableColumns.Count == 0)
            {
                return new DataLoadResult { ErrorMessage = $"未找到表 '{tableName}' 的列定义。" };
            }

            var pkColumns = schemaInfo.TablePrimaryKeys
                .FirstOrDefault()?
                .Columns?
                .Select(c => c.ColumnName)
                .ToList() ?? new List<string>();

            var identityColumns = tableColumns
                .Where(c => c.IsIdentity)
                .Select(c => c.Name)
                .ToList();

            var columnInfos = tableColumns
                .Select(c => new DataColumnInfo
                {
                    Name = c.Name,
                    DataType = c.DataType ?? string.Empty,
                    IsPrimaryKey = pkColumns.Contains(c.Name, StringComparer.OrdinalIgnoreCase),
                    IsIdentity = c.IsIdentity,
                    IsComputed = c.IsComputed,
                    IsNullable = c.IsNullable,
                    Order = c.Order,
                })
                .ToList();

            var tableInfo = new DataTableInfo
            {
                DatabaseName = databaseName,
                Schema = schema,
                Name = tableName,
                IsView = isView,
                Columns = columnInfos,
                PrimaryKeyColumns = pkColumns,
                IdentityColumns = identityColumns,
            };

            // 2. 读取分页数据。
            var table = new Table
            {
                Schema = schema,
                Name = tableName,
            };

            var (total, dataTable) = await interpreter.GetPagedDataTableAsync(
                table,
                orderColumns: string.Empty,
                pageSize,
                pageNumber,
                cancellationToken,
                whereClause: string.Empty,
                isForView: isView,
                columns: tableColumns);

            var rows = ConvertDataTableToRows(dataTable, columnInfos);

            return new DataLoadResult
            {
                TableInfo = tableInfo,
                Rows = rows,
                TotalCount = total,
            };
        }
        catch (Exception ex)
        {
            return new DataLoadResult { ErrorMessage = ex.Message };
        }
    }

    public async Task<DataSaveResult> SaveChangesAsync(
        string connectionName,
        string databaseName,
        string tableName,
        string? schema,
        IReadOnlyList<DataEditRow> inserts,
        IReadOnlyList<DataEditRow> updates,
        IReadOnlyList<DataEditRow> deletes,
        CancellationToken cancellationToken = default)
    {
        var connection = FindConnection(connectionName);
        if (connection is null)
        {
            return new DataSaveResult { ErrorMessage = $"未找到连接 '{connectionName}'。" };
        }

        var interpreter = CreateInterpreter(connection, databaseName);
        var scriptGenerator = DbScriptGeneratorHelper.GetDbScriptGenerator(interpreter);

        // 组装 Table 与 TableColumn 元数据。
        var table = new Table { Schema = schema, Name = tableName };
        var columns = BuildTableColumns(inserts, updates);

        try
        {
            var insertScripts = new List<string>();
            var updateScripts = new List<string>();
            var deleteScripts = new List<string>();

            // 生成 INSERT 脚本（复用 scriptGenerator 批量插入生成，简化用逐行生成）。
            foreach (var row in inserts)
            {
                var script = BuildInsertScript(interpreter, scriptGenerator, table, columns, row);
                if (!string.IsNullOrWhiteSpace(script))
                    insertScripts.Add(script);
            }

            // 生成 UPDATE 脚本（基于原始主键值定位）。
            foreach (var row in updates)
            {
                var script = BuildUpdateScript(interpreter, scriptGenerator, table, columns, row);
                if (!string.IsNullOrWhiteSpace(script))
                    updateScripts.Add(script);
            }

            // 生成 DELETE 脚本（基于原始主键值定位）。
            foreach (var row in deletes)
            {
                var script = BuildDeleteScript(interpreter, scriptGenerator, table, columns, row);
                if (!string.IsNullOrWhiteSpace(script))
                    deleteScripts.Add(script);
            }

            int affected = await ExecuteInTransactionAsync(
                interpreter,
                connectionName,
                insertScripts,
                updateScripts,
                deleteScripts,
                cancellationToken);

            return new DataSaveResult { IsSuccess = true, RowCount = affected };
        }
        catch (Exception ex)
        {
            return new DataSaveResult { IsSuccess = false, ErrorMessage = ex.Message };
        }
    }

    private async Task<int> ExecuteInTransactionAsync(
        DbInterpreter interpreter,
        string connectionName,
        List<string> insertScripts,
        List<string> updateScripts,
        List<string> deleteScripts,
        CancellationToken cancellationToken)
    {
        int affected = 0;

        using var dbConnection = interpreter.CreateConnection();
        if (dbConnection.State != ConnectionState.Open)
        {
            await dbConnection.OpenAsync(cancellationToken);
        }

        var transaction = await dbConnection.BeginTransactionAsync(cancellationToken);

        try
        {
            // 顺序：先删、再改、后插，保证外键与主键约束不被破坏。
            foreach (var sql in deleteScripts.Concat(updateScripts).Concat(insertScripts))
            {
                if (string.IsNullOrWhiteSpace(sql))
                    continue;

                var commandInfo = new CommandInfo
                {
                    CommandText = sql.TrimEnd(';'),
                    Transaction = transaction,
                    CancellationToken = cancellationToken,
                };

                var result = await interpreter.ExecuteNonQueryAsync(dbConnection, commandInfo);
                if (result is not null && result.HasError)
                {
                    throw new Exception(result.Message);
                }

                affected += result?.NumberOfRowsAffected ?? 0;
            }

            await transaction.CommitAsync(cancellationToken);
            return affected;
        }
        catch
        {
            try { await transaction.RollbackAsync(cancellationToken); } catch { /* 忽略 */ }
            throw;
        }
    }

    private string BuildInsertScript(
        DbInterpreter interpreter,
        DbScriptGenerator scriptGenerator,
        Table table,
        List<TableColumn> columns,
        DataEditRow row)
    {
        var includedColumns = columns
            .Where(c => !c.IsIdentity && !c.IsComputed)
            .ToList();

        if (includedColumns.Count == 0)
            return string.Empty;

        string tableName = interpreter.GetQuotedDbObjectNameWithSchema(table.Schema, table.Name);

        var colNames = string.Join(", ", includedColumns.Select(c => interpreter.GetQuotedString(c.Name)));

        var valueParts = includedColumns.Select(c =>
        {
            var value = row.GetValue(c.Name);
            return ParseValueLiteral(scriptGenerator, c, value);
        });

        string values = string.Join(", ", valueParts);

        return $"INSERT INTO {tableName} ({colNames}) VALUES ({values});";
    }

    private string BuildUpdateScript(
        DbInterpreter interpreter,
        DbScriptGenerator scriptGenerator,
        Table table,
        List<TableColumn> columns,
        DataEditRow row)
    {
        var dirtyColumns = row.GetDirtyColumns()
            .Where(kv => !kv.Column.IsIdentity && !kv.Column.IsComputed)
            .ToList();

        if (dirtyColumns.Count == 0)
            return string.Empty;

        string tableName = interpreter.GetQuotedDbObjectNameWithSchema(table.Schema, table.Name);

        var setClauses = dirtyColumns.Select(kv =>
        {
            var value = row.GetValue(kv.Column.Name);
            var tc = FindColumn(columns, kv.Column.Name) ?? ToTableColumn(kv.Column);
            return $"{interpreter.GetQuotedString(kv.Column.Name)} = {ParseValueLiteral(scriptGenerator, tc, value)}";
        });

        string whereClause = BuildWhereClause(interpreter, scriptGenerator, columns, row);
        if (string.IsNullOrWhiteSpace(whereClause))
            return string.Empty;

        return $"UPDATE {tableName} SET {string.Join(", ", setClauses)} WHERE {whereClause};";
    }

    private string BuildDeleteScript(
        DbInterpreter interpreter,
        DbScriptGenerator scriptGenerator,
        Table table,
        List<TableColumn> columns,
        DataEditRow row)
    {
        string whereClause = BuildWhereClause(interpreter, scriptGenerator, columns, row);
        if (string.IsNullOrWhiteSpace(whereClause))
            return string.Empty;

        string tableName = interpreter.GetQuotedDbObjectNameWithSchema(table.Schema, table.Name);

        return $"DELETE FROM {tableName} WHERE {whereClause};";
    }

    /// <summary>基于原始值构造 WHERE 条件（优先主键列，其次全部非空列）。</summary>
    private string BuildWhereClause(
        DbInterpreter interpreter,
        DbScriptGenerator scriptGenerator,
        List<TableColumn> columns,
        DataEditRow row)
    {
        var conditions = new List<string>();

        // 优先使用主键列（取原始值定位）。
        var pkColumns = row.GetPrimaryKeyConditions().ToList();
        if (pkColumns.Count > 0)
        {
            foreach (var (colName, value) in pkColumns)
            {
                var tc = FindColumn(columns, colName);
                if (tc is null) continue;

                var literal = ParseValueLiteral(scriptGenerator, tc, value);
                conditions.Add($"{interpreter.GetQuotedString(colName)} = {literal}");
            }

            if (conditions.Count > 0)
                return string.Join(" AND ", conditions);
        }

        // 无主键：退化用全部原始非空列做等值条件。
        foreach (var col in columns.Where(c => !c.IsComputed))
        {
            var original = row.GetOriginal(col.Name);
            if (original is null || original == DBNull.Value)
                continue;

            var literal = ParseValueLiteral(scriptGenerator, col, original);
            conditions.Add($"{interpreter.GetQuotedString(col.Name)} = {literal}");
        }

        return string.Join(" AND ", conditions);
    }

    private static string ParseValueLiteral(DbScriptGenerator scriptGenerator, TableColumn column, object? value)
    {
        if (value is null || value == DBNull.Value)
            return "NULL";

        try
        {
            var parsed = scriptGenerator.ParseValue(column, value, bytesAsString: true);
            if (parsed is null)
                return "NULL";
            return parsed.ToString() ?? "NULL";
        }
        catch
        {
            // 解析失败时退回字符串字面量。
            return $"'{value.ToString()?.Replace("'", "''")}'";
        }
    }

    private static TableColumn? FindColumn(List<TableColumn> columns, string name)
        => columns.FirstOrDefault(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase));

    /// <summary>从待编辑行集合中推断出 TableColumn 元数据（名称 + 类型 + 标识/计算属性）。</summary>
    private static List<TableColumn> BuildTableColumns(IReadOnlyList<DataEditRow> inserts, IReadOnlyList<DataEditRow> updates)
    {
        var result = new List<TableColumn>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var row in inserts.Concat(updates))
        {
            foreach (var (column, _) in row.GetAllValues())
            {
                if (seen.Add(column.Name))
                {
                    result.Add(ToTableColumn(column));
                }
            }
        }

        return result;
    }

    /// <summary>将 AppCore 的 <see cref="DataColumnInfo"/> 转为脚本生成用的 <see cref="TableColumn"/>。</summary>
    private static TableColumn ToTableColumn(DataColumnInfo column)
    {
        return new TableColumn
        {
            Name = column.Name,
            DataType = column.DataType,
            IsIdentity = column.IsIdentity,
            // IsComputed 由 ComputeExp 派生，故通过 ComputeExp 表达计算列。
            ComputeExp = column.IsComputed ? "1" : string.Empty,
            IsNullable = column.IsNullable,
        };
    }

    private static List<DataEditRow> ConvertDataTableToRows(DataTable dataTable, IReadOnlyList<DataColumnInfo> columnInfos)
    {
        var rows = new List<DataEditRow>();

        foreach (DataRow dr in dataTable.Rows)
        {
            var row = new DataEditRow(columnInfos);

            for (int i = 0; i < columnInfos.Count; i++)
            {
                var col = columnInfos[i];
                var value = dr.Table.Columns.Contains(col.Name) ? dr[col.Name] : null;
                if (value == DBNull.Value) value = null;

                // 通过索引器写入当前值（只读列会被 SetValue 忽略，但加载阶段需要填充）。
                row.SetCellValueDirect(i, value);
            }

            // 将当前值快照为原始值，作为未修改的基线。
            row.MarkAsSaved();

            rows.Add(row);
        }

        return rows;
    }

    private DbInterpreter CreateInterpreter(ConnectionItem connection, string? databaseOverride = null)
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
            ObjectFetchMode = DatabaseObjectFetchMode.Simple,
            ShowTextForGeometry = true,
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
