using DatabaseInterpreter.Model;

namespace DatabaseManager.AppCore.Services;

/// <summary>数据库管理功能在权限不足或配置错误时的可操作提示。</summary>
public static class DbAdminGuidance
{
    public static string GetSessionPermissionHint(DatabaseType databaseType) => databaseType switch
    {
        DatabaseType.MySql => "需要 PROCESS 权限；读取锁信息通常还需要 PERFORMANCE_SCHEMA 或相应 InnoDB 视图权限。",
        DatabaseType.Postgres => "建议授予 pg_read_all_stats（或使用超级用户）；终止其他会话需要 pg_signal_backend。",
        DatabaseType.SqlServer => "需要 VIEW SERVER STATE 以读取 dm_exec_sessions/dm_exec_requests；终止会话需要 processadmin 或 sysadmin。",
        DatabaseType.Oracle => "需要 SELECT_CATALOG_ROLE（或 v_$session/v_$lock 的 SELECT 权限）；终止会话需要 ALTER SYSTEM。",
        _ => "请使用具备服务器监控权限的账号。",
    };

    public static string GetUserPermissionHint(DatabaseType databaseType) => databaseType switch
    {
        DatabaseType.MySql => "读取 mysql.user 与 SHOW GRANTS 通常需要管理员权限。",
        DatabaseType.Postgres => "读取所有角色与权限建议使用超级用户或授予 pg_read_all_stats；普通账号可能只能看到自身可见信息。",
        DatabaseType.SqlServer => "读取服务器主体和角色成员建议授予 VIEW ANY DEFINITION；数据库对象权限取决于当前数据库可见性。",
        DatabaseType.Oracle => "查询 DBA_* 权限视图需要 SELECT_CATALOG_ROLE；无该权限时请改用具备 DBA 权限的连接。",
        _ => "请使用具备用户与权限管理权限的账号。",
    };

    /// <summary>验证可选的客户端工具路径；空值表示允许服务采用自动发现。</summary>
    public static string? ValidateClientToolPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;
        if (!Path.IsPathFullyQualified(path))
            return "客户端工具路径必须是绝对路径。";
        if (!File.Exists(path))
            return "客户端工具文件不存在，请检查路径。";
        return null;
    }
}
