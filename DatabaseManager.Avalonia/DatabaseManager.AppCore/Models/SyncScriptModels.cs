using CommunityToolkit.Mvvm.ComponentModel;
using DatabaseInterpreter.Model;
using DatabaseManager.Core.Model;

namespace DatabaseManager.AppCore.Models;

/// <summary>脚本的类别：结构变更 / 数据同步。</summary>
public enum ScriptKind
{
    Structural = 0,
    Data = 1,
}

/// <summary>
/// 可审阅的同步脚本条目（结构差异或数据差异生成的 SQL）。
/// 在脚本预览窗口中可勾选后统一执行。
/// </summary>
public partial class ScriptItem : ObservableObject
{
    /// <summary>显示标题（如「[修改] 表 dbo.Users」「数据同步：表 Orders」）。</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>补充说明。</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>SQL 文本。</summary>
    public string SqlText { get; set; } = string.Empty;

    /// <summary>脚本类别。</summary>
    public ScriptKind Kind { get; set; } = ScriptKind.Structural;

    /// <summary>是否勾选执行。</summary>
    [ObservableProperty]
    private bool _isSelected = true;

    public ScriptItem()
    {
    }

    public ScriptItem(string title, string sqlText, ScriptKind kind, string description = "")
    {
        Title = title;
        SqlText = sqlText;
        Kind = kind;
        Description = description;
    }
}

/// <summary>
/// 结构对比的完整上下文：差异树 + 扁平差异列表 + 源/目标 SchemaInfo 与连接。
/// 由 <see cref="Services.ICompareService"/> 返回，供生成变更/回滚脚本使用。
/// </summary>
public sealed class SchemaCompareContext
{
    public ConnectionItem Source { get; init; } = null!;

    public ConnectionItem Target { get; init; } = null!;

    public SchemaInfo SourceSchemaInfo { get; init; } = new();

    public SchemaInfo TargetSchemaInfo { get; init; } = new();

    /// <summary>扁平差异列表（SchemaCompare.Compare 的原始输出）。</summary>
    public List<SchemaCompareDifference> Differences { get; init; } = new();

    /// <summary>差异树根节点（UI 展示用）。</summary>
    public IReadOnlyList<SchemaCompareItem> Roots { get; init; } = Array.Empty<SchemaCompareItem>();

    public bool HasDifferences => Roots.Count > 0;
}

/// <summary>脚本执行结果。</summary>
public class ScriptExecutionResult
{
    public bool IsSuccess { get; set; }

    public int ExecutedCount { get; set; }

    public string Message { get; set; } = string.Empty;
}
