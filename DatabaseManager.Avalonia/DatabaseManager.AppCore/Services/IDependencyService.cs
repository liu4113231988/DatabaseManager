using DatabaseManager.AppCore.Models;

namespace DatabaseManager.AppCore.Services;

/// <summary>
/// 依赖分析服务（阶段 4）。
/// 复用 <c>DatabaseManager.Core.DepencencyFetcher</c> 分析指定对象的依赖关系。
/// </summary>
public interface IDependencyService
{
    /// <summary>
    /// 分析指定数据库对象的依赖关系。
    /// </summary>
    /// <param name="connection">连接。</param>
    /// <param name="objectType">对象类型（Table / View / Function / Procedure）。</param>
    /// <param name="schema">Schema。</param>
    /// <param name="objectName">对象名。</param>
    /// <param name="dependOnThis">true 表示查询哪些对象依赖它；false 表示它依赖哪些对象。</param>
    Task<IReadOnlyList<DependencyNode>> FetchAsync(
        ConnectionItem connection,
        string objectType,
        string? schema,
        string objectName,
        bool dependOnThis,
        CancellationToken cancellationToken = default);
}
