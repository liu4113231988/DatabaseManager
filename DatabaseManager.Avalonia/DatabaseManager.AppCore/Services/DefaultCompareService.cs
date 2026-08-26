using System.Data;
using DatabaseInterpreter.Core;
using DatabaseInterpreter.Model;
using DatabaseInterpreter.Utility;
using DatabaseManager.AppCore.Models;
using DatabaseManager.Core;
using DatabaseManager.Core.Model;

namespace DatabaseManager.AppCore.Services;

/// <summary>
/// 对比服务实现（阶段 4）。接入 <c>SchemaCompare</c> 完成结构对比。
/// </summary>
public class DefaultCompareService : ICompareService
{
    public async Task<IReadOnlyList<SchemaCompareItem>> CompareSchemaAsync(
        ConnectionItem source,
        ConnectionItem target,
        DatabaseObjectType databaseObjectType,
        Action<string>? onFeedback = null,
        CancellationToken cancellationToken = default)
    {
        onFeedback?.Invoke("正在校验源/目标连接...");

        if (string.IsNullOrEmpty(source.Database) || string.IsNullOrEmpty(target.Database))
        {
            onFeedback?.Invoke("错误：源/目标数据库不能为空。");
            return Array.Empty<SchemaCompareItem>();
        }

        var sourceDbType = ParseDatabaseType(source.DatabaseType);
        var targetDbType = ParseDatabaseType(target.DatabaseType);

        if (sourceDbType == DatabaseType.Unknown || targetDbType == DatabaseType.Unknown)
        {
            onFeedback?.Invoke("错误：源/目标数据库类型无效。");
            return Array.Empty<SchemaCompareItem>();
        }

        if (sourceDbType != targetDbType)
        {
            onFeedback?.Invoke($"错误：结构对比要求源与目标数据库类型相同（当前：源={source.DatabaseType}，目标={target.DatabaseType}）。");
            return Array.Empty<SchemaCompareItem>();
        }

        if (IsSameDatabase(source, target))
        {
            onFeedback?.Invoke("错误：源数据库与目标数据库不能相同。");
            return Array.Empty<SchemaCompareItem>();
        }

        try
        {
            var sourceInterpreter = CreateInterpreter(source, sourceDbType, databaseObjectType);
            var targetInterpreter = CreateInterpreter(target, targetDbType, databaseObjectType);

            onFeedback?.Invoke("正在读取源库对象信息...");
            var sourceFilter = new SchemaInfoFilter { DatabaseObjectType = databaseObjectType };
            var sourceSchemaInfo = await sourceInterpreter.GetSchemaInfoAsync(sourceFilter);

            onFeedback?.Invoke("正在读取目标库对象信息...");
            var targetFilter = new SchemaInfoFilter { DatabaseObjectType = databaseObjectType };
            var targetSchemaInfo = await targetInterpreter.GetSchemaInfoAsync(targetFilter);

            onFeedback?.Invoke("开始对比结构差异...");

            var schemaCompare = new SchemaCompare(
                targetInterpreter.DatabaseType,
                sourceInterpreter,
                targetInterpreter,
                sourceSchemaInfo,
                targetSchemaInfo);

            var differences = await schemaCompare.Compare();

            onFeedback?.Invoke("对比完成，正在整理结果...");

            var roots = BuildTree(differences);
            onFeedback?.Invoke($"对比完成，共发现 {CountDifferences(roots)} 处差异。");

            return roots;
        }
        catch (Exception ex)
        {
            onFeedback?.Invoke($"对比过程出现异常：{ex.Message}");
            return Array.Empty<SchemaCompareItem>();
        }
    }

