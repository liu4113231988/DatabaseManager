using Avalonia.Controls;
using Avalonia.Interactivity;
using DatabaseManager.AppCore.Models;
using DatabaseManager.AppCore.Services;
using DatabaseManager.AppCore.ViewModels;

namespace DatabaseManager.Avalonia.Views;

/// <summary>
/// 查询结果图表窗口：把查询结果绘制为柱状/折线/饼图，可保存为仪表盘图表。
/// 数据来源两种：当前查询标签的结果集，或仪表盘图表定义（重新执行 SQL）。
/// </summary>
public partial class ChartWindow : Window
{
    /// <summary>数据列名。</summary>
    private List<string> _columns = new();

    /// <summary>数据行（列名 → 字符串值）。</summary>
    private List<Dictionary<string, string>> _rows = new();

    /// <summary>从仪表盘打开时的定义（保存时更新而非新建）。</summary>
    private DashboardChart? _existingChart;

    private readonly IDashboardService? _dashboardService;

    public ChartWindow()
    {
        InitializeComponent();
    }

    /// <summary>从查询标签创建（推荐入口）。</summary>
    public ChartWindow(QueryTabViewModel tab, IDashboardService? dashboardService)
        : this()
    {
        _dashboardService = dashboardService;
        _columns = tab.Columns.ToList();
        _rows = tab.GetAllRowsSnapshot()
            .Select(r => _columns.ToDictionary(c => c, c => r[c]?.ToString() ?? string.Empty))
            .ToList();
        _existingChart = null;

        DataContext = new ChartContext
        {
            ConnectionName = tab.ConnectionName,
            Database = tab.DatabaseName,
            Sql = tab.SqlText,
        };

        PopulateColumns(tab.ConnectionName, tab.DatabaseName, tab.SqlText);
    }

    /// <summary>从仪表盘图表定义创建（重新执行 SQL 获取数据）。</summary>
    public ChartWindow(DashboardChart chart, QueryResult result, IDashboardService? dashboardService)
        : this()
    {
        _dashboardService = dashboardService;
        _existingChart = chart;

        if (result.IsSuccess && !result.IsNonQuery)
        {
            _columns = result.Columns.ToList();
            _rows = result.Rows.Select(r => _columns
                .Select((c, i) => new KeyValuePair<string, string>(c, i < r.Count ? r[i] ?? string.Empty : string.Empty))
                .ToDictionary(kv => kv.Key, kv => kv.Value)).ToList();
        }

        PopulateColumns(chart.ConnectionName, chart.Database, chart.Sql);

        ComboChartType.SelectedIndex = chart.ChartType switch
        {
            ChartTypes.Line => 1,
            ChartTypes.Pie => 2,
            _ => 0,
        };
        ComboXColumn.SelectedItem = chart.XColumn;
        ComboAggregation.SelectedItem = chart.Aggregation;
        TxtChartName.Text = chart.Name;

        BtnRender_Click(this, new RoutedEventArgs());
    }

    private void PopulateColumns(string? connectionName, string? database, string? sql)
    {
        ComboXColumn.ItemsSource = _columns;
        ListYColumns.ItemsSource = _columns;

        if (_columns.Count > 0)
        {
            ComboXColumn.SelectedIndex = 0;
        }

        if (_existingChart is null)
        {
            TxtChartName.Text = $"图表 {DateTime.Now:MMdd-HHmm}";
        }

        Title = string.IsNullOrEmpty(_existingChart?.Name)
            ? Title
            : $"{Title} - {_existingChart.Name}";
    }

