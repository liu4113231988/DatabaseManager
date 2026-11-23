using DatabaseInterpreter.Core;
using DatabaseInterpreter.Model;
using DatabaseManager.AppCore.Models;

namespace DatabaseManager.AppCore.Services;

/// <summary>
/// 管理型只读目录：PostgreSQL/KingbaseES 扩展、Oracle/DM 表空间、SQL Server 分区方案与分区函数。
/// 与 <see cref="DbPrincipalCatalog"/> 同模式，仅读取目录并返回树节点，不生成或修改对象。
/// </summary>
internal static class DbAdminCatalog
{
    /// <summary>按连接层级创建只读目录文件夹；连接级与数据库级分别由调用方传入 databaseName。</summary>
    internal static IEnumerable<DbObjectTreeNode> CreateFolders(ConnectionItem connection, string? databaseName)
    {
        var dbType = ConnectionHelper.ParseDatabaseType(connection.DatabaseType);
        (DbCatalogKind Kind, string Label)[] folders = (dbType, databaseName is not null) switch
        {
#if !DATABASE_MANAGER_AOT
            (DatabaseType.KingbaseES, false) when KingbaseCompatibilityModes.GetAdminFeatureBlockReason(connection.KingbaseCompatibilityMode) is null
                => [(DbCatalogKind.Extension, "扩展")],
#endif
            (DatabaseType.Postgres, false) => [(DbCatalogKind.Extension, "扩展")],
            (DatabaseType.Oracle, true) => [(DbCatalogKind.Tablespace, "表空间")],
#if !DATABASE_MANAGER_AOT
            (DatabaseType.DM, true) => [(DbCatalogKind.Tablespace, "表空间")],
#endif
            (DatabaseType.SqlServer, true) =>
            [
                (DbCatalogKind.PartitionScheme, "分区方案"),
                (DbCatalogKind.PartitionFunction, "分区函数"),
            ],
            _ => [],
        };

        foreach (var (kind, label) in folders)
        {
            var folder = new DbObjectTreeNode
            {
                Name = label,
                Text = label,
                NodeType = DbObjectTreeNodeType.CatalogFolder,
                CatalogKind = kind,
                DatabaseName = databaseName,
            };
            folder.AddChild(new DbObjectTreeNode { IsPlaceholder = true });
            yield return folder;
        }
    }

    /// <summary>返回目录查询 SQL；dbaScope 用于 Oracle 需要 DBA_* 视图的场景。</summary>
    internal static string GetSql(DatabaseType dbType, DbCatalogKind kind, bool dbaScope = false) => (dbType, kind) switch
    {
        (DatabaseType.Postgres or DatabaseType.KingbaseES, DbCatalogKind.Extension) =>
            "SELECT e.extname, e.extversion, n.nspname FROM pg_catalog.pg_extension e "
            + "JOIN pg_catalog.pg_namespace n ON n.oid = e.extnamespace ORDER BY e.extname",
        (DatabaseType.Oracle, DbCatalogKind.Tablespace) =>
            $"SELECT TABLESPACE_NAME, STATUS, CONTENTS, BIGFILE FROM {(dbaScope ? "DBA_TABLESPACES" : "USER_TABLESPACES")} ORDER BY TABLESPACE_NAME",
        (DatabaseType.DM, DbCatalogKind.Tablespace) =>
            "SELECT TABLESPACE_NAME FROM USER_TABLESPACES ORDER BY TABLESPACE_NAME",
        (DatabaseType.SqlServer, DbCatalogKind.PartitionScheme) =>
            "SELECT s.name, f.name, (SELECT COUNT(*) FROM sys.destination_data_spaces d "
            + "WHERE d.partition_scheme_id = s.data_space_id) FROM sys.partition_schemes s "
            + "JOIN sys.partition_functions f ON f.function_id = s.function_id ORDER BY s.name",
        (DatabaseType.SqlServer, DbCatalogKind.PartitionFunction) =>
            "SELECT f.name, f.type, (SELECT COUNT(*) FROM sys.partition_range_values v "
            + "WHERE v.function_id = f.function_id) FROM sys.partition_functions f ORDER BY f.name",
        _ => throw new NotSupportedException("该目录在当前数据库类型中不可用。"),
    };

