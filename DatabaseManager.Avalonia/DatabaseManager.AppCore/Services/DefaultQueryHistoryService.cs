using System.IO;
using Newtonsoft.Json;

namespace DatabaseManager.AppCore.Services;

/// <summary>
/// 查询历史服务实现：JSON 文件存储（随程序目录），内存中保留最近 500 条。
/// </summary>
public class DefaultQueryHistoryService : IQueryHistoryService
{
    private const int MaxEntries = 500;
    private const int MaxSqlLength = 64 * 1024;

    private static readonly object FileLock = new();

    private readonly string _filePath;
    private readonly List<QueryHistoryEntry> _entries;

    public DefaultQueryHistoryService()
    {
        var dir = Path.Combine(AppContext.BaseDirectory, "Profiles");
        Directory.CreateDirectory(dir);
        _filePath = Path.Combine(dir, "query-history.json");

        _entries = Load();
    }

    public void Add(QueryHistoryEntry entry)
    {
        if (entry is null)
        {
            return;
        }

        if (entry.SqlText is { Length: > MaxSqlLength })
        {
            entry.SqlText = entry.SqlText[..MaxSqlLength];
        }

        lock (FileLock)
        {
            _entries.Insert(0, entry);
            if (_entries.Count > MaxEntries)
            {
                _entries.RemoveRange(MaxEntries, _entries.Count - MaxEntries);
            }

            Save();
        }
    }

    public IReadOnlyList<QueryHistoryEntry> GetRecent(int maxCount = 200)
    {
        lock (FileLock)
        {
            return _entries.Take(Math.Max(1, maxCount)).ToList();
        }
    }

    public void Clear()
    {
        lock (FileLock)
        {
            _entries.Clear();
            Save();
        }
    }

    private List<QueryHistoryEntry> Load()
    {
        try
        {
            if (!File.Exists(_filePath))
            {
                return new List<QueryHistoryEntry>();
            }

            return JsonConvert.DeserializeObject<List<QueryHistoryEntry>>(File.ReadAllText(_filePath))
                   ?? new List<QueryHistoryEntry>();
        }
        catch
        {
            // 历史文件损坏时静默重建，不影响主流程。
            return new List<QueryHistoryEntry>();
        }
    }

    private void Save()
    {
        try
        {
            File.WriteAllText(_filePath, JsonConvert.SerializeObject(_entries, Formatting.Indented));
        }
        catch
        {
            // 写入失败（如磁盘只读）时忽略，历史记录仅在内存中生效。
        }
    }
}
