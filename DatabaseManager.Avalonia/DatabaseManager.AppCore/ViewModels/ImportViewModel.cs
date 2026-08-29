using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DatabaseManager.AppCore.Common;
using DatabaseManager.AppCore.Models;
using DatabaseManager.AppCore.Services;
using DatabaseManager.Core.Model;

namespace DatabaseManager.AppCore.ViewModels;

/// <summary>
/// 数据导入 ViewModel（阶段 6 / M6）。对应原 WinForms <c>frmImportData</c>。
/// 从 CSV / Excel / JSON / XML 文件导入数据到指定表，支持列映射、文件预览、
/// 文本编码选择、跳行续导与跳过错误行。
/// </summary>
public partial class ImportViewModel : ViewModelBase
{
    private readonly IDbConnectionService _connectionService;
    private readonly IExportImportService _exportImportService;
    private CancellationTokenSource? _importCts;

    /// <summary>快速切换连接时，淘汰过期的异步加载结果（竞态防护）。</summary>
    private int _loadTableVersion;

    /// <summary>全部已保存连接。</summary>
    public ObservableCollection<ConnectionItem> Connections { get; } = new();

    /// <summary>表列表。</summary>
    public ObservableCollection<ExportTableItem> Tables { get; } = new();

    /// <summary>列映射（SourceColumn = 文件列，TargetColumn = 表列）。</summary>
    public ObservableCollection<ColumnMappingItem> ColumnMappings { get; } = new();

    /// <summary>执行日志。</summary>
    public ObservableCollection<string> Logs { get; } = new();

    /// <summary>表列名列表（用于列映射下拉/自动匹配）。</summary>
    public ObservableCollection<string> TableColumns { get; } = new();

    /// <summary>文件预览列名（窗口据此重建预览 DataGrid 列）。</summary>
    public ObservableCollection<string> PreviewColumns { get; } = new();

    /// <summary>文件预览样例行（最多 5 行）。</summary>
    public ObservableCollection<DataRowItem> PreviewRows { get; } = new();

    /// <summary>导入错误行报告（校验失败或跳过错误行时有值）。</summary>
    public ObservableCollection<ImportErrorRowItem> ErrorRows { get; } = new();

    /// <summary>文本编码选项。</summary>
    public IReadOnlyList<string> EncodingOptions { get; } = DatabaseManager.FileUtility.TextEncoding.CommonNames;

    [ObservableProperty]
    private ConnectionItem? _selectedConnection;

    [ObservableProperty]
    private ExportTableItem? _selectedTable;

    [ObservableProperty]
    private string _filePath = string.Empty;

    [ObservableProperty]
    private bool _firstRowIsColumnName = true;

    [ObservableProperty]
    private bool _useColumnMapping;

    [ObservableProperty]
    private string _selectedEncoding = DatabaseManager.FileUtility.TextEncoding.CommonNames[0];

    [ObservableProperty]
    private int _skipRows;

    [ObservableProperty]
    private bool _continueOnInvalidRows;

    [ObservableProperty]
    private bool _hasPreview;

    [ObservableProperty]
    private bool _hasErrors;

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

    partial void OnSelectedTableChanged(ExportTableItem? value)
    {
        _ = LoadTableColumnsAsync(value);
    }

    partial void OnFirstRowIsColumnNameChanged(bool value)
    {
        _ = LoadPreviewAsync();
    }

