using DatabaseManager.AppCore.Models;

namespace DatabaseManager.AppCore.Services;

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

    /// <summary>重建指定索引，返回是否成功及消息。</summary>
    Task<(bool IsOK, string Message)> RebuildIndexAsync(
        ConnectionItem connection,
        IndexFragmentationItem item,
        CancellationToken cancellationToken = default);
}
