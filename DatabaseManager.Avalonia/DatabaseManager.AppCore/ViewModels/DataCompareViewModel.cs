using System.Collections.ObjectModel;
using System.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DatabaseManager.AppCore.Common;
using DatabaseManager.AppCore.Models;
using DatabaseManager.AppCore.Services;
using DatabaseManager.Core;
using DatabaseManager.Core.Model;

namespace DatabaseManager.AppCore.ViewModels;

/// <summary>
/// 数据对比 ViewModel（阶段 4）。
/// 对比两个同类型数据库的表数据差异：选择源/目标连接、表与展示模式，执行对比并查看明细/生成同步脚本。
/// </summary>
public partial class DataCompareViewModel : ViewModelBase
{
    private readonly IDbConnectionService _connectionService;
    private readonly ICompareService _compareService;
    private readonly ISyncScriptService _syncScriptService;

    private ConnectionItem? _effectiveSource;
    private ConnectionItem? _effectiveTarget;
    private IReadOnlyList<DataCompareResultItem>? _lastResults;

    /// <summary>全部已保存连接（源/目标下拉共用）。</summary>
    public ObservableCollection<ConnectionItem> Connections { get; } = new();

    /// <summary>源库表列表（可选择要对比的表）。</summary>
    public ObservableCollection<TableItem> Tables { get; } = new();

    /// <summary>数据对比结果（按表概览）。</summary>
    public ObservableCollection<DataCompareResultItem> Results { get; } = new();

    /// <summary>展示模式选项。</summary>
    public IReadOnlyList<DataCompareModeOption> Modes { get; }

    /// <summary>执行日志。</summary>
    public ObservableCollection<string> Logs { get; } = new();

    /// <summary>同步脚本。</summary>
    public ObservableCollection<string> Scripts { get; } = new();

    /// <summary>当前选中表的数据明细行（动态列）。</summary>
    public ObservableCollection<DataRowItem> DetailRows { get; } = new();

    [ObservableProperty]
    private ConnectionItem? _sourceConnection;

    [ObservableProperty]
    private ConnectionItem? _targetConnection;

    [ObservableProperty]
    private DataCompareModeOption? _selectedMode;

    [ObservableProperty]
    private DataCompareResultItem? _selectedResult;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private bool _isTablesLoading;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    /// <summary>当前明细的列标题（Dynamic 数据行用）。</summary>
    [ObservableProperty]
    private IReadOnlyList<string> _detailColumns = System.Array.Empty<string>();

    /// <summary>由窗口注入的脚本预览窗口打开回调。</summary>
    public Action<ScriptPreviewViewModel>? RequestScriptPreview { get; set; }

    public DataCompareViewModel(IDbConnectionService connectionService, ICompareService compareService, ISyncScriptService syncScriptService)
    {
        _connectionService = connectionService;
        _compareService = compareService;
        _syncScriptService = syncScriptService;

        Modes = new[]
        {
            new DataCompareModeOption(DataCompareDisplayMode.Different, "差异记录"),
            new DataCompareModeOption(DataCompareDisplayMode.OnlyInSource, "仅在源库"),
            new DataCompareModeOption(DataCompareDisplayMode.OnlyInTarget, "仅在目标库"),
            new DataCompareModeOption(DataCompareDisplayMode.Indentical, "完全一致"),
            new DataCompareModeOption(DataCompareDisplayMode.Different | DataCompareDisplayMode.OnlyInSource | DataCompareDisplayMode.OnlyInTarget, "全部"),
        };

        SelectedMode = Modes.Last();
    }

    /// <summary>加载已保存连接并刷新源/目标选择。</summary>
    public void RefreshConnections()
    {
        var previousSourceId = SourceConnection?.Id;
        var previousTargetId = TargetConnection?.Id;

        Connections.Clear();
        foreach (var item in _connectionService.GetConnections())
        {
            Connections.Add(item);
        }

        SourceConnection = FindConnection(previousSourceId) ?? Connections.FirstOrDefault();
        TargetConnection = FindConnection(previousTargetId) ?? Connections.Skip(1).FirstOrDefault();

        _effectiveSource = SourceConnection;
        _effectiveTarget = TargetConnection;
    }

    private ConnectionItem? FindConnection(string? id)
        => Connections.FirstOrDefault(c => c.Id == id);

    partial void OnSourceConnectionChanged(ConnectionItem? value)
    {
        _effectiveSource = value;
        // 源连接变化时重新加载表列表。
        _ = LoadTablesAsync(value);
    }