    /// <summary>
    /// 将扁平差异列表整理成可展示的树结构：
    /// 顶层按对象类型（Type/View/Function/Procedure/Table 根）分组，Table 节点展开列/索引/键/约束/触发器子文件夹。
    /// </summary>
    private static IReadOnlyList<SchemaCompareItem> BuildTree(List<SchemaCompareDifference> differences)
    {
        var roots = new List<SchemaCompareItem>();

        // 顶层分组：按 DatabaseObjectType 分组，仅保留有实际差异的对象类型。
        foreach (var group in differences.GroupBy(d => d.DatabaseObjectType))
        {
            var typeFolder = new SchemaCompareItem(new SchemaCompareDifference
            {
                Type = GetTypeDisplayName(group.Key),
                DatabaseObjectType = group.Key,
            });
            typeFolder.Text = GetTypeDisplayName(group.Key);
            DifferenceTypeMarker(typeFolder, group.Key, group.ToList());

            foreach (var diff in group.OrderBy(d => d.Target?.Name ?? d.Source?.Name))
            {
                var item = new SchemaCompareItem(diff)
                {
                    Text = diff.Target?.Name ?? diff.Source?.Name ?? string.Empty,
                    Description = BuildDescription(diff),
                };

                // Table 对象：附加子差异（列/索引/主键/外键/约束/触发器）。
                if (diff.DatabaseObjectType == DatabaseObjectType.Table)
                {
                    AppendTableChildren(item, diff);
                }

                typeFolder.Children.Add(item);
            }

            if (typeFolder.Children.Count > 0)
            {
                roots.Add(typeFolder);
            }
        }

        return roots;
    }

    /// <summary>为 Table 差异节点附加子文件夹（Columns / Indexes / Primary Keys / Foreign Keys / Constraints / Triggers）。</summary>
    private static void AppendTableChildren(SchemaCompareItem tableItem, SchemaCompareDifference tableDiff)
    {
        var subDiffs = tableDiff.SubDifferences
            .Where(d => d.DifferenceType != SchemaCompareDifferenceType.None)
            .ToList();

        if (subDiffs.Count == 0)
            return;

        // 按对象类型分组子差异为文件夹。
        foreach (var subGroup in subDiffs.GroupBy(d => d.DatabaseObjectType))
        {
            var folder = new SchemaCompareItem(new SchemaCompareDifference
            {
                Type = GetTypeDisplayName(subGroup.Key),
                DatabaseObjectType = subGroup.Key,
                Parent = tableDiff,
            })
            {
                Text = GetTypeDisplayName(subGroup.Key),
            };
            DifferenceTypeMarker(folder, subGroup.Key, subGroup.ToList());

            foreach (var child in subGroup.OrderBy(d => d.Target?.Name ?? d.Source?.Name))
            {
                folder.Children.Add(new SchemaCompareItem(child)
                {
                    Text = child.Target?.Name ?? child.Source?.Name ?? string.Empty,
                    Description = BuildDescription(child),
                });
            }

            tableItem.Children.Add(folder);
        }
    }

    private static void DifferenceTypeMarker(SchemaCompareItem item, DatabaseObjectType type, List<SchemaCompareDifference> diffs)
    {
        if (diffs.Any(d => d.DifferenceType == SchemaCompareDifferenceType.Added))
            item.Text += " (＋)";
        else if (diffs.Any(d => d.DifferenceType == SchemaCompareDifferenceType.Deleted))
            item.Text += " (－)";
        else if (diffs.Any(d => d.DifferenceType == SchemaCompareDifferenceType.Modified))
            item.Text += " (△)";
    }

    private static string BuildDescription(SchemaCompareDifference diff)
    {
        var parts = new List<string>();
        if (diff.DifferenceType == SchemaCompareDifferenceType.Added)
            parts.Add("仅在源库存在");
        else if (diff.DifferenceType == SchemaCompareDifferenceType.Deleted)
            parts.Add("仅在目标库存在");
        else if (diff.DifferenceType == SchemaCompareDifferenceType.Modified)
            parts.Add("定义有差异");

        var source = diff.Source?.Name;
        var target = diff.Target?.Name;
        if (source != null && target != null && !string.Equals(source, target))
        {
            parts.Add($"源:{source} → 目标:{target}");
        }

        return string.Join("，", parts);
    }

    private static int CountDifferences(IReadOnlyList<SchemaCompareItem> roots)
    {
        int count = 0;
        foreach (var root in roots)
        {
            foreach (var child in root.Children)
            {
                if (child.DifferenceType != SchemaCompareDifferenceType.None)
                    count++;
                foreach (var sub in child.Children)
                {
                    if (sub.DifferenceType != SchemaCompareDifferenceType.None)
                        count++;
                }
            }
        }
        return count;
    }

