using Newtonsoft.Json;
using DatabaseManager.AppCore.Models;

namespace DatabaseManager.AppCore.Services;

/// <summary>仪表盘上保存的一张图表定义。</summary>
public class DashboardChart
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public string Name { get; set; } = string.Empty;

    public string ConnectionName { get; set; } = string.Empty;

    public string? Database { get; set; }

    public string Sql { get; set; } = string.Empty;

    /// <summary>图表类型：Bar / Line / Pie。</summary>
    public string ChartType { get; set; } = "Bar";

    public string XColumn { get; set; } = string.Empty;

    public List<string> YColumns { get; set; } = new();

    /// <summary>聚合方式：无 / 计数 / 求和 / 平均。</summary>
    public string Aggregation { get; set; } = "无";

    /// <summary>渲染时最多取用的结果/分组数量（1-1000）。</summary>
    public int SampleLimit { get; set; } = ChartSampling.DefaultLimit;

    public DateTime CreatedAt { get; set; } = DateTime.Now;
}

/// <summary>
/// 仪表盘服务：图表定义的持久化（Profiles\dashboard-charts.json）。
/// 图表数据由 UI 层经 IQueryService 重新执行 SQL 获取。
/// </summary>
public interface IDashboardService
{
    /// <summary>读取全部图表定义（保存顺序，最新在后）。</summary>
    IReadOnlyList<DashboardChart> GetAll();

    /// <summary>新增或更新图表定义。</summary>
    void Save(DashboardChart chart);

    /// <summary>删除图表定义。</summary>
    void Delete(string id);
}

/// <summary>仪表盘服务默认实现（JSON 文件，锁 + 静默容错模式）。</summary>
public class DefaultDashboardService : IDashboardService
{
    private static readonly object FileLock = new();
    private readonly string _filePath;
    private List<DashboardChart> _items;

    public DefaultDashboardService()
    {
        var dir = Path.Combine(AppContext.BaseDirectory, "Profiles");
        Directory.CreateDirectory(dir);
        _filePath = Path.Combine(dir, "dashboard-charts.json");
        _items = Load();
    }

    public IReadOnlyList<DashboardChart> GetAll() => _items.ToList();

    public void Save(DashboardChart chart)
    {
        var existing = _items.FirstOrDefault(c => c.Id == chart.Id);
        if (existing is not null)
        {
            int index = _items.IndexOf(existing);
            _items[index] = chart;
        }
        else
        {
            _items.Add(chart);
        }

        Persist();
    }

    public void Delete(string id)
    {
        int removed = _items.RemoveAll(c => c.Id == id);
        if (removed > 0)
        {
            Persist();
        }
    }

    private List<DashboardChart> Load()
    {
        try
        {
            if (File.Exists(_filePath))
            {
                return JsonConvert.DeserializeObject<List<DashboardChart>>(File.ReadAllText(_filePath))
                       ?? new List<DashboardChart>();
            }
        }
        catch
        {
            // 损坏时静默重建。
        }

        return new List<DashboardChart>();
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
                // 持久化失败不抛出。
            }
        }
    }
}
