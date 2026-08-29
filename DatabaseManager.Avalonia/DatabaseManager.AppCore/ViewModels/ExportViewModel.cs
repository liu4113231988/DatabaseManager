using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DatabaseManager.AppCore.Common;
using DatabaseManager.AppCore.Models;
using DatabaseManager.AppCore.Services;

namespace DatabaseManager.AppCore.ViewModels;

/// <summary>
/// 数据导出 ViewModel（阶段 6 / M6）。对应原 WinForms <c>frmExportData</c>。
/// 选择连接/表/视图与导出格式，导出数据到 CSV / Excel / SQL / JSON / XML 文件。
/// 支持文本编码选择与起始页续传。
/// </summary>
public partial class ExportViewModel : ViewModelBase
{
    private readonly IDbConnectionService _connectionService;
    private readonly IExportImportService _exportImportService;
    private CancellationTokenSource? _exportCts;

    /// <summary>快速切换连接时，淘汰过期的异步加载结果（竞态防护）。</summary>
    private int _loadTableVersion;

    /// <summary>全部已保存连接。</summary>
    public ObservableCollection<ConnectionItem> Connections { get; } = new();

    /// <summary>表/视图列表（可勾选，预选勾选状态会在刷新时按名称恢复）。</summary>
    public ObservableCollection<ExportTableItem> Tables { get; } = new();

    /// <summary>导出格式。</summary>
    public IReadOnlyList<string> Formats { get; }

    /// <summary>文本编码选项。</summary>
    public IReadOnlyList<string> EncodingOptions { get; } = DatabaseManager.FileUtility.TextEncoding.CommonNames;

    /// <summary>执行日志。</summary>
    public ObservableCollection<string> Logs { get; } = new();

    [ObservableProperty]
    private ConnectionItem? _selectedConnection;

    [ObservableProperty]
    private ExportTableItem? _selectedTable;

    [ObservableProperty]
    private string _selectedFormat = "Excel";

    [ObservableProperty]
    private string _selectedEncoding = DatabaseManager.FileUtility.TextEncoding.CommonNames[0];

    [ObservableProperty]
    private long _startPageNumber = 1;

    [ObservableProperty]
    private string _filePath = string.Empty;

    [ObservableProperty]
    private bool _showColumnNames = true;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private bool _isTablesLoading;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    public ExportViewModel(IDbConnectionService connectionService, IExportImportService exportImportService)
    {
        _connectionService = connectionService;
        _exportImportService = exportImportService;

        Formats = _exportImportService.GetExportFormats();
        if (Formats.Count > 0)
        {
            SelectedFormat = Formats[0];
        }
    }

    /// <summary>加载已保存连接并刷新选择。</summary>
    public void RefreshConnections()
    {
        var previousId = SelectedConnection?.Id;

        Connections.Clear();
        foreach (var item in _connectionService.GetConnections())
        {
            Connections.Add(item);
        }

        SelectedConnection = Connections.FirstOrDefault(c => c.Id == previousId) ?? Connections.FirstOrDefault();
    }

    partial void OnSelectedConnectionChanged(ConnectionItem? value)
    {
        _ = LoadTablesAsync(value);
    }

    private async Task LoadTablesAsync(ConnectionItem? connection)
    {
        // 捕获当前版本号；如果后续发起了新的加载，当前结果抵达时应被丢弃。
        var currentVersion = ++_loadTableVersion;

        if (connection is null)
        {
            Tables.Clear();
            return;
        }

        // 预选表同步：加载前先记住当前勾选的 (Schema,Name) 集合，刷新后还原。
        var preSelected = new HashSet<(string? Schema, string Name)>(
            Tables.Where(t => t.IsSelected).Select(t => (t.Schema, t.Name)));

        IsTablesLoading = true;
        try
        {
            var tables = await _exportImportService.GetTablesAsync(connection);

            // 竞态检查：如果版本号不再匹配，说明用户已经切换过连接或重入加载。
            if (currentVersion != _loadTableVersion)
                return;

            Tables.Clear();
            foreach (var table in tables)
            {
                table.IsSelected = preSelected.Contains((table.Schema, table.Name));
                Tables.Add(table);
            }

            StatusMessage = $"已加载 {Tables.Count} 个对象。";
        }
        catch (Exception ex)
        {
            if (currentVersion != _loadTableVersion)
                return;

            Tables.Clear();
            StatusMessage = $"加载表列表失败：{ex.Message}";
        }
        finally
        {
            if (currentVersion == _loadTableVersion)
            {
                IsTablesLoading = false;
            }
        }
    }

    /// <summary>设置导出文件路径（由 UI 文件对话框调用）。</summary>
    public void SetFilePath(string? path)
    {
        FilePath = path ?? string.Empty;
    }

    /// <summary>取消正在进行的导出。</summary>
    [RelayCommand(CanExecute = nameof(IsBusy))]
    private void CancelExport()
    {
        _exportCts?.Cancel();
    }

    partial void OnIsBusyChanged(bool value) => CancelExportCommand.NotifyCanExecuteChanged();

    [RelayCommand]
    private async Task ExportAsync()
    {
        if (SelectedConnection is null)
        {
            StatusMessage = "请选择连接。";
            return;
        }

        if (SelectedTable is null)
        {
            StatusMessage = "请选择要导出的表/视图。";
            return;
        }

        if (string.IsNullOrWhiteSpace(FilePath))
        {
            StatusMessage = "请选择导出文件路径。";
            return;
        }

        IsBusy = true;
        StatusMessage = string.Empty;
        Logs.Clear();
        _exportCts = new CancellationTokenSource();

        var feedbackBuffer = new List<string>();
        void CollectFeedback(string message) => feedbackBuffer.Add(message);

        try
        {
            AppendLog($"连接：{SelectedConnection.Description}");
            AppendLog($"对象：{SelectedTable.DisplayName}{(SelectedTable.IsView ? "（视图）" : "（表）")}");
            AppendLog($"格式：{SelectedFormat}");
            AppendLog($"文件：{FilePath}");
            if (StartPageNumber > 1)
            {
                AppendLog($"起始页：{StartPageNumber}（续传模式）");
            }
            AppendLog("开始导出...");

            var result = await _exportImportService.ExportDataAsync(
                SelectedConnection,
                SelectedTable.Name,
                SelectedTable.Schema,
                isView: SelectedTable.IsView,
                SelectedFormat,
                FilePath,
                ShowColumnNames,
                SelectedEncoding,
                StartPageNumber,
                CollectFeedback,
                _exportCts.Token);

            foreach (var line in feedbackBuffer)
            {
                AppendLog(line);
            }

            StatusMessage = result.IsSuccess ? $"导出成功：{result.FilePath}" : $"导出失败：{result.Message}";
            AppendLog(StatusMessage);
        }
        catch (Exception ex)
        {
            StatusMessage = $"导出失败：{ex.Message}";
            AppendLog(StatusMessage);
        }
        finally
        {
            _exportCts?.Dispose();
            _exportCts = null;
            IsBusy = false;
        }
    }

    private void AppendLog(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;

        var time = DateTime.Now.ToString("HH:mm:ss");
        Logs.Add($"[{time}] {message}");
    }
}
