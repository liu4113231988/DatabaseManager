namespace DatabaseManager.AppCore.Services;

/// <summary>
/// KingbaseES PG 兼容模式的用户/权限只读 SQL。
/// 优先使用 sys_catalog 视图；实例未暴露时由 <see cref="DefaultDbUserService"/> 回退到 pg_catalog。
/// 权限明细尽量使用 information_schema，避免依赖未经验证的版本专属目录列。
/// </summary>
public static class KingbaseUserSql
{
    public static string BuildUsersSql(bool useSystemCatalog) => useSystemCatalog
        ? "SELECT rolname, '', CASE WHEN rolsuper THEN 'superuser' WHEN rolcanlogin THEN 'login' ELSE 'nologin' END, '' FROM sys_catalog.sys_roles WHERE rolname NOT LIKE 'sys\\_%' ORDER BY rolname"
        : "SELECT rolname, '', CASE WHEN rolsuper THEN 'superuser' WHEN rolcanlogin THEN 'login' ELSE 'nologin' END, '' FROM pg_catalog.pg_roles WHERE rolname NOT LIKE 'pg\\_%' ORDER BY rolname";

    public static string BuildFallbackUsersSql() => BuildUsersSql(false);

    public static string BuildGrantsSql(string userName)
    {
        string name = EscapeLiteral(userName);
        return $@"SELECT grant_text FROM (
SELECT grantee || ': ' || privilege_type || ' ON TABLE ' || table_schema || '.' || table_name AS grant_text
FROM information_schema.role_table_grants WHERE grantee = '{name}'
UNION ALL
SELECT grantee || ': EXECUTE ON FUNCTION ' || routine_schema || '.' || routine_name
FROM information_schema.role_routine_grants WHERE grantee = '{name}'
UNION ALL
SELECT grantee || ' -> 成员角色 ' || role_name
FROM information_schema.applicable_roles WHERE grantee = '{name}'
) grants ORDER BY grant_text";
    }

    public static string EscapeLiteral(string value) => (value ?? string.Empty).Replace("'", "''", StringComparison.Ordinal);
}
