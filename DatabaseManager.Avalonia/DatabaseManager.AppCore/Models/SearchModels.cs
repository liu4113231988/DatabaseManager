namespace DatabaseManager.AppCore.Models;

/// <summary>元数据搜索结果的对象类别。</summary>
public enum SearchObjectKind
{
    Table,
    View,
    Column,
    Procedure,
    Function,
    Sequence,
}

/// <summary>
/// 元数据搜索结果项（对应 DBeaver 的 DB Metadata Search）。
/// 由 <c>IDbSchemaService.SearchMetadataAsync</code> 返回，供搜索窗口展示与定位。
/// </summary>
public class SearchResultItem
{
    /// <summary>对象类别。</summary>
    public SearchObjectKind Kind { get; set; }

    /// <summary>发起搜索的连接名称。</summary>
    public string ConnectionName { get; set; } = string.Empty;

    /// <summary>所属数据库名。</summary>
    public string DatabaseName { get; set; } = string.Empty;

    /// <summary>Schema 名（可为空）。</summary>
    public string? Schema { get; set; }

    /// <summary>对象名称。</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>父对象名（Kind=Column 时为所属表/视图名）。</summary>
    public string? ParentName { get; set; }

    /// <summary>类别显示文本。</summary>
    public string KindText => Kind switch
    {
        SearchObjectKind.Table => "表",
        SearchObjectKind.View => "视图",
        SearchObjectKind.Column => "列",
        SearchObjectKind.Procedure => "存储过程",
        SearchObjectKind.Function => "函数",
        SearchObjectKind.Sequence => "序列",
        _ => "对象",
    };

    /// <summary>带 Schema 的全名（schema.name）。</summary>
    public string FullName => string.IsNullOrEmpty(Schema) ? Name : $"{Schema}.{Name}";

    /// <summary>列表展示文本（列显示 表.列，其余显示 schema.name）。</summary>
    public string DisplayText => Kind == SearchObjectKind.Column
        ? $"{ParentName}.{Name}"
        : FullName;
}
