using DatabaseManager.AppCore.Models;

namespace DatabaseManager.AppCore.Services;

/// <summary>
/// 索引碎片重建结果（单条索引）。
/// </summary>
public sealed record IndexRebuildResult(
    string Schema,
    string TableName,
    string IndexName,
    bool IsOK,
    string Message);

/// <summary>
/// 索引碎片分析服务（阶段 5）。
/// 复用 <c>DatabaseManager.Core.Analysiser</c> 获取索引碎片并支持重建索引。
/// </summary>
public interface IIndexFragmentationService
{
    /// <summary>获取各索引碎片率。</summary>
    Task<IReadOnlyList<IndexFragmentationItem>> GetIndexFragmentationsAsync(
        ConnectionItem connection,
        Action<string>? onFeedback = null,
        CancellationToken cancellationToken = default);

    /// <summary>批量重建勾选的索引（按顺序逐条执行，返回每条索引的执行结果）。</summary>
    Task<IReadOnlyList<IndexRebuildResult>> RebuildIndexesAsync(
        ConnectionItem connection,
        IReadOnlyList<IndexFragmentationItem> items,
        CancellationToken cancellationToken = default);
}