    private static string GetTypeDisplayName(DatabaseObjectType type) => type switch
    {
        DatabaseObjectType.Table => "表",
        DatabaseObjectType.View => "视图",
        DatabaseObjectType.Type => "类型",
        DatabaseObjectType.Function => "函数",
        DatabaseObjectType.Procedure => "存储过程",
        DatabaseObjectType.Column => "列",
        DatabaseObjectType.Trigger => "触发器",
        DatabaseObjectType.PrimaryKey => "主键",
        DatabaseObjectType.ForeignKey => "外键",
        DatabaseObjectType.Index => "索引",
        DatabaseObjectType.Constraint => "约束",
        DatabaseObjectType.Sequence => "序列",
        _ => type.ToString(),
    };

    private static bool IsSameDatabase(ConnectionItem a, ConnectionItem b)
        => string.Equals(a.DatabaseType, b.DatabaseType, StringComparison.OrdinalIgnoreCase)
           && string.Equals(a.Server, b.Server, StringComparison.OrdinalIgnoreCase)
           && string.Equals(a.Port, b.Port, StringComparison.OrdinalIgnoreCase)
           && string.Equals(a.Database, b.Database, StringComparison.OrdinalIgnoreCase)
           && string.Equals(a.UserId ?? string.Empty, b.UserId ?? string.Empty, StringComparison.OrdinalIgnoreCase)
           && string.Equals(a.IntegratedSecurity ? "1" : "0", b.IntegratedSecurity ? "1" : "0", StringComparison.OrdinalIgnoreCase);

    private static DatabaseType ParseDatabaseType(string databaseType)
        => Enum.TryParse<DatabaseType>(databaseType, true, out var type) ? type : DatabaseType.Unknown;

    // ---------- 数据对比（Data Compare） ----------

    public async Task<IReadOnlyList<TableItem>> GetTablesAsync(
        ConnectionItem connection,
        CancellationToken cancellationToken = default)
    {
        var dbType = ParseDatabaseType(connection.DatabaseType);
        if (dbType == DatabaseType.Unknown || string.IsNullOrEmpty(connection.Database))
        {
            throw new InvalidOperationException("连接或数据库无效。");
        }

        var interpreter = CreateDataInterpreter(connection, dbType);

        var schemaInfo = await interpreter.GetSchemaInfoAsync(new SchemaInfoFilter
        {
            DatabaseObjectType = DatabaseObjectType.Table,
        });

        return schemaInfo.Tables
            .OrderBy(t => t.Schema)
            .ThenBy(t => t.Name)
            .Select(t => new TableItem(t.Name, t.Schema, FormatTableName(t)))
            .ToList();
    }

