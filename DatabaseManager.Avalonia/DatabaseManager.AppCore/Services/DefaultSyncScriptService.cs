using System.Data;
using System.Text;
using DatabaseInterpreter.Core;
using DatabaseInterpreter.Model;
using DatabaseInterpreter.Utility;
using DatabaseManager.AppCore.Models;
using DatabaseManager.Core;
using DatabaseManager.Core.Model;

namespace DatabaseManager.AppCore.Services;

/// <summary>
/// 差异到变更发布服务实现。
/// 结构差异经 <c>DbSynchro</c> 生成变更/回滚脚本；数据差异复用 <c>DataCompare.GenerateScripts</c>（同步）
/// 并按对称逻辑生成回滚脚本；执行统一经 <c>ScriptRunner</c>（结构脚本单事务）。
/// </summary>
public class DefaultSyncScriptService : ISyncScriptService
{
    private const int DataPageSize = 100;

    public async Task<IReadOnlyList<ScriptItem>> GenerateStructuralScriptsAsync(
        SchemaCompareContext context,
        IReadOnlyList<SchemaCompareItem> roots,
        Action<string>? onFeedback = null,
        CancellationToken cancellationToken = default)
    {
        var selected = CollectSelectedDifferences(roots);
        var items = new List<ScriptItem>();
        if (selected.Count == 0)
        {
            return items;
        }

        var sourceInterpreter = CreateInterpreter(context.Source);
        var targetInterpreter = CreateInterpreter(context.Target);
        var synchro = new DbSynchro(sourceInterpreter, targetInterpreter);
        synchro.Subscribe(new FeedbackObserver(onFeedback));
        var targetDbSchema = GetTargetDbSchema(targetInterpreter);

        foreach (var item in selected)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var diff = item.Difference;
            var objectName = diff.TargetName ?? diff.SourceName ?? diff.Type;

            onFeedback?.Invoke($"正在生成 [{item.DifferenceTypeText}] {objectName} 的变更脚本...");

            List<Script> scripts = diff.DatabaseObjectType switch
            {
                DatabaseObjectType.Table => await synchro.GenerateTableChangedScripts(context.SourceSchemaInfo, diff, targetDbSchema),
                DatabaseObjectType.View or DatabaseObjectType.Function or DatabaseObjectType.Procedure
                    => synchro.GenereateScriptDbObjectChangedScripts(diff, targetDbSchema),
                DatabaseObjectType.Type => synchro.GenereateUserDefinedTypeChangedScripts(diff, targetDbSchema),
                _ => await synchro.GenerateTableChildChangedScripts(diff),
            };

            if (scripts is { Count: > 0 })
            {
                items.Add(new ScriptItem(
                    $"[{item.DifferenceTypeText}] {objectName}",
                    string.Join(Environment.NewLine, scripts.Select(s => s.Content)),
                    ScriptKind.Structural,
                    $"对象类型：{item.ObjectType}"));
            }
        }

