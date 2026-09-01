using System.IO;
using Newtonsoft.Json;

namespace DatabaseManager.AppCore.Services;

/// <summary>
/// 脚本库服务实现：JSON 文件存储（随程序目录）。
/// </summary>
public class DefaultScriptLibraryService : IScriptLibraryService
{
    private const int MaxRecentFiles = 15;

    private static readonly object FileLock = new();

    private readonly string _filePath;
    private ScriptLibraryStore _store;

    public DefaultScriptLibraryService()
    {
        var dir = Path.Combine(AppContext.BaseDirectory, "Profiles");
        Directory.CreateDirectory(dir);
        _filePath = Path.Combine(dir, "script-library.json");

        _store = Load();
    }

    public IReadOnlyList<ScriptLibraryItem> GetAll()
    {
        lock (FileLock)
        {
            return _store.Scripts.OrderByDescending(s => s.UpdatedAt).ToList();
        }
    }

    public void Save(ScriptLibraryItem item)
    {
        if (item is null)
        {
            return;
        }

        item.UpdatedAt = DateTime.Now;

        lock (FileLock)
        {
            var existing = _store.Scripts.FirstOrDefault(s => s.Id == item.Id);
            if (existing is null)
            {
                _store.Scripts.Add(item);
            }
            else
            {
                existing.Name = item.Name;
                existing.SqlText = item.SqlText;
                existing.Category = item.Category;
                existing.UpdatedAt = item.UpdatedAt;
            }

            Save();
        }
    }

    public bool Delete(string id)
    {
        lock (FileLock)
        {
            var removed = _store.Scripts.RemoveAll(s => s.Id == id);
            if (removed > 0)
            {
                Save();
            }
            return removed > 0;
        }
    }

    public IReadOnlyList<string> GetRecentFiles()
    {
        lock (FileLock)
        {
            return _store.RecentFiles.ToList();
        }
    }

    public void AddRecentFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        lock (FileLock)
        {
            _store.RecentFiles.Remove(path);
            _store.RecentFiles.Insert(0, path);
            if (_store.RecentFiles.Count > MaxRecentFiles)
            {
                _store.RecentFiles.RemoveRange(MaxRecentFiles, _store.RecentFiles.Count - MaxRecentFiles);
            }

            Save();
        }
    }

    private ScriptLibraryStore Load()
    {
        try
        {
            if (!File.Exists(_filePath))
            {
                return new ScriptLibraryStore();
            }

            return JsonConvert.DeserializeObject<ScriptLibraryStore>(File.ReadAllText(_filePath))
                   ?? new ScriptLibraryStore();
        }
        catch
        {
            return new ScriptLibraryStore();
        }
    }

    private void Save()
    {
        try
        {
            File.WriteAllText(_filePath, JsonConvert.SerializeObject(_store, Formatting.Indented));
        }
        catch
        {
            // 写入失败时忽略（内存态仍可用）。
        }
    }

    private sealed class ScriptLibraryStore
    {
        public List<ScriptLibraryItem> Scripts { get; set; } = new();

        public List<string> RecentFiles { get; set; } = new();
    }
}
