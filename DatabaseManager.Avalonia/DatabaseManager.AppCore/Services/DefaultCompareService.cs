using DatabaseInterpreter.Core;
using DatabaseInterpreter.Model;
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
            throw new InvalidOperationException("源/目标数据库不能为空。");
        }

        var sourceDbType = ParseDatabaseType(source.DatabaseType);
        var targetDbType = ParseDatabaseType(target.DatabaseType);

        if (sourceDbType == DatabaseType.Unknown || targetDbType == DatabaseType.Unknown)
        {
            throw new InvalidOperationException("源/目标数据库类型无效。");
        }

        if (sourceDbType != targetDbType)
        {
            throw new InvalidOperationException("结构对比要求源与目标数据库类型相同。");
        }

        if (IsSameDatabase(source, target))
        {
            throw new InvalidOperationException("源数据库与目标数据库不能相同。");
        }

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
        => string.Equals(a.Server, b.Server, StringComparison.OrdinalIgnoreCase)
           && string.Equals(a.Port, b.Port, StringComparison.OrdinalIgnoreCase)
           && string.Equals(a.Database, b.Database, StringComparison.OrdinalIgnoreCase);

    private static DatabaseType ParseDatabaseType(string databaseType)
        => Enum.TryParse<DatabaseType>(databaseType, true, out var type) ? type : DatabaseType.Unknown;

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
