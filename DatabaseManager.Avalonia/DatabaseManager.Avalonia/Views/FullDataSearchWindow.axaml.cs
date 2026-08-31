using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using DatabaseManager.AppCore.Models;
using DatabaseManager.AppCore.Services;

namespace DatabaseManager.Avalonia.Views;

/// <summary>
/// 全库数据搜索窗口：跨表/视图搜索数据内容，结果可生成 SELECT 在新查询标签打开。
/// </summary>
public partial class FullDataSearchWindow : Window
{
    private readonly IFullDataSearchService _searchService;
    private readonly IDbConnectionService _connectionService;

    /// <summary>打开 SQL 到新查询标签的回调（connectionName, databaseName, sql）。</summary>
    private readonly Action<string, string?, string>? _openSqlCallback;

    private CancellationTokenSource? _cts;
    private bool _searching;

    public FullDataSearchWindow()
    {
        InitializeComponent();
    }

    public FullDataSearchWindow(
        IFullDataSearchService searchService,
        IDbConnectionService connectionService,
        Action<string, string?, string>? openSqlCallback)
        : this()
    {
        _searchService = searchService;
        _connectionService = connectionService;
        _openSqlCallback = openSqlCallback;

        CmbConnection.ItemsSource = _connectionService.GetConnections();
    }

    private async void CmbConnection_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (CmbConnection.SelectedItem is not ConnectionItem connection)
            return;

