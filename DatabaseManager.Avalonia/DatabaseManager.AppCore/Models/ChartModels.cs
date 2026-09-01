namespace DatabaseManager.AppCore.Models;

/// <summary>图表类型。</summary>
public static class ChartTypes
{
    public const string Bar = "Bar";
    public const string Line = "Line";
    public const string Pie = "Pie";

    /// <summary>显示名 → 类型值。</summary>
    public static readonly Dictionary<string, string> DisplayNames = new()
    {
        ["柱状图"] = Bar,
        ["折线图"] = Line,
        ["饼图"] = Pie,
    };
}

/// <summary>图表聚合方式。</summary>
public static class ChartAggregations
{
    public const string None = "无";
    public const string Count = "计数";
    public const string Sum = "求和";
    public const string Average = "平均";

    public static readonly string[] All = { None, Count, Sum, Average };
}

/// <summary>图表渲染模型（UI 无关；颜色为 hex 字符串）。</summary>
public class ChartRenderModel
{
    /// <summary>图表类型（ChartTypes）。</summary>
    public string ChartType { get; set; } = ChartTypes.Bar;

    public string Title { get; set; } = string.Empty;

    /// <summary>X 轴标签（分组/行）。</summary>
    public List<string> Labels { get; set; } = new();

    /// <summary>数据系列（饼图仅使用第一个系列）。</summary>
    public List<ChartSeriesModel> Series { get; set; } = new();
}

/// <summary>图表数据系列。</summary>
public class ChartSeriesModel
{
    public string Name { get; set; } = string.Empty;

    public List<double> Values { get; set; } = new();

    /// <summary>颜色（hex，如 #1E88E5；空时由渲染器分配）。</summary>
    public string? ColorHex { get; set; }
}
