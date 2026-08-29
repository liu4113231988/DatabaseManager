using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DatabaseManager.AppCore.Common;
using DatabaseManager.AppCore.Models;
using DatabaseManager.AppCore.Services;

namespace DatabaseManager.AppCore.ViewModels;

/// <summary>
/// 数据编辑器 ViewModel（AppCore 层）。
/// 负责表数据的加载（分页）、可编辑行集合管理、增删改（Add/Remove）、保存（Commit）与还原（Revert）。
/// </summary>
public partial class DataEditorViewModel : ViewModelBase
{
    private readonly IDataEditService _editService;

    /// <summary>当前编辑表的列定义。</summary>
    public ObservableCollection<DataColumnInfo> Columns { get; } = new();

    /// <summary>可编辑数据行。</summary>
    public ObservableCollection<DataEditRow> Rows { get; } = new();

    /// <summary>已标记删除的行（保存时统一删除，展示时从网格移除）。</summary>
    private readonly List<DataEditRow> _deletedRows = new();

    /// <summary>当前连接名。</summary>
    private string _connectionName = string.Empty;

    /// <summary>当前表是否具有主键（无主键表只读）。</summary>
    private bool _hasPrimaryKey = true;

    /// <summary>当前表是否为大表（超过阈值默认只读，避免误操作与性能问题）。</summary>
    private bool _isLargeTable;

    /// <summary>大表判定阈值（行数）。超过此值默认只读。</summary>
    private const long LargeTableRowThreshold = 500_000;

    /// <summary>加载任务的取消令牌源（取消上一次未完成的加载，避免快速翻页串数据）。</summary>
    private CancellationTokenSource? _loadCts;

    /// <summary>当前是否加载了数据（可编辑状态）。</summary>
    [ObservableProperty]
    private bool _isLoaded;

    /// <summary>当前是否正忙（加载/保存中）。</summary>
    [ObservableProperty]
    private bool _isBusy;

    /// <summary>状态消息。</summary>
    [ObservableProperty]
    private string _statusMessage = string.Empty;

    /// <summary>当前表名。</summary>
    [ObservableProperty]
    private string _tableName = string.Empty;

    /// <summary>当前数据库名。</summary>
    [ObservableProperty]
    private string _databaseName = string.Empty;

    /// <summary>当前 Schema（可为空）。</summary>
    [ObservableProperty]
    private string _schema = string.Empty;

    /// <summary>是否为视图（只读提示）。</summary>
    [ObservableProperty]
    private bool _isView;

    /// <summary>每页行数。</summary>
    [ObservableProperty]
    private int _pageSize = 100;

    /// <summary>每页行数可选项（供下拉选择）。</summary>
    public IReadOnlyList<int> PageSizeOptions { get; } = new[] { 50, 100, 200, 500, 1000 };

    /// <summary>当前页码。</summary>
    [ObservableProperty]
    private long _pageNumber = 1;

    /// <summary>总行数。</summary>
    [ObservableProperty]
    private long _totalCount;

    /// <summary>是否有未保存的改动。</summary>
    [ObservableProperty]
    private bool _hasChanges;

    /// <summary>总行数是否未知（翻页跳过 COUNT 时）。</summary>
    private bool _totalCountUnknown;

    /// <summary>总页数（总数未知时返回 -1，表示未知，由翻页逻辑按需尝试）。</summary>
    public long PageCount => _totalCountUnknown || PageSize <= 0
        ? -1
        : (TotalCount + PageSize - 1) / PageSize;

    /// <summary>总页数显示文本（总数未知时显示 "?"，避免直接显示 -1）。</summary>
    public string PageCountText => _totalCountUnknown ? "?" : PageCount.ToString();

    /// <summary>是否可新增/编辑（表且有主键、非大表时可编辑；视图/无主键/大表仅查看）。</summary>
    public bool IsEditable => !IsView && _hasPrimaryKey && !_isLargeTable;

    /// <summary>是否存在未保存的改动（含已标记删除的行）。</summary>
    public bool HasUnsavedChanges => HasChanges || _deletedRows.Count > 0;

    /// <summary>只读原因（供 UI 展示；可编辑时为空）。</summary>
    public string ReadOnlyReason
    {
        get
        {
            if (IsView) return "视图为只读。";
            if (!_hasPrimaryKey) return "该表无主键，无法安全编辑，已置为只读。";
            if (_isLargeTable) return $"该表行数超过 {LargeTableRowThreshold:N0}，已置为只读以保护性能。";
            return string.Empty;
        }
    }

    public DataEditorViewModel(IDataEditService editService)
    {
        _editService = editService;
    }

