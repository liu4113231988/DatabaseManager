using System.Text.RegularExpressions;
using DatabaseInterpreter.Model;

namespace DatabaseManager.AppCore.Services;

/// <summary>会话与锁监控的方言查询模板。</summary>
public static class DbSessionSql
{
    public static string BuildSessionsSql(DatabaseType dbType) => dbType switch
    {
        DatabaseType.MySql => "SELECT CAST(Id AS CHAR), User, IFNULL(Host,''), IFNULL(Db,''), IFNULL(Command,''), IFNULL(State,''), CAST(Time AS CHAR), IFNULL(LEFT(Info,300),'') FROM information_schema.processlist",
        DatabaseType.Postgres => "SELECT pid::text, COALESCE(usename,''), COALESCE(client_addr::text || ' ' || COALESCE(application_name,''),''), COALESCE(datname,''), COALESCE(state,''), COALESCE(wait_event_type || ' ' || COALESCE(wait_event,''),''), COALESCE((now()-query_start)::text,''), COALESCE(LEFT(query,300),'') FROM pg_stat_activity WHERE pid <> pg_backend_pid()",
        // KingbaseES PG 兼容模式优先使用 sys_* 监控视图（与官方文档一致）；若实例未暴露，则由服务层回退到 pg_*。
        DatabaseType.KingbaseES => "SELECT pid::text, COALESCE(usename,''), COALESCE(client_addr::text || ' ' || COALESCE(application_name,''),''), COALESCE(datname,''), COALESCE(state,''), COALESCE(wait_event_type || ' ' || COALESCE(wait_event,''),''), COALESCE((now()-query_start)::text,''), COALESCE(LEFT(query,300),'') FROM sys_stat_activity WHERE pid <> sys_backend_pid()",
        DatabaseType.SqlServer => "SELECT CAST(s.session_id AS varchar(20)), s.login_name, ISNULL(s.host_name,'') + '/' + ISNULL(s.program_name,''), ISNULL(DB_NAME(s.database_id),''), s.status, ISNULL(r.wait_type,''), ISNULL(CONVERT(varchar(20), s.last_request_start_time, 120),''), ISNULL(LEFT(t.text,300),'') FROM sys.dm_exec_sessions s LEFT JOIN sys.dm_exec_requests r ON r.session_id = s.session_id OUTER APPLY sys.dm_exec_sql_text(r.sql_handle) t WHERE s.is_user_process = 1",
        DatabaseType.Oracle => "SELECT TO_CHAR(sid) || ',' || TO_CHAR(serial#), NVL(username,''), NVL(machine || ' ' || program,''), NVL(username,''), NVL(status,''), NVL(event,''), TO_CHAR(logon_time,'YYYY-MM-DD HH24:MI:SS'), '' FROM v$session WHERE username IS NOT NULL",
        _ => string.Empty,
    };

    /// <summary>KingbaseES 未暴露 sys_* 视图时的回退会话 SQL。</summary>
    public static string BuildKingbaseFallbackSessionsSql() =>
        "SELECT pid::text, COALESCE(usename,''), COALESCE(client_addr::text || ' ' || COALESCE(application_name,''),''), COALESCE(datname,''), COALESCE(state,''), COALESCE(wait_event_type || ' ' || COALESCE(wait_event,''),''), COALESCE((now()-query_start)::text,''), COALESCE(LEFT(query,300),'') FROM pg_stat_activity WHERE pid <> pg_backend_pid()";

    public static string BuildLocksSql(DatabaseType dbType) => dbType switch
    {
        DatabaseType.MySql => "SELECT CAST(w.requesting_trx_id AS CHAR), CAST(w.blocking_trx_id AS CHAR), IFNULL(w.requested_lock_id,''), '' FROM information_schema.innodb_lock_waits w",
        DatabaseType.Postgres => "SELECT blocked.pid::text, blk.pid::text, COALESCE(blocked.wait_event::text,''), COALESCE((now()-blocked.query_start)::text,'') FROM pg_stat_activity blocked JOIN pg_stat_activity blk ON blk.pid = ANY(pg_blocking_pids(blocked.pid))",
        DatabaseType.KingbaseES => "SELECT blocked.pid::text, blocker.pid::text, COALESCE(blocked.wait_event_type || ' ' || blocked.wait_event,''), COALESCE((now()-blocked.query_start)::text,'') FROM sys_stat_activity blocked JOIN sys_stat_activity blocker ON blocker.pid = ANY(sys_blocking_pids(blocked.pid)) WHERE blocked.pid <> sys_backend_pid()",
        DatabaseType.SqlServer => "SELECT CAST(r.session_id AS varchar(20)), CAST(r.blocking_session_id AS varchar(20)), ISNULL(r.wait_type,''), ISNULL(CAST(r.wait_time AS varchar(20)),'') FROM sys.dm_exec_requests r WHERE r.blocking_session_id <> 0",
        DatabaseType.Oracle => "SELECT s.sid || ',' || s.serial#, TO_CHAR(s.blocking_session), NVL(s.event,''), NVL(TO_CHAR(s.seconds_in_wait) || 's','') FROM v$session s WHERE s.blocking_session IS NOT NULL",
        _ => string.Empty,
    };

    /// <summary>KingbaseES 未暴露 sys_* 视图时的回退锁 SQL。</summary>
    public static string BuildKingbaseFallbackLocksSql() =>
        "SELECT blocked.pid::text, blocker.pid::text, COALESCE(blocked.wait_event_type || ' ' || blocked.wait_event,''), COALESCE((now()-blocked.query_start)::text,'') FROM pg_stat_activity blocked JOIN pg_stat_activity blocker ON blocker.pid = ANY(pg_blocking_pids(blocked.pid)) WHERE blocked.pid <> pg_backend_pid()";

    /// <summary>构建终止会话的 SQL，并只接受数值 PID 以避免注入。</summary>
    public static string? BuildTerminateSessionSql(DatabaseType dbType, string sessionId)
    {
        if (!Regex.IsMatch(sessionId, @"^\d+$"))
            return null;

        return dbType switch
        {
            DatabaseType.MySql => $"KILL {sessionId}",
            DatabaseType.Postgres => $"SELECT pg_terminate_backend({sessionId})",
            DatabaseType.KingbaseES => $"SELECT sys_terminate_backend({sessionId})",
            DatabaseType.SqlServer => $"KILL {sessionId}",
            _ => null,
        };
    }

    /// <summary>KingbaseES 未暴露 sys_* 函数时的回退终止 SQL。</summary>
    public static string? BuildKingbaseFallbackTerminateSessionSql(string sessionId)
    {
        if (!Regex.IsMatch(sessionId, @"^\d+$"))
            return null;
        return $"SELECT pg_terminate_backend({sessionId})";
    }

    /// <summary>探测 KingbaseES 是否暴露 sys_stat_activity 视图。</summary>
    public static string BuildKingbaseProbeSql() =>
        "SELECT 1 FROM pg_catalog.pg_class c JOIN pg_catalog.pg_namespace n ON n.oid = c.relnamespace WHERE n.nspname = 'sys_catalog' AND c.relname = 'stat_activity' LIMIT 1";
}