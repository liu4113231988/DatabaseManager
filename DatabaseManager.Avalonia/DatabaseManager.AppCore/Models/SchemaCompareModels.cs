using CommunityToolkit.Mvvm.ComponentModel;
using DatabaseInterpreter.Model;
using DatabaseManager.Core.Model;

namespace DatabaseManager.AppCore.Models;

/// <summary>
/// 结构对比结果节点（UI 友好）。包装 <see cref="SchemaCompareDifference"/> 为可绑定树结构。
/// </summary>
public partial class SchemaCompareItem : ObservableObject
{
    /// <summary>底层差异数据。</summary>
    public SchemaCompareDifference Difference { get; }

    /// <summary>节点显示文本。</summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>变更类型（Added / Modified / Deleted / None）。</summary>
    public SchemaCompareDifferenceType DifferenceType => Difference.DifferenceType;

    /// <summary>对象类型名（Table / View / Column / Index ...）。</summary>
    public string ObjectType => Difference.Type;

    /// <summary>变更类型显示标签。</summary>
    public string DifferenceTypeText => DifferenceType switch
    {
        SchemaCompareDifferenceType.Added => "新增",
        SchemaCompareDifferenceType.Modified => "修改",
        SchemaCompareDifferenceType.Deleted => "删除",
        _ => string.Empty,
    };

    /// <summary>源对象名。</summary>
    public string? SourceName => Difference.SourceName;

    /// <summary>目标对象名。</summary>
    public string? TargetName => Difference.TargetName;

    /// <summary>差异简述（用于详情展示）。</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>子节点。</summary>
    public IList<SchemaCompareItem> Children { get; } = new List<SchemaCompareItem>();

    /// <summary>是否选中（用于选择性生成/应用变更脚本；默认全选，父节点勾选会传播到子节点）。</summary>
    [ObservableProperty]
    private bool _isSelected = true;

    partial void OnIsSelectedChanged(bool value)
    {
        // 文件夹/表节点勾选状态传播到全部子节点（叶子无子节点，不会形成循环）。
        foreach (var child in Children)
        {
            child.IsSelected = value;
        }
    }

    public SchemaCompareItem(SchemaCompareDifference difference)
    {
        Difference = difference;
    }

    /// <summary>是否为可展开的文件夹节点（Table 或根）。</summary>
    public bool IsExpandable => DifferenceType == SchemaCompareDifferenceType.None
                                || Difference.DatabaseObjectType == DatabaseObjectType.Table;
}
