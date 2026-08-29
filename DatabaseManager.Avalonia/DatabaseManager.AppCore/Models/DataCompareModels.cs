using System.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using DatabaseManager.Core.Model;

namespace DatabaseManager.AppCore.Models;

/// <summary>
/// 数据对比结果（UI 友好）。对应底层 <see cref="DataCompareResultDetail"/>。
/// 描述单张表在源/目标数据库中的数据差异概览。
/// </summary>
public partial class DataCompareResultItem : ObservableObject
{
    /// <summary>底层差异明细（含各分类 Key 行集合）。</summary>
    public DataCompareResultDetail Detail { get; }

    /// <summary>对比顺序。</summary>
    public int Order => Detail.Order;

    /// <summary>表名。</summary>
    public string TableName => Detail.SourceTable?.Name ?? string.Empty;

    /// <summary>源库记录数。</summary>
    public long SourceRecordCount => Detail.SourceTableRecordCount;

    /// <summary>目标库记录数。</summary>
    public long TargetRecordCount => Detail.TargetTableRecordCount;

    /// <summary>差异记录数（相同主键但值不同）。</summary>
    public int DifferentCount => Detail.DifferentCount;

    /// <summary>仅在源库存在的记录数。</summary>
    public int OnlyInSourceCount => Detail.OnlyInSourceCount;

    /// <summary>仅在目标库存在的记录数。</summary>
    public int OnlyInTargetCount => Detail.OnlyInTargetCount;

    /// <summary>完全一致的记录数。</summary>
    public int IdenticalCount => Detail.IdenticalCount;

    /// <summary>是否完全一致。</summary>
    public bool IsIdentical => Detail.IsIndentical;

    /// <summary>是否选中（用于选择性生成/应用同步脚本）。</summary>
    [ObservableProperty]
    private bool _isSelected = true;

    public DataCompareResultItem(DataCompareResultDetail detail)
    {
        Detail = detail;
    }
}

/// <summary>
/// 数据对比展示模式选项。
/// </summary>
public sealed record DataCompareModeOption(DataCompareDisplayMode Value, string DisplayName);