    partial void OnTargetConnectionChanged(ConnectionItem? value)
    {
        _effectiveTarget = value;
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
            var tables = await _compareService.GetTablesAsync(connection);
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

    /// <summary>当前选中结果的明细分类是否为空。</summary>
    public bool CanShowDetail => SelectedResult is not null;

    [RelayCommand]
    private async Task ExecuteAsync()
    {
        if (SourceConnection is null || TargetConnection is null)
        {
            StatusMessage = "请选择源连接和目标连接。";
            return;
        }

        var selectedTables = Tables.Where(t => t.IsSelected).Select(t => t.Name).ToList();
        if (selectedTables.Count == 0)
        {
            StatusMessage = "请至少选择一张要对比的表。";
            return;
        }

        var mode = SelectedMode?.Value ?? DataCompareDisplayMode.None;

        IsBusy = true;
        StatusMessage = string.Empty;
        Logs.Clear();
        Results.Clear();
        DetailRows.Clear();
        Scripts.Clear();

        try
        {
            var feedbackBuffer = new List<string>();
            void CollectFeedback(string message) => feedbackBuffer.Add(message);

            AppendLog($"源：{SourceConnection.Description}");
            AppendLog($"目标：{TargetConnection.Description}");
            AppendLog($"对比表数：{selectedTables.Count}");
            AppendLog($"展示模式：{SelectedMode?.DisplayName}");
            AppendLog("开始对比...");

            var results = await _compareService.CompareDataAsync(
                SourceConnection,
                TargetConnection,
                selectedTables,
                mode,
                CollectFeedback);

            _lastResults = results;

            foreach (var line in feedbackBuffer)
            {
                AppendLog(line);
            }

            foreach (var result in results)
            {
                Results.Add(result);
            }

            var diffCount = results.Sum(r => r.DifferentCount);
            var onlySource = results.Sum(r => r.OnlyInSourceCount);
            var onlyTarget = results.Sum(r => r.OnlyInTargetCount);

            StatusMessage = $"对比完成：{results.Count} 张表，差异 {diffCount} 条，仅源 {onlySource} 条，仅目标 {onlyTarget} 条。";
            AppendLog(StatusMessage);
        }
        catch (Exception ex)
        {
            StatusMessage = $"对比失败：{ex.Message}";
            AppendLog(StatusMessage);
        }
        finally
        {
            IsBusy = false;
        }
    }

    partial void OnSelectedResultChanged(DataCompareResultItem? value)
    {
        // 选中某张表时，默认展示该表的差异明细。
        if (value is null)
        {
            DetailRows.Clear();
            DetailColumns = System.Array.Empty<string>();
            return;
        }

        _ = ShowDetailAsync(value, "Different", 1);
    }

    /// <summary>展示指定分类的明细数据（分页）。</summary>
    public async Task ShowDetailAsync(DataCompareResultItem item, string category, long pageNumber)
    {
        if (_effectiveSource is null || _effectiveTarget is null)
        {
            return;
        }

        try
        {
            IsBusy = true;
            var pageSize = 100;

            var (data, valueInfos) = await _compareService.GetTableDataAsync(
                _effectiveSource,
                _effectiveTarget,
                item.Detail,
                category,
                pageSize,
                pageNumber);

            PopulateDetailRows(data, valueInfos);
            StatusMessage = $"表 {item.TableName} · {GetCategoryName(category)}：第 {pageNumber} 页。";
        }
        catch (Exception ex)
        {
            StatusMessage = $"加载明细失败：{ex.Message}";
            DetailRows.Clear();
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void PopulateDetailRows(DataTable data, Dictionary<int, List<DataCompareValueInfo>> valueInfos)
    {
        DetailColumns = data.Columns.Cast<DataColumn>()
            .Select(c => DataCompare.GetDifferentDataColumnDisplayText(c.ColumnName))
            .ToList();

        DetailRows.Clear();

        foreach (DataRow row in data.Rows)
        {
            var values = new List<string>();
            for (int i = 0; i < data.Columns.Count; i++)
            {
                var value = row[i];
                values.Add(FormatCellValue(value));
            }
            DetailRows.Add(new DataRowItem(values));
        }
    }

    /// <summary>格式化单元格值：区分 DBNull / 空串 / 二进制字节数组 / 普通值。</summary>
    private static string FormatCellValue(object? value)
    {
        if (value is null || value is DBNull)
            return "[NULL]";

        if (value is byte[] bytes)
        {
            if (bytes.Length == 0)
                return "[BINARY 0B]";
            string suffix = bytes.Length > 128 ? $"…({bytes.Length}B)" : string.Empty;
            try
            {
                int take = Math.Min(bytes.Length, 128);
                return Convert.ToBase64String(bytes, 0, take) + suffix;
            }
            catch
            {
                return $"[BINARY {bytes.Length}B]";
            }
        }

        var str = Convert.ToString(value);
        if (str is null)
            return "[NULL]";
        if (str.Length == 0)
            return "[EMPTY]";
        return str;
    }

    /// <summary>生成同步脚本（按勾选的表逐表生成）并打开脚本预览窗口，支持审阅后执行。</summary>
    [RelayCommand]
    private async Task GenerateScriptsAsync()
    {
        await GenerateAndPreviewAsync(isRollback: false);
    }

    /// <summary>生成回滚脚本（把目标库数据恢复为对比前状态）并打开脚本预览窗口。</summary>
    [RelayCommand]
    private async Task GenerateDataRollbackAsync()
    {
        await GenerateAndPreviewAsync(isRollback: true);
    }

    private async Task GenerateAndPreviewAsync(bool isRollback)
    {
        if (_lastResults is null || _lastResults.Count == 0)
        {
            StatusMessage = "请先执行数据对比。";
            return;
        }

        if (SourceConnection is null || TargetConnection is null)
        {
            StatusMessage = "源/目标连接无效。";
            return;
        }

        // 仅对勾选且存在差异的表生成脚本。
        var selectedItems = _lastResults
            .Where(r => r.IsSelected && (r.DifferentCount > 0 || r.OnlyInSourceCount > 0 || r.OnlyInTargetCount > 0))
            .ToList();

        if (selectedItems.Count == 0)
        {
            StatusMessage = isRollback ? "勾选的表没有需要回滚的数据差异。" : "勾选的表没有需要同步的数据。";
            return;
        }

        IsBusy = true;
        Scripts.Clear();
        try
        {
            var feedbackBuffer = new List<string>();
            void CollectFeedback(string message) => feedbackBuffer.Add(message);

            var scripts = isRollback
                ? await _syncScriptService.GenerateDataRollbackScriptsAsync(SourceConnection, TargetConnection, selectedItems, CollectFeedback)
                : await _syncScriptService.GenerateDataSyncScriptsAsync(SourceConnection, TargetConnection, selectedItems, CollectFeedback);

            foreach (var line in feedbackBuffer)
            {
                AppendLog(line);
            }

            // 同步脚本页仍保留合并文本，便于快速查看。
            foreach (var script in scripts)
            {
                Scripts.Add($"-- ===== {script.Title} =====");
                foreach (var line in script.SqlText.Split('\n'))
                {
                    Scripts.Add(line.TrimEnd('\r'));
                }
                Scripts.Add(string.Empty);
            }

            if (scripts.Count == 0)
            {
                StatusMessage = isRollback ? "没有可生成的回滚脚本。" : "没有可生成的同步脚本。";
                return;
            }

            var previewVm = new ScriptPreviewViewModel(_syncScriptService)
            {
                TargetConnection = TargetConnection,
                SourceDescription = isRollback
                    ? $"数据对比回滚（恢复 {TargetConnection.Database} 为对比前状态）"
                    : $"数据对比（{SourceConnection.Database} → 同步到 {TargetConnection.Database}）",
            };
            foreach (var script in scripts)
            {
                previewVm.Scripts.Add(script);
            }

            StatusMessage = $"已生成 {scripts.Count} 项脚本，请审阅后选择执行。";
            AppendLog(StatusMessage);
            RequestScriptPreview?.Invoke(previewVm);
        }
        catch (Exception ex)
        {
            StatusMessage = $"生成脚本失败：{ex.Message}";
            AppendLog(StatusMessage);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static string GetCategoryName(string category) => category switch
    {
        "Different" => "差异记录",
        "OnlyInSource" => "仅在源库",
        "OnlyInTarget" => "仅在目标库",
        "Identical" => "完全一致",
        _ => category,
    };

    private void AppendLog(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;

        var time = DateTime.Now.ToString("HH:mm:ss");
        Logs.Add($"[{time}] {message}");
    }
}

/// <summary>动态列数据行（UI 绑定，支持按索引访问值）。</summary>
public sealed class DataRowItem
{
    private readonly IReadOnlyList<string> _values;

    public DataRowItem(IReadOnlyList<string> values)
    {
        _values = values;
    }

    /// <summary>按列索引取值（用于 DataGrid 的 [i] 绑定）。</summary>
    public string this[int index] => index >= 0 && index < _values.Count ? _values[index] : string.Empty;
}
