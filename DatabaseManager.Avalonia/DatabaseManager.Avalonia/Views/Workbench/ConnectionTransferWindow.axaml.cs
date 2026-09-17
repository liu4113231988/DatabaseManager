using System.ComponentModel;
using Avalonia.Controls;
using DatabaseManager.AppCore.Models;
using DatabaseManager.AppCore.Services;

namespace DatabaseManager.Avalonia.Views.Workbench;

/// <summary>连接迁移表格的一行：连接信息 + 勾选状态。</summary>
public sealed class ConnectionRow : INotifyPropertyChanged
{
    public ConnectionRow(ConnectionItem item) => Item = item;

    public ConnectionItem Item { get; }
    public string Name => Item.Name;
    public string DatabaseType => Item.DatabaseType;
    public string Database => Item.Database;
    public string Description => Item.Description;

    private bool _isSelected = true;
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value) return;
            _isSelected = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}

/// <summary>连接配置导入 / 导出：表格勾选连接，加密携带密码或按策略导入。</summary>
public sealed partial class ConnectionTransferWindow : Window
{
    private readonly WorkbenchSupport _shell;
    private readonly List<ConnectionRow> _rows = new();
    private bool _isImport;
    private bool _syncingSelection;

    public ConnectionTransferWindow(IServiceProvider services, string? connectionName = null, string? database = null)
    {
        InitializeComponent();
        _shell = new WorkbenchSupport
        {
            Window = this, Services = services, ConnectionBox = ConnectionBox, DatabaseBox = DatabaseBox, LoadButton = LoadButton,
            TableBox = TableBox, OutputBox = OperationOutput, ResultGrid = PreviewGrid, StatusText = OperationStatus,
            CancelButton = CancelButton, BodyGrid = BodyGrid, PageHost = PageScroll,
            InitialConnection = connectionName, InitialDatabase = database
        };

        SelectAllBox.IsCheckedChanged += (_, _) =>
        {
            if (_syncingSelection) return;
            bool value = SelectAllBox.IsChecked == true;
            foreach (var row in _rows) row.IsSelected = value;
            RefreshSelection();
        };

        ShowConnections(_shell.Connections.GetConnections());
        _shell.Bind(LoadConnectionsButton, _ => { ShowConnections(_shell.Connections.GetConnections()); _isImport = false; return Task.CompletedTask; });
        _shell.Bind(ExportButton, async ct =>
        {
            var selected = Selected();
            if (selected.Length == 0) throw new InvalidOperationException("请至少勾选一个连接。");
            var json = ConnectionTransferService.Export(selected, IncludePasswordBox.IsChecked == true, PasswordBox.Text ?? "");
            var path = await _shell.SavePath("connections.dbm.json");
            if (path is not null) await File.WriteAllTextAsync(path, json, ct);
        });
        _shell.Bind(OpenImportButton, async ct =>
        {
            var path = await _shell.OpenPath("选择连接配置文件"); if (path is null) return;
            if (new FileInfo(path).Length > 8 * 1024 * 1024) throw new InvalidOperationException("连接文件过大。");
            var imported = ConnectionTransferService.Preview(await File.ReadAllTextAsync(path, ct), PasswordBox.Text ?? "");
            ShowConnections(imported); _isImport = true;
            _shell.SetOutput($"已解读 {imported.Count} 个连接。请勾选需要导入的项，再点击保存。");
        });
        _shell.Bind(SaveImportButton, async ct =>
        {
            if (!_isImport) throw new InvalidOperationException("请先打开导入文件。");
            var selected = Selected();
            if (selected.Length == 0) throw new InvalidOperationException("请至少勾选一个连接。");
            int count = await ConnectionTransferService.ImportAsync(_shell.Connections, selected, PolicyBox.SelectedIndex == 0, ct);
            _isImport = false; _shell.SetOutput($"已导入 {count} 个连接。");
            _shell.ConnectionBox.ItemsSource = _shell.Connections.GetConnections();
        });
        _shell.Initialize();
    }

    private ConnectionItem[] Selected() => _rows.Where(r => r.IsSelected).Select(r => r.Item).ToArray();

    private void ShowConnections(IReadOnlyList<ConnectionItem> items)
    {
        foreach (var row in _rows) row.PropertyChanged -= OnRowChanged;
        _rows.Clear();
        foreach (var item in items)
        {
            var row = new ConnectionRow(item);
            row.PropertyChanged += OnRowChanged;
            _rows.Add(row);
        }
        ConnectionsGrid.ItemsSource = _rows.ToArray();
        RefreshSelection();
    }

    private void OnRowChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ConnectionRow.IsSelected)) RefreshSelection();
    }

    private void RefreshSelection()
    {
        int selected = _rows.Count(r => r.IsSelected);
        _syncingSelection = true;
        SelectAllBox.IsChecked = _rows.Count > 0 && selected == _rows.Count;
        _syncingSelection = false;
        SelectionSummary.Text = _rows.Count == 0 ? "" : $"已选 {selected} / {_rows.Count} 个连接";
    }
}
