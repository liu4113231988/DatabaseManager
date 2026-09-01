using DatabaseManager.AppCore.Models;

namespace DatabaseManager.AppCore.Services;

/// <summary>
/// 执行计划服务：按数据库类型把 SQL 包装为 EXPLAIN/SHOWPLAN 语句并读取计划结果。
/// </summary>
public interface IExecutionPlanService
{
    /// <summary>
    /// 获取指定 SQL 的执行计划。
    /// </summary>
    /// <param name="connection">连接。</param>
    /// <param name="sql">要分析的 SQL（通常为 SELECT）。</param>
    /// <param name="analyze">是否实际执行以获取真实计划/耗时（MySQL/PostgreSQL 支持）。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    Task<QueryResult> ExplainAsync(
        ConnectionItem connection,
        string sql,
        bool analyze = false,
        CancellationToken cancellationToken = default);
}
