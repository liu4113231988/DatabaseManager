using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using DatabaseManager.AppCore.Models;
using DatabaseManager.AppCore.Services;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;

namespace DatabaseManager.Avalonia.Views.Workbench;

/// <summary>数据预览表格的行数据。</summary>
public sealed record PreviewRow(IReadOnlyList<string> Values);

/// <summary>
/// 数据工作台各独立页面的公共基础设施：连接 / 对象选择、结果输出、忙碌状态与上区自适应行高。
/// 页面布局由各窗口自己的 axaml 声明；本类不创建任何页面内容，各页面之间互不依赖。
/// </summary>
public sealed class WorkbenchSupport
{
    public required Window Window { get; init; }
    public required IServiceProvider Services { get; init; }
    public required ComboBox ConnectionBox { get; init; }
    public required TextBox DatabaseBox { get; init; }
    public required Button LoadButton { get; init; }
    public required ComboBox TableBox { get; init; }
    public required TextBox OutputBox { get; init; }
    public required DataGrid ResultGrid { get; init; }
    public required TextBlock StatusText { get; init; }
    public required Button CancelButton { get; init; }
    public required Grid BodyGrid { get; init; }
    /// <summary>页面主容器。多数页面是外层 ScrollViewer（整页可滚动）；内容自带滚动的页面可直接传布局容器。</summary>
    public required Control PageHost { get; init; }
    public string? InitialConnection { get; init; }
    public string? InitialDatabase { get; init; }

    /// <summary>连接或数据库变化后触发，页面可据此清理自己持有的对象列表。</summary>
    public event Action? ContextCleared;
    /// <summary>“加载对象”完成后触发，页面可据此刷新自己的对象列表。</summary>
    public event Action? TablesLoaded;

    private CancellationTokenSource? _cts;
    private IReadOnlyList<ExportTableItem> _tables = Array.Empty<ExportTableItem>();
    private IDbConnectionService? _connections;
    private IExportImportService? _files;
    private IDataEditService? _edits;

    public IDbConnectionService Connections => _connections ??= Services.GetRequiredService<IDbConnectionService>();
    public IExportImportService Files => _files ??= Services.GetRequiredService<IExportImportService>();
    public IDataEditService Edits => _edits ??= Services.GetRequiredService<IDataEditService>();
    public IReadOnlyList<ExportTableItem> Tables => _tables;

    public ConnectionItem Connection
    {
        get
        {
            var selected = ConnectionBox.SelectedItem as ConnectionItem ?? throw new InvalidOperationException("请选择连接。");
            var copy = JsonConvert.DeserializeObject<ConnectionItem>(JsonConvert.SerializeObject(selected))!; copy.Ssh = selected.Ssh;
            copy.Database = DatabaseBox.Text?.Trim() ?? "";
            if (string.IsNullOrEmpty(copy.Database)) throw new InvalidOperationException("请填写数据库。");
            return copy;
        }
    }

    public ExportTableItem Table => TableBox.SelectedItem as ExportTableItem ?? throw new InvalidOperationException("请先加载并选择表。");

    /// <summary>绑定公共控件的交互（供各页面在构造末尾调用一次）。</summary>
    public void Initialize()
    {
        ConnectionBox.ItemsSource = Connections.GetConnections();
        ConnectionBox.ItemTemplate = new FuncDataTemplate<ConnectionItem>((c, _) => new TextBlock { Text = c?.Description });
        TableBox.ItemTemplate = new FuncDataTemplate<ExportTableItem>((t, _) => new TextBlock { Text = t?.DisplayName });
        ConnectionBox.SelectionChanged += (_, _) => { DatabaseBox.Text = (ConnectionBox.SelectedItem as ConnectionItem)?.Database; ClearContext(); };
        DatabaseBox.TextChanged += (_, _) => ClearContext();
        CancelButton.Click += (_, _) => _cts?.Cancel();
        Window.Closed += (_, _) => _cts?.Cancel();
        Bind(LoadButton, LoadTablesAsync);
        ConnectionBox.SelectedItem = ConnectionBox.ItemsSource.Cast<ConnectionItem>().FirstOrDefault(c => c.Name == InitialConnection);
        if (ConnectionBox.SelectedIndex < 0) ConnectionBox.SelectedIndex = ConnectionBox.ItemCount > 0 ? 0 : -1;
        if (!string.IsNullOrEmpty(InitialDatabase)) DatabaseBox.Text = InitialDatabase;
        BodyGrid.SizeChanged += (_, _) => Dispatcher.UIThread.Post(UpdatePageRowHeight, DispatcherPriority.Loaded);
    }

