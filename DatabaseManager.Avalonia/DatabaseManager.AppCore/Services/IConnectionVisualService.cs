using Newtonsoft.Json;

namespace DatabaseManager.AppCore.Services;

/// <summary>
/// 连接可视化标注（分组与颜色标签）。侧车存储于 Profiles\connection-visuals.json，
/// 按连接 Id 关联，不改动底层连接 Profile 的数据结构。
/// </summary>
public class ConnectionVisualInfo
{
    public string ConnectionId { get; set; } = string.Empty;

    public string ConnectionName { get; set; } = string.Empty;

    /// <summary>分组名（空表示未分组）。</summary>
    public string? Group { get; set; }

    /// <summary>颜色标签（hex，如 #1E88E5；空表示无色）。</summary>
    public string? ColorTag { get; set; }
}

/// <summary>连接可视化标注服务。</summary>
public interface IConnectionVisualService
{
    /// <summary>读取全部标注。</summary>
    IReadOnlyList<ConnectionVisualInfo> GetAll();

    /// <summary>按连接 Id 查找标注（未设置时返回 null）。</summary>
    ConnectionVisualInfo? Find(string? connectionId);

    /// <summary>保存/更新标注（group/colorTag 传空串表示清除该项）。</summary>
    void Save(string connectionId, string connectionName, string? group, string? colorTag);

    /// <summary>删除标注（连接被删除时调用）。</summary>
    void Remove(string connectionId);

    /// <summary>预置颜色标签（hex 列表，供连接编辑器选择）。</summary>
    IReadOnlyList<string> PaletteColors { get; }
}

/// <summary>
/// 默认实现：JSON 文件持久化（仿 task-history 的锁 + 静默容错模式）。
/// </summary>
public class DefaultConnectionVisualService : IConnectionVisualService
{
    private static readonly object FileLock = new();
    private readonly string _filePath;
    private List<ConnectionVisualInfo> _items;

    public IReadOnlyList<string> PaletteColors { get; } = new[]
    {
        "#E53935", "#FB8C00", "#F6BF26", "#43A047", "#00ACC1",
        "#1E88E5", "#5E35B1", "#8E24AA", "#D81B60", "#6D4C41",
    };

    public DefaultConnectionVisualService()
    {
        var dir = Path.Combine(AppContext.BaseDirectory, "Profiles");
        Directory.CreateDirectory(dir);
        _filePath = Path.Combine(dir, "connection-visuals.json");
        _items = Load();
    }

    public IReadOnlyList<ConnectionVisualInfo> GetAll() => _items.ToList();

    public ConnectionVisualInfo? Find(string? connectionId)
        => string.IsNullOrEmpty(connectionId)
            ? null
            : _items.FirstOrDefault(i => string.Equals(i.ConnectionId, connectionId, StringComparison.OrdinalIgnoreCase));

    public void Save(string connectionId, string connectionName, string? group, string? colorTag)
    {
        if (string.IsNullOrEmpty(connectionId))
            return;

        group = string.IsNullOrWhiteSpace(group) ? null : group.Trim();
        colorTag = string.IsNullOrWhiteSpace(colorTag) ? null : colorTag.Trim();

        var existing = Find(connectionId);
        if (existing is not null)
        {
            existing.ConnectionName = connectionName;
            existing.Group = group;
            existing.ColorTag = colorTag;
        }
        else
        {
            _items.Add(new ConnectionVisualInfo
            {
                ConnectionId = connectionId,
                ConnectionName = connectionName,
                Group = group,
                ColorTag = colorTag,
            });
        }

        Persist();
    }

    public void Remove(string connectionId)
    {
        if (string.IsNullOrEmpty(connectionId))
            return;

        int removed = _items.RemoveAll(i => string.Equals(i.ConnectionId, connectionId, StringComparison.OrdinalIgnoreCase));
        if (removed > 0)
        {
            Persist();
        }
    }

    private List<ConnectionVisualInfo> Load()
    {
        try
        {
            if (File.Exists(_filePath))
            {
                return JsonConvert.DeserializeObject<List<ConnectionVisualInfo>>(File.ReadAllText(_filePath))
                       ?? new List<ConnectionVisualInfo>();
            }
        }
        catch
        {
            // 损坏时静默重建，不影响主流程。
        }

        return new List<ConnectionVisualInfo>();
    }

    private void Persist()
    {
        lock (FileLock)
        {
            try
            {
                File.WriteAllText(_filePath, JsonConvert.SerializeObject(_items, Formatting.Indented));
            }
            catch
            {
                // 持久化失败不抛出（与 task-history 同策略）。
            }
        }
    }
}
