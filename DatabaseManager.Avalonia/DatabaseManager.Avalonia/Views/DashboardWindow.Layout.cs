using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using DatabaseManager.AppCore.Models;
using DatabaseManager.AppCore.Services;
using DatabaseManager.Avalonia.Controls;
using Newtonsoft.Json;
using System.Net;

namespace DatabaseManager.Avalonia.Views;

public partial class DashboardWindow
{
    private void UpdatePages(string? selected = null)
    {
        _updatingPages = true;
        var page = selected ?? PageSelector.SelectedItem?.ToString() ?? "首页";
        var pages = _dashboardService.GetAll().Select(c => c.Page).Append("首页").Distinct().Order().ToArray();
        PageSelector.ItemsSource = pages; PageSelector.SelectedItem = pages.Contains(page) ? page : pages[0]; _updatingPages = false;
    }
    private async void PageChanged(object? sender, SelectionChangedEventArgs e) { if (!_updatingPages && _dashboardService is not null) await RefreshAllAsync(); }
    private async Task<QueryResult> ReadChartAsync(DashboardChart chart)
    {
        var invalid = SqlSafety.ValidateProfilerStatement(chart.Sql); if (invalid is not null) throw new InvalidOperationException("仪表盘只允许单条 SELECT 查询。");
        if (_connections is null) return await _queryService.ExecuteAsync(chart.ConnectionName, chart.Sql, _lifetime.Token, 120);
        var saved = _connections.GetConnections().FirstOrDefault(c => c.Name == chart.ConnectionName) ?? throw new InvalidOperationException("找不到图表连接。");
        var connection = JsonConvert.DeserializeObject<ConnectionItem>(JsonConvert.SerializeObject(saved))!; connection.Ssh = saved.Ssh;
        if (!string.IsNullOrWhiteSpace(chart.Database)) connection.Database = chart.Database;
        return await _queryService.ExecuteStandaloneAsync(connection, chart.Sql, _lifetime.Token, 120);
    }
    private void ApplyCardFilter(DashboardCard card)
    {
        if (card.RawResult is null) return;
        try
        {
            var result = DashboardTransform.Apply(card.RawResult, card.Chart.CalculatedFields, FilterColumn.Text, FilterValue.Text);
            card.Update(ChartModelBuilder.Build(card.Chart, result), result.IsTruncated ? "结果已截断，图表基于样本。" : "");
        }
        catch (Exception ex) { card.Update(null, ex.Message); }
    }
    private void ApplyFilter_Click(object? sender, RoutedEventArgs e) { foreach (var card in _cards) ApplyCardFilter(card); }
    private void ClearFilter_Click(object? sender, RoutedEventArgs e) { FilterColumn.Text = ""; FilterValue.Text = ""; ApplyFilter_Click(sender, e); }
    private void CardFilterChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (sender is ComboBox { Tag: DashboardCard card, SelectedItem: string value }) { FilterColumn.Text = card.Chart.XColumn; FilterValue.Text = value; foreach (var target in _cards) ApplyCardFilter(target); }
    }
    private void Presentation_Click(object? sender, RoutedEventArgs e)
    {
        bool presentation = WindowState != WindowState.FullScreen;
        WindowState = presentation ? WindowState.FullScreen : WindowState.Normal; FilterBar.IsVisible = !presentation;
    }
    private async void CardLayout_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Control { Tag: DashboardCard card }) return;
        var copy = JsonConvert.DeserializeObject<DashboardChart>(JsonConvert.SerializeObject(card.Chart))!;
        var page = new TextBox { Text = copy.Page }; var order = new TextBox { Text = copy.Position.ToString() }; var width = new TextBox { Text = copy.CardWidth.ToString() }; var height = new TextBox { Text = copy.CardHeight.ToString() };
        var calculations = new TextBox { AcceptsReturn = true, Height = 130, Text = string.Join("\n", copy.CalculatedFields.Select(c => c.Name + "=" + c.Expression)) };
        var y = new TextBox { Text = string.Join(",", copy.YColumns) }; var error = new TextBlock { TextWrapping = global::Avalonia.Media.TextWrapping.Wrap };
        var save = new Button { Content = "保存布局与计算" };
        var panel = new StackPanel { Spacing = 8, Margin = new Thickness(18) };
        foreach (var control in new Control[] { new TextBlock { Text = "页名（新名称会建立新页）" }, page, new TextBlock { Text = "页内顺序" }, order, new TextBlock { Text = "卡片宽度 300–1200 / 高度 220–1000" }, width, height, new TextBlock { Text = "计算字段，每行：新列名=[列名]*2；支持四则运算和括号" }, calculations, new TextBlock { Text = "Y 列（逗号分隔，可填写新计算字段）" }, y, error, save }) panel.Children.Add(control);
        var dialog = new Window { Title = "仪表盘布局与计算字段", Width = 550, Height = 650, WindowStartupLocation = WindowStartupLocation.CenterOwner, Content = new ScrollViewer { Content = panel } };
        save.Click += (_, _) =>
        {
            try
            {
                copy.Page = page.Text?.Trim() ?? ""; copy.Position = int.Parse(order.Text!); copy.CardWidth = int.Parse(width.Text!); copy.CardHeight = int.Parse(height.Text!);
                copy.CalculatedFields = (calculations.Text ?? "").Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(line => { int separator = line.IndexOf('='); if (separator <= 0) throw new ArgumentException("计算字段格式为 名称=表达式。"); return new CalculatedField { Name = line[..separator].Trim(), Expression = line[(separator + 1)..].Trim() }; }).ToList();
                copy.YColumns = (y.Text ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
                if (card.RawResult is not null)
                {
                    var computed = DashboardTransform.Apply(card.RawResult, copy.CalculatedFields);
                    if (copy.YColumns.Any(n => !computed.Columns.Contains(n))) throw new ArgumentException("Y 列不存在。");
                }
                _dashboardService.Save(copy); dialog.Close(true);
            }
            catch (Exception ex) { error.Text = ex.Message; }
        };
        if (await dialog.ShowDialog<bool>(this)) { UpdatePages(copy.Page); await RefreshAllAsync(); }
    }
    private async void ExportPage_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions { SuggestedFileName = "dashboard.html", ShowOverwritePrompt = true });
            var path = file?.TryGetLocalPath(); if (path is null) return;
            await ExportPageAsync(path);
            DashboardStatus.Text = "已导出当前页图表快照。";
        }
        catch (Exception ex) { DashboardStatus.Text = ex.Message; }
    }
    internal async Task ExportPageAsync(string path)
    {
            var html = new System.Text.StringBuilder("<!doctype html><html lang=\"zh\"><meta charset=\"utf-8\"><title>仪表盘</title><style>body{font-family:sans-serif}main{display:flex;flex-wrap:wrap}figure{border:1px solid #ccc;padding:12px}img{max-width:100%}</style><h1>" + WebUtility.HtmlEncode(PageSelector.SelectedItem?.ToString()) + "</h1><p>导出时的筛选：" + WebUtility.HtmlEncode(FilterColumn.Text + " = " + FilterValue.Text) + "</p><main>");
            var charts = CardsHost.GetVisualDescendants().OfType<ChartRenderControl>().ToArray();
            foreach (var chart in charts)
            {
                if (chart.Bounds.Width < 1 || chart.Bounds.Height < 1) continue;
                using var bitmap = new RenderTargetBitmap(new PixelSize((int)chart.Bounds.Width, (int)chart.Bounds.Height), new Vector(96, 96)); bitmap.Render(chart);
                using var stream = new MemoryStream(); bitmap.Save(stream);
                html.Append("<figure><figcaption>").Append(WebUtility.HtmlEncode(chart.Chart?.Title)).Append("</figcaption><img alt=\"图表快照\" src=\"data:image/png;base64,").Append(Convert.ToBase64String(stream.ToArray())).Append("\"></figure>");
            }
            html.Append("</main></html>"); await File.WriteAllTextAsync(path, html.ToString(), _lifetime.Token);
    }
}
