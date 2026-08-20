using DatabaseInterpreter.Core;
using DatabaseInterpreter.Model;
using DatabaseManager.AppCore.Models;

namespace DatabaseManager.AppCore.Services;

/// <summary>
/// 基于 <see cref="DbInterpreter"/> 的 Schema 服务实现。
/// 对象浏览功能：完整实现「连接 → 数据库 → Schema → 类型文件夹 → 对象 → 表/视图子对象」的懒加载层级。
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
                DatabaseObjectType = DatabaseObjectType.None,
                DatabaseName = db.Name,
                DbObject = db,
            };

            // 判断是否为多 Schema 结构（Oracle/Postgres 等）。
            var schemas = await TryGetSchemasAsync(connection, interpreter, db.Name);
            if (schemas.Count > 1)
            {
                foreach (var schema in schemas.OrderBy(s => s.Name))
                {
                    var schemaNode = new DbObjectTreeNode
                    {
                        Name = schema.Name,
                        Text = schema.Name,
                        NodeType = DbObjectTreeNodeType.Schema,
                        DatabaseObjectType = DatabaseObjectType.None,
                        DatabaseName = db.Name,
                        Schema = schema.Name,
                        DbObject = schema,
                    };
                    AddTypeFolders(schemaNode, interpreter, db.Name, schema.Name);
                    dbNode.AddChild(schemaNode);
                }
            }
            else
            {
                // 单 Schema（或无法枚举）直接挂类型文件夹。
                AddTypeFolders(dbNode, interpreter, db.Name, null);
            }

            result.Add(dbNode);
        }

        return result;
    }

    public async Task<bool> HasMultipleSchemasAsync(string connectionName, string databaseName, CancellationToken cancellationToken = default)
    {
        var connection = FindConnection(connectionName);
        if (connection is null)
            return false;

        var interpreter = CreateInterpreter(connection, databaseName);
        var schemas = await TryGetSchemasAsync(connection, interpreter, databaseName);
        return schemas.Count > 1;
    }

    public async Task<IReadOnlyList<DbObjectTreeNode>> GetSchemasAsync(string connectionName, string databaseName, CancellationToken cancellationToken = default)
    {
        var connection = FindConnection(connectionName);
        if (connection is null)
            return new List<DbObjectTreeNode>();

        var interpreter = CreateInterpreter(connection, databaseName);
        var schemas = await TryGetSchemasAsync(connection, interpreter, databaseName);

        return schemas.OrderBy(s => s.Name)
                      .Select(s => new DbObjectTreeNode
                      {
                          Name = s.Name,
                          Text = s.Name,
                          NodeType = DbObjectTreeNodeType.Schema,
                          DatabaseName = databaseName,
                          Schema = s.Name,
                          DbObject = s,
                      })
                      .ToList<DbObjectTreeNode>();
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
        switch (objectType)
        {
            case DatabaseObjectType.Table:
                var tables = await interpreter.GetTablesAsync(filter);
                nodes.AddRange(tables.OrderBy(t => t.Name).Select(t => ToNode(t, databaseName, schema)));
                break;
            case DatabaseObjectType.View:
                var views = await interpreter.GetViewsAsync(filter);
                nodes.AddRange(views.OrderBy(v => v.Name).Select(v => ToNode(v, databaseName, schema)));
                break;
            case DatabaseObjectType.Procedure:
                var procedures = await interpreter.GetProceduresAsync(filter);
                nodes.AddRange(procedures.OrderBy(p => p.Name).Select(p => ToNode(p, databaseName, schema)));
                break;
            case DatabaseObjectType.Function:
                var functions = await interpreter.GetFunctionsAsync(filter);
                nodes.AddRange(functions.OrderBy(f => f.Name).Select(f => ToNode(f, databaseName, schema)));
                break;
            case DatabaseObjectType.Sequence:
                var sequences = await interpreter.GetSequencesAsync(filter);
                nodes.AddRange(sequences.OrderBy(s => s.Name).Select(s => ToNode(s, databaseName, schema)));
                break;
            case DatabaseObjectType.Type:
                var types = await interpreter.GetUserDefinedTypesAsync(filter);
                nodes.AddRange(types.OrderBy(t => t.Name).Select(t => ToNode(t, databaseName, schema)));
                break;
        }

        return nodes;
    }

    public async Task<IReadOnlyList<DbObjectTreeNode>> GetTableChildNodesAsync(
        string connectionName,
        string databaseName,
        DbObjectChildType childFolderType,
        DatabaseObject tableOrView,
        bool isForView = false,
        CancellationToken cancellationToken = default)
    {
        var connection = FindConnection(connectionName);
        if (connection is null)
            return new List<DbObjectTreeNode>();

        var interpreter = CreateInterpreter(connection, databaseName);
        var schema = tableOrView.Schema;

        var filter = new SchemaInfoFilter
        {
            Strict = true,
            Schema = schema,
            TableNames = new[] { tableOrView.Name },
            DatabaseObjectType = childFolderType switch
            {
                DbObjectChildType.Column => DatabaseObjectType.Column,
                DbObjectChildType.Trigger => DatabaseObjectType.Trigger,
                DbObjectChildType.Index => DatabaseObjectType.Index,
                DbObjectChildType.PrimaryKey => DatabaseObjectType.PrimaryKey | DatabaseObjectType.ForeignKey,
                DbObjectChildType.Constraint => DatabaseObjectType.Constraint,
                _ => DatabaseObjectType.None,
            },
        };

        if (isForView)
        {
            filter.ColumnType = ColumnType.ViewColumn;
            filter.IsForView = true;
        }

        var schemaInfo = await interpreter.GetSchemaInfoAsync(filter);

        List<DbObjectTreeNode> nodes = new();

        switch (childFolderType)
        {
            case DbObjectChildType.Column:
                nodes.AddRange(schemaInfo.TableColumns
                    .OrderBy(c => c.Order)
                    .Select(c => CreateChildNode(c, c.Name, GetColumnText(c), DbObjectChildType.Column)));
                break;
            case DbObjectChildType.Trigger:
                nodes.AddRange(schemaInfo.TableTriggers
                    .OrderBy(t => t.Name)
                    .Select(t => CreateChildNode(t, t.Name, t.Name, DbObjectChildType.Trigger)));
                break;
            case DbObjectChildType.Index:
                nodes.AddRange(schemaInfo.TableIndexes
                    .OrderBy(i => i.Name)
                    .Select(i => CreateChildNode(i, i.Name, GetIndexText(i), DbObjectChildType.Index)));
                break;
            case DbObjectChildType.PrimaryKey:
                foreach (var pk in schemaInfo.TablePrimaryKeys.OrderBy(k => k.Name))
                {
                    string text = string.IsNullOrEmpty(pk.Name)
                        ? $"PK_{tableOrView.Name}(unnamed)"
                        : GetKeyText(pk);
                    nodes.Add(CreateChildNode(pk, string.IsNullOrEmpty(pk.Name) ? $"PK_{tableOrView.Name}" : pk.Name, text, DbObjectChildType.PrimaryKey));
                }
                foreach (var fk in schemaInfo.TableForeignKeys.OrderBy(k => k.Name))
                {
                    string text = string.IsNullOrEmpty(fk.Name)
                        ? $"FK_{tableOrView.Name}(unnamed)"
                        : GetForeignKeyText(fk);
                    nodes.Add(CreateChildNode(fk, string.IsNullOrEmpty(fk.Name) ? $"FK_{tableOrView.Name}" : fk.Name, text, DbObjectChildType.ForeignKey));
                }
                break;
            case DbObjectChildType.Constraint:
                nodes.AddRange(schemaInfo.TableConstraints
                    .OrderBy(c => c.Name)
                    .Select(c => CreateChildNode(c, c.Name, c.Name, DbObjectChildType.Constraint)));
                break;
        }

        return nodes;
    }

    private void AddTypeFolders(DbObjectTreeNode parent, DbInterpreter interpreter, string databaseName, string? schema)
    {
        var supported = interpreter.SupportDbObjectType;

        AddFolder(parent, "Tables", DatabaseObjectType.Table, databaseName, schema);
        AddFolder(parent, "Views", DatabaseObjectType.View, databaseName, schema);

        if (supported.HasFlag(DatabaseObjectType.Function))
            AddFolder(parent, "Functions", DatabaseObjectType.Function, databaseName, schema);

        if (supported.HasFlag(DatabaseObjectType.Procedure))
            AddFolder(parent, "Procedures", DatabaseObjectType.Procedure, databaseName, schema);

        if (supported.HasFlag(DatabaseObjectType.Type))
            AddFolder(parent, "Types", DatabaseObjectType.Type, databaseName, schema);

        if (supported.HasFlag(DatabaseObjectType.Sequence))
            AddFolder(parent, "Sequences", DatabaseObjectType.Sequence, databaseName, schema);
    }

    private static void AddFolder(DbObjectTreeNode parent, string text, DatabaseObjectType type, string databaseName, string? schema)
    {
        var folder = new DbObjectTreeNode
        {
            Name = text,
            Text = text,
            NodeType = DbObjectTreeNodeType.Folder,
            DatabaseObjectType = type,
            DatabaseName = databaseName,
            Schema = schema,
        };
        // 预置占位子节点以显示展开箭头，点击展开时再懒加载真实对象。
        folder.AddChild(new DbObjectTreeNode
        {
            Name = "_Placeholder_",
            Text = string.Empty,
            NodeType = DbObjectTreeNodeType.Folder,
            IsPlaceholder = true,
        });
        parent.AddChild(folder);
    }

    private async Task<List<DatabaseSchema>> TryGetSchemasAsync(ConnectionItem connection, DbInterpreter interpreter, string databaseName)
    {
        try
        {
            // 仅对支持多 Schema 的数据库（Oracle/Postgres）枚举；其余返回空。
            if (connection.DatabaseType is "Oracle" or "Postgres")
            {
                return await interpreter.GetDatabaseSchemasAsync();
            }
        }
        catch
        {
            // 忽略枚举失败，退化为单 Schema。
        }

        return new List<DatabaseSchema>();
    }

    private static DbObjectTreeNode ToNode(DatabaseObject dbObject, string databaseName, string? schema)
    {
        var node = new DbObjectTreeNode
        {
            Name = dbObject.Name,
            Text = string.IsNullOrEmpty(dbObject.Schema) ? dbObject.Name : $"{dbObject.Schema}.{dbObject.Name}",
            NodeType = DbObjectTreeNodeType.DbObject,
            DatabaseObjectType = GetObjectType(dbObject),
            DatabaseName = databaseName,
            Schema = dbObject.Schema ?? schema,
            DbObject = dbObject,
        };

        // 表/视图拥有可展开的子节点（列/索引/键/约束/触发器）。
        if (dbObject is Table or View)
        {
            AddTableChildFolders(node);
        }

        return node;
    }

    private static void AddTableChildFolders(DbObjectTreeNode parent)
    {
        AddChildFolder(parent, "Columns", DatabaseObjectType.Column);
        AddChildFolder(parent, "Triggers", DatabaseObjectType.Trigger);
        AddChildFolder(parent, "Indexes", DatabaseObjectType.Index);
        AddChildFolder(parent, "Keys", DatabaseObjectType.PrimaryKey);
        AddChildFolder(parent, "Constraints", DatabaseObjectType.Constraint);
    }

    /// <summary>添加表/视图子文件夹（带占位子节点以显示展开箭头，点击展开时懒加载）。</summary>
    private static void AddChildFolder(DbObjectTreeNode parent, string name, DatabaseObjectType type)
    {
        var folder = new DbObjectTreeNode
        {
            Name = name,
            Text = name,
            NodeType = DbObjectTreeNodeType.ChildFolder,
            DatabaseObjectType = type,
            DatabaseName = parent.DatabaseName,
            Schema = parent.Schema,
        };
        folder.AddChild(new DbObjectTreeNode
        {
            Name = "_Placeholder_",
            Text = string.Empty,
            NodeType = DbObjectTreeNodeType.ChildFolder,
            IsPlaceholder = true,
        });
        parent.AddChild(folder);
    }

    private static DbObjectTreeNode CreateChildNode(DatabaseObject obj, string name, string text, DbObjectChildType childType)
    {
        return new DbObjectTreeNode
        {
            Name = name,
            Text = text,
            NodeType = DbObjectTreeNodeType.ChildObject,
            DatabaseObjectType = childType switch
            {
                DbObjectChildType.Column => DatabaseObjectType.Column,
                DbObjectChildType.Trigger => DatabaseObjectType.Trigger,
                DbObjectChildType.Index => DatabaseObjectType.Index,
                DbObjectChildType.PrimaryKey => DatabaseObjectType.PrimaryKey,
                DbObjectChildType.ForeignKey => DatabaseObjectType.ForeignKey,
                DbObjectChildType.Constraint => DatabaseObjectType.Constraint,
                _ => DatabaseObjectType.None,
            },
            DbObject = obj,
            Schema = obj.Schema,
        };
    }

    /// <summary>生成表/视图列显示文本（名称 + 类型/可空等）。</summary>
    private static string GetColumnText(TableColumn column)
    {
        var sb = new System.Text.StringBuilder(column.Name);

        string dataType = column.DataType;
        if (!string.IsNullOrEmpty(column.DataTypeSchema))
        {
            dataType = $"{column.DataTypeSchema}.{dataType}";
        }

        sb.Append($" ({dataType}");

        if (column.IsNullable)
            sb.Append(", nullable");
        else
            sb.Append(", not null");

        if (column.IsIdentity)
            sb.Append(", identity");

        sb.Append(')');

        return sb.ToString();
    }

    private static string GetIndexText(TableIndex index)
    {
        string columns = string.Join(",", index.Columns.OrderBy(c => c.Order).Select(c => c.ColumnName));
        string content = index.Columns.Count > 0 ? (index.IsUnique ? $"(Unique, {columns})" : $"({columns})")
                                                 : (index.IsUnique ? "(Unique)" : "");
        return $"{index.Name}{content}";
    }

    private static string GetKeyText(TablePrimaryKey key)
    {
        string columns = string.Join(",", key.Columns.OrderBy(c => c.Order).Select(c => c.ColumnName));
        return $"{key.Name} ({columns})";
    }

    private static string GetForeignKeyText(TableForeignKey key)
    {
        var sb = new System.Text.StringBuilder(string.IsNullOrEmpty(key.Name) ? "FK" : key.Name);
        string columns = string.Join(",", key.Columns.OrderBy(c => c.Order).Select(c => c.ColumnName));
        string refColumns = string.Join(",", key.Columns.OrderBy(c => c.Order).Select(c => c.ReferencedColumnName));
        string refTable = string.IsNullOrEmpty(key.ReferencedSchema) ? key.ReferencedTableName : $"{key.ReferencedSchema}.{key.ReferencedTableName}";
        sb.Append($" ({columns}) → {refTable}({refColumns})");
        return sb.ToString();
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
        };

        return DbInterpreterHelper.GetDbInterpreter(dbType, connectionInfo, option);
    }

    private ConnectionItem? FindConnection(string connectionName)
        => _connectionService.GetConnections().FirstOrDefault(c =>
            string.Equals(c.Name, connectionName, StringComparison.OrdinalIgnoreCase));

    private static DatabaseObjectType GetObjectType(DatabaseObject obj)
        => obj switch
        {
            Table => DatabaseObjectType.Table,
            View => DatabaseObjectType.View,
            Procedure => DatabaseObjectType.Procedure,
            Function => DatabaseObjectType.Function,
            Sequence => DatabaseObjectType.Sequence,
            UserDefinedType => DatabaseObjectType.Type,
            _ => DatabaseObjectType.None,
        };

    private static DatabaseType ParseDatabaseType(string databaseType)
    {
        if (Enum.TryParse<DatabaseType>(databaseType, true, out var type))
            return type;
        return DatabaseType.Unknown;
    }
}
