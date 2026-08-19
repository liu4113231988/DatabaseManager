namespace DatabaseManager.AppCore.Services;

/// <summary>
/// 查询服务。封装 SQL 查询执行与结果获取。
/// </summary>
public interface IQueryService
{
    /// <summary>执行一条 SQL 语句，返回受影响的记录数（或结果描述）。</summary>
    Task<string> ExecuteAsync(string connectionName, string sql, CancellationToken cancellationToken = default);
}
