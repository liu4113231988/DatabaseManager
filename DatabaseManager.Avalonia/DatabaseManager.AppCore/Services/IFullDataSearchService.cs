using DatabaseManager.AppCore.Models;

namespace DatabaseManager.AppCore.Services;

/// <summary>全库数据搜索选项。</summary>
public class FullDataSearchOptions
{
    /// <summary>限定数据库名（空表示连接默认数据库；当前实现按连接的数据库执行）。</summary>
    public string? Database { get; set; }

    /// <summary>限定 Schema（空表示全部）。</summary>
    public string? Schema { get; set; }

    /// <summary>仅搜索文本类列（否则搜索全部列的字符串化值）。</summary>
    public bool TextColumnsOnly { get; set; } = true;

    /// <summary>包含视图（默认仅表）。</summary>
    public bool IncludeViews { get; set; }

    /// <summary>每张表最多返回的匹配行数。</summary>
    public int MaxMatchesPerTable { get; set; } = 20;

    /// <summary>最多扫描的表/视图数量（控制大库成本）。</summary>
    public int MaxTables { get; set; } = 500;

    /// <summary>单表查询超时秒数。</summary>
    public int CommandTimeoutSeconds { get; set; } = 15;
}

/// <summary>单表的匹配结果。</summary>
public class FullDataSearchTableResult
{
    public string Schema { get; set; } = string.Empty;

    public string TableName { get; set; } = string.Empty;

    /// <summary>命中的列名（客户端二次确认后）。</summary>
    public List<string> MatchedColumns { get; } = new();

    /// <summary>匹配行（列名与值的对应集合）。</summary>
    public List<FullDataSearchRow> Rows { get; } = new();

    /// <summary>表级错误（权限/超时等；跳过该表继续）。</summary>
    public string? Error { get; set; }

    public string DisplayName => string.IsNullOrEmpty(Schema) ? TableName : $"{Schema}.{TableName}";
}

/// <summary>单条匹配行。</summary>
public class FullDataSearchRow
{
    /// <summary>展示用预览文本（命中列=值，最多 3 对）。</summary>
    public string Preview { get; set; } = string.Empty;

    /// <summary>生成 SELECT WHERE 片段所用的条件（列名 → 值）。</summary>
    public List<KeyValuePair<string, string>> Conditions { get; } = new();
}

/// <summary>全库数据搜索结果。</summary>
public class FullDataSearchResult
{
    public int ScannedTables { get; set; }

    public int MatchedTables { get; set; }

    public int TotalMatches { get; set; }

    public long ElapsedMilliseconds { get; set; }

    public string? Error { get; set; }

    public List<FullDataSearchTableResult> Tables { get; } = new();
}

/// <summary>
/// 全库数据搜索服务：跨表/视图搜索数据内容（服务端 LIKE 过滤 + 客户端确认），
/// 逐表执行、支持进度回报与取消；单表错误（超时/权限）不中断整体搜索。
/// </summary>
public interface IFullDataSearchService
{
    /// <summary>
    /// 在指定连接上按关键字搜索数据内容。
    /// </summary>
    Task<FullDataSearchResult> SearchAsync(
        ConnectionItem connection,
        string keyword,
        FullDataSearchOptions? options = null,
        Action<string>? onProgress = null,
        CancellationToken cancellationToken = default);
}