    /// <summary>PageSize 变化时重置到第一页并重新加载。</summary>
    partial void OnPageSizeChanged(int value)
    {
        // 仅在已加载数据时响应，避免初始化/加载流程中的递归重载。
        if (!IsLoaded || IsBusy)
            return;

        PageNumber = 1;
        _ = LoadCurrentPageAsync();
    }

    /// <summary>加载指定表的数据。返回是否成功。</summary>
    public async Task<bool> LoadAsync(
        string connectionName,
        string databaseName,
        string tableName,
        string? schema,
        bool isView)
    {
        _connectionName = connectionName;
        DatabaseName = databaseName;
        TableName = tableName;
        Schema = schema ?? string.Empty;
        IsView = isView;
        PageNumber = 1;
        _hasPrimaryKey = true;
        _isLargeTable = false;
        _totalCountUnknown = false;
        _deletedRows.Clear();

        return await LoadCurrentPageAsync();
    }

    /// <summary>加载指定页的数据。返回是否成功。</summary>
    [RelayCommand]
    private async Task LoadPageAsync()
    {
        if (IsBusy) return;
        await LoadCurrentPageAsync();
    }

    /// <summary>上一页。</summary>
    [RelayCommand]
    private async Task PreviousPageAsync()
    {
        if (IsBusy || PageNumber <= 1) return;
        PageNumber--;
        await LoadCurrentPageAsync();
    }

    /// <summary>下一页。</summary>
    [RelayCommand]
    private async Task NextPageAsync()
    {
        if (IsBusy) return;
        // 总数未知时允许翻页尝试（返回空数据由加载逻辑回退）。
        if (!_totalCountUnknown && PageNumber >= PageCount) return;
        PageNumber++;
        await LoadCurrentPageAsync();
    }

