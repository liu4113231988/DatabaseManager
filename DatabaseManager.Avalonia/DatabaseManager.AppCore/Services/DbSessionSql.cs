using DatabaseInterpreter.Model;

namespace DatabaseManager.AppCore.Services;

/// <summary>会话与锁监控的方言查询模板。</summary>
public static class DbSessionSql
{
    public static string BuildSessionsSql(DatabaseType dbType) => dbType switch
    {
        DatabaseType.MySql => "SELECT CAST(Id AS CHAR), User, IFNULL(Host,''), IFNULL(Db,''), IFNULL(Command,''), IFNULL(State,''), CAST(Time AS CHAR), IFNULL(LEFT(Info,300),'') FROM information_schema.processlist",
        DatabaseType.Postgres => "SELECT pid::text, COALESCE(usename,''), COALESCE(client_addr::text || ' ' || COALESCE(application_name,''),''), COALESCE(datname,''), COALESCE(state,''), COALESCE(wait_event_type || ' ' || COALESCE(wait_event,''),''), COALESCE((now()-query_start)::text,''), COALESCE(LEFT(query,300),'') FROM pg_stat_activity WHERE pid <> pg_backend_pid()",
        DatabaseType.SqlServer => "SELECT CAST(s.session_id AS varchar(20)), s.login_name, ISNULL(s.host_name,'') + '/' + ISNULL(s.program_name,''), ISNULL(DB_NAME(s.database_id),''), s.status, ISNULL(r.wait_type,''), ISNULL(CONVERT(varchar(20), s.last_request_start_time, 120),''), ISNULL(LEFT(t.text,300),'') FROM sys.dm_exec_sessions s LEFT JOIN sys.dm_exec_requests r ON r.session_id = s.session_id OUTER APPLY sys.dm_exec_sql_text(r.sql_handle) t WHERE s.is_user_process = 1",
        DatabaseType.Oracle => "SELECT TO_CHAR(sid) || ',' || TO_CHAR(serial#), NVL(username,''), NVL(machine || ' ' || program,''), NVL(username,''), NVL(status,''), NVL(event,''), TO_CHAR(logon_time,'YYYY-MM-DD HH24:MI:SS'), '' FROM v$session WHERE username IS NOT NULL",
        _ => string.Empty,
    };

    public static string BuildLocksSql(DatabaseType dbType) => dbType switch
    {
        DatabaseType.MySql => "SELECT CAST(w.requesting_trx_id AS CHAR), CAST(w.blocking_trx_id AS CHAR), IFNULL(w.requested_lock_id,''), '' FROM information_schema.innodb_lock_waits w",
        DatabaseType.Postgres => "SELECT blocked.pid::text, blk.pid::text, COALESCE(blocked.wait_event::text,''), COALESCE((now()-blocked.query_start)::text,'') FROM pg_stat_activity blocked JOIN pg_stat_activity blk ON blk.pid = ANY(pg_blocking_pids(blocked.pid))",
        DatabaseType.SqlServer => "SELECT CAST(r.session_id AS varchar(20)), CAST(r.blocking_session_id AS varchar(20)), ISNULL(r.wait_type,''), ISNULL(CAST(r.wait_time AS varchar(20)),'') FROM sys.dm_exec_requests r WHERE r.blocking_session_id <> 0",
        DatabaseType.Oracle => "SELECT s.sid || ',' || s.serial#, TO_CHAR(s.blocking_session), NVL(s.event,''), NVL(TO_CHAR(s.seconds_in_wait) || 's','') FROM v$session s WHERE s.blocking_session IS NOT NULL",
        _ => string.Empty,
    };
}