    public async Task<IReadOnlyList<DataCompareResultItem>> CompareDataAsync(
        ConnectionItem source,
        ConnectionItem target,
        IReadOnlyList<string> tableNames,
        DataCompareDisplayMode displayMode = DataCompareDisplayMode.None,
        Action<string>? onFeedback = null,
        CancellationToken cancellationToken = default)
    {
        onFeedback?.Invoke("正在校验源/目标连接...");

        if (string.IsNullOrEmpty(source.Database) || string.IsNullOrEmpty(target.Database))
        {
            onFeedback?.Invoke("错误：源/目标数据库不能为空。");
            return Array.Empty<DataCompareResultItem>();
        }

        var sourceDbType = ParseDatabaseType(source.DatabaseType);
        var targetDbType = ParseDatabaseType(target.DatabaseType);

        if (sourceDbType == DatabaseType.Unknown || targetDbType == DatabaseType.Unknown)
        {
            onFeedback?.Invoke("错误：源/目标数据库类型无效。");
            return Array.Empty<DataCompareResultItem>();
        }

        if (sourceDbType != targetDbType)
        {
            onFeedback?.Invoke($"错误：数据对比要求源与目标数据库类型相同（当前：源={source.DatabaseType}，目标={target.DatabaseType}）。");
            return Array.Empty<DataCompareResultItem>();
        }

        if (IsSameDatabase(source, target))
        {
            onFeedback?.Invoke("错误：源数据库与目标数据库不能相同。");
            return Array.Empty<DataCompareResultItem>();
        }

        if (tableNames is null || tableNames.Count == 0)
        {
            onFeedback?.Invoke("错误：请至少选择一张表进行对比。");
            return Array.Empty<DataCompareResultItem>();
        }

        try
        {
            var sourceInterpreter = CreateDataInterpreter(source, sourceDbType);
            var targetInterpreter = CreateDataInterpreter(target, targetDbType);

            // 构造含所选表的 SchemaInfo（同时加载列/主键信息）。
            var schemaInfo = new SchemaInfo();
            var sourceFilter = new SchemaInfoFilter
            {
                DatabaseObjectType = DatabaseObjectType.Table | DatabaseObjectType.Column | DatabaseObjectType.PrimaryKey,
                TableNames = tableNames.ToArray(),
            };
            var sourceSchemaInfo = await sourceInterpreter.GetSchemaInfoAsync(sourceFilter);

            // 过滤无主键的表（无主键会触发全表笛卡儿扫描，风险很高），提前通知用户。
            var tablesWithPk = new List<Table>();
            foreach (var table in sourceSchemaInfo.Tables)
            {
                var hasPk = sourceSchemaInfo.TablePrimaryKeys.Any(pk =>
                    string.Equals(pk.TableName, table.Name, StringComparison.OrdinalIgnoreCase)
                    && (string.IsNullOrEmpty(table.Schema) || string.Equals(pk.Schema, table.Schema, StringComparison.OrdinalIgnoreCase)));
                if (hasPk)
                {
                    tablesWithPk.Add(table);
                }
                else
                {
                    var display = string.IsNullOrEmpty(table.Schema) ? table.Name : $"{table.Schema}.{table.Name}";
                    onFeedback?.Invoke($"警告：表 {display} 无主键，已跳过（无主键对比会触发全表扫描，性能风险高）。");
                }
            }

            if (tablesWithPk.Count == 0)
            {
                onFeedback?.Invoke("错误：所选表均无主键，无法执行安全的数据对比。");
                return Array.Empty<DataCompareResultItem>();
            }

            schemaInfo.Tables.AddRange(tablesWithPk);
            schemaInfo.TableColumns.AddRange(sourceSchemaInfo.TableColumns.Where(c =>
                tablesWithPk.Any(t =>
                    string.Equals(t.Name, c.TableName, StringComparison.OrdinalIgnoreCase)
                    && (string.IsNullOrEmpty(t.Schema) || string.Equals(t.Schema, c.Schema, StringComparison.OrdinalIgnoreCase)))));
            schemaInfo.TablePrimaryKeys.AddRange(sourceSchemaInfo.TablePrimaryKeys.Where(pk =>
                tablesWithPk.Any(t =>
                    string.Equals(t.Name, pk.TableName, StringComparison.OrdinalIgnoreCase)
                    && (string.IsNullOrEmpty(t.Schema) || string.Equals(t.Schema, pk.Schema, StringComparison.OrdinalIgnoreCase)))));

            onFeedback?.Invoke($"开始对比 {tablesWithPk.Count} 张有主键表的数据差异...");

            var dataCompare = new DataCompare(sourceInterpreter, targetInterpreter, schemaInfo, new DataCompareOption
            {
                DisplayMode = displayMode == DataCompareDisplayMode.None
                    ? DataCompareDisplayMode.Different | DataCompareDisplayMode.OnlyInSource | DataCompareDisplayMode.OnlyInTarget
                    : displayMode,
            });

            dataCompare.Subscribe(new FeedbackObserver(onFeedback));

            var result = await dataCompare.Compare(cancellationToken);

            onFeedback?.Invoke("对比完成。");

            return result.Details
                .OrderBy(d => d.Order)
                .Select(d => new DataCompareResultItem(d))
                .ToList();
        }
        catch (Exception ex)
        {
            onFeedback?.Invoke($"数据对比出现异常：{ex.Message}");
            return Array.Empty<DataCompareResultItem>();
        }
    }

