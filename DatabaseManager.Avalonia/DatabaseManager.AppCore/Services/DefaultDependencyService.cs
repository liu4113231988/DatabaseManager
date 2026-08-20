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

            var nodes = usages
                .Select(u => new DependencyNode(
                    dependOnThis ? u.ObjectType : u.RefObjectType,
                    dependOnThis ? u.ObjectSchema : u.RefObjectSchema,
                    dependOnThis ? u.ObjectName : u.RefObjectName))
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
        return objectType switch
        {
            "Table" => new Table { Schema = schema, Name = objectName },
            "View" => new View { Schema = schema, Name = objectName },
            "Function" => new Function { Schema = schema, Name = objectName },
            "Procedure" => new Procedure { Schema = schema, Name = objectName },
            _ => throw new InvalidOperationException($"不支持的对象类型：{objectType}"),
        };
    }
}