    private async Task LoadTableColumnsAsync(ExportTableItem? table)
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
        catch (Exception ex)
        {
            // 列加载失败不再静默吞掉：提示用户以便排查连接 / 权限问题。
            StatusMessage = $"加载表列失败：{ex.Message}";
            AppendLog(StatusMessage);
        }
    }

    private async Task LoadTablesAsync(ConnectionItem? connection)
    {
        var currentVersion = ++_loadTableVersion;

        if (connection is null)
        {
            Tables.Clear();
            return;
        }

        IsTablesLoading = true;
        try
        {
            var tables = await _exportImportService.GetTablesAsync(connection);

            if (currentVersion != _loadTableVersion)
                return;

            Tables.Clear();
            foreach (var table in tables)
            {
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

    /// <summary>设置导入文件路径（由 UI 文件对话框调用），并加载文件预览与表列用于列映射。</summary>
    public void SetFilePath(string? path)
    {
        FilePath = path ?? string.Empty;
        _ = LoadPreviewAsync();
    }

    /// <summary>读取导入文件的列与样例行（用于列映射预览），并按文件列自动匹配映射。</summary>
    public async Task LoadPreviewAsync()
    {
        PreviewColumns.Clear();
        PreviewRows.Clear();
        HasPreview = false;

        if (string.IsNullOrWhiteSpace(FilePath) || !File.Exists(FilePath))
        {
            return;
        }

        try
        {
            var preview = await _exportImportService.PreviewFileAsync(FilePath, FirstRowIsColumnName, SelectedEncoding);

            if (!preview.IsSuccess)
            {
                StatusMessage = preview.Message;
                return;
            }

            foreach (var column in preview.Columns)
            {
                PreviewColumns.Add(column);
            }

            foreach (var row in preview.SampleRows)
            {
                PreviewRows.Add(new DataRowItem(row));
            }

            HasPreview = PreviewColumns.Count > 0;

            // 按文件真实表头自动匹配映射。
            if (UseColumnMapping && HasPreview)
            {
                RefreshColumnMappings(preview.Columns);
            }

            StatusMessage = preview.TotalRows > 0
                ? $"文件预览：{preview.Columns.Count} 列，共 {preview.TotalRows} 行。"
                : $"文件预览：{preview.Columns.Count} 列。{preview.Message}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"读取文件预览失败：{ex.Message}";
        }
    }

    /// <summary>
    /// 加载列映射（按名称自动匹配文件列 → 表列）。
    /// SourceColumn = 文件列名（默认与表列同名），TargetColumn = 表列名。
    /// 用户可根据实际文件首行修改 SourceColumn。
    /// </summary>
    public void RefreshColumnMappings()
    {
        RefreshColumnMappings(null);
    }

    /// <summary>加载列映射（优先使用文件的真实表头进行自动匹配）。</summary>
    public void RefreshColumnMappings(IReadOnlyList<string>? fileColumns)
    {
        ColumnMappings.Clear();

        if (SelectedTable is null)
            return;

        if (fileColumns is { Count: > 0 })
        {
            // 文件列 → 表列（按名称忽略大小写匹配，未匹配到的留空由用户指定）。
            foreach (var fileColumn in fileColumns)
            {
                var matched = TableColumns.FirstOrDefault(c =>
                    string.Equals(c, fileColumn, StringComparison.OrdinalIgnoreCase));

                ColumnMappings.Add(new ColumnMappingItem
                {
                    SourceColumn = fileColumn,
                    TargetColumn = matched ?? string.Empty,
                });
            }

            if (ColumnMappings.Count == 0)
            {
                ColumnMappings.Add(new ColumnMappingItem());
            }
            return;
        }

        foreach (var column in TableColumns)
        {
            ColumnMappings.Add(new ColumnMappingItem
            {
                SourceColumn = column, // 默认假设文件列名与表列一致
                TargetColumn = column, // 写入到表列
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

    /// <summary>取消正在进行的导入。</summary>
    [RelayCommand(CanExecute = nameof(IsBusy))]
    private void CancelImport()
    {
        _importCts?.Cancel();
    }

    partial void OnIsBusyChanged(bool value) => CancelImportCommand.NotifyCanExecuteChanged();

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
        ErrorRows.Clear();
        HasErrors = false;
        _importCts = new CancellationTokenSource();

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
            if (SkipRows > 0) AppendLog($"跳过前 {SkipRows} 行（续导）");
            if (ContinueOnInvalidRows) AppendLog("跳过错误行：开");
            if (UseColumnMapping) AppendLog($"列映射：{mappings?.Count ?? 0} 条");
            AppendLog("开始导入...");

            var result = await _exportImportService.ImportDataAsync(
                SelectedConnection,
                SelectedTable.Name,
                SelectedTable.Schema,
                FilePath,
                FirstRowIsColumnName,
                mappings,
                SelectedEncoding,
                SkipRows,
                ContinueOnInvalidRows,
                CollectFeedback,
                _importCts.Token);

            foreach (var line in feedbackBuffer)
            {
                AppendLog(line);
            }

            PopulateErrorRows(result);

            StatusMessage = result.IsSuccess ? result.Message : $"导入失败：{result.Message}";
            AppendLog(StatusMessage);
        }
        catch (Exception ex)
        {
            StatusMessage = $"导入失败：{ex.Message}";
            AppendLog(StatusMessage);
        }
        finally
        {
            _importCts?.Dispose();
            _importCts = null;
            IsBusy = false;
        }
    }

    /// <summary>把校验结果中的错误行整理到错误报告集合。</summary>
    private void PopulateErrorRows(ImportResult result)
    {
        ErrorRows.Clear();

        var detail = result.ValidateResultDetail;
        if (detail?.Rows is null)
        {
            HasErrors = false;
            return;
        }

        foreach (var row in detail.Rows.Where(r => !r.IsValid))
        {
            var messages = new List<string>();

            foreach (var cell in row.Cells ?? new List<DataValidateResultCell>())
            {
                if (!cell.IsValid && !string.IsNullOrEmpty(cell.InvalidMessage))
                {
                    var columnName = detail.Columns is { Count: > 0 } && cell.ColumnIndex < detail.Columns.Count
                        ? detail.Columns[cell.ColumnIndex].Name
                        : null;
                    messages.Add($"{(string.IsNullOrEmpty(columnName) ? $"第 {cell.ColumnIndex + 1} 列" : columnName)}：{cell.InvalidMessage}");
                }
            }

            foreach (var invalid in row.InvalidMessages ?? new List<DataValidateResultRowInvalidMessage>())
            {
                messages.Add(invalid.Message);
            }

            ErrorRows.Add(new ImportErrorRowItem
            {
                RowNumber = row.RowIndex + 1,
                Message = messages.Count > 0 ? string.Join("；", messages) : "校验失败。",
            });
        }

        HasErrors = ErrorRows.Count > 0;
    }

    private void AppendLog(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;

        var time = DateTime.Now.ToString("HH:mm:ss");
        Logs.Add($"[{time}] {message}");
    }
}

/// <summary>导入错误行报告条目。</summary>
public class ImportErrorRowItem
{
    /// <summary>数据行号（从 1 开始，不含表头）。</summary>
    public int RowNumber { get; set; }

    /// <summary>错误描述。</summary>
    public string Message { get; set; } = string.Empty;
}