        return items;
    }

    public async Task<IReadOnlyList<ScriptItem>> GenerateStructuralRollbackScriptsAsync(
        SchemaCompareContext context,
        IReadOnlyList<SchemaCompareItem> roots,
        Action<string>? onFeedback = null,
        CancellationToken cancellationToken = default)
    {
        var selected = CollectSelectedDifferences(roots);
        var items = new List<ScriptItem>();
        if (selected.Count == 0)
        {
            return items;
        }

        // 回滚方向：以对比时捕获的目标库结构为"期望状态"，在目标库上反向应用差异。
        var targetInterpreter = CreateInterpreter(context.Target);
        var synchro = new DbSynchro(targetInterpreter, targetInterpreter);
        synchro.Subscribe(new FeedbackObserver(onFeedback));
        var targetDbSchema = GetTargetDbSchema(targetInterpreter);

        foreach (var item in selected)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var reversed = ReverseDifference(item.Difference);
            var objectName = reversed.TargetName ?? reversed.SourceName ?? reversed.Type;

            onFeedback?.Invoke($"正在生成 [{reversed.DifferenceType}] {objectName} 的回滚脚本...");

            List<Script> scripts = reversed.DatabaseObjectType switch
            {
                DatabaseObjectType.Table => await synchro.GenerateTableChangedScripts(context.TargetSchemaInfo, reversed, targetDbSchema),
                DatabaseObjectType.View or DatabaseObjectType.Function or DatabaseObjectType.Procedure
                    => synchro.GenereateScriptDbObjectChangedScripts(reversed, targetDbSchema),
                DatabaseObjectType.Type => synchro.GenereateUserDefinedTypeChangedScripts(reversed, targetDbSchema),
                _ => await synchro.GenerateTableChildChangedScripts(reversed),
            };

            if (scripts is { Count: > 0 })
            {
                items.Add(new ScriptItem(
                    $"[回滚·{item.DifferenceTypeText}] {objectName}",
                    string.Join(Environment.NewLine, scripts.Select(s => s.Content)),
                    ScriptKind.Structural,
                    $"对象类型：{item.ObjectType}（恢复目标库为对比前状态）"));
            }
        }

        return items;
    }

    public async Task<IReadOnlyList<ScriptItem>> GenerateDataSyncScriptsAsync(
        ConnectionItem source,
        ConnectionItem target,
        IReadOnlyList<DataCompareResultItem> results,
        Action<string>? onFeedback = null,
        CancellationToken cancellationToken = default)
    {
        var items = new List<ScriptItem>();

        var sourceInterpreter = CreateInterpreter(source);
        var targetInterpreter = CreateInterpreter(target);
        var dataCompare = new DataCompare(sourceInterpreter, targetInterpreter, new SchemaInfo());
        dataCompare.Subscribe(new FeedbackObserver(onFeedback));

        foreach (var result in results.Where(r => r.IsSelected && !r.IsIdentical))
        {
            cancellationToken.ThrowIfCancellationRequested();

            onFeedback?.Invoke($"正在生成表 {result.TableName} 的同步脚本...");
            var text = await dataCompare.GenerateScripts(new List<DataCompareResultDetail> { result.Detail }, cancellationToken);

            if (!string.IsNullOrWhiteSpace(text))
            {
                items.Add(new ScriptItem(
                    $"数据同步：表 {result.TableName}",
                    text,
                    ScriptKind.Data,
                    $"差异 {result.DifferentCount}，仅源 {result.OnlyInSourceCount}，仅目标 {result.OnlyInTargetCount}"));
            }
        }

        return items;
    }

    public async Task<IReadOnlyList<ScriptItem>> GenerateDataRollbackScriptsAsync(
        ConnectionItem source,
        ConnectionItem target,
        IReadOnlyList<DataCompareResultItem> results,
        Action<string>? onFeedback = null,
        CancellationToken cancellationToken = default)
    {
        var items = new List<ScriptItem>();

        var targetInterpreter = CreateInterpreter(target);
        var targetScriptGenerator = DbScriptGeneratorHelper.GetDbScriptGenerator(targetInterpreter);

        foreach (var result in results.Where(r => r.IsSelected && !r.IsIdentical))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var detail = result.Detail;
            var tableName = detail.TargetTable.Name;
            onFeedback?.Invoke($"正在生成表 {tableName} 的回滚脚本...");

            var sb = new StringBuilder();

            // 回滚 INSERT：同步会删除"仅目标库有"的行，回滚时先从目标库读出这些行再插回。
            if (detail.OnlyInTargetCount > 0)
            {
                await AppendInsertRestoreScriptsAsync(sb, targetInterpreter, targetScriptGenerator,
                    detail.TargetTable, detail.TargetTableColumns, detail.OnlyInTargetKeyRows,
                    detail.KeyColumns, cancellationToken,
                    msg => onFeedback?.Invoke($"[{tableName}] {msg}"));
            }

            // 回滚 UPDATE：同步把目标行改成源值，回滚时以目标库当前值恢复。
            if (detail.DifferentCount > 0)
            {
                await AppendUpdateRestoreScriptsAsync(sb, targetInterpreter, targetScriptGenerator,
                    detail.TargetTable, detail.TargetTableColumns, detail.SameTableColumns,
                    detail.KeyColumns, detail.DifferentKeyRows, cancellationToken,
                    msg => onFeedback?.Invoke($"[{tableName}] {msg}"));
            }

            // 回滚 DELETE：同步会插入"仅源库有"的行，回滚时按主键删除目标库中的这些行。
            if (detail.OnlyInSourceCount > 0)
            {
                AppendDeleteScripts(sb, targetInterpreter, detail.TargetTable,
                    detail.OnlyInSourceKeyRows, detail.KeyColumns);
            }

            var text = sb.ToString().Trim();
            if (text.Length > 0)
            {
                items.Add(new ScriptItem(
                    $"数据回滚：表 {tableName}",
                    text,
                    ScriptKind.Data,
                    "将目标库数据恢复为对比前状态"));
            }
        }

        return items;
    }

    public async Task<ScriptExecutionResult> ExecuteScriptsAsync(
        ConnectionItem target,
        IReadOnlyList<ScriptItem> scripts,
        Action<string>? onFeedback = null,
        CancellationToken cancellationToken = default)
    {
        var result = new ScriptExecutionResult();
        var selected = scripts.Where(s => s.IsSelected && !string.IsNullOrWhiteSpace(s.SqlText)).ToList();

        if (selected.Count == 0)
        {
            result.Message = "没有要执行的脚本。";
            return result;
        }

        int executed = 0;

        // 结构脚本：单事务执行（任一脚本失败整体回滚）。
        var structuralScripts = selected.Where(s => s.Kind == ScriptKind.Structural)
            .Select(s => new Script(s.SqlText))
            .ToList();

        if (structuralScripts.Count > 0)
        {
            onFeedback?.Invoke($"开始在目标库执行 {structuralScripts.Count} 项结构脚本（单事务）...");
            var interpreter = CreateInterpreter(target);
            var runner = new ScriptRunner();
            runner.Subscribe(new FeedbackObserver(onFeedback));
            await runner.Run(interpreter, structuralScripts, cancellationToken);
            executed += structuralScripts.Count;
            onFeedback?.Invoke("结构脚本执行完成。");
        }

        // 数据脚本：按条目（表）逐个执行，每条内部按语句切分并包裹事务。
        foreach (var dataScript in selected.Where(s => s.Kind == ScriptKind.Data))
        {
            cancellationToken.ThrowIfCancellationRequested();

            onFeedback?.Invoke($"开始执行数据脚本 [{dataScript.Title}]...");
            var runner = new ScriptRunner();
            runner.Subscribe(new FeedbackObserver(onFeedback));
            await runner.Run(ParseDatabaseType(target.DatabaseType), ToConnectionInfo(target), dataScript.SqlText, cancellationToken);
            executed++;
            onFeedback?.Invoke($"数据脚本 [{dataScript.Title}] 执行完成。");
        }

        result.IsSuccess = true;
        result.ExecutedCount = executed;
        result.Message = $"已执行 {executed} 项脚本。";
        onFeedback?.Invoke(result.Message);

        return result;
    }

    #region 数据回滚脚本生成

    private static async Task AppendInsertRestoreScriptsAsync(
        StringBuilder sb,
        DbInterpreter interpreter,
        DbScriptGenerator scriptGenerator,
        Table table,
        List<TableColumn> columns,
        List<DataRow> keyRows,
        List<TableColumn> keyColumns,
        CancellationToken cancellationToken,
        Action<string>? onFeedback)
    {
        int total = keyRows.Count;
        long pageCount = PaginationHelper.GetPageCount(total, DataPageSize);

        interpreter.Option.ScriptOutputMode = GenerateScriptOutputMode.WriteToString;

        using var connection = interpreter.CreateConnection();

        for (int i = 1; i <= pageCount; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            onFeedback?.Invoke($"正在读取待恢复行（{Math.Min(i * DataPageSize, total)}/{total}）...");
            var pagedKeyRows = DataCompare.GetPagedKeyRows(keyRows, DataPageSize, i);
            var whereCondition = DataCompare.GetKeyColumnWhereCondition(interpreter, pagedKeyRows, keyColumns);

            var dataTable = await interpreter.GetPagedDataTableAsync(
                connection, table, columns, null, DataPageSize, 1, cancellationToken, whereCondition);

            var rows = interpreter.ConvertDataTableToDictionaryList(dataTable, columns);
            scriptGenerator.AppendDataScripts(sb, table, columns,
                new Dictionary<long, List<Dictionary<string, object>>> { { 1, rows } });
            sb.AppendLine();
        }
    }

    private static async Task AppendUpdateRestoreScriptsAsync(
        StringBuilder sb,
        DbInterpreter interpreter,
        DbScriptGenerator scriptGenerator,
        Table table,
        List<TableColumn> tableColumns,
        List<TableColumn> sameColumns,
        List<TableColumn> keyColumns,
        List<DataRow> keyRows,
        CancellationToken cancellationToken,
        Action<string>? onFeedback)
    {
        int total = keyRows.Count;
        long pageCount = PaginationHelper.GetPageCount(total, DataPageSize);

        using var connection = interpreter.CreateConnection();

        for (int pageNumber = 1; pageNumber <= pageCount; pageNumber++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var pagedKeyRows = DataCompare.GetPagedKeyRows(keyRows, DataPageSize, pageNumber);
            var whereCondition = DataCompare.GetKeyColumnWhereCondition(interpreter, pagedKeyRows, keyColumns);
            var orderColumns = interpreter.GetQuotedColumnNames(keyColumns);

            var targetDataTable = await interpreter.GetPagedDataTableAsync(
                connection, table, sameColumns, orderColumns, DataPageSize, 1, cancellationToken, whereCondition);

            for (int i = 0; i < targetDataTable.Rows.Count; i++)
            {
                var row = targetDataTable.Rows[i];
                var updateItems = new List<string>();

                foreach (var column in sameColumns.Where(c => !keyColumns.Any(k => k.Name == c.Name)))
                {
                    var value = row[column.Name];
                    string strValue;

                    if (value == null || value == DBNull.Value)
                    {
                        strValue = "null";
                    }
                    else
                    {
                        var tableColumn = tableColumns.FirstOrDefault(item => item.Name == column.Name);
                        strValue = scriptGenerator.ParseValue(tableColumn, value, true)?.ToString();
                    }

                    updateItems.Add($"{interpreter.GetQuotedString(column.Name)}={strValue}");
                }

                if (updateItems.Count > 0)
                {
                    var keyCondition = DataCompare.GetKeyColumnWhereCondition(
                        interpreter, new List<DataRow> { pagedKeyRows[i] }, keyColumns);

                    sb.AppendLine($"UPDATE {interpreter.GetQuotedString(table.Name)} SET {string.Join(",", updateItems)} {keyCondition};");
                }
            }
        }
    }

    private static void AppendDeleteScripts(
        StringBuilder sb,
        DbInterpreter interpreter,
        Table table,
        List<DataRow> keyRows,
        List<TableColumn> keyColumns)
    {
        int total = keyRows.Count;
        long pageCount = PaginationHelper.GetPageCount(total, DataPageSize);

        for (int i = 1; i <= pageCount; i++)
        {
            var pagedKeyRows = DataCompare.GetPagedKeyRows(keyRows, DataPageSize, i);
            var condition = DataCompare.GetKeyColumnWhereCondition(interpreter, pagedKeyRows, keyColumns);
            sb.AppendLine($"DELETE FROM {interpreter.GetQuotedString(table.Name)} {condition};");
        }
    }

    #endregion

    #region 辅助

    /// <summary>遍历差异树，收集勾选的差异节点（表节点未勾选时允许其部分子节点参与）。</summary>
    private static List<SchemaCompareItem> CollectSelectedDifferences(IEnumerable<SchemaCompareItem> items)
    {
        var result = new List<SchemaCompareItem>();

        foreach (var item in items)
        {
            if (item.DifferenceType == SchemaCompareDifferenceType.None)
            {
                result.AddRange(CollectSelectedDifferences(item.Children));
                continue;
            }

            if (item.Children.Count > 0)
            {
                if (item.IsSelected)
                {
                    result.Add(item);
                }
                else
                {
                    result.AddRange(CollectSelectedDifferences(item.Children));
                }
            }
            else if (item.IsSelected)
            {
                result.Add(item);
            }
        }

        return result;
    }

    /// <summary>反转差异方向（Added↔Deleted、Modified 交换源/目标），用于生成回滚脚本。</summary>
    private static SchemaCompareDifference ReverseDifference(SchemaCompareDifference difference)
    {
        var reversed = new SchemaCompareDifference
        {
            Type = difference.Type,
            Parent = difference.Parent,
            ParentType = difference.ParentType,
            ParentName = difference.ParentName,
            DatabaseObjectType = difference.DatabaseObjectType,
            Source = difference.Target,
            Target = difference.Source,
            DifferenceType = difference.DifferenceType switch
            {
                SchemaCompareDifferenceType.Added => SchemaCompareDifferenceType.Deleted,
                SchemaCompareDifferenceType.Deleted => SchemaCompareDifferenceType.Added,
                _ => SchemaCompareDifferenceType.Modified,
            },
        };

        foreach (var sub in difference.SubDifferences)
        {
            reversed.SubDifferences.Add(ReverseDifference(sub));
        }

        return reversed;
    }

    /// <summary>与旧版 UI 一致的目标库 Schema 推导（Oracle 取用户 Schema，MySQL 取库名，SqlServer/Postgres 取默认 Schema）。</summary>
    private static string? GetTargetDbSchema(DbInterpreter targetInterpreter)
        => targetInterpreter.DatabaseType switch
        {
            DatabaseType.Oracle => ((OracleInterpreter)targetInterpreter).GetDbSchema(),
            DatabaseType.MySql => targetInterpreter.ConnectionInfo.Database,
            DatabaseType.SqlServer or DatabaseType.Postgres => targetInterpreter.DefaultSchema,
            _ => null,
        };

    private static DbInterpreter CreateInterpreter(ConnectionItem connection)
    {
        var connectionInfo = ToConnectionInfo(connection);
        var dbType = ParseDatabaseType(connection.DatabaseType);

        var option = new DbInterpreterOption
        {
            ObjectFetchMode = DatabaseObjectFetchMode.Details,
            ScriptOutputMode = GenerateScriptOutputMode.WriteToString,
            SortObjectsByReference = true,
            GetTableAllObjects = true,
            ThrowExceptionWhenErrorOccurs = true,
        };

        return DbInterpreterHelper.GetDbInterpreter(dbType, connectionInfo, option);
    }

    private static ConnectionInfo ToConnectionInfo(ConnectionItem connection)
        => new()
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

    private static DatabaseType ParseDatabaseType(string databaseType)
        => Enum.TryParse<DatabaseType>(databaseType, true, out var type) ? type : DatabaseType.Unknown;

    /// <summary>反馈观察者：将核心库 <see cref="FeedbackInfo"/> 转发到回调日志。</summary>
    private sealed class FeedbackObserver : IObserver<FeedbackInfo>
    {
        private readonly Action<string>? _onFeedback;

        public FeedbackObserver(Action<string>? onFeedback)
        {
            _onFeedback = onFeedback;
        }

        public void OnCompleted() { }

        public void OnError(Exception error)
            => _onFeedback?.Invoke($"错误：{error.Message}");

        public void OnNext(FeedbackInfo value)
        {
            if (!string.IsNullOrWhiteSpace(value.Message))
            {
                _onFeedback?.Invoke(value.Message);
            }
        }
    }

    #endregion
}
