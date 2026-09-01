namespace DatabaseManager.AppCore.Services;

/// <summary>脚本库条目。</summary>
public class ScriptLibraryItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public string Name { get; set; } = string.Empty;

    public string SqlText { get; set; } = string.Empty;

    public string Category { get; set; } = "默认";

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public DateTime UpdatedAt { get; set; } = DateTime.Now;
}

/// <summary>
/// 脚本库服务：用户脚本 CRUD + 最近打开的 SQL 脚本文件，JSON 文件持久化。
/// </summary>
public interface IScriptLibraryService
{
    /// <summary>获取全部用户脚本（按更新时间倒序）。</summary>
    IReadOnlyList<ScriptLibraryItem> GetAll();

    /// <summary>新增或更新脚本（按 Id 判断）。</summary>
    void Save(ScriptLibraryItem item);

    /// <summary>删除脚本。</summary>
    bool Delete(string id);

    /// <summary>获取最近打开的脚本文件路径（最新在前）。</summary>
    IReadOnlyList<string> GetRecentFiles();

    /// <summary>记录最近打开的脚本文件（去重、上限 15 条）。</summary>
    void AddRecentFile(string path);
}