    /// <summary>把目录行转换为只读节点；列数按方言差异做容错。</summary>
    internal static DbObjectTreeNode CreateNode(string?[] row, DbCatalogKind kind)
    {
        string name = row[0] ?? string.Empty;
        string details = kind switch
        {
            DbCatalogKind.Extension => row.Length >= 3
                ? $"版本 {row[1] ?? string.Empty} · Schema {row[2] ?? string.Empty}"
                : string.Empty,
            DbCatalogKind.Tablespace => row.Length >= 4
                ? $"{row[1] ?? string.Empty} · {row[2] ?? string.Empty} · BIGFILE={row[3] ?? string.Empty}"
                : string.Empty,
            DbCatalogKind.PartitionScheme => row.Length >= 3
                ? $"函数 {row[1] ?? string.Empty} · 分区数 {row[2] ?? string.Empty}"
                : string.Empty,
            DbCatalogKind.PartitionFunction => row.Length >= 3
                ? $"{(string.Equals(row[1], "R", StringComparison.OrdinalIgnoreCase) ? "RANGE" : string.Equals(row[1], "L", StringComparison.OrdinalIgnoreCase) ? "LIST" : row[1] ?? string.Empty)} · 边界数 {row[2] ?? string.Empty}"
                : string.Empty,
            _ => string.Empty,
        };

        return new DbObjectTreeNode
        {
            Name = name,
            Text = name,
            NodeType = DbObjectTreeNodeType.CatalogObject,
            CatalogKind = kind,
            CatalogDetails = details.Trim(),
            IsLoaded = true,
        };
    }

    /// <summary>执行目录查询并返回节点；失败时返回带权限提示的错误文本。</summary>
    internal static async Task<(List<DbObjectTreeNode> Nodes, string? Error)> ReadAsync(
        ConnectionItem connection,
        string? databaseName,
        DbCatalogKind kind,
        CancellationToken cancellationToken = default)
    {
        var dbType = ConnectionHelper.ParseDatabaseType(connection.DatabaseType);
        if (dbType == DatabaseType.KingbaseES &&
            KingbaseCompatibilityModes.GetAdminFeatureBlockReason(connection.KingbaseCompatibilityMode) is { } modeReason)
        {
            return (new List<DbObjectTreeNode>(), modeReason);
        }

        var connectionInfo = ConnectionHelper.ToConnectionInfo(connection);
        if (!ConnectionHelper.IsFileDatabase(dbType) && !string.IsNullOrEmpty(databaseName))
            connectionInfo.Database = databaseName;

        var interpreter = DbInterpreterHelper.GetDbInterpreter(dbType, connectionInfo, new DbInterpreterOption());
        string sql = GetSql(dbType, kind);
        try
        {
            var rows = await DefaultDbUserService.ExecuteReaderAsync(interpreter, sql, cancellationToken);
            return (rows.Where(r => r.Length > 0 && !string.IsNullOrEmpty(r[0]))
                        .Select(r => CreateNode(r, kind)).ToList(), null);
        }
        catch (Exception ex) when (dbType == DatabaseType.Oracle && kind == DbCatalogKind.Tablespace && !cancellationToken.IsCancellationRequested)
        {
            // USER_TABLESPACES 只返回当前用户有配额的表空间；尝试 DBA_* 读取全量。
            try
            {
                var rows = await DefaultDbUserService.ExecuteReaderAsync(
                    interpreter, GetSql(dbType, kind, dbaScope: true), cancellationToken);
                return (rows.Where(r => r.Length > 0 && !string.IsNullOrEmpty(r[0]))
                            .Select(r => CreateNode(r, kind)).ToList(), null);
            }
            catch (Exception fallbackEx)
            {
                return (new List<DbObjectTreeNode>(),
                    $"USER_TABLESPACES 读取失败：{ex.Message}；DBA_TABLESPACES 回退失败：{fallbackEx.Message}{Environment.NewLine}权限提示：{DbAdminGuidance.GetUserPermissionHint(dbType)}");
            }
        }
        catch (Exception ex)
        {
            return (new List<DbObjectTreeNode>(),
                $"{ex.Message}{Environment.NewLine}权限提示：{DbAdminGuidance.GetUserPermissionHint(dbType)}");
        }
    }
}
