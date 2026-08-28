using DatabaseManager.AppCore.Models;

namespace DatabaseManager.AppCore.Services;

/// <summary>
/// 查询服务。封装 SQL 查询执行、结果获取与事务管理（Commit / Rollback / Auto-commit）。
/// </summary>
public interface IQueryService
{
    /// <summary>执行一条 SQL 语句，返回查询结果（含列、行数据、受影响行数与耗时）。</summary>
    /// <param name="commandTimeoutSeconds">命令超时秒数；传入非正数时使用引擎默认值。</param>
    Task<QueryResult> ExecuteAsync(
        string connectionName,
        string sql,
        CancellationToken cancellationToken = default,
        int commandTimeoutSeconds = 60);

    /// <summary>开启一个事务（持久化连接）。若已存在活动事务则返回 false。</summary>
    Task<bool> BeginTransactionAsync(string connectionName, CancellationToken cancellationToken = default);

    /// <summary>提交当前事务并关闭事务连接。</summary>
    Task<bool> CommitAsync(string connectionName, CancellationToken cancellationToken = default);

    /// <summary>回滚当前事务并关闭事务连接。</summary>
    Task<bool> RollbackAsync(string connectionName, CancellationToken cancellationToken = default);

    /// <summary>查询当前连接是否处于活动事务中。</summary>
    bool IsTransactionActive(string connectionName);

    /// <summary>设置当前连接的自动提交模式（true=每条 SQL 自动提交；false=手动事务）。</summary>
    void SetAutoCommit(string connectionName, bool enabled);

    /// <summary>获取当前连接是否处于自动提交模式。</summary>
    bool IsAutoCommit(string connectionName);

    /// <summary>关闭并释放连接（含活动事务连接）。</summary>
    void CloseConnection(string connectionName);

    /// <summary>查询指定连接是否处于已连接状态（对象浏览器已建立连接）。</summary>
    bool IsConnected(string connectionName);

    /// <summary>标记指定连接为已连接（对象浏览器连接成功后调用）。</summary>
    void NotifyConnected(string connectionName);
}
