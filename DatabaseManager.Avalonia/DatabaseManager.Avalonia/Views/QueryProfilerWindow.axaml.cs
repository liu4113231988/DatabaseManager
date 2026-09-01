using Avalonia.Controls;
using Avalonia.Interactivity;
using DatabaseManager.AppCore.Models;
using DatabaseManager.AppCore.Services;

namespace DatabaseManager.Avalonia.Views;

/// <summary>
/// 查询性能剖析窗口：重复执行 SQL 并分阶段计时，可选输出 EXPLAIN ANALYZE。
/// </summary>
public partial class QueryProfilerWindow : Window
{
    private readonly IQueryProfilerService _profilerService;
    private readonly IDbConnectionService _connectionService;

    public QueryProfilerWindow()
    {
        InitializeComponent();
    }

    public QueryProfilerWindow(IQueryProfilerService profilerService, IDbConnectionService connectionService, string? initialSql = null)
        : this()
    {
        _profilerService = profilerService;
        _connectionService = connectionService;

        CmbConnection.ItemsSource = _connectionService.GetConnections();

        if (!string.IsNullOrEmpty(initialSql))
        {
            TxtSql.Text = initialSql;
        }
    }

    /// <summary>预设连接与 SQL（主窗口打开时注入当前上下文）。</summary>
    public void SetContext(string connectionName, string? sql)
    {
        var target = CmbConnection.ItemsSource?.Cast<ConnectionItem>()
            .FirstOrDefault(c => string.Equals(c.Name, connectionName, StringComparison.OrdinalIgnoreCase));
        if (target is not null)
        {
            CmbConnection.SelectedItem = target;
        }

        if (!string.IsNullOrEmpty(sql))
        {
            TxtSql.Text = sql;
        }
    }

    private async void BtnRun_Click(object? sender, RoutedEventArgs e)
    {
        if (CmbConnection.SelectedItem is not ConnectionItem connection)
        {
            TxtSummary.Text = "请先选择连接。";
            return;
        }

        string sql = TxtSql.Text?.Trim() ?? string.Empty;
        if (sql.Length == 0)
        {
            TxtSummary.Text = "请输入要剖析的 SQL。";
            return;
        }

        int runs = int.TryParse(TxtRuns.Text, out int r) ? Math.Clamp(r, 1, 50) : 5;

        BtnRun.IsEnabled = false;
        TxtSummary.Text = $"正在剖析（{runs} 次运行）...";

        try
        {
            var result = await _profilerService.ProfileAsync(
                connection, sql, runs, ChkAnalyze.IsChecked == true);

            RunsGrid.ItemsSource = result.Runs;
            TxtAnalyze.Text = result.AnalyzeText ?? string.Empty;

            var okRuns = result.Runs.Where(x => x.Error is null).ToList();
            string stats = okRuns.Count > 0
                ? $"平均 {result.AverageMs} ms · 最快 {result.MinMs} ms · 最慢 {result.MaxMs} ms · 合计返回 {result.TotalRows} 行（连接建立 {result.OpenMs} ms）"
                : "无成功运行。";

            TxtSummary.Text = result.Error is not null
                ? $"{result.Error} {stats}"
                : stats;
        }
        catch (Exception ex)
        {
            TxtSummary.Text = $"剖析失败：{ex.Message}";
        }
        finally
        {
            BtnRun.IsEnabled = true;
        }
    }

    private void BtnClose_Click(object? sender, RoutedEventArgs e) => Close();
}
