using DatabaseManager.AppCore.Models;

namespace DatabaseManager.AppCore.Services;

/// <summary>
/// 数据库优化服务（阶段 4）。
/// 复用 <c>DatabaseManager.Core.Optimizer</c> 对数据库执行优化（SQLite VACUUM / MySQL 表整理），返回优化前后数据长度。
/// </summary>
public interface IOptimizeService
{
    /// <summary>
    /// 对指定连接执行数据库优化。
    /// </summary>
    /// <param name="connection">目标连接。</param>
    /// <param name="onFeedback">实时反馈回调。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    Task<IReadOnlyList<OptimizeResultItem>> OptimizeAsync(
        ConnectionItem connection,
        Action<string>? onFeedback = null,
        CancellationToken cancellationToken = default);
}