    private void ComboAggregation_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        // 聚合模式只使用第一个 Y 列（提示语已说明）。
    }

    private void BtnRender_Click(object? sender, RoutedEventArgs e)
    {
        var model = BuildChartModel();
        Chart.Chart = model;
        Chart.InvalidateVisual();
    }

    private ChartRenderModel BuildChartModel()
    {
        string chartType = (ComboChartType.SelectedIndex) switch
        {
            1 => ChartTypes.Line,
            2 => ChartTypes.Pie,
            _ => ChartTypes.Bar,
        };

        string aggregation = (ComboAggregation.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? ChartAggregations.None;
        string? xColumn = ComboXColumn.SelectedItem as string;
        var yColumns = (ListYColumns.SelectedItems as System.Collections.IEnumerable)?
            .OfType<string>().ToList() ?? new List<string>();

        var model = new ChartRenderModel
        {
            ChartType = chartType,
            Title = TxtChartName.Text?.Trim() ?? string.Empty,
        };

        if (string.IsNullOrEmpty(xColumn) || yColumns.Count == 0)
        {
            return model;
        }

        if (aggregation == ChartAggregations.None)
        {
            // 每行一个数据点（最多 100 行）。
            foreach (var row in _rows.Take(100))
            {
                model.Labels.Add(Shorten(row.GetValueOrDefault(xColumn, string.Empty)));
            }

            foreach (var yColumn in yColumns)
            {
                var series = new ChartSeriesModel { Name = yColumn };
                foreach (var row in _rows.Take(100))
                {
                    series.Values.Add(TryParse(row.GetValueOrDefault(yColumn, string.Empty)));
                }

                model.Series.Add(series);
            }
        }
        else
        {
            // 按 X 列分组聚合（仅使用第一个 Y 列）。
            string yColumn = yColumns[0];
            var groups = _rows
                .GroupBy(r => r.GetValueOrDefault(xColumn, string.Empty), StringComparer.OrdinalIgnoreCase)
                .Take(100)
                .ToList();

            foreach (var group in groups)
            {
                model.Labels.Add(Shorten(group.Key));

                double value = aggregation switch
                {
                    ChartAggregations.Count => group.Count(),
                    ChartAggregations.Sum => group.Sum(r => TryParse(r.GetValueOrDefault(yColumn, string.Empty))),
                    ChartAggregations.Average => group.Average(r => TryParse(r.GetValueOrDefault(yColumn, string.Empty))),
                    _ => 0,
                };

                model.Series.Add(new ChartSeriesModel
                {
                    Name = aggregation == ChartAggregations.Count ? "计数" : yColumn,
                    Values = new List<double> { value },
                });
            }

            // 分组模式：单系列需要把每个分组的值合并到一个系列。
            if (model.Series.Count > 0)
            {
                var merged = new ChartSeriesModel
                {
                    Name = aggregation == ChartAggregations.Count ? "计数" : yColumn,
                    Values = model.Series.Select(s => s.Values[0]).ToList(),
                };
                model.Series.Clear();
                model.Series.Add(merged);
            }
        }

        return model;
    }

    private void BtnSaveToDashboard_Click(object? sender, RoutedEventArgs e)
    {
        if (_dashboardService is null)
        {
            TxtHint.Text = "仪表盘服务不可用，无法保存。";
            return;
        }

        string chartType = ComboChartType.SelectedIndex switch
        {
            1 => ChartTypes.Line,
            2 => ChartTypes.Pie,
            _ => ChartTypes.Bar,
        };

        string xColumn = ComboXColumn.SelectedItem as string ?? string.Empty;
        var yColumns = (ListYColumns.SelectedItems as System.Collections.IEnumerable)?
            .OfType<string>().ToList() ?? new List<string>();

        if (string.IsNullOrEmpty(xColumn) || yColumns.Count == 0)
        {
            TxtHint.Text = "请先选择 X 列与至少一个 Y 列。";
            return;
        }

        var chart = _existingChart ?? new DashboardChart();
        chart.Name = TxtChartName.Text?.Trim() ?? $"图表 {DateTime.Now:MMdd-HHmm}";
        chart.ChartType = chartType;
        chart.XColumn = xColumn;
        chart.YColumns = yColumns;
        chart.Aggregation = (ComboAggregation.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? ChartAggregations.None;

        if (_existingChart is null)
        {
            if (DataContext is ChartContext context)
            {
                chart.ConnectionName = context.ConnectionName;
                chart.Database = context.Database;
                chart.Sql = context.Sql;
            }
        }

        _dashboardService.Save(chart);
        _existingChart = chart;
        TxtHint.Text = $"已保存到仪表盘：{chart.Name}。可在「工具 → 仪表盘...」查看。";
    }

    private void BtnClose_Click(object? sender, RoutedEventArgs e) => Close();

    private static double TryParse(string value)
        => double.TryParse(value, out double result) ? result : 0;

    private static string Shorten(string text)
    {
        text ??= string.Empty;
        return text.Length <= 16 ? text : text[..16] + "…";
    }
}

/// <summary>图表窗口的数据上下文（查询标签来源时携带 SQL 定义供保存仪表盘）。</summary>
public class ChartContext
{
    public string ConnectionName { get; set; } = string.Empty;

    public string? Database { get; set; }

    public string Sql { get; set; } = string.Empty;
}
