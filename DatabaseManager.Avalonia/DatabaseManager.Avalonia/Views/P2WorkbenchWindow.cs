using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using DatabaseManager.AppCore.Models;
using DatabaseManager.AppCore.Services;
using DatabaseManager.AppCore.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;

namespace DatabaseManager.Avalonia.Views;

public sealed partial class P2WorkbenchWindow : Window
{
    private readonly IServiceProvider _services;
    private readonly Action<ConnectionItem, string> _openSql;
    private readonly IDbConnectionService _connections;
    private readonly IExportImportService _files;
    private readonly IDataEditService _edits;
    private readonly ComboBox _connection = new() { Width = 280 };
    private readonly TextBox _database = new() { Width = 170, Watermark = "数据库" };
    private readonly ComboBox _table = new() { MinWidth = 230 };
    private readonly TextBlock _status = new() { Name = "OperationStatus", TextWrapping = TextWrapping.Wrap };
    private readonly TextBox _output = new() { Name = "OperationOutput", AcceptsReturn = true, IsReadOnly = true, MinHeight = 130, VerticalContentAlignment = VerticalAlignment.Top, FontFamily = FontFamily.Parse("Consolas,Microsoft YaHei"), TextWrapping = TextWrapping.Wrap };
    private readonly DataGrid _grid = new() { Name = "PreviewGrid", IsReadOnly = true, AutoGenerateColumns = false, MinHeight = 140 };
    private readonly TabControl _tabs = new() { Name = "WorkbenchTabs" };
    private CancellationTokenSource? _cts;
    private QueryResult? _sourceResult;
    private IReadOnlyList<ExportTableItem> _tables = Array.Empty<ExportTableItem>();
    private ConnectionItem Connection
    {
        get
        {
            var selected = _connection.SelectedItem as ConnectionItem ?? throw new InvalidOperationException("请选择连接。");
            var copy = JsonConvert.DeserializeObject<ConnectionItem>(JsonConvert.SerializeObject(selected))!; copy.Ssh = selected.Ssh;
            copy.Database = _database.Text?.Trim() ?? "";
            if (string.IsNullOrEmpty(copy.Database)) throw new InvalidOperationException("请填写数据库。");
            return copy;
        }
    }
    private ExportTableItem Table => _table.SelectedItem as ExportTableItem ?? throw new InvalidOperationException("请先加载并选择表。");
    public P2WorkbenchWindow(IServiceProvider services, Action<ConnectionItem, string> openSql, string? connectionName = null, string? database = null, QueryResult? result = null, int tab = 0)
    {
        _services = services; _openSql = openSql; _connections = services.GetRequiredService<IDbConnectionService>();
        _files = services.GetRequiredService<IExportImportService>(); _edits = services.GetRequiredService<IDataEditService>(); _sourceResult = result;
        Title = "数据工作台"; Width = 1150; Height = 850; MinWidth = 900; MinHeight = 650; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        _connection.ItemsSource = _connections.GetConnections();
        _connection.ItemTemplate = new FuncDataTemplate<ConnectionItem>((c, _) => new TextBlock { Text = c?.Description });
        _table.ItemTemplate = new FuncDataTemplate<ExportTableItem>((t, _) => new TextBlock { Text = t?.DisplayName });
        _connection.SelectionChanged += (_, _) => { _database.Text = (_connection.SelectedItem as ConnectionItem)?.Database; ClearContext(); };
        _database.TextChanged += (_, _) => ClearContext();
        _connection.SelectedItem = _connection.ItemsSource.Cast<ConnectionItem>().FirstOrDefault(c => c.Name == connectionName);
        if (_connection.SelectedIndex < 0) _connection.SelectedIndex = _connection.ItemCount > 0 ? 0 : -1;
        if (!string.IsNullOrEmpty(database)) _database.Text = database;
        var top = Row(Label("连接"), _connection, Label("数据库"), _database, Button("加载对象", LoadTables), _table);
        _tabs.ItemsSource = new[] { Tab("查询构建", BuildQueryPage()), Tab("测试数据", BuildGeneratorPage()), Tab("质量剖析", BuildQualityPage()), Tab("数据字典", BuildDictionaryPage()), Tab("AI SQL", BuildAiPage()), Tab("连接迁移", BuildTransferPage()), Tab("扩展导入", BuildExternalPage()), Tab("数据脱敏", BuildMaskPage()) };
        _tabs.SelectedIndex = tab;
        _tabs.SelectionChanged += (_, e) => { if (e.Source == _tabs) { _output.Text = ""; _status.Text = ""; } };
        var body = new Grid { RowDefinitions = new RowDefinitions("Auto,*,5,230,Auto"), Margin = new Thickness(14) };
        body.Children.Add(top); Grid.SetRow(_tabs, 1); body.Children.Add(_tabs);
        var splitter = new GridSplitter { ResizeDirection = GridResizeDirection.Rows, HorizontalAlignment = HorizontalAlignment.Stretch }; Grid.SetRow(splitter, 2); body.Children.Add(splitter);
        var results = new TabControl { ItemsSource = new[] { new TabItem { Header = "预览 / 日志", Content = _output }, new TabItem { Header = "数据预览", Content = _grid } } }; Grid.SetRow(results, 3); body.Children.Add(results);
        var cancel = new Button { Content = "取消当前操作" }; cancel.Click += (_, _) => _cts?.Cancel();
        var footer = Row(cancel, _status); Grid.SetRow(footer, 4); body.Children.Add(footer); Content = body;
        Closed += (_, _) => _cts?.Cancel();
    }
    private void ClearContext() { _tables = Array.Empty<ExportTableItem>(); _table.ItemsSource = _tables; _builderAvailable.ItemsSource = _tables; }
    private async Task LoadTables(CancellationToken ct) { _tables = await _files.GetTablesAsync(Connection, ct); ct.ThrowIfCancellationRequested(); _table.ItemsSource = _tables; _builderAvailable.ItemsSource = _tables; _table.SelectedIndex = _tables.Count > 0 ? 0 : -1; _status.Text = $"已加载 {_tables.Count} 个对象。"; }
    private static TabItem Tab(string title, Control body) => new() { Header = title, Content = new ScrollViewer { Content = body, HorizontalScrollBarVisibility = global::Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled } };
    private static TextBlock Label(string value) => new() { Text = value, VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap };
    private static TextBox Text(string value = "", int width = 170) => new() { Text = value, Width = width, VerticalContentAlignment = VerticalAlignment.Center };
    private static ComboBox Choice(params string[] choices) => new() { ItemsSource = choices, SelectedIndex = 0, MinWidth = 105 };
    private static string Chosen(ComboBox combo) => combo.SelectedItem?.ToString() ?? "";
    private static StackPanel Panel(params Control[] controls) { var p = new StackPanel { Spacing = 9, Margin = new Thickness(6, 12) }; foreach (var c in controls) p.Children.Add(c); return p; }
    private static WrapPanel Row(params Control[] controls) { var p = new WrapPanel { Orientation = Orientation.Horizontal }; foreach (var c in controls) { c.Margin = new Thickness(0, 0, 8, 6); p.Children.Add(c); } return p; }
    private Button Button(string title, Func<CancellationToken, Task> action)
    {
        var b = new Button { Content = title };
        b.Click += async (_, _) =>
        {
            if (_cts is not null) { _status.Text = "请等待当前操作完成，或先取消。"; return; }
            _cts = new CancellationTokenSource(); b.IsEnabled = false; _tabs.IsEnabled = false; _connection.IsEnabled = false; _database.IsEnabled = false; _table.IsEnabled = false; _status.Text = "正在处理…";
            try { await action(_cts.Token); if (_status.Text == "正在处理…") _status.Text = "已完成。"; }
            catch (OperationCanceledException) { _status.Text = "已取消。"; }
            catch (Exception ex) { _status.Text = ex.Message; }
            finally { _cts.Dispose(); _cts = null; b.IsEnabled = true; _tabs.IsEnabled = true; _connection.IsEnabled = true; _database.IsEnabled = true; _table.IsEnabled = true; }
        };
        return b;
    }
    private void ShowData(QueryResult result)
    {
        _grid.Columns.Clear();
        for (int i = 0; i < result.Columns.Count; i++) _grid.Columns.Add(new DataGridTextColumn { Header = result.Columns[i], Binding = new Binding($"Values[{i}]"), Width = new DataGridLength(150) });
        _grid.ItemsSource = result.Rows.Take(1000).Select(r => new PreviewRow(r)).ToArray();
        _status.Text = $"共 {result.Rows.Count} 行，表格预览最多 1000 行。{result.WarningMessage}";
    }
    public sealed record PreviewRow(IReadOnlyList<string> Values);
    private async Task<string?> SavePath(string name)
    {
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions { Title = "选择保存位置", SuggestedFileName = name, ShowOverwritePrompt = true });
        return file?.TryGetLocalPath();
    }
    private async Task<string?> OpenPath(string title)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions { Title = title, AllowMultiple = false }); return files.FirstOrDefault()?.TryGetLocalPath();
    }
    private static string SettingsPath(string name) => Path.Combine(AppContext.BaseDirectory, "Profiles", name);
    private static void SaveSettings<T>(string name, T value)
    {
        string path = SettingsPath(name); Directory.CreateDirectory(Path.GetDirectoryName(path)!); File.WriteAllText(path + ".tmp", JsonConvert.SerializeObject(value, Formatting.Indented)); File.Move(path + ".tmp", path, true);
    }
    private static T LoadSettings<T>(string name) where T : new() => File.Exists(SettingsPath(name)) ? JsonConvert.DeserializeObject<T>(File.ReadAllText(SettingsPath(name))) ?? new() : new();
}
