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
}

/// <summary>按数据库类型组织的用户管理方言实现。</summary>
public class DefaultDbUserService : IDbUserService
{
    public bool IsSupported(string databaseType)
    {
        var dbType = ParseDatabaseType(databaseType);
        return dbType is DatabaseType.MySql or DatabaseType.Postgres or DatabaseType.SqlServer or DatabaseType.Oracle;
    }

    public async Task<(List<DbUserInfo> Users, string? Error)> GetUsersAsync(ConnectionItem connection, CancellationToken cancellationToken = default)
    {
        var dbType = ParseDatabaseType(connection.DatabaseType);
        if (!IsSupported(connection.DatabaseType))
        {
            return (new List<DbUserInfo>(), "该数据库类型不支持用户管理（SQLite 无用户体系）。");
        }

        string sql = dbType switch
        {
            DatabaseType.MySql => "SELECT `User`, `Host`, IFNULL(`authentication_string`,'') <> '' AS HasPassword, '' FROM mysql.user ORDER BY `User`",
            DatabaseType.Postgres => "SELECT rolname, '', CASE WHEN rolsuper THEN 'superuser' WHEN rolcanlogin THEN 'login' ELSE 'nologin' END, '' FROM pg_roles WHERE rolname NOT LIKE 'pg\\_%' ORDER BY rolname",
            DatabaseType.SqlServer => "SELECT name, '', ISNULL(type_desc,''), ISNULL(CONVERT(varchar(10), create_date, 120),'') FROM sys.server_principals WHERE type IN ('S','U') ORDER BY name",
            DatabaseType.Oracle => "SELECT username, '', NVL(account_status,''), TO_CHAR(created,'YYYY-MM-DD') FROM all_users ORDER BY username",
            _ => string.Empty,
        };

        try
        {
            var rows = await ExecuteReaderAsync(connection, sql, cancellationToken);
            var users = rows.Select(r => new DbUserInfo
            {
                Name = r[0] ?? string.Empty,
                Host = r[1] ?? string.Empty,
                Attributes = r[2] ?? string.Empty,
                Created = r[3] ?? string.Empty,
            }).ToList();

            return (users, null);
        }
        catch (Exception ex)
        {
            return (new List<DbUserInfo>(), $"{ex.Message}{Environment.NewLine}权限提示：{DbAdminGuidance.GetUserPermissionHint(dbType)}");
        }
    }

    public async Task<(string Text, string? Error)> GetGrantsAsync(ConnectionItem connection, string userName, string? host, CancellationToken cancellationToken = default)
    {
        var dbType = ParseDatabaseType(connection.DatabaseType);
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
            DatabaseType.Postgres => $"CREATE USER \"{user.Replace("\"", "\"\"")}\" WITH PASSWORD '{pwd}';",
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
            DatabaseType.Postgres => $"DROP USER \"{user.Replace("\"", "\"\"")}\";",
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
            DatabaseType.Postgres => $"GRANT {priv} ON {target} TO {SqlDialectHelper.QuoteIdentifier(dbType, userName)};",
            DatabaseType.SqlServer => $"GRANT {priv} ON {target} TO {SqlDialectHelper.QuoteIdentifier(dbType, userName)};",
            DatabaseType.Oracle => $"GRANT {priv} ON {target} TO {SqlDialectHelper.QuoteIdentifier(dbType, userName)};",
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
                DatabaseType.Postgres => $"ALL TABLES IN SCHEMA {SqlDialectHelper.QuoteIdentifier(dbType, parts[0])}",
                DatabaseType.SqlServer => $"SCHEMA::{SqlDialectHelper.QuoteIdentifier(dbType, parts[0])}",
                _ => string.Empty,
            };
            return target.Length > 0;
        }

        target = dbType switch
        {
            DatabaseType.Postgres => "TABLE " + SqlDialectHelper.QuoteQualifiedIdentifier(dbType, onObject ?? string.Empty),
            DatabaseType.SqlServer => "OBJECT::" + SqlDialectHelper.QuoteQualifiedIdentifier(dbType, onObject ?? string.Empty),
            DatabaseType.Oracle when parts.Length == 2 => SqlDialectHelper.QuoteQualifiedIdentifier(dbType, onObject ?? string.Empty),
            _ => string.Empty,
        };
        return target.Length > 0;
    }

    private static string EscapeSingleQuote(string value) => value?.Replace("'", "''") ?? string.Empty;

    private static async Task<List<string?[]>> ExecuteReaderAsync(ConnectionItem connection, string sql, CancellationToken ct)
    {
        var rows = new List<string?[]>();

        var interpreter = CreateInterpreter(connection);
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

        return DbInterpreterHelper.GetDbInterpreter(
            ParseDatabaseType(connection.DatabaseType), connectionInfo, new DbInterpreterOption());
    }

    private static DatabaseType ParseDatabaseType(string databaseType)
        => Enum.TryParse<DatabaseType>(databaseType, true, out var type) ? type : DatabaseType.Unknown;
}
