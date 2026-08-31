using Avalonia.Controls;
using Avalonia.Interactivity;
using DatabaseManager.AppCore.Models;
using DatabaseManager.AppCore.Services;

namespace DatabaseManager.Avalonia.Views;

/// <summary>仪表盘卡片（图表定义 + 渲染模型 + 刷新状态）。</summary>
public class DashboardCard : System.ComponentModel.INotifyPropertyChanged
{
    public DashboardChart Chart { get; }

    public string Name => Chart.Name;

    private ChartRenderModel? _model;

    public ChartRenderModel? Model
    {
        get => _model;
        private set
        {
            if (ReferenceEquals(_model, value)) return;
            _model = value;
            OnPropertyChanged(nameof(Model));
        }
    }

    private string _errorText = string.Empty;

    public string ErrorText
    {
        get => _errorText;
        private set
        {
            if (_errorText == value) return;
            _errorText = value;
            OnPropertyChanged(nameof(ErrorText));
        }
    }

    public string MetaText =>
        $"{Chart.ChartType} · {Chart.Aggregation} · {Chart.ConnectionName}"
        + (string.IsNullOrEmpty(Chart.Database) ? string.Empty : $"/{Chart.Database}");

    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

    public DashboardCard(DashboardChart chart)
    {
        Chart = chart;
    }

    public void Update(ChartRenderModel? model, string error)
    {
        Model = model;
        ErrorText = error;
    }

    private void OnPropertyChanged(string name)
        => PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(name));
}

/// <summary>
/// 仪表盘窗口：以卡片网格展示已保存的图表；「刷新」按定义重新执行 SQL 渲染。
/// </summary>
public partial class DashboardWindow : Window
{
    private readonly IDashboardService _dashboardService;
    private readonly IQueryService _queryService;

    public DashboardWindow()
    {
        InitializeComponent();
    }

    public DashboardWindow(IDashboardService dashboardService, IQueryService queryService)
        : this()
    {
        _dashboardService = dashboardService;
        _queryService = queryService;

        _ = RefreshAllAsync();
    }

    private async void BtnRefresh_Click(object? sender, RoutedEventArgs e) => await RefreshAllAsync();

    private async Task RefreshAllAsync()
    {
        var charts = _dashboardService.GetAll();
        var cards = charts.Select(c => new DashboardCard(c)).ToList();
        CardsHost.ItemsSource = cards;

        foreach (var card in cards)
        {
            await RenderCardAsync(card);
        }
    }

    private async Task RenderCardAsync(DashboardCard card)
    {
        try
        {
            var result = await _queryService.ExecuteAsync(
                card.Chart.ConnectionName, card.Chart.Sql, CancellationToken.None, 120);

            if (!result.IsSuccess)
            {
                card.Update(null, result.ErrorMessage ?? "执行失败");
                return;
            }

            if (result.IsNonQuery)
            {
                card.Update(null, "该 SQL 无结果集");
                return;
            }

            var model = ChartModelBuilder.Build(card.Chart, result);
            card.Update(model, string.Empty);
        }
        catch (Exception ex)
        {
            card.Update(null, ex.Message);
        }
    }

    private async void CardRefresh_Click(object? sender, RoutedEventArgs e)
    {
        if ((sender as Control)?.Tag is DashboardCard card)
        {
            await RenderCardAsync(card);
        }
    }

    private async void CardOpen_Click(object? sender, RoutedEventArgs e)
    {
        if ((sender as Control)?.Tag is not DashboardCard card)
            return;

        var chart = card.Chart;
        var result = await _queryService.ExecuteAsync(chart.ConnectionName, chart.Sql, CancellationToken.None, 120);

        if (!result.IsSuccess)
        {
            card.Update(null, result.ErrorMessage ?? "执行失败");
            return;
        }

        var window = new ChartWindow(chart, result, _dashboardService);
        window.Show(this);
    }

    private async void CardDelete_Click(object? sender, RoutedEventArgs e)
    {
        if ((sender as Control)?.Tag is not DashboardCard card)
            return;

        var confirm = await AppCore.Common.DialogHelper.ShowConfirmAsync(
            "删除图表", $"确定从仪表盘删除「{card.Chart.Name}」吗？");

        if (confirm != true)
        {
            return;
        }

        _dashboardService.Delete(card.Chart.Id);
        await RefreshAllAsync();
    }

    private void BtnClose_Click(object? sender, RoutedEventArgs e) => Close();
}

/// <summary>把查询结果按图表定义转换为渲染模型（ChartWindow 与仪表盘共用）。</summary>
internal static class ChartModelBuilder
{
    public static ChartRenderModel Build(DashboardChart chart, QueryResult result)
    {
        var model = new ChartRenderModel
        {
            ChartType = chart.ChartType,
            Title = chart.Name,
        };

        var columns = result.Columns.ToList();
        if (columns.Count == 0)
        {
            return model;
        }

        int xIndex = FindColumnIndex(columns, chart.XColumn);
        if (xIndex < 0)
        {
            return model;
        }

        var yIndexes = chart.YColumns
            .Select(c => FindColumnIndex(columns, c))
            .Where(i => i >= 0)
            .ToList();

        var rows = result.Rows;

        if (chart.Aggregation == ChartAggregations.None)
        {
            foreach (var row in rows.Take(100))
            {
                model.Labels.Add(GetValue(row, xIndex));
            }

            foreach (int yIndex in yIndexes)
            {
                var series = new ChartSeriesModel { Name = columns[yIndex] };
                foreach (var row in rows.Take(100))
                {
                    series.Values.Add(TryParse(GetValue(row, yIndex)));
                }

                model.Series.Add(series);
            }
        }
        else
        {
            int yIndex = yIndexes.FirstOrDefault(xIndex);
            if (yIndex < 0)
            {
                return model;
            }

            var groups = rows
                .Select((row, index) => (Row: row, Index: index))
                .GroupBy(t => GetValue(t.Row, xIndex), StringComparer.OrdinalIgnoreCase)
                .Take(100)
                .ToList();

            var values = new List<double>();

            foreach (var group in groups)
            {
                model.Labels.Add(Shorten(group.Key));

                double value = chart.Aggregation switch
                {
                    ChartAggregations.Count => group.Count(),
                    ChartAggregations.Sum => group.Sum(t => TryParse(GetValue(t.Row, yIndex))),
                    ChartAggregations.Average => group.Average(t => TryParse(GetValue(t.Row, yIndex))),
                    _ => 0,
                };

                values.Add(value);
            }

            model.Series.Add(new ChartSeriesModel
            {
                Name = chart.Aggregation == ChartAggregations.Count ? "计数" : columns[yIndex],
                Values = values,
            });
        }

        return model;
    }

    private static int FindColumnIndex(List<string> columns, string name)
    {
        for (int i = 0; i < columns.Count; i++)
        {
            if (string.Equals(columns[i], name, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return -1;
    }

    private static string GetValue(IReadOnlyList<string> row, int index)
        => index >= 0 && index < row.Count ? row[index] ?? string.Empty : string.Empty;

    private static double TryParse(string value)
        => double.TryParse(value, out double result) ? result : 0;

    private static string Shorten(string text)
    {
        text ??= string.Empty;
        return text.Length <= 16 ? text : text[..16] + "…";
    }
}