    public async Task<(DataTable Data, Dictionary<int, List<DataCompareValueInfo>> ValueInfos)> GetTableDataAsync(
        ConnectionItem source,
        ConnectionItem target,
        DataCompareResultDetail detail,
        string category,
        int pageSize,
        long pageNumber,
        CancellationToken cancellationToken = default)
    {
        var sourceDbType = ParseDatabaseType(source.DatabaseType);
        var targetDbType = ParseDatabaseType(target.DatabaseType);
        var sourceInterpreter = CreateDataInterpreter(source, sourceDbType);
        var targetInterpreter = CreateDataInterpreter(target, targetDbType);

        // Different：展示源/目标两侧不同列值的对比表格。
        if (string.Equals(category, "Different", StringComparison.OrdinalIgnoreCase))
        {
            return await DataCompare.GetDifferentData(sourceInterpreter, targetInterpreter, detail, pageSize, pageNumber);
        }

        // 其余分类：从源/目标库分页读取关键行对应的完整数据。
        var rows = category switch
        {
            "OnlyInSource" => detail.OnlyInSourceKeyRows,
            "OnlyInTarget" => detail.OnlyInTargetKeyRows,
            "Identical" => detail.IdenticalKeyRows,
            _ => detail.IdenticalKeyRows,
        };

        var pagedKeyRows = DataCompare.GetPagedKeyRows(rows, pageSize, pageNumber);
        var whereCondition = DataCompare.GetKeyColumnWhereCondition(sourceInterpreter, pagedKeyRows, detail.KeyColumns);

        bool useSource = category switch
        {
            "OnlyInSource" or "Identical" => true,
            _ => false,
        };

        var interpreter = useSource ? sourceInterpreter : targetInterpreter;
        var table = useSource ? detail.SourceTable : detail.TargetTable;
        var columns = useSource ? detail.SourceTableColumns : detail.TargetTableColumns;

        var dataTable = await interpreter.GetPagedDataTableAsync(
            interpreter.CreateConnection(),
            table,
            columns,
            null,
            pageSize,
            pageNumber,
            cancellationToken,
            whereCondition);

        return (dataTable, new Dictionary<int, List<DataCompareValueInfo>>());
    }

    public async Task<string> GenerateSyncScriptsAsync(
        ConnectionItem source,
        ConnectionItem target,
        IReadOnlyList<DataCompareResultDetail> details,
        Action<string>? onFeedback = null,
        CancellationToken cancellationToken = default)
    {
        if (details is null || details.Count == 0)
        {
            return string.Empty;
        }

        var sourceDbType = ParseDatabaseType(source.DatabaseType);
        var targetDbType = ParseDatabaseType(target.DatabaseType);
        var sourceInterpreter = CreateDataInterpreter(source, sourceDbType);
        var targetInterpreter = CreateDataInterpreter(target, targetDbType);

        var dataCompare = new DataCompare(sourceInterpreter, targetInterpreter, new SchemaInfo());
        dataCompare.Subscribe(new FeedbackObserver(onFeedback));

        onFeedback?.Invoke("开始生成数据同步脚本...");
        var scripts = await dataCompare.GenerateScripts(details.ToList(), cancellationToken);
        onFeedback?.Invoke("脚本生成完成。");

        return scripts;
    }

    private static string FormatTableName(Table t)
        => string.IsNullOrEmpty(t.Schema) ? t.Name : $"{t.Schema}.{t.Name}";

    private static DbInterpreter CreateDataInterpreter(ConnectionItem connection, DatabaseType dbType)
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
            ObjectFetchMode = DatabaseObjectFetchMode.Details,
            ScriptOutputMode = GenerateScriptOutputMode.WriteToString,
            SortObjectsByReference = true,
            GetTableAllObjects = true,
            ThrowExceptionWhenErrorOccurs = true,
        };

        return DbInterpreterHelper.GetDbInterpreter(dbType, connectionInfo, option);
    }

    /// <summary>
    /// 对比反馈观察者：将 <see cref="FeedbackInfo"/> 消息转发到回调。
    /// </summary>
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

    private static DbInterpreter CreateInterpreter(ConnectionItem connection, DatabaseType dbType, DatabaseObjectType objectType)
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
            ObjectFetchMode = DatabaseObjectFetchMode.Details,
            ScriptOutputMode = GenerateScriptOutputMode.WriteToString,
            SortObjectsByReference = true,
            GetTableAllObjects = objectType.HasFlag(DatabaseObjectType.Table),
            ThrowExceptionWhenErrorOccurs = true,
        };

        return DbInterpreterHelper.GetDbInterpreter(dbType, connectionInfo, option);
    }
}
