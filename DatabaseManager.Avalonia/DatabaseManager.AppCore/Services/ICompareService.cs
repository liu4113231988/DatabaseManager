using DatabaseInterpreter.Model;
using DatabaseManager.AppCore.Models;

namespace DatabaseManager.AppCore.Services;

/// <summary>
/// 结构/数据对比服务（阶段 4）。封装 <c>SchemaCompare</c> / <c>DataCompare</c> 能力。
/// </summary>
public interface ICompareService
{
    /// <summary>
    /// 对比两个（同类型）数据库的结构差异。
    /// </summary>
    /// <param name="source">源连接。</param>
    /// <param name="target">目标连接。</param>
    /// <param name="databaseObjectType">要对比的数据库对象类型（表/视图/函数/过程等）。</param>
    /// <param name="onFeedback">进度/反馈回调。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>结构差异树根节点列表。</returns>
    Task<IReadOnlyList<SchemaCompareItem>> CompareSchemaAsync(
        ConnectionItem source,
        ConnectionItem target,
        DatabaseObjectType databaseObjectType,
        Action<string>? onFeedback = null,
        CancellationToken cancellationToken = default);
}
