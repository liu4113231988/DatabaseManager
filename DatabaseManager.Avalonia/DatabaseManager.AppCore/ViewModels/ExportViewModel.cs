using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DatabaseManager.AppCore.Common;
using DatabaseManager.AppCore.Models;
using DatabaseManager.AppCore.Services;

namespace DatabaseManager.AppCore.ViewModels;

/// <summary>
/// 数据导出 ViewModel（阶段 6 / M6）。对应原 WinForms <c>frmExportData</c>。
/// 选择连接/表/视图与导出格式，导出数据到 CSV / Excel 文件。
/// </summary>
public partial class ExportViewModel : ViewModelBase
{
    private readonly IDbConnectionService _connectionService;
    private readonly IExportImportService _exportImportService;

    /// <summary>全部已保存连接。</summary>
    public ObservableCollection<ConnectionItem> Connections { get; } = new();

    /// <summary>表/视图列表（可勾选）。</summary>
    public ObservableCollection<TableItem> Tables { get; } = new();

    /// <summary>导出格式。</summary>
    public IReadOnlyList<string> Formats { get; }

    /// <summary>执行日志。</summary>
    public ObservableCollection<string> Logs { get; } = new();

    [ObservableProperty]
    private ConnectionItem? _selectedConnection;

    [ObservableProperty]
    private TableItem? _selectedTable;

    [ObservableProperty]
    private string _selectedFormat = "Excel";

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
        if (connection is null)
        {
            Tables.Clear();
            return;
        }

        IsTablesLoading = true;
        try
        {
            var tables = await _exportImportService.GetTablesAsync(connection);
            Tables.Clear();
            foreach (var table in tables)
            {
                Tables.Add(table);
            }
            StatusMessage = $"已加载 {Tables.Count} 张表。";
        }
        catch (Exception ex)
        {
            Tables.Clear();
            StatusMessage = $"加载表列表失败：{ex.Message}";
        }
        finally
        {
            IsTablesLoading = false;
        }
    }

    /// <summary>设置导出文件路径（由 UI 文件对话框调用）。</summary>
    public void SetFilePath(string? path)
    {
        FilePath = path ?? string.Empty;
    }

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

        var feedbackBuffer = new List<string>();
        void CollectFeedback(string message) => feedbackBuffer.Add(message);

        try
        {
            AppendLog($"连接：{SelectedConnection.Description}");
            AppendLog($"对象：{SelectedTable.DisplayName}");
            AppendLog($"格式：{SelectedFormat}");
            AppendLog($"文件：{FilePath}");
            AppendLog("开始导出...");

            var result = await _exportImportService.ExportDataAsync(
                SelectedConnection,
                SelectedTable.Name,
                SelectedTable.Schema,
                isView: false,
                SelectedFormat,
                FilePath,
                ShowColumnNames,
                CollectFeedback);

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
