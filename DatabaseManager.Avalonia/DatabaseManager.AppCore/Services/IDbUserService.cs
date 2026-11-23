using DatabaseInterpreter.Core;
using DatabaseInterpreter.Model;
using DatabaseManager.AppCore.Models;

namespace DatabaseManager.AppCore.Services;

/// <summary>数据库用户信息。</summary>
public class DbUserInfo
{
    public string Name { get; set; } = string.Empty;

    /// <summary>主机（MySQL 特有；其他库为空）。</summary>
    public string Host { get; set; } = string.Empty;

    /// <summary>状态/属性（如账户状态、是否超级用户）。</summary>
    public string Attributes { get; set; } = string.Empty;

    public string Created { get; set; } = string.Empty;

    /// <summary>主体类别：user（可登录）/ role（不可登录）/ account（MySQL 账户与角色同表）。</summary>
    public string Kind { get; set; } = "user";
}

/// <summary>
/// 用户/权限管理服务（方言适配的只读查询 + 模板化 DDL 生成 + 受控执行）。
/// SQLite 无用户体系，返回不支持。
/// </summary>
public interface IDbUserService
{
    /// <summary>指定数据库类型是否支持用户管理。</summary>
    bool IsSupported(string databaseType);

    /// <summary>读取用户列表。</summary>
    Task<(List<DbUserInfo> Users, string? Error)> GetUsersAsync(ConnectionItem connection, CancellationToken cancellationToken = default);

    /// <summary>读取指定用户的权限文本（逐行 GRANT/权限说明）。</summary>
    Task<(string Text, string? Error)> GetGrantsAsync(ConnectionItem connection, string userName, string? host, CancellationToken cancellationToken = default);

    /// <summary>执行一条用户管理 SQL（调用方必须已取得用户确认）。</summary>
    Task<(bool Success, string? Error)> ExecuteAsync(ConnectionItem connection, string sql, CancellationToken cancellationToken = default);

    /// <summary>生成创建用户的 SQL（方言模板）。</summary>
    string BuildCreateUserSql(string databaseType, string userName, string password, string? host);

    /// <summary>生成删除用户的 SQL（方言模板）。</summary>
    string BuildDropUserSql(string databaseType, string userName, string? host, bool cascade);

    /// <summary>生成授权的 SQL（方言模板）。</summary>
    string BuildGrantSql(string databaseType, string userName, string? host, string privilege, string onObject);

    /// <summary>生成撤销授权的 SQL（方言模板）。</summary>
    string BuildRevokeSql(string databaseType, string userName, string? host, string privilege, string onObject);

    /// <summary>生成创建角色的 SQL（方言模板）。canLogin 仅对 PostgreSQL/KingbaseES 有效。</summary>
    string BuildCreateRoleSql(string databaseType, string roleName, string? password, bool canLogin, string? host);

    /// <summary>生成删除角色的 SQL（方言模板）。</summary>
    string BuildDropRoleSql(string databaseType, string roleName, string? host);

    /// <summary>生成把角色授予成员的 SQL（方言模板）。</summary>
    string BuildGrantRoleSql(string databaseType, string roleName, string memberName, string? memberHost);

    /// <summary>生成从成员撤销角色的 SQL（方言模板）。</summary>
    string BuildRevokeRoleSql(string databaseType, string roleName, string memberName, string? memberHost);

    /// <summary>生成修改密码的 SQL（方言模板）。</summary>
    string BuildAlterPasswordSql(string databaseType, string userName, string? host, string newPassword);
}

/// <summary>按数据库类型组织的用户管理方言实现。</summary>
public class DefaultDbUserService : IDbUserService
{
    public bool IsSupported(string databaseType)
    {
        var dbType = ParseDatabaseType(databaseType);
        return dbType is DatabaseType.MySql or DatabaseType.Postgres or DatabaseType.KingbaseES
            or DatabaseType.SqlServer or DatabaseType.Oracle;
    }

