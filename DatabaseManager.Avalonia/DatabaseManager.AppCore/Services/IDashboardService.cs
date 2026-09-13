using Newtonsoft.Json;
using DatabaseManager.AppCore.Models;

namespace DatabaseManager.AppCore.Services;

/// <summary>仪表盘上保存的一张图表定义。</summary>
public class DashboardChart
{
    public string Page { get; set; } = "首页";
    public int Position { get; set; }
    public int CardWidth { get; set; } = 450;
    public int CardHeight { get; set; } = 280;
    public List<CalculatedField> CalculatedFields { get; set; } = new();
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

/// <summary>仪表盘定义使用原子文件替换；写入失败保留上一版内存状态。</summary>
public class DefaultDashboardService : IDashboardService
{
    private static readonly object FileLock = new();
    private readonly string _filePath;
    private List<DashboardChart> _items;

    public DefaultDashboardService(string? storageDirectory = null)
    {
        var dir = storageDirectory ?? Path.Combine(AppContext.BaseDirectory, "Profiles");
        Directory.CreateDirectory(dir);
        _filePath = Path.Combine(dir, "dashboard-charts.json");
        _items = Load();
    }

    private static List<DashboardChart> Copy(List<DashboardChart> items) => JsonConvert.DeserializeObject<List<DashboardChart>>(JsonConvert.SerializeObject(items))!;
    public IReadOnlyList<DashboardChart> GetAll() { lock (FileLock) return Copy(_items); }

    public void Save(DashboardChart chart)
    {
        if (string.IsNullOrWhiteSpace(chart.Page) || chart.CardWidth is < 300 or > 1200 || chart.CardHeight is < 220 or > 1000) throw new ArgumentException("仪表盘页名或卡片尺寸无效。");
        if (SqlSafety.ValidateProfilerStatement(chart.Sql) is not null) throw new ArgumentException("仪表盘只允许保存单条 SELECT。");
        lock (FileLock)
        {
            var next = Copy(_items);
            var existing = next.FirstOrDefault(c => c.Id == chart.Id);
            if (existing is not null) next[next.IndexOf(existing)] = chart;
            else next.Add(chart);
            Persist(next);
            _items = Copy(next);
        }
    }

    public void Delete(string id)
    {
        lock (FileLock)
        {
            var next = Copy(_items);
            if (next.RemoveAll(c => c.Id == id) > 0) { Persist(next); _items = next; }
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

    private void Persist(List<DashboardChart> items)
    {
        lock (FileLock)
        {
            File.WriteAllText(_filePath + ".tmp", JsonConvert.SerializeObject(items, Formatting.Indented));
            File.Move(_filePath + ".tmp", _filePath, true);
        }
    }
}