    /// <summary>把按钮动作包装成带忙碌状态、取消与统一错误提示的操作。</summary>
    public void Bind(Button button, Func<CancellationToken, Task> action)
    {
        button.Click += async (_, _) =>
        {
            if (_cts is not null) { SetStatus("请等待当前操作完成，或先取消。"); return; }
            _cts = new CancellationTokenSource(); button.IsEnabled = false; PageHost.IsEnabled = false; ConnectionBox.IsEnabled = false; DatabaseBox.IsEnabled = false; TableBox.IsEnabled = false; SetStatus("正在处理…");
            try { await action(_cts.Token); if (StatusText.Text == "正在处理…") SetStatus("已完成。"); }
            catch (OperationCanceledException) { SetStatus("已取消。"); }
            catch (Exception ex) { SetStatus(ex.Message); }
            finally { _cts.Dispose(); _cts = null; button.IsEnabled = true; PageHost.IsEnabled = true; ConnectionBox.IsEnabled = true; DatabaseBox.IsEnabled = true; TableBox.IsEnabled = true; }
        };
    }

    public void SetOutput(string text) => OutputBox.Text = text;
    public void SetStatus(string text) => StatusText.Text = text;

    public void ShowData(QueryResult result)
    {
        ResultGrid.Columns.Clear();
        for (int i = 0; i < result.Columns.Count; i++) ResultGrid.Columns.Add(new DataGridTextColumn { Header = result.Columns[i], Binding = new Binding($"Values[{i}]"), Width = new DataGridLength(150) });
        ResultGrid.ItemsSource = result.Rows.Take(1000).Select(r => new PreviewRow(r)).ToArray();
        SetStatus($"共 {result.Rows.Count} 行，表格预览最多 1000 行。{result.WarningMessage}");
    }

    /// <summary>让页面内的某个滚动区域随窗口高度自适应限高，避免页面总高超出上区而出现外层滚动条。</summary>
    public void FitSection(ScrollViewer section, double reserved)
    {
        void Update()
        {
            double maxUpper = Math.Max(400, BodyGrid.Bounds.Height * 0.75);
            section.MaxHeight = Math.Clamp(maxUpper - reserved, 140, 480);
        }
        BodyGrid.SizeChanged += (_, _) => Update();
        Update();
    }

    /// <summary>让上区行固定占用视口比例（用于页面自身可滚动、按钮固定在底部的页面，避免再套一层外层滚动）。
    /// <paramref name="extraBottomSpace"/> 用于把额外高度让给下方结果区。</summary>
    public void UseFixedPageRatio(double ratio, double extraBottomSpace = 0)
    {
        void Update() => BodyGrid.RowDefinitions[1].Height = new GridLength(Math.Max(400, BodyGrid.Bounds.Height * ratio) - extraBottomSpace, GridUnitType.Pixel);
        BodyGrid.SizeChanged += (_, _) => Update();
        Update();
    }

    public async Task<string?> SavePath(string name)
    {
        var file = await Window.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions { Title = "选择保存位置", SuggestedFileName = name, ShowOverwritePrompt = true });
        return file?.TryGetLocalPath();
    }

    public async Task<string?> OpenPath(string title)
    {
        var files = await Window.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions { Title = title, AllowMultiple = false });
        return files.FirstOrDefault()?.TryGetLocalPath();
    }

    private void ClearContext()
    {
        _tables = Array.Empty<ExportTableItem>(); TableBox.ItemsSource = _tables;
        ContextCleared?.Invoke();
    }

    private async Task LoadTablesAsync(CancellationToken ct)
    {
        _tables = await Files.GetTablesAsync(Connection, ct); ct.ThrowIfCancellationRequested();
        TableBox.ItemsSource = _tables; TableBox.SelectedIndex = _tables.Count > 0 ? 0 : -1;
        SetStatus($"已加载 {_tables.Count} 个对象。");
        TablesLoaded?.Invoke();
    }

    /// <summary>按页面内容的自然高度自适应上区行高：内容少时收缩、把空间让给下方预览区，内容多时上限约 75% 视口并滚动。</summary>
    private void UpdatePageRowHeight()
    {
        if (PageHost is not ScrollViewer page || page.Content is not Control content) return;
        double natural = content.DesiredSize.Height + page.Padding.Top + page.Padding.Bottom;
        double max = Math.Max(400, BodyGrid.Bounds.Height * 0.75);
        double height = Math.Clamp(natural, 200, max);
        BodyGrid.RowDefinitions[1].Height = new GridLength(Math.Round(height), GridUnitType.Pixel);
    }

    private static string SettingsPath(string name) => Path.Combine(AppContext.BaseDirectory, "Profiles", name);

    public static void SaveSettings<T>(string name, T value)
    {
        string path = SettingsPath(name); Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path + ".tmp", JsonConvert.SerializeObject(value, Formatting.Indented)); File.Move(path + ".tmp", path, true);
    }

    public static T LoadSettings<T>(string name) where T : new() =>
        File.Exists(SettingsPath(name)) ? JsonConvert.DeserializeObject<T>(File.ReadAllText(SettingsPath(name))) ?? new() : new();
}