        // 加载连接下的数据库列表（与连接编辑器同源）。
        CmbDatabase.ItemsSource = null;
        try
        {
            var connectionService = AppServices.ConnectionService;
            if (connectionService is null)
                return;

            var databases = await connectionService.TestConnectionAsync(connection);
            CmbDatabase.ItemsSource = databases;
            if (databases.Count > 0)
            {
                var target = databases.FirstOrDefault(d =>
                    string.Equals(d, connection.Database, StringComparison.OrdinalIgnoreCase));
                CmbDatabase.SelectedItem = target ?? databases[0];
            }
        }
        catch
        {
            // 数据库列表加载失败不阻断：用户可手动输入库名。
        }
    }

    private async void TxtKeyword_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            await RunSearchAsync();
            e.Handled = true;
        }
    }

    private async void BtnSearch_Click(object? sender, RoutedEventArgs e) => await RunSearchAsync();

    private void BtnCancel_Click(object? sender, RoutedEventArgs e)
    {
        _cts?.Cancel();
    }

    private async Task RunSearchAsync()
    {
        if (_searching)
            return;

        if (CmbConnection.SelectedItem is not ConnectionItem connection)
        {
            TxtSummary.Text = "请先选择连接。";
            return;
        }

        string keyword = TxtKeyword.Text?.Trim() ?? string.Empty;
        if (keyword.Length == 0)
        {
            TxtSummary.Text = "请输入搜索关键字。";
            return;
        }

        _searching = true;
        _cts = new CancellationTokenSource();
        BtnSearch.IsEnabled = false;
        BtnCancel.IsEnabled = true;
        Progress.IsVisible = true;
        ResultsTree.ItemsSource = null;
        TxtSummary.Text = "搜索中...";

        try
        {
            var options = new FullDataSearchOptions
            {
                Database = (CmbDatabase.SelectedItem as string) ?? CmbDatabase.Text,
                Schema = string.IsNullOrWhiteSpace(TxtSchema.Text) ? null : TxtSchema.Text.Trim(),
                TextColumnsOnly = ChkTextOnly.IsChecked == true,
                IncludeViews = ChkIncludeViews.IsChecked == true,
                MaxMatchesPerTable = TryParseInt(TxtPerTable.Text, 20),
                MaxTables = TryParseInt(TxtMaxTables.Text, 500),
            };

            var result = await _searchService.SearchAsync(
                connection, keyword, options, msg => TxtProgress.Text = msg, _cts.Token);

            var matches = result.Tables.Where(t => t.Rows.Count > 0).ToList();
            ResultsTree.ItemsSource = matches;

            TxtSummary.Text = result.Error is not null
                ? $"{result.Error}（已扫描 {result.ScannedTables} 个对象，命中 {result.MatchedTables} 个对象 / {result.TotalMatches} 行，耗时 {result.ElapsedMilliseconds} ms）"
                : $"共扫描 {result.ScannedTables} 个对象，命中 {result.MatchedTables} 个对象 / {result.TotalMatches} 行，耗时 {result.ElapsedMilliseconds} ms。";
        }
        catch (Exception ex)
        {
            TxtSummary.Text = $"搜索失败：{ex.Message}";
        }
        finally
        {
            _searching = false;
            _cts?.Dispose();
            _cts = null;
            BtnSearch.IsEnabled = true;
            BtnCancel.IsEnabled = false;
            Progress.IsVisible = false;
        }
    }

    private void ResultsTree_DoubleTapped(object? sender, RoutedEventArgs e)
    {
        OpenSelectedSelect();
    }

    private void BtnOpenSelect_Click(object? sender, RoutedEventArgs e) => OpenSelectedSelect();

    /// <summary>把选中对象/行的 SELECT 打开到新查询标签。</summary>
    private void OpenSelectedSelect()
    {
        if (_openSqlCallback is null || CmbConnection.SelectedItem is not ConnectionItem connection)
            return;

        string? database = (CmbDatabase.SelectedItem as string) ?? CmbDatabase.Text;
        if (!Enum.TryParse<DatabaseInterpreter.Model.DatabaseType>(connection.DatabaseType, true, out var dbType))
            return;

        if (ResultsTree.SelectedItem is FullDataSearchTableResult table)
        {
            string columns = table.MatchedColumns.Count > 0
                ? string.Join(" OR ", table.MatchedColumns.Distinct()
                    .Select(c => $"{SqlDialectHelper.QuoteIdentifier(dbType, c)} LIKE '%{SqlDialectHelper.EscapeLiteral(TxtKeyword.Text?.Trim() ?? string.Empty)}%'"))
                : "1 = 1";
            _openSqlCallback(connection.Name, database,
                $"SELECT * FROM {SqlDialectHelper.QuoteQualifiedIdentifier(dbType, table.DisplayName)}\nWHERE {columns};");
        }
        else if (ResultsTree.SelectedItem is FullDataSearchRow row)
        {
            var parent = FindParentTable(row);
            if (parent is null)
                return;

            string conditions = string.Join("\n  AND ", row.Conditions
                .Select(kv => $"{SqlDialectHelper.QuoteIdentifier(dbType, kv.Key)} = '{SqlDialectHelper.EscapeLiteral(kv.Value)}'"));
            _openSqlCallback(connection.Name, database,
                $"SELECT * FROM {SqlDialectHelper.QuoteQualifiedIdentifier(dbType, parent.DisplayName)}\nWHERE {(conditions.Length > 0 ? conditions : "1 = 1")};");
        }
    }

    /// <summary>按行对象反查所属表结果（结果树为两级结构）。</summary>
    private FullDataSearchTableResult? FindParentTable(FullDataSearchRow row)
        => (ResultsTree.ItemsSource as IEnumerable<FullDataSearchTableResult>)
            ?.FirstOrDefault(t => t.Rows.Contains(row));

    private void BtnClose_Click(object? sender, RoutedEventArgs e) => Close();

    private static int TryParseInt(string? text, int fallback)
        => int.TryParse(text, out int value) && value > 0 ? value : fallback;
}

/// <summary>App 服务便捷访问（供窗口内使用）。</summary>
internal static class AppServices
{
    public static IDbConnectionService? ConnectionService
        => (App.Current as App)?.Services?.GetService(typeof(IDbConnectionService)) as IDbConnectionService;
}
