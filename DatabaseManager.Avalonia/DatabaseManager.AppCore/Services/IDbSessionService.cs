using DatabaseManager.AppCore.Models;

namespace DatabaseManager.AppCore.Services;

/// <summary>一条数据库会话信息。</summary>
public class DbSessionInfo
{
    public string SessionId { get; set; } = string.Empty;

    public string User { get; set; } = string.Empty;

    public string Client { get; set; } = string.Empty;

    public string Database { get; set; } = string.Empty;

    public string State { get; set; } = string.Empty;

    public string WaitInfo { get; set; } = string.Empty;

    public string Duration { get; set; } = string.Empty;

    public string CurrentSql { get; set; } = string.Empty;
}

/// <summary>一条锁/阻塞信息。</summary>
public class DbLockInfo
{
    public string BlockedSession { get; set; } = string.Empty;

    public string BlockingSession { get; set; } = string.Empty;

    public string WaitResource { get; set; } = string.Empty;

    public string WaitTime { get; set; } = string.Empty;
}

/// <summary>会话与锁快照。</summary>
public class DbSessionSnapshot
{
    public List<DbSessionInfo> Sessions { get; } = new();

    public List<DbLockInfo> Locks { get; } = new();

    public string? Error { get; set; }

    public bool IsSuccess => string.IsNullOrEmpty(Error);
}

/// <summary>
/// 会话与锁监控服务：查看活动会话、阻塞链，并支持终止会话（方言适配）。
/// SQLite 无服务端会话概念，返回不支持。
/// </summary>
public interface IDbSessionService
{
    /// <summary>指定数据库类型是否支持会话监控。</summary>
    bool IsSupported(string databaseType);

    /// <summary>读取会话与锁快照（逐项查询，锁查询失败不影响会话列表）。</summary>
    Task<DbSessionSnapshot> GetSnapshotAsync(ConnectionItem connection, CancellationToken cancellationToken = default);

    /// <summary>终止指定会话（调用方必须已取得用户确认）。</summary>
    Task<(bool Success, string? Error)> KillSessionAsync(ConnectionItem connection, string sessionId, CancellationToken cancellationToken = default);
}
