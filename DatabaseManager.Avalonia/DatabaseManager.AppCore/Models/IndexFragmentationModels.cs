using CommunityToolkit.Mvvm.ComponentModel;
using DatabaseManager.Core.Model;

namespace DatabaseManager.AppCore.Models;

/// <summary>
/// 索引碎片（UI 友好）。对应底层 <see cref="IndexFragmentation"/>。
/// </summary>
public partial class IndexFragmentationItem : ObservableObject
{
    /// <summary>所属数据库名（重建 SQL 限定范围时使用）。</summary>
    public string DatabaseName { get; }

    /// <summary>Schema。</summary>
    public string Schema { get; }

    /// <summary>表名。</summary>
    public string TableName { get; }

    /// <summary>索引名。</summary>
    public string IndexName { get; }

    /// <summary>碎片率（%）。</summary>
    public string FragmentationPercent { get; }

    /// <summary>UI 多选是否勾选。</summary>
    [ObservableProperty]
    private bool _isSelected = true;

    /// <summary>展示用表名（含 Schema）。</summary>
    public string DisplayTableName =>
        string.IsNullOrEmpty(Schema) ? TableName : $"{Schema}.{TableName}";

    public IndexFragmentationItem(IndexFragmentation frag, string? databaseName = null)
    {
        DatabaseName = databaseName ?? string.Empty;
        Schema = frag.Schema ?? string.Empty;
        TableName = frag.TableName ?? string.Empty;
        IndexName = frag.IndexName ?? string.Empty;
        FragmentationPercent = frag.FragmentationPercent ?? string.Empty;
    }
}
