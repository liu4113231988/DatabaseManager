using DatabaseManager.Core.Model;

namespace DatabaseManager.AppCore.Models;

/// <summary>
/// 索引碎片（UI 友好）。对应底层 <see cref="IndexFragmentation"/>。
/// </summary>
public class IndexFragmentationItem
{
    /// <summary>Schema。</summary>
    public string Schema { get; }

    /// <summary>表名。</summary>
    public string TableName { get; }

    /// <summary>索引名。</summary>
    public string IndexName { get; }

    /// <summary>碎片率（%）。</summary>
    public string FragmentationPercent { get; }

    /// <summary>展示用表名（含 Schema）。</summary>
    public string DisplayTableName =>
        string.IsNullOrEmpty(Schema) ? TableName : $"{Schema}.{TableName}";

    public IndexFragmentationItem(IndexFragmentation frag)
    {
        Schema = frag.Schema ?? string.Empty;
        TableName = frag.TableName ?? string.Empty;
        IndexName = frag.IndexName ?? string.Empty;
        FragmentationPercent = frag.FragmentationPercent ?? string.Empty;
    }
}