    private async Task<bool> LoadCurrentPageAsync()
    {
        if (string.IsNullOrWhiteSpace(_connectionName) || string.IsNullOrWhiteSpace(TableName))
            return false;

        // 取消上一次未完成的加载，避免快速翻页导致旧结果覆盖新结果（数据串页）。
        _loadCts?.Cancel();
        _loadCts?.Dispose();
        _loadCts = new CancellationTokenSource();
        var ct = _loadCts.Token;

        IsBusy = true;
        StatusMessage = $"正在加载 {TableName} 数据...";

        try
        {
            // 首次加载统计总数；翻页时跳过 COUNT（大表避免全表扫描）。
            bool loadTotalCount = PageNumber == 1;

            var result = await _editService.LoadDataAsync(
                _connectionName,
                DatabaseName,
                TableName,
                string.IsNullOrEmpty(Schema) ? null : Schema,
                IsView,
                PageSize,
                PageNumber,
                loadTotalCount,
                ct);

            ct.ThrowIfCancellationRequested();

            if (!result.IsSuccess)
            {
                StatusMessage = $"加载失败：{result.ErrorMessage}";
                return false;
            }

            _hasPrimaryKey = result.HasPrimaryKey;
            _totalCountUnknown = result.TotalCountUnknown;

            // 大表判定：仅在统计到总数时判断（翻页跳过了 COUNT，不重复判定）。
            if (!result.TotalCountUnknown)
            {
                _isLargeTable = result.TotalCount > LargeTableRowThreshold;
            }

            Columns.Clear();
            foreach (var col in result.TableInfo.Columns)
            {
                Columns.Add(col);
            }

            Rows.Clear();
            foreach (var row in result.Rows)
            {
                Rows.Add(row);
                SubscribeRow(row);
            }

            TotalCount = result.TotalCount;

            // 总数未知时翻页越过末尾：当前页为空则回退页码并重载上一页。
            if (result.TotalCountUnknown && Rows.Count == 0 && PageNumber > 1)
            {
                PageNumber--;
                await LoadCurrentPageAsync();
                return true;
            }

            IsLoaded = true;
            HasChanges = false;
            _deletedRows.Clear();

            // 通知 UI 刷新 IsEditable / ReadOnlyReason（无主键/大表置为只读）。
            OnPropertyChanged(nameof(IsEditable));
            OnPropertyChanged(nameof(ReadOnlyReason));

            StatusMessage = BuildLoadStatusMessage(result.TotalCountUnknown);
            return true;
        }
        catch (OperationCanceledException)
        {
            // 被新的加载请求取消，静默返回，不覆盖状态消息。
            return false;
        }
        catch (Exception ex)
        {
            StatusMessage = $"加载失败：{ex.Message}";
            return false;
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>根据加载结果构造状态消息（区分总数未知/大表/无主键/正常）。</summary>
    private string BuildLoadStatusMessage(bool totalCountUnknown)
    {
        if (totalCountUnknown)
        {
            // 翻页未统计总数：仅显示当前页与页码。
            return $"第 {PageNumber} 页，已加载 {Rows.Count} 行。";
        }

        if (_isLargeTable)
        {
            return $"已加载 {Rows.Count} / {TotalCount:N0} 行（大表已置为只读）。";
        }

        if (!_hasPrimaryKey)
        {
            return $"已加载 {Rows.Count} / {TotalCount} 行（该表无主键，仅只读）。";
        }

        return $"已加载 {Rows.Count} / {TotalCount} 行，共 {PageCount} 页。";
    }

    /// <summary>新增一行。</summary>
    [RelayCommand]
    private void AddRow()
    {
        if (!IsEditable) return;

        var row = new DataEditRow(Columns.ToList());
        row.MarkAsAdded();
        Rows.Add(row);
        SubscribeRow(row);
        HasChanges = true;
        StatusMessage = "已新增一行，填写后点击「保存」。";
    }

    /// <summary>删除指定行（标记为已删除并从网格移除，保存时统一删除）。</summary>
    [RelayCommand]
    private void RemoveRow(DataEditRow? row)
    {
        if (!IsEditable || row is null) return;

        if (row.State == DataRowState.Added)
        {
            // 新增行直接丢弃，不产生 DELETE。
            Rows.Remove(row);
        }
        else
        {
            row.MarkAsDeleted();
            Rows.Remove(row);
            _deletedRows.Add(row);
        }

        HasChanges = true;
        StatusMessage = "已删除所选行，点击「保存」生效。";
    }

    /// <summary>还原全部改动。</summary>
    [RelayCommand]
    private async Task RevertAsync()
    {
        if (!HasChanges && _deletedRows.Count == 0)
        {
            StatusMessage = "当前没有需要还原的改动。";
            return;
        }

        _deletedRows.Clear();
        await LoadCurrentPageAsync();
        StatusMessage = "已还原改动。";
    }

    /// <summary>保存全部改动。</summary>
    [RelayCommand]
    private async Task SaveAsync()
    {
        if (!HasChanges && _deletedRows.Count == 0)
        {
            StatusMessage = "当前没有需要保存的改动。";
            return;
        }

        IsBusy = true;
        StatusMessage = "正在保存改动...";

        try
        {
            var inserts = Rows.Where(r => r.State == DataRowState.Added).ToList();
            var updates = Rows.Where(r => r.State == DataRowState.Modified).ToList();
            var deletes = _deletedRows.Where(r => r.State == DataRowState.Deleted).ToList();

            // 保存前校验：主键列不能为空（否则定位/插入都会失败）。
            // 自增/标识列允许为空（由数据库自动填充）。
            var invalidPkRow = updates.Concat(deletes).FirstOrDefault(r =>
                r.GetPrimaryKeyConditions().Any(pk => pk.Value is null));
            if (invalidPkRow is not null)
            {
                StatusMessage = "保存失败：主键列不能为空，请先填写主键值。";
                return;
            }

            var result = await _editService.SaveChangesAsync(
                _connectionName,
                DatabaseName,
                TableName,
                string.IsNullOrEmpty(Schema) ? null : Schema,
                inserts,
                updates,
                deletes);

            if (!result.IsSuccess)
            {
                // 错误信息已包含并发冲突/无主键等具体原因（见 DefaultDataEditService）。
                StatusMessage = $"保存失败：{result.ErrorMessage}";
                return;
            }

            // 保存成功后刷新最新数据（提交后重载当前页，保证主键/自增值正确）。
            await LoadPageAsync();

            StatusMessage = $"保存成功，影响 {result.RowCount} 行。";
        }
        catch (Exception ex)
        {
            StatusMessage = $"保存失败：{ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>清除当前编辑状态。</summary>
    public void Clear()
    {
        _loadCts?.Cancel();
        _loadCts?.Dispose();
        _loadCts = null;

        foreach (var row in Rows)
        {
            row.PropertyChanged -= Row_PropertyChanged;
        }

        Columns.Clear();
        Rows.Clear();
        _deletedRows.Clear();
        IsLoaded = false;
        HasChanges = false;
        TotalCount = 0;
        PageNumber = 1;
        TableName = string.Empty;
        StatusMessage = string.Empty;
        _hasPrimaryKey = true;
        _isLargeTable = false;
        _totalCountUnknown = false;
        OnPropertyChanged(nameof(IsEditable));
        OnPropertyChanged(nameof(ReadOnlyReason));
    }

    /// <summary>订阅行属性变化，以实时刷新 HasChanges。</summary>
    private void SubscribeRow(DataEditRow row)
    {
        row.PropertyChanged -= Row_PropertyChanged;
        row.PropertyChanged += Row_PropertyChanged;
    }

    private void Row_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(DataEditRow.IsDirty))
        {
            HasChanges = Rows.Any(r => r.IsDirty) || _deletedRows.Count > 0;
        }
    }
}