    public async Task<(List<DbUserInfo> Users, string? Error)> GetUsersAsync(ConnectionItem connection, CancellationToken cancellationToken = default)
    {
        var dbType = ParseDatabaseType(connection.DatabaseType);
        if (!IsSupported(connection.DatabaseType))
        {
            return (new List<DbUserInfo>(), "该数据库类型不支持用户管理（例如 SQLite 无用户体系）。");
        }
        if (dbType == DatabaseType.KingbaseES &&
            KingbaseCompatibilityModes.GetAdminFeatureBlockReason(connection.KingbaseCompatibilityMode) is { } modeReason)
        {
            return (new List<DbUserInfo>(), modeReason);
        }

        string sql = dbType switch
        {
            DatabaseType.MySql => "SELECT `User`, `Host`, IFNULL(`authentication_string`,'') <> '' AS HasPassword, '', 'account' FROM mysql.user ORDER BY `User`",
            DatabaseType.Postgres => "SELECT rolname, '', CASE WHEN rolsuper THEN 'superuser' WHEN rolcanlogin THEN 'login' ELSE 'nologin' END, '', CASE WHEN rolcanlogin THEN 'user' ELSE 'role' END FROM pg_roles WHERE rolname NOT LIKE 'pg\\_%' ORDER BY rolname",
            DatabaseType.KingbaseES => KingbaseUserSql.BuildUsersSql(useSystemCatalog: true),
            DatabaseType.SqlServer => "SELECT name, '', ISNULL(type_desc,''), ISNULL(CONVERT(varchar(10), create_date, 120),''), 'user' FROM sys.server_principals WHERE type IN ('S','U') ORDER BY name",
            DatabaseType.Oracle => "SELECT username, '', NVL(account_status,''), TO_CHAR(created,'YYYY-MM-DD'), 'user' FROM all_users ORDER BY username",
            _ => string.Empty,
        };

        try
        {
            var rows = await ExecuteReaderAsync(connection, sql, cancellationToken);
            return (ToUsers(rows), null);
        }
        catch (Exception ex) when (dbType == DatabaseType.KingbaseES)
        {
            try
            {
                var fallbackRows = await ExecuteReaderAsync(connection, KingbaseUserSql.BuildFallbackUsersSql(), cancellationToken);
                return (ToUsers(fallbackRows), null);
            }
            catch (Exception fallbackEx)
            {
                return (new List<DbUserInfo>(),
                    $"sys_roles 读取失败：{ex.Message}；pg_roles 回退失败：{fallbackEx.Message}{Environment.NewLine}权限提示：{DbAdminGuidance.GetUserPermissionHint(dbType)}");
            }
        }
        catch (Exception ex)
        {
            return (new List<DbUserInfo>(), $"{ex.Message}{Environment.NewLine}权限提示：{DbAdminGuidance.GetUserPermissionHint(dbType)}");
        }
    }

