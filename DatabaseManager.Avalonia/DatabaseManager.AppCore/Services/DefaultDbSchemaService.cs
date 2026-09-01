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

        var interpreter = CreateInterpreter(connection, useConnectionDatabase: false);

        var databases = (await interpreter.GetDatabasesAsync().WaitAsync(cancellationToken)).OrderBy(d => d.Name).ToList();
        var result = new List<DbObjectTreeNode>();

        // 并行枚举各库的 schema 列表，避免多库实例（如 SQL Server 几十个库）连接时串行 N+1 查询过慢。
        // 并发度限制为 4：几十个库时避免连接风暴；说明：SQL Server / Postgres 分支在 TryGetSchemasAsync
        // 内部会用目标库自己的解释器查询；Oracle 分支复用默认解释器（覆盖 Database 会破坏服务名连接串）。
        using var schemaSemaphore = new SemaphoreSlim(4);
        var schemaLists = await Task.WhenAll(
            databases.Select(db => Task.Run(async () =>
            {
                await schemaSemaphore.WaitAsync(cancellationToken);
                try
                {
                    return await TryGetSchemasAsync(connection, interpreter, db.Name);
                }
                finally
                {
                    schemaSemaphore.Release();
                }
            }, cancellationToken)));

        for (int i = 0; i < databases.Count; i++)
        {
            var db = databases[i];
            var schemas = schemaLists[i];

            var dbNode = new DbObjectTreeNode
            {
                Name = db.Name,
                Text = db.Name,
                NodeType = DbObjectTreeNodeType.Database,
                DatabaseObjectType = DatabaseObjectType.None,
                DatabaseName = db.Name,
                DbObject = db,
            };

            // 判断是否为多 Schema 结构（SQL Server/Postgres/Oracle 等），结果来自并行枚举。
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
                // 单 Schema：若恰好能枚举出唯一 schema（如 SQL Server 的 dbo、Oracle 当前用户），
                // 将其作为过滤条件传入，避免表查询混入其他 schema 的对象；
                // MySQL/SQLite 无 schema 概念时 TryGetSchemasAsync 返回空，schema 保持 null。
                string? singleSchema = schemas.Count == 1 ? schemas[0].Name : null;
                AddTypeFolders(dbNode, interpreter, db.Name, singleSchema);
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
        // 解释器层未全部透传取消令牌，这里用 WaitAsync 保证取消时等待立即中断。
        switch (objectType)
        {
            case DatabaseObjectType.Table:
                var tables = await interpreter.GetTablesAsync(filter).WaitAsync(cancellationToken);
                nodes.AddRange(tables.OrderBy(t => t.Name).Select(t => ToNode(t, databaseName, schema)));
                break;
            case DatabaseObjectType.View:
                var views = await interpreter.GetViewsAsync(filter).WaitAsync(cancellationToken);
                nodes.AddRange(views.OrderBy(v => v.Name).Select(v => ToNode(v, databaseName, schema)));
                break;
            case DatabaseObjectType.Procedure:
                var procedures = await interpreter.GetProceduresAsync(filter).WaitAsync(cancellationToken);
                nodes.AddRange(procedures.OrderBy(p => p.Name).Select(p => ToNode(p, databaseName, schema)));
                break;
            case DatabaseObjectType.Function:
                var functions = await interpreter.GetFunctionsAsync(filter).WaitAsync(cancellationToken);
                nodes.AddRange(functions.OrderBy(f => f.Name).Select(f => ToNode(f, databaseName, schema)));
                break;
            case DatabaseObjectType.Sequence:
                var sequences = await interpreter.GetSequencesAsync(filter).WaitAsync(cancellationToken);
                nodes.AddRange(sequences.OrderBy(s => s.Name).Select(s => ToNode(s, databaseName, schema)));
                break;
            case DatabaseObjectType.Type:
                var types = await interpreter.GetUserDefinedTypesAsync(filter).WaitAsync(cancellationToken);
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

    /// <inheritdoc cref="IDbSchemaService.SearchMetadataAsync" />
    public async Task<IReadOnlyList<SearchResultItem>> SearchMetadataAsync(
        string connectionName,
        string keyword,
        int limitPerKind = 100,
        string? databaseName = null,
        string? schema = null,
        CancellationToken cancellationToken = default)
    {
        var result = new List<SearchResultItem>();
        keyword = keyword?.Trim() ?? string.Empty;
        if (keyword.Length == 0)
            return result;

        var connection = FindConnection(connectionName);
        if (connection is null)
            return result;

        // 未指定范围时枚举该连接下的数据库；指定时严格限制到调用方所选数据库。
        var databaseNames = await GetDatabaseNamesForSearchAsync(connection, databaseName);
        var counters = new Dictionary<SearchObjectKind, int>();

        foreach (var dbName in databaseNames)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // 全部类别都达到上限时提前结束。
            if (counters.Count >= 6 && counters.Values.All(c => c >= limitPerKind))
                break;

            DbInterpreter interpreter;
            try
            {
                interpreter = CreateInterpreter(connection, dbName);
            }
            catch
            {
                continue;
            }

            // 表 / 视图 / 过程 / 函数 / 序列：取全量后内存模糊过滤（跨库一致，不依赖方言 SQL）。
            var objectFilter = new SchemaInfoFilter { Schema = schema };
            var matchedTables = await SafeFetchAsync(
                () => interpreter.GetTablesAsync(objectFilter),
                item => MatchesKeyword(item.Name, keyword),
                limitPerKind);

            foreach (var t in matchedTables)
            {
                AddResult(result, counters, SearchObjectKind.Table,
                    connection.Name, dbName, t.Schema, t.Name, null, limitPerKind);
            }

            var views = await SafeFetchAsync(
                () => interpreter.GetViewsAsync(objectFilter),
                item => MatchesKeyword(item.Name, keyword),
                limitPerKind);

            foreach (var v in views)
            {
                AddResult(result, counters, SearchObjectKind.View,
                    connection.Name, dbName, v.Schema, v.Name, null, limitPerKind);
            }

            if (interpreter.SupportDbObjectType.HasFlag(DatabaseObjectType.Procedure))
            {
                var procedures = await SafeFetchAsync(
                    () => interpreter.GetProceduresAsync(objectFilter),
                    item => MatchesKeyword(item.Name, keyword),
                    limitPerKind);

                foreach (var p in procedures)
                {
                    AddResult(result, counters, SearchObjectKind.Procedure,
                        connection.Name, dbName, p.Schema, p.Name, null, limitPerKind);
                }
            }

            if (interpreter.SupportDbObjectType.HasFlag(DatabaseObjectType.Function))
            {
                var functions = await SafeFetchAsync(
                    () => interpreter.GetFunctionsAsync(objectFilter),
                    item => MatchesKeyword(item.Name, keyword),
                    limitPerKind);

                foreach (var f in functions)
                {
                    AddResult(result, counters, SearchObjectKind.Function,
                        connection.Name, dbName, f.Schema, f.Name, null, limitPerKind);
                }
            }

            if (interpreter.SupportDbObjectType.HasFlag(DatabaseObjectType.Sequence))
            {
                var sequences = await SafeFetchAsync(
                    () => interpreter.GetSequencesAsync(objectFilter),
                    item => MatchesKeyword(item.Name, keyword),
                    limitPerKind);

                foreach (var s in sequences)
                {
                    AddResult(result, counters, SearchObjectKind.Sequence,
                        connection.Name, dbName, s.Schema, s.Name, null, limitPerKind);
                }
            }

            // 列名和表名是独立条件。必须扫描当前范围内的全部列，不能以表名是否命中
            // 作为前提，否则搜索 "id" 会遗漏 users.id、orders.id 等绝大多数结果。
            try
            {
                var columns = await SafeFetchAsync(
                    () => interpreter.GetTableColumnsAsync(new SchemaInfoFilter { Schema = schema }),
                    column => MatchesKeyword(column.Name, keyword),
                    limitPerKind);

                foreach (var column in columns)
                {
                    AddResult(result, counters, SearchObjectKind.Column,
                        connection.Name, dbName, column.Schema,
                        column.Name, column.TableName, limitPerKind);
                }
            }
            catch
            {
                // 列搜索失败不影响其他结果。
            }
        }

        return result;
    }

    /// <summary>枚举参与搜索的数据库列表：目标库优先，其余按名称排序。</summary>
    private async Task<List<string>> GetDatabaseNamesForSearchAsync(ConnectionItem connection, string? databaseName)
    {
        if (!string.IsNullOrWhiteSpace(databaseName))
            return new List<string> { databaseName };

        var names = new List<string>();
        var targetDb = connection.Database;

        try
        {
            var interpreter = CreateInterpreter(connection, useConnectionDatabase: false);
            var databases = await interpreter.GetDatabasesAsync();
            names.AddRange(databases.Select(d => d.Name).Where(n => !string.IsNullOrEmpty(n)));
        }
        catch
        {
            // 枚举失败则退化为仅搜索连接配置中的目标库。
        }

        if (!string.IsNullOrEmpty(targetDb) && !names.Contains(targetDb, StringComparer.OrdinalIgnoreCase))
        {
            names.Insert(0, targetDb);
        }
        else if (!string.IsNullOrEmpty(targetDb))
        {
            names.Remove(targetDb);
            names.Insert(0, targetDb);
        }

        return names.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>安全获取对象列表：异常时返回空集合，避免单类型失败中断整个搜索。</summary>
    private static async Task<List<T>> SafeFetchAsync<T>(
        Func<Task<List<T>>> fetch,
        Func<T, bool>? predicate = null,
        int limit = int.MaxValue)
    {
        try
        {
            var items = await fetch() ?? Enumerable.Empty<T>().ToList();
            IEnumerable<T> query = items;

            if (predicate is not null)
            {
                query = items.Where(predicate);
            }

            return query.Take(limit).ToList();
        }
        catch
        {
            return new List<T>();
        }
    }

    /// <summary>不区分大小写的包含匹配。</summary>
    private static bool MatchesKeyword(string? name, string keyword)
        => !string.IsNullOrEmpty(name)
           && name.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0;

    /// <summary>添加一条搜索结果并累加对应类别的计数。</summary>
    private static void AddResult(
        List<SearchResultItem> result,
        Dictionary<SearchObjectKind, int> counters,
        SearchObjectKind kind,
        string connectionName,
        string databaseName,
        string? schema,
        string name,
        string? parentName,
        int limitPerKind)
    {
        counters.TryGetValue(kind, out var count);
        if (count >= limitPerKind)
            return;

        counters[kind] = count + 1;
        result.Add(new SearchResultItem
        {
            Kind = kind,
            ConnectionName = connectionName,
            DatabaseName = databaseName,
            Schema = schema,
            Name = name,
            ParentName = parentName,
        });
    }

    /// <summary>在父节点下添加类型文件夹（Tables / Views / Procedures 等）。</summary>
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
            // 仅对支持多 Schema 的数据库（SQL Server/Postgres/KingbaseES/Oracle）枚举；其余返回空。
            if (connection.DatabaseType is "SqlServer" or "Postgres" or "KingbaseES")
            {
                // SQL Server、Postgres 与 KingbaseES 的 Schema 是每个数据库独立的，需用目标库自己的解释器查询（避免跨库复用默认库的 schema）。
                // Oracle 的 Schema 即当前用户，且覆盖 Database 会破坏 Oracle 连接串（服务名），故用默认解释器。
                var dbInterpreter = CreateInterpreter(connection, databaseName);
                return await dbInterpreter.GetDatabaseSchemasAsync();
            }
            if (connection.DatabaseType is "Oracle")
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
        // 视图没有索引/主键/外键/约束，仅保留 Columns，避免展开时以视图名执行无效查询。
        if (dbObject is Table or View)
        {
            AddTableChildFolders(node, isView: dbObject is View);
        }

        return node;
    }

    /// <summary>
    /// 添加表/视图子文件夹（带占位子节点以显示展开箭头，点击展开时懒加载）。
    /// 视图仅保留 Columns；表才有 Triggers/Indexes/Keys/Constraints。
    /// </summary>
    private static void AddTableChildFolders(DbObjectTreeNode parent, bool isView)
    {
        AddChildFolder(parent, "Columns", DatabaseObjectType.Column);

        if (isView)
            return;

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

    private DbInterpreter CreateInterpreter(ConnectionItem connection, string? databaseOverride = null, bool useConnectionDatabase = true)
    {
        var dbType = ParseDatabaseType(connection.DatabaseType);

        var connectionInfo = new ConnectionInfo
        {
            Server = connection.Server,
            Port = connection.Port,
            ServerVersion = connection.ServerVersion,
            Database = useConnectionDatabase ? (string.IsNullOrEmpty(databaseOverride) ? connection.Database : databaseOverride) : null,
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
