using DatabaseInterpreter.Core;
using DatabaseInterpreter.Model;
using DatabaseManager.AppCore.Models;

namespace DatabaseManager.AppCore.Services;

/// <summary>
/// 基于 <see cref="DbInterpreter"/> 的 Schema 服务实现。
/// 阶段 2：真正接入核心引擎，提供对象树（数据库 → Schema → 类型 → 对象）的浏览能力。
/// </summary>
public class DefaultDbSchemaService : IDbSchemaService
{
    private readonly IDbConnectionService _connectionService;

    public DefaultDbSchemaService(IDbConnectionService connectionService)
    {
        _connectionService = connectionService;
    }

    public IReadOnlyList<string> GetSupportedDatabaseTypes()
        => Enum.GetValues<DatabaseType>()
               .Where(t => t != DatabaseType.Unknown)
               .Select(t => t.ToString())
               .ToList();

    public async Task<IReadOnlyList<DbObjectTreeNode>> GetObjectTreeAsync(string connectionName, CancellationToken cancellationToken = default)
    {
        var connection = FindConnection(connectionName);
        if (connection is null)
            return new List<DbObjectTreeNode>();

        var interpreter = CreateInterpreter(connection);

        var databases = await interpreter.GetDatabasesAsync();
        var result = new List<DbObjectTreeNode>();

        foreach (var db in databases.OrderBy(d => d.Name))
        {
            var dbNode = new DbObjectTreeNode
            {
                Name = db.Name,
                Text = db.Name,
                NodeType = DbObjectTreeNodeType.Database,
                DbObject = db,
            };

            // 类型文件夹（阶段 2：表/视图/存储过程/函数/序列）
            foreach (var folder in GetTypeFolders())
            {
                var folderNode = new DbObjectTreeNode
                {
                    Name = folder.Key,
                    Text = folder.Key,
                    NodeType = DbObjectTreeNodeType.Folder,
                    DatabaseObjectType = folder.Value,
                };
                dbNode.AddChild(folderNode);
            }

            result.Add(dbNode);
        }

        return result;
    }

    public async Task<IReadOnlyList<DbObjectTreeNode>> GetDbObjectNodesAsync(
        string connectionName,
        string databaseName,
        DatabaseObjectType objectType,
        string? schema = null,
        CancellationToken cancellationToken = default)
    {
        var connection = FindConnection(connectionName);
        if (connection is null)
            return new List<DbObjectTreeNode>();

        var interpreter = CreateInterpreter(connection, databaseName);

        var filter = new SchemaInfoFilter
        {
            Schema = schema,
            DatabaseObjectType = objectType,
        };

        List<DbObjectTreeNode> nodes = new();
        if (objectType == DatabaseObjectType.Table)
        {
            var tables = await interpreter.GetTablesAsync(filter);
            nodes.AddRange(tables.OrderBy(t => t.Name).Select(t => ToNode(t)));
        }
        else if (objectType == DatabaseObjectType.View)
        {
            var views = await interpreter.GetViewsAsync(filter);
            nodes.AddRange(views.OrderBy(v => v.Name).Select(v => ToNode(v)));
        }
        else if (objectType == DatabaseObjectType.Procedure)
        {
            var procedures = await interpreter.GetProceduresAsync(filter);
            nodes.AddRange(procedures.OrderBy(p => p.Name).Select(p => ToNode(p)));
        }
        else if (objectType == DatabaseObjectType.Function)
        {
            var functions = await interpreter.GetFunctionsAsync(filter);
            nodes.AddRange(functions.OrderBy(f => f.Name).Select(f => ToNode(f)));
        }
        else if (objectType == DatabaseObjectType.Sequence)
        {
            var sequences = await interpreter.GetSequencesAsync(filter);
            nodes.AddRange(sequences.OrderBy(s => s.Name).Select(s => ToNode(s)));
        }

        return nodes;
    }

    /// <summary>创建数据库解释器（带目标数据库）。</summary>
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
        };

        return DbInterpreterHelper.GetDbInterpreter(dbType, connectionInfo, option);
    }

    private ConnectionItem? FindConnection(string connectionName)
        => _connectionService.GetConnections().FirstOrDefault(c =>
            string.Equals(c.Name, connectionName, StringComparison.OrdinalIgnoreCase));

    private static DbObjectTreeNode ToNode(DatabaseObject dbObject)
        => new()
        {
            Name = dbObject.Name,
            Text = string.IsNullOrEmpty(dbObject.Schema) ? dbObject.Name : $"{dbObject.Schema}.{dbObject.Name}",
            NodeType = DbObjectTreeNodeType.DbObject,
            DatabaseObjectType = GetObjectType(dbObject),
            DbObject = dbObject,
        };

    private static DatabaseObjectType GetObjectType(DatabaseObject obj)
        => obj switch
        {
            Table => DatabaseObjectType.Table,
            View => DatabaseObjectType.View,
            Procedure => DatabaseObjectType.Procedure,
            Function => DatabaseObjectType.Function,
            Sequence => DatabaseObjectType.Sequence,
            _ => DatabaseObjectType.None,
        };

    private static IEnumerable<KeyValuePair<string, DatabaseObjectType>> GetTypeFolders()
    {
        yield return new("Tables", DatabaseObjectType.Table);
        yield return new("Views", DatabaseObjectType.View);
        yield return new("Procedures", DatabaseObjectType.Procedure);
        yield return new("Functions", DatabaseObjectType.Function);
        yield return new("Sequences", DatabaseObjectType.Sequence);
    }

    private static DatabaseType ParseDatabaseType(string databaseType)
    {
        if (Enum.TryParse<DatabaseType>(databaseType, true, out var type))
            return type;
        return DatabaseType.Unknown;
    }
}
