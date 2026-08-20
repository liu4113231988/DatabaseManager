using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DatabaseManager.AppCore.Common;
using DatabaseManager.AppCore.Models;
using DatabaseManager.AppCore.Services;

namespace DatabaseManager.AppCore.ViewModels;

/// <summary>
/// 数据导入 ViewModel（阶段 6 / M6）。对应原 WinForms <c>frmImportData</c>。
/// 从 CSV / Excel 文件导入数据到指定表，支持列映射。
/// </summary>
public partial class ImportViewModel : ViewModelBase
{
    private readonly IDbConnectionService _connectionService;
    private readonly IExportImportService _exportImportService;

    /// <summary>全部已保存连接。</summary>
    public ObservableCollection<ConnectionItem> Connections { get; } = new();

    /// <summary>表列表。</summary>
    public ObservableCollection<TableItem> Tables { get; } = new();

    /// <summary>列映射（本表列 → 文件列）。</summary>
    public ObservableCollection<ColumnMappingItem> ColumnMappings { get; } = new();

    /// <summary>执行日志。</summary>
    public ObservableCollection<string> Logs { get; } = new();

    /// <summary>表列名列表（用于列映射下拉/自动匹配）。</summary>
    public ObservableCollection<string> TableColumns { get; } = new();

    [ObservableProperty]
    private ConnectionItem? _selectedConnection;

    [ObservableProperty]
    private TableItem? _selectedTable;

    [ObservableProperty]
    private string _filePath = string.Empty;

    [ObservableProperty]
    private bool _firstRowIsColumnName = true;

    [ObservableProperty]
    private bool _useColumnMapping;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private bool _isTablesLoading;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    public ImportViewModel(IDbConnectionService connectionService, IExportImportService exportImportService)
    {
        _connectionService = connectionService;
        _exportImportService = exportImportService;
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

    partial void OnSelectedTableChanged(TableItem? value)
    {
        _ = LoadTableColumnsAsync(value);
    }

    private async Task LoadTableColumnsAsync(TableItem? table)
    {
        TableColumns.Clear();

        if (SelectedConnection is null || table is null)
            return;

        try
        {
            var columns = await _exportImportService.GetTableColumnsAsync(
                SelectedConnection, table.Name, table.Schema);

            TableColumns.Clear();
            foreach (var column in columns)
            {
                TableColumns.Add(column);
            }
        }
        catch
        {
            // 忽略列加载失败，列映射仍可手动填写。
        }
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

    /// <summary>设置导入文件路径（由 UI 文件对话框调用），并加载表列用于列映射。</summary>
    public void SetFilePath(string? path)
    {
        FilePath = path ?? string.Empty;
    }

    /// <summary>加载列映射（表列 → 文件列按名称匹配）。</summary>
    public void RefreshColumnMappings()
    {
        ColumnMappings.Clear();

        if (SelectedTable is null)
            return;

        // 以表列为基准，用户可编辑「文件列」。
        foreach (var column in TableColumns)
        {
            ColumnMappings.Add(new ColumnMappingItem
            {
                SourceColumn = column,
                TargetColumn = column,
            });
        }

        if (ColumnMappings.Count == 0)
        {
            ColumnMappings.Add(new ColumnMappingItem());
        }
    }

    /// <summary>新增一条列映射。</summary>
    [RelayCommand]
    private void AddColumnMapping()
    {
        ColumnMappings.Add(new ColumnMappingItem());
    }

    /// <summary>移除指定列映射。</summary>
    [RelayCommand]
    private void RemoveColumnMapping(ColumnMappingItem? item)
    {
        if (item is not null)
        {
            ColumnMappings.Remove(item);
        }
    }

    [RelayCommand]
    private async Task ImportAsync()
    {
        if (SelectedConnection is null)
        {
            StatusMessage = "请选择连接。";
            return;
        }

        if (SelectedTable is null)
        {
            StatusMessage = "请选择目标表。";
            return;
        }

        if (string.IsNullOrWhiteSpace(FilePath) || !File.Exists(FilePath))
        {
            StatusMessage = "请选择有效的导入文件。";
            return;
        }

        IsBusy = true;
        StatusMessage = string.Empty;
        Logs.Clear();

        var feedbackBuffer = new List<string>();
        void CollectFeedback(string message) => feedbackBuffer.Add(message);

        // 组装列映射（仅保留填写完整的行）。
        IReadOnlyList<ColumnMappingItem>? mappings = null;
        if (UseColumnMapping)
        {
            mappings = ColumnMappings
                .Where(m => !string.IsNullOrWhiteSpace(m.SourceColumn) && !string.IsNullOrWhiteSpace(m.TargetColumn))
                .ToList();
        }

        try
        {
            AppendLog($"连接：{SelectedConnection.Description}");
            AppendLog($"目标表：{SelectedTable.DisplayName}");
            AppendLog($"文件：{FilePath}");
            AppendLog($"首行为列名：{FirstRowIsColumnName}");
            AppendLog("开始导入...");

            var result = await _exportImportService.ImportDataAsync(
                SelectedConnection,
                SelectedTable.Name,
                SelectedTable.Schema,
                FilePath,
                FirstRowIsColumnName,
                mappings,
                CollectFeedback);

            foreach (var line in feedbackBuffer)
            {
                AppendLog(line);
            }

            StatusMessage = result.IsSuccess ? "导入成功。" : $"导入失败：{result.Message}";
            AppendLog(StatusMessage);
        }
        catch (Exception ex)
        {
            StatusMessage = $"导入失败：{ex.Message}";
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
