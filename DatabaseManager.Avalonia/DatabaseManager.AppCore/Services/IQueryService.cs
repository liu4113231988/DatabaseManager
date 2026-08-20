using DatabaseManager.AppCore.Models;

namespace DatabaseManager.AppCore.Services;

/// <summary>
/// 查询服务。封装 SQL 查询执行与结果获取。
/// </summary>
public interface IQueryService
{
    /// <summary>执行一条 SQL 语句，返回查询结果（含列、行数据、受影响行数与耗时）。</summary>
    Task<QueryResult> ExecuteAsync(string connectionName, string sql, CancellationToken cancellationToken = default);
}
