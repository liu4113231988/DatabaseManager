using DatabaseInterpreter.Core;
using DatabaseInterpreter.Model;
using DatabaseManager.AppCore.Models;
using DatabaseManager.Core;

namespace DatabaseManager.AppCore.Services;

/// <summary>
/// 依赖分析服务实现（阶段 4）。接入 <c>DatabaseManager.Core.DepencencyFetcher</c>。
/// </summary>
public class DefaultDependencyService : IDependencyService
{
    public Task<IReadOnlyList<DependencyNode>> FetchAsync(
        ConnectionItem connection,
        string objectType,
        string? schema,
        string objectName,
        bool dependOnThis,
        CancellationToken cancellationToken = default)
    {
        return Task.Run(async () =>
        {
            var dbType = ConnectionHelper.ParseDatabaseType(connection.DatabaseType);
            if (dbType == DatabaseType.Unknown || string.IsNullOrEmpty(connection.Database))
            {
                throw new InvalidOperationException("连接或数据库无效。");
            }

            var interpreter = DbInterpreterHelper.GetDbInterpreter(
                dbType, ConnectionHelper.ToConnectionInfo(connection),
                new DbInterpreterOption { ObjectFetchMode = DatabaseObjectFetchMode.Simple });

            var dbObject = CreateDatabaseObject(objectType, schema, objectName);

            var fetcher = new DepencencyFetcher(interpreter);

            var usages = await fetcher.Fetch(dbObject, dependOnThis);

            // 语义修正：
            // dependOnThis = true  → 查询「哪些对象依赖于此对象」，结果展示引用方（RefObject 侧）。
            // dependOnThis = false → 查询「此对象依赖于哪些对象」，结果展示被引用方（Object 侧）。
            var nodes = usages
                .Select(u => new DependencyNode(
                    dependOnThis ? u.RefObjectType : u.ObjectType,
                    dependOnThis ? u.RefObjectSchema : u.ObjectSchema,
                    dependOnThis ? u.RefObjectName : u.ObjectName))
                .Where(n => !string.IsNullOrWhiteSpace(n.ObjectName))
                .GroupBy(n => (n.ObjectType, n.Schema, n.ObjectName))
                .Select(g => g.First())
                .OrderBy(n => n.ObjectType)
                .ThenBy(n => n.DisplayName)
                .ToList();

            return (IReadOnlyList<DependencyNode>)nodes;
        }, cancellationToken);
    }

    private static DatabaseObject CreateDatabaseObject(string objectType, string? schema, string objectName)
    {
        if (string.IsNullOrWhiteSpace(objectType))
            throw new InvalidOperationException("对象类型不能为空。");

        // 大小写不敏感匹配，避免 UI 输入 "table"/"TABLE" 等变体导致失败。
        var comparer = StringComparer.OrdinalIgnoreCase;
        if (comparer.Equals(objectType, "Table"))
            return new Table { Schema = schema, Name = objectName };
        if (comparer.Equals(objectType, "View"))
            return new View { Schema = schema, Name = objectName };
        if (comparer.Equals(objectType, "Function"))
            return new Function { Schema = schema, Name = objectName };
        if (comparer.Equals(objectType, "Procedure"))
            return new Procedure { Schema = schema, Name = objectName };

        throw new InvalidOperationException($"不支持的对象类型：{objectType}");
    }
}
