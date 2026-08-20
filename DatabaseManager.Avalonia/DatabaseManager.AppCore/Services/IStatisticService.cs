using DatabaseManager.AppCore.Models;

namespace DatabaseManager.AppCore.Services;

/// <summary>
/// 数据库统计服务（阶段 5）。
/// 复用 <c>DatabaseManager.Core.DbStatistic</c> 完成表记录数与列内容最大长度统计。
/// </summary>
public interface IStatisticService
{
    /// <summary>统计各表记录数。</summary>
    Task<IReadOnlyList<RecordCountItem>> CountTableRecordsAsync(
        ConnectionItem connection,
        Action<string>? onFeedback = null,
        CancellationToken cancellationToken = default);

    /// <summary>统计各字符列的内容最大长度。</summary>
    Task<IReadOnlyList<ColumnLengthItem>> GetTableColumnLengthsAsync(
        ConnectionItem connection,
        Action<string>? onFeedback = null,
        CancellationToken cancellationToken = default);
}