    public async Task<(string Text, string? Error)> GetGrantsAsync(ConnectionItem connection, string userName, string? host, CancellationToken cancellationToken = default)
    {
        var dbType = ParseDatabaseType(connection.DatabaseType);
        if (dbType == DatabaseType.KingbaseES &&
            KingbaseCompatibilityModes.GetAdminFeatureBlockReason(connection.KingbaseCompatibilityMode) is { } modeReason)
        {
            return (string.Empty, modeReason);
        }

        string sql;

        switch (dbType)
        {
            case DatabaseType.MySql:
                sql = $"SHOW GRANTS FOR '{EscapeSingleQuote(userName)}'@'{EscapeSingleQuote(host ?? "%")}';";
                break;
            case DatabaseType.Postgres:
                sql = $@"SELECT grant_text FROM (
SELECT grantee || ': ' || privilege_type || ' ON TABLE ' || table_schema || '.' || table_name AS grant_text
FROM information_schema.role_table_grants WHERE grantee = '{EscapeSingleQuote(userName)}'
UNION ALL
SELECT grantee || ': EXECUTE ON FUNCTION ' || routine_schema || '.' || routine_name
FROM information_schema.role_routine_grants WHERE grantee = '{EscapeSingleQuote(userName)}'
UNION ALL
SELECT member.rolname || ' -> 成员角色 ' || role.rolname
FROM pg_auth_members m
JOIN pg_roles role ON role.oid = m.roleid
JOIN pg_roles member ON member.oid = m.member
WHERE member.rolname = '{EscapeSingleQuote(userName)}'
) grants ORDER BY grant_text";
                break;
            case DatabaseType.KingbaseES:
                sql = KingbaseUserSql.BuildGrantsSql(userName);
                break;
            case DatabaseType.SqlServer:
                sql = $@"SELECT permission_name + ' ON ' + COALESCE(OBJECT_SCHEMA_NAME(major_id) + '.', '') + COALESCE(OBJECT_NAME(major_id), 'DATABASE')
FROM sys.database_permissions p JOIN sys.database_principals u ON u.principal_id = p.grantee_principal_id
WHERE u.name = '{EscapeSingleQuote(userName)}'
UNION ALL
SELECT USER_NAME(member_principal_id) + ' -> 成员角色 ' + USER_NAME(role_principal_id)
FROM sys.server_role_members WHERE USER_NAME(member_principal_id) = '{EscapeSingleQuote(userName)}'";
                break;
            case DatabaseType.Oracle:
                sql = $"SELECT privilege || ' (系统)' FROM dba_sys_privs WHERE grantee = UPPER('{EscapeSingleQuote(userName)}') UNION ALL SELECT privilege || ' ON ' || table_schema || '.' || table_name || ' (对象)' FROM dba_tab_privs WHERE grantee = UPPER('{EscapeSingleQuote(userName)}') UNION ALL SELECT '成员角色 ' || granted_role FROM dba_role_privs WHERE grantee = UPPER('{EscapeSingleQuote(userName)}')";
                break;
            default:
                return (string.Empty, "不支持的数据库类型。");
        }

