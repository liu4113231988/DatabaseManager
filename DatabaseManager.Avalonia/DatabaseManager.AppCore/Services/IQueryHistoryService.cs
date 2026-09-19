using DatabaseManager.AppCore.Models;

namespace DatabaseManager.AppCore.Services;

/// <summary>查询历史条目。</summary>
public class QueryHistoryEntry
{
    /// <summary>存储主键（SQLite 自增；仅由查询历史服务填充，用于 Update 定位）。</summary>
    public long Id { get; set; }

    public DateTime Time { get; set; } = DateTime.Now;

    public string ConnectionName { get; set; } = string.Empty;

    public string Database { get; set; } = string.Empty;

    public string SqlText { get; set; } = string.Empty;

    public bool IsSuccess { get; set; }

    public long RowCount { get; set; }

    public long ElapsedMilliseconds { get; set; }

    public string? ErrorMessage { get; set; }

    /// <summary>是否收藏（收藏项不参与 500 条上限裁剪）。</summary>
    public bool IsFavorite { get; set; }

    /// <summary>列表展示用的 SQL 摘要（单行、截断）。</summary>
    public string SqlPreview
    {
        get
        {
            var text = SqlText ?? string.Empty;
            text = text.Replace("\r", " ").Replace("\n", " ").Trim();
            return text.Length <= 80 ? text : text[..80] + "...";
        }
    }
}

/// <summary>
/// 查询历史服务：记录每次 SQL 执行（成功与失败），按 JSON 文件持久化（上限 500 条）。
/// </summary>
public interface IQueryHistoryService
{
    /// <summary>记录一次执行。</summary>
    void Add(QueryHistoryEntry entry);

    /// <summary>更新一条历史记录（用于收藏标记等元数据变更），按引用或时间匹配。</summary>
    void Update(QueryHistoryEntry entry);

    /// <summary>按时间倒序获取最近的历史记录。</summary>
    IReadOnlyList<QueryHistoryEntry> GetRecent(int maxCount = 200);

    /// <summary>清空全部历史。</summary>
    void Clear();
}
