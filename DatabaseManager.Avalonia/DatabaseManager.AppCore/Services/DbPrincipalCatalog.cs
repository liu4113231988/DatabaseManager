using DatabaseInterpreter.Model;
using DatabaseManager.AppCore.Models;

namespace DatabaseManager.AppCore.Services;

internal static class DbPrincipalCatalog
{
    internal static IEnumerable<DbObjectTreeNode> CreateFolders(ConnectionItem connection, string? databaseName = null)
    {
        var databaseType = ConnectionHelper.ParseDatabaseType(connection.DatabaseType);
        (DbPrincipalKind Kind, string Text)[] folders = (databaseType, databaseName is not null) switch
        {
            (DatabaseType.SqlServer, true) => [(DbPrincipalKind.User, "数据库用户"), (DbPrincipalKind.Role, "数据库角色")],
            (DatabaseType.SqlServer, false) => [(DbPrincipalKind.Login, "登录名"), (DbPrincipalKind.Role, "服务器角色")],
            (DatabaseType.MySql, false) => [(DbPrincipalKind.Account, "账户与角色")],
            (DatabaseType.Oracle, false) => [(DbPrincipalKind.User, "用户"), (DbPrincipalKind.Role, "角色")],
            (DatabaseType.Postgres, false) => [(DbPrincipalKind.User, "可登录角色"), (DbPrincipalKind.Role, "不可登录角色")],
#if !DATABASE_MANAGER_AOT
            (DatabaseType.KingbaseES, false) when KingbaseCompatibilityModes.GetAdminFeatureBlockReason(connection.KingbaseCompatibilityMode) is null
                => [(DbPrincipalKind.User, "可登录角色"), (DbPrincipalKind.Role, "不可登录角色")],
#endif
            _ => [],
        };

        foreach (var (kind, label) in folders)
        {
            var folder = new DbObjectTreeNode
            {
                Name = label,
                Text = label,
                NodeType = DbObjectTreeNodeType.PrincipalFolder,
                PrincipalKind = kind,
                DatabaseName = databaseName,
            };
            folder.AddChild(new DbObjectTreeNode { IsPlaceholder = true });
            yield return folder;
        }
    }

    internal static string GetSql(DatabaseType databaseType, DbPrincipalKind kind, bool databaseScope, bool kingbaseFallback = false)
    {
        string loginPredicate = kind == DbPrincipalKind.User ? "rolcanlogin" : "NOT rolcanlogin";
        return (databaseType, kind, databaseScope) switch
        {
            (DatabaseType.SqlServer, DbPrincipalKind.Login, false) =>
                "SELECT name FROM sys.server_principals WHERE type IN ('S','U','G','C','K','E','X') ORDER BY name",
            (DatabaseType.SqlServer, DbPrincipalKind.Role, false) =>
                "SELECT name FROM sys.server_principals WHERE type = 'R' ORDER BY name",
            (DatabaseType.SqlServer, DbPrincipalKind.User, true) =>
                "SELECT name FROM sys.database_principals WHERE type IN ('S','U','G','C','K','E','X') ORDER BY name",
            (DatabaseType.SqlServer, DbPrincipalKind.Role, true) =>
                "SELECT name FROM sys.database_principals WHERE type IN ('R','A') ORDER BY name",
            (DatabaseType.Oracle, DbPrincipalKind.User, false) => "SELECT username FROM all_users ORDER BY username",
            (DatabaseType.Oracle, DbPrincipalKind.Role, false) => "SELECT role FROM dba_roles ORDER BY role",
            (DatabaseType.MySql, DbPrincipalKind.Account, false) => "SELECT `User`, `Host` FROM mysql.user ORDER BY `User`, `Host`",
            (DatabaseType.Postgres or DatabaseType.KingbaseES, DbPrincipalKind.User or DbPrincipalKind.Role, false) =>
                $"SELECT rolname FROM {(databaseType == DatabaseType.KingbaseES && !kingbaseFallback ? "sys_catalog.sys_roles" : "pg_catalog.pg_roles")} WHERE {loginPredicate} ORDER BY rolname",
            _ => throw new NotSupportedException("该目录在当前数据库类型或层级中不可用。"),
        };
    }

    internal static DbObjectTreeNode CreatePrincipalNode(string name, string? host, string? databaseName, DbPrincipalKind kind)
    {
        // MySQL 同名账户由 User 和 Host 共同标识，空用户名表示匿名账户。
        string label = host is null ? name : $"'{name.Replace("'", "''")}'@'{host.Replace("'", "''")}'";
        return new DbObjectTreeNode
        {
            Name = label,
            Text = label,
            NodeType = DbObjectTreeNodeType.Principal,
            PrincipalKind = kind,
            DatabaseName = databaseName,
            IsLoaded = true,
        };
    }
}