        try
        {
            var rows = await ExecuteReaderAsync(connection, sql, cancellationToken);
            var lines = rows
                .SelectMany(r => r.Where(c => !string.IsNullOrEmpty(c)))
                .ToList();

            return (lines.Count > 0 ? string.Join(Environment.NewLine, lines) : "（未读取到权限记录，可能需要管理员权限）", null);
        }
        catch (Exception ex)
        {
            return (string.Empty, $"{ex.Message}{Environment.NewLine}权限提示：{DbAdminGuidance.GetUserPermissionHint(dbType)}");
        }
    }

    public async Task<(bool Success, string? Error)> ExecuteAsync(ConnectionItem connection, string sql, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sql))
        {
            return (false, "SQL 为空。");
        }
        if (ParseDatabaseType(connection.DatabaseType) == DatabaseType.KingbaseES &&
            KingbaseCompatibilityModes.GetAdminFeatureBlockReason(connection.KingbaseCompatibilityMode) is { } modeReason)
        {
            return (false, modeReason);
        }

        try
        {
            await ExecuteNonQueryAsync(connection, sql, cancellationToken);
            return (true, null);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    public string BuildCreateUserSql(string databaseType, string userName, string password, string? host)
    {
        var dbType = ParseDatabaseType(databaseType);
        string user = EscapeSingleQuote(userName);
        string pwd = EscapeSingleQuote(password);

        return dbType switch
        {
            DatabaseType.MySql => $"CREATE USER '{user}'@'{EscapeSingleQuote(host ?? "%")}' IDENTIFIED BY '{pwd}';",
            DatabaseType.Postgres or DatabaseType.KingbaseES => $"CREATE USER \"{user.Replace("\"", "\"\"")}\" WITH PASSWORD '{pwd}';",
            DatabaseType.SqlServer => $"CREATE LOGIN [{userName.Replace("]", "]]")}] WITH PASSWORD = '{pwd}';",
            DatabaseType.Oracle => $"CREATE USER \"{user.Replace("\"", "\"\"")}\" IDENTIFIED BY \"{pwd}\";",
            _ => string.Empty,
        };
    }

    public string BuildDropUserSql(string databaseType, string userName, string? host, bool cascade)
    {
        var dbType = ParseDatabaseType(databaseType);
        string user = EscapeSingleQuote(userName);

        return dbType switch
        {
            DatabaseType.MySql => $"DROP USER '{user}'@'{EscapeSingleQuote(host ?? "%")}';",
            DatabaseType.Postgres or DatabaseType.KingbaseES => $"DROP USER \"{user.Replace("\"", "\"\"")}\";",
            DatabaseType.SqlServer => $"DROP LOGIN [{userName.Replace("]", "]]")}]",
            DatabaseType.Oracle => $"DROP USER \"{user.Replace("\"", "\"\"")}\"{(cascade ? " CASCADE" : string.Empty)};",
            _ => string.Empty,
        };
    }

    public string BuildGrantSql(string databaseType, string userName, string? host, string privilege, string onObject)
    {
        var dbType = ParseDatabaseType(databaseType);
        string priv = privilege.Trim().ToUpperInvariant();
        if (priv is not ("SELECT" or "INSERT" or "UPDATE" or "DELETE" or "ALL PRIVILEGES")
            || !TryBuildGrantTarget(dbType, onObject, out var target))
        {
            return string.Empty;
        }

        return dbType switch
        {
            DatabaseType.MySql => $"GRANT {priv} ON {target} TO '{EscapeSingleQuote(userName)}'@'{EscapeSingleQuote(host ?? "%")}';",
            DatabaseType.Postgres or DatabaseType.KingbaseES => $"GRANT {priv} ON {target} TO {SqlDialectHelper.QuoteIdentifier(dbType, userName)};",
            DatabaseType.SqlServer => $"GRANT {priv} ON {target} TO {SqlDialectHelper.QuoteIdentifier(dbType, userName)};",
            DatabaseType.Oracle => $"GRANT {priv} ON {target} TO {SqlDialectHelper.QuoteIdentifier(dbType, userName)};",
            _ => string.Empty,
        };
    }

    public string BuildRevokeSql(string databaseType, string userName, string? host, string privilege, string onObject)
    {
        var dbType = ParseDatabaseType(databaseType);
        string priv = privilege.Trim().ToUpperInvariant();
        if (priv is not ("SELECT" or "INSERT" or "UPDATE" or "DELETE" or "ALL PRIVILEGES")
            || !TryBuildGrantTarget(dbType, onObject, out var target))
        {
            return string.Empty;
        }

        return dbType switch
        {
            DatabaseType.MySql => $"REVOKE {priv} ON {target} FROM '{EscapeSingleQuote(userName)}'@'{EscapeSingleQuote(host ?? "%")}';",
            DatabaseType.Postgres or DatabaseType.KingbaseES => $"REVOKE {priv} ON {target} FROM {SqlDialectHelper.QuoteIdentifier(dbType, userName)};",
            DatabaseType.SqlServer => $"REVOKE {priv} ON {target} FROM {SqlDialectHelper.QuoteIdentifier(dbType, userName)};",
            DatabaseType.Oracle => $"REVOKE {priv} ON {target} FROM {SqlDialectHelper.QuoteIdentifier(dbType, userName)};",
            _ => string.Empty,
        };
    }

    public string BuildCreateRoleSql(string databaseType, string roleName, string? password, bool canLogin, string? host)
    {
        var dbType = ParseDatabaseType(databaseType);
        string role = EscapeSingleQuote(roleName);
        string quoted = SqlDialectHelper.QuoteIdentifier(dbType, roleName);
        string pwd = EscapeSingleQuote(password ?? string.Empty);

        return dbType switch
        {
            DatabaseType.MySql => $"CREATE ROLE '{role}'@'{EscapeSingleQuote(host ?? "%")}';",
            DatabaseType.Postgres or DatabaseType.KingbaseES =>
                $"CREATE ROLE {quoted}{(canLogin ? " LOGIN" : string.Empty)}{(pwd.Length > 0 ? $" PASSWORD '{pwd}'" : string.Empty)};",
            DatabaseType.SqlServer => $"CREATE ROLE [{roleName.Replace("]", "]]")}];",
            DatabaseType.Oracle => $"CREATE ROLE {quoted};",
            _ => string.Empty,
        };
    }

    public string BuildDropRoleSql(string databaseType, string roleName, string? host)
    {
        var dbType = ParseDatabaseType(databaseType);
        string role = EscapeSingleQuote(roleName);
        string quoted = SqlDialectHelper.QuoteIdentifier(dbType, roleName);

        return dbType switch
        {
            DatabaseType.MySql => $"DROP ROLE '{role}'@'{EscapeSingleQuote(host ?? "%")}';",
            DatabaseType.Postgres or DatabaseType.KingbaseES => $"DROP ROLE {quoted};",
            DatabaseType.SqlServer => $"DROP ROLE [{roleName.Replace("]", "]]")}];",
            DatabaseType.Oracle => $"DROP ROLE {quoted};",
            _ => string.Empty,
        };
    }

    public string BuildGrantRoleSql(string databaseType, string roleName, string memberName, string? memberHost)
        => BuildRoleMembershipSql(ParseDatabaseType(databaseType), roleName, memberName, memberHost, grant: true);

    public string BuildRevokeRoleSql(string databaseType, string roleName, string memberName, string? memberHost)
        => BuildRoleMembershipSql(ParseDatabaseType(databaseType), roleName, memberName, memberHost, grant: false);

    private static string BuildRoleMembershipSql(DatabaseType dbType, string roleName, string memberName, string? memberHost, bool grant)
    {
        string role = EscapeSingleQuote(roleName);
        string member = EscapeSingleQuote(memberName);
        string quotedRole = SqlDialectHelper.QuoteIdentifier(dbType, roleName);
        string quotedMember = SqlDialectHelper.QuoteIdentifier(dbType, memberName);

        return dbType switch
        {
            DatabaseType.MySql => grant
                ? $"GRANT '{role}'@'%' TO '{member}'@'{EscapeSingleQuote(memberHost ?? "%")}';"
                : $"REVOKE '{role}'@'%' FROM '{member}'@'{EscapeSingleQuote(memberHost ?? "%")}';",
            DatabaseType.Postgres or DatabaseType.KingbaseES => grant
                ? $"GRANT {quotedRole} TO {quotedMember};"
                : $"REVOKE {quotedRole} FROM {quotedMember};",
            DatabaseType.SqlServer => grant
                ? $"ALTER ROLE [{roleName.Replace("]", "]]")}] ADD MEMBER [{memberName.Replace("]", "]]")}];"
                : $"ALTER ROLE [{roleName.Replace("]", "]]")}] DROP MEMBER [{memberName.Replace("]", "]]")}];",
            DatabaseType.Oracle => grant
                ? $"GRANT {quotedRole} TO {quotedMember};"
                : $"REVOKE {quotedRole} FROM {quotedMember};",
            _ => string.Empty,
        };
    }

    public string BuildAlterPasswordSql(string databaseType, string userName, string? host, string newPassword)
    {
        var dbType = ParseDatabaseType(databaseType);
        string user = EscapeSingleQuote(userName);
        string pwd = EscapeSingleQuote(newPassword);
        string quoted = SqlDialectHelper.QuoteIdentifier(dbType, userName);

        return dbType switch
        {
            DatabaseType.MySql => $"ALTER USER '{user}'@'{EscapeSingleQuote(host ?? "%")}' IDENTIFIED BY '{pwd}';",
            DatabaseType.Postgres or DatabaseType.KingbaseES => $"ALTER USER {quoted} WITH PASSWORD '{pwd}';",
            DatabaseType.SqlServer => $"ALTER LOGIN [{userName.Replace("]", "]]")}] WITH PASSWORD = '{pwd}';",
            DatabaseType.Oracle => $"ALTER USER {quoted} IDENTIFIED BY \"{pwd}\";",
            _ => string.Empty,
        };
    }

    private static bool TryBuildGrantTarget(DatabaseType dbType, string? onObject, out string target)
    {
        target = string.Empty;
        var parts = (onObject ?? string.Empty)
            .Split('.', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length is < 1 or > 2)
            return false;

        if (dbType == DatabaseType.MySql)
        {
            if (parts.Length != 2)
                return false;

            target = string.Join('.', parts.Select(p => p == "*" ? p : SqlDialectHelper.QuoteIdentifier(dbType, p)));
            return true;
        }

        if (parts.Any(p => p == "*"))
        {
            if (parts.Length != 2 || parts[1] != "*" || parts[0] == "*")
                return false;

            target = dbType switch
            {
                DatabaseType.Postgres or DatabaseType.KingbaseES => $"ALL TABLES IN SCHEMA {SqlDialectHelper.QuoteIdentifier(dbType, parts[0])}",
                DatabaseType.SqlServer => $"SCHEMA::{SqlDialectHelper.QuoteIdentifier(dbType, parts[0])}",
                _ => string.Empty,
            };
            return target.Length > 0;
        }

        target = dbType switch
        {
            DatabaseType.Postgres or DatabaseType.KingbaseES => "TABLE " + SqlDialectHelper.QuoteQualifiedIdentifier(dbType, onObject ?? string.Empty),
            DatabaseType.SqlServer => "OBJECT::" + SqlDialectHelper.QuoteQualifiedIdentifier(dbType, onObject ?? string.Empty),
            DatabaseType.Oracle when parts.Length == 2 => SqlDialectHelper.QuoteQualifiedIdentifier(dbType, onObject ?? string.Empty),
            _ => string.Empty,
        };
        return target.Length > 0;
    }

    private static List<DbUserInfo> ToUsers(List<string?[]> rows) => rows.Select(r => new DbUserInfo
    {
        Name = r[0] ?? string.Empty,
        Host = r[1] ?? string.Empty,
        Attributes = r[2] ?? string.Empty,
        Created = r[3] ?? string.Empty,
        Kind = r.Length > 4 && !string.IsNullOrEmpty(r[4]) ? r[4]! : "user",
    }).ToList();

    private static string EscapeSingleQuote(string value) => value?.Replace("'", "''") ?? string.Empty;

    private static Task<List<string?[]>> ExecuteReaderAsync(ConnectionItem connection, string sql, CancellationToken ct)
        => ExecuteReaderAsync(CreateInterpreter(connection), sql, ct);

    internal static async Task<List<string?[]>> ExecuteReaderAsync(DbInterpreter interpreter, string sql, CancellationToken ct)
    {
        var rows = new List<string?[]>();

        await using var conn = interpreter.CreateConnection();
        await conn.OpenAsync(ct);

        await using var cmd = conn.CreateCommand();
        cmd.CommandTimeout = 15;
        cmd.CommandText = sql;

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var cells = new string?[reader.FieldCount];
            for (int i = 0; i < reader.FieldCount; i++)
            {
                cells[i] = reader.IsDBNull(i) ? null : reader.GetValue(i)?.ToString();
            }

            rows.Add(cells);
        }

        return rows;
    }

    private static async Task ExecuteNonQueryAsync(ConnectionItem connection, string sql, CancellationToken ct)
    {
        var interpreter = CreateInterpreter(connection);
        await using var conn = interpreter.CreateConnection();
        await conn.OpenAsync(ct);

        await using var cmd = conn.CreateCommand();
        cmd.CommandTimeout = 30;
        cmd.CommandText = sql;
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static DbInterpreter CreateInterpreter(ConnectionItem connection)
    {
        var connectionInfo = ConnectionHelper.ToConnectionInfo(connection);

        return DbInterpreterHelper.GetDbInterpreter(
            ParseDatabaseType(connection.DatabaseType), connectionInfo, new DbInterpreterOption());
    }

    private static DatabaseType ParseDatabaseType(string databaseType)
        => Enum.TryParse<DatabaseType>(databaseType, true, out var type) ? type : DatabaseType.Unknown;
}
