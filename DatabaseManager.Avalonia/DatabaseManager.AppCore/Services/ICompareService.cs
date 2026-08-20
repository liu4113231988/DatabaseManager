using System.Data;
using DatabaseInterpreter.Model;
using DatabaseManager.AppCore.Models;
using DatabaseManager.Core.Model;

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

    /// <summary>
    /// 获取指定连接的数据库中的表列表（用于数据对比时选择要对比的表）。
    /// </summary>
    /// <param name="connection">源连接。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    Task<IReadOnlyList<TableItem>> GetTablesAsync(
        ConnectionItem connection,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 对比两个（同类型）数据库的指定表数据差异。
    /// </summary>
    /// <param name="source">源连接。</param>
    /// <param name="target">目标连接。</param>
    /// <param name="tableNames">要对比的表名列表。</param>
    /// <param name="displayMode">展示模式（Different / OnlyInSource / OnlyInTarget / Identical）。</param>
    /// <param name="onFeedback">进度/反馈回调。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>各表的数据差异概览。</returns>
    Task<IReadOnlyList<DataCompareResultItem>> CompareDataAsync(
        ConnectionItem source,
        ConnectionItem target,
        IReadOnlyList<string> tableNames,
        DataCompareDisplayMode displayMode = DataCompareDisplayMode.None,
        Action<string>? onFeedback = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 查看某表在指定分类下的数据行（分页）。
    /// </summary>
    /// <param name="source">源连接。</param>
    /// <param name="target">目标连接。</param>
    /// <param name="detail">对比明细。</param>
    /// <param name="category">数据分类（Different / OnlyInSource / OnlyInTarget / Identical）。</param>
    /// <param name="pageSize">每页行数。</param>
    /// <param name="pageNumber">页码（从 1 开始）。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>数据表格（DataTable）与列的差异标记信息。</returns>
    Task<(DataTable Data, Dictionary<int, List<DataCompareValueInfo>> ValueInfos)> GetTableDataAsync(
        ConnectionItem source,
        ConnectionItem target,
        DataCompareResultDetail detail,
        string category,
        int pageSize,
        long pageNumber,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 生成数据同步脚本（DELETE/UPDATE/INSERT），用于将目标库数据同步为源库。
    /// </summary>
    /// <param name="source">源连接。</param>
    /// <param name="target">目标连接。</param>
    /// <param name="details">对比明细列表。</param>
    /// <param name="onFeedback">进度/反馈回调。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>同步脚本文本。</returns>
    Task<string> GenerateSyncScriptsAsync(
        ConnectionItem source,
        ConnectionItem target,
        IReadOnlyList<DataCompareResultDetail> details,
        Action<string>? onFeedback = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// 数据对比的表选择项（UI 友好）。
/// </summary>
public sealed record TableItem(string Name, string? Schema, string DisplayName)
{
    public bool IsSelected { get; set; } = true;
}
