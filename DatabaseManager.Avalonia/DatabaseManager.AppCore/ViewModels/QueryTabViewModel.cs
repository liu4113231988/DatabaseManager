using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DatabaseManager.AppCore.Common;
using DatabaseManager.AppCore.Models;
using DatabaseManager.AppCore.Services;

namespace DatabaseManager.AppCore.ViewModels;

/// <summary>
/// 查询标签页 ViewModel（对齐 DBeaver 多标签设计）。
/// 每个标签页拥有独立的 SQL 编辑器、执行状态和结果集。
/// 结果集为单表简单 SELECT 时支持内联编辑（增删改，经 IDataEditService 保存）。
/// </summary>
public partial class QueryTabViewModel : ViewModelBase
{
    private readonly IQueryService _queryService;
    private readonly IDataEditService? _editService;
    private static int _tabCounter = 0;

    [ObservableProperty]
    private string _title = "查询";

    private string _sqlText = string.Empty;

    /// <summary>SQL 文本（带修改跟踪）。</summary>
    public string SqlText
    {
        get => _sqlText;
        set
        {
            if (SetProperty(ref _sqlText, value))
            {
                // 当用户修改 SQL 时标记为已修改
                if (!_isSaving)
                {
                    IsModified = true;
                    // 更新标题显示修改状态
                    UpdateTitleWithModifiedMark();
                }
            }
        }
    }

    [ObservableProperty]
    private bool _isExecuting;

    [ObservableProperty]
    private string _statusMessage = "就绪";

    [ObservableProperty]
    private bool _hasResult;

    [ObservableProperty]
    private bool _showNoResult = true;

    [ObservableProperty]
    private string _connectionName = string.Empty;

    /// <summary>当前数据库名（内联编辑定位表时使用；由主窗口在执行前同步）。</summary>
    [ObservableProperty]
    private string _databaseName = string.Empty;

    [ObservableProperty]
    private bool _isModified;

    /// <summary>原始标题（不含修改标记）。</summary>
    private string _baseTitle = "查询";

    /// <summary>是否正在保存中（防止保存时触发修改标记）。</summary>
    private bool _isSaving;

    private readonly List<QueryResultRow> _allRows = new();

    /// <summary>查询结果列名。</summary>
    public ObservableCollection<string> Columns { get; } = new();

    /// <summary>当前页的行数据（绑定到结果表格；全量数据在 _allRows 中分页切片）。</summary>
    public ObservableCollection<QueryResultRow> Rows { get; } = new();

    [ObservableProperty]
    private int _pageSize = 50;

    [ObservableProperty]
    private int _currentPage = 1;

    #region 内联编辑状态

    /// <summary>结果集是否可内联编辑（单表简单 SELECT + 有主键 + 结果含全部主键列）。</summary>
    [ObservableProperty]
    private bool _isResultEditable;

    /// <summary>不可编辑的原因（可编辑时为空）。</summary>
    [ObservableProperty]
    private string _editReadOnlyReason = string.Empty;

    /// <summary>是否存在未保存的内联编辑改动。</summary>
    [ObservableProperty]
    private bool _hasPendingChanges;

    /// <summary>是否有未保存改动或正在保存（供保存按钮禁用态）。</summary>
    [ObservableProperty]
    private bool _isSavingChanges;

    /// <summary>可编辑目标表的元数据（可编辑时有值）。</summary>
    private DataTableInfo? _editableTableInfo;

    /// <summary>待删除的行（从结果集中移除，保存时统一 DELETE；还原时按原位置放回）。</summary>
    private readonly List<(QueryResultRow Row, int OriginalIndex)> _pendingDeletes = new();

    /// <summary>目标表名 / Schema（可编辑时有值）。</summary>
    private string? _editableTableName;
    private string? _editableSchema;

    #endregion

    /// <summary>每页大小可选项（供下拉选择）。</summary>
    public int[] PageSizeOptions { get; } = { 50, 100, 200, 500, 1000 };

    /// <summary>总行数。</summary>
    public int TotalRows => _allRows.Count;

    /// <summary>总行数（含待删除行）。</summary>
    public int TotalRowsIncludingDeleted => _allRows.Count + _pendingDeletes.Count;

    /// <summary>总页数。</summary>
    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling(_allRows.Count / (double)PageSize) : 0;

    /// <summary>分页信息文案（如「第 2 / 5 页 · 共 243 行」）。</summary>
    public string PageInfo =>
        TotalRows == 0 ? "共 0 行" : $"第 {CurrentPage} / {Math.Max(1, TotalPages)} 页 · 共 {TotalRows} 行";

    /// <summary>是否可以翻到上一页。</summary>
    public bool CanGoPrevPage => CurrentPage > 1;

    /// <summary>是否可以翻到下一页。</summary>
    public bool CanGoNextPage => CurrentPage < TotalPages;

    /// <summary>此标签页的唯一 ID。</summary>
    public int TabId { get; }

    public QueryTabViewModel(IQueryService queryService, IDataEditService? editService = null, string? title = null)
    {
        _queryService = queryService;
        _editService = editService;
        TabId = ++_tabCounter;
        _baseTitle = title ?? $"查询 {TabId}";
        Title = _baseTitle;
    }

    /// <summary>根据修改状态更新标题（添加 * 标记）。</summary>
    private void UpdateTitleWithModifiedMark()
    {
        Title = IsModified ? $"{_baseTitle} *" : _baseTitle;
    }

    /// <summary>标记为已保存（清除修改标记）。</summary>
    public void MarkAsSaved()
    {
        IsModified = false;
        UpdateTitleWithModifiedMark();
    }

    /// <summary>执行当前 SQL。</summary>
    [RelayCommand]
    public async Task ExecuteAsync()
    {
        if (IsExecuting)
            return;

        if (string.IsNullOrWhiteSpace(ConnectionName))
        {
            StatusMessage = "请先在对象浏览器中选择一个连接。";
            return;
        }

        if (string.IsNullOrWhiteSpace(SqlText))
        {
            StatusMessage = "请输入要执行的 SQL 语句。";
            return;
        }

        IsExecuting = true;
        StatusMessage = "正在执行...";

        try
        {
            var result = await _queryService.ExecuteAsync(ConnectionName, SqlText);
            ApplyResult(result);

            // 执行成功且返回结果集时，尝试启用内联编辑。
            if (HasResult)
            {
                await TryEnableEditingAsync();
            }
            else
            {
                ResetEditingState();
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"执行失败：{ex.Message}";
            HasResult = false;
        }
        finally
        {
            IsExecuting = false;
        }
    }

    private void ApplyResult(QueryResult result)
    {
        Columns.Clear();
        _allRows.Clear();

        if (!result.IsSuccess)
        {
            StatusMessage = $"执行失败：{result.ErrorMessage}";
            HasResult = false;
            ShowNoResult = true;
            RefreshPage();
            return;
        }

        if (result.IsNonQuery)
        {
            StatusMessage = $"命令已执行，影响 {result.RowCount} 行。";
            HasResult = false;
            ShowNoResult = true;
            RefreshPage();
            return;
        }

        foreach (var col in result.Columns)
        {
            Columns.Add(col);
        }

        foreach (var row in result.Rows)
        {
            _allRows.Add(new QueryResultRow(result.Columns, row));
        }

        StatusMessage = $"查询完成，返回 {result.RowCount} 行，耗时 {result.ElapsedMilliseconds} ms。";
        HasResult = true;
        ShowNoResult = false;

        // 新结果集回到第一页并切片显示
        CurrentPage = 1;
        RefreshPage();
    }

    #region 内联编辑

    /// <summary>执行成功后判定结果集能否内联编辑：解析 SQL 定位目标表并校验主键覆盖。</summary>
    private async Task TryEnableEditingAsync()
    {
        ResetEditingState();

        if (_editService is null || string.IsNullOrWhiteSpace(DatabaseName))
        {
            EditReadOnlyReason = "缺少数据库上下文，结果只读。";
            return;
        }

        var parse = SimpleSelectParser.Parse(SqlText);
        if (!parse.IsSimpleSelect)
        {
            EditReadOnlyReason = $"结果只读：{parse.NotEditableReason}";
            return;
        }

        var metadata = await _editService.GetTableMetadataAsync(
            ConnectionName, DatabaseName, parse.TableName!, parse.Schema);

        if (!metadata.IsSuccess)
        {
            EditReadOnlyReason = $"结果只读：无法读取表结构（{metadata.ErrorMessage}）。";
            return;
        }

        if (!metadata.HasPrimaryKey)
        {
            EditReadOnlyReason = "结果只读：目标表没有主键，无法安全定位行。";
            return;
        }

        // 校验：结果列必须包含目标表全部主键列，且都能映射到表列（否则该列只读）。
        var tableColumns = metadata.TableInfo.Columns
            .ToDictionary(c => c.Name, StringComparer.OrdinalIgnoreCase);
        var missingPk = metadata.TableInfo.PrimaryKeyColumns
            .FirstOrDefault(pk => !Columns.Contains(pk, StringComparer.OrdinalIgnoreCase));

        if (missingPk is not null)
        {
            EditReadOnlyReason = $"结果只读：SELECT 未包含主键列 {missingPk}。";
            return;
        }

        _editableTableInfo = metadata.TableInfo;
        _editableTableName = parse.TableName;
        _editableSchema = parse.Schema;
        IsResultEditable = true;
        string tableLabel = string.IsNullOrEmpty(_editableSchema) ? _editableTableName! : $"{_editableSchema}.{_editableTableName}";
        StatusMessage = $"查询完成，返回 {_allRows.Count} 行（可编辑：{tableLabel}）。";
    }

    private void ResetEditingState()
    {
        IsResultEditable = false;
        HasPendingChanges = false;
        IsSavingChanges = false;
        EditReadOnlyReason = string.Empty;
        _pendingDeletes.Clear();
        _editableTableInfo = null;
        _editableTableName = null;
        _editableSchema = null;
    }

    /// <summary>判断结果集中第 index 列（0 基）是否允许编辑。</summary>
    public bool IsColumnEditable(int columnIndex)
    {
        if (!IsResultEditable || _editableTableInfo is null)
            return false;
        if (columnIndex < 0 || columnIndex >= Columns.Count)
            return false;

        var col = FindTableColumn(Columns[columnIndex]);
        return col is not null && !col.IsReadOnly;
    }

    private DataColumnInfo? FindTableColumn(string name)
    {
        foreach (var c in _editableTableInfo!.Columns)
        {
            if (string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase))
                return c;
        }
        return null;
    }

    /// <summary>新增一行（插入当前页末尾并通知视图滚动定位）。</summary>
    public void AddRowForEdit(out QueryResultRow? newRow)
    {
        newRow = null;
        if (!IsResultEditable)
            return;

        var row = new QueryResultRow(Columns.ToList());
        row.MarkAsAdded();

        // 插入到当前页末尾对应的全量位置（保证刷新后仍在本页可见）。
        int insertIndex = Math.Min((CurrentPage - 1) * PageSize + Rows.Count, _allRows.Count);
        insertIndex = Math.Clamp(insertIndex, 0, _allRows.Count);
        _allRows.Insert(insertIndex, row);

        HasPendingChanges = true;
        RefreshPage();
        newRow = row;
    }

    /// <summary>删除指定行（新增行直接丢弃；已有行移入待删除列表）。</summary>
    public void RemoveRowForEdit(QueryResultRow? row)
    {
        if (!IsResultEditable || row is null)
            return;

        if (row.State == DataRowState.Added)
        {
            _allRows.Remove(row);
        }
        else
        {
            int idx = _allRows.IndexOf(row);
            if (idx >= 0)
            {
                _pendingDeletes.Add((row, idx));
                _allRows.Remove(row);
            }
        }

        RecalculatePendingChanges();
        RefreshPage();
    }

    /// <summary>还原全部内联编辑改动（恢复原始值/放回删除行/丢弃新增行）。</summary>
    public void RevertEdits()
    {
        // 1. 移除所有新增的行（完全还原）
        var addedRows = _allRows.Where(r => r.State == DataRowState.Added).ToList();
        foreach (var row in addedRows)
        {
            _allRows.Remove(row);
        }

        // 2. 放回已删除的行
        foreach (var (row, originalIndex) in _pendingDeletes.AsEnumerable().Reverse())
        {
            var restoreIndex = Math.Clamp(originalIndex, 0, _allRows.Count);
            _allRows.Insert(restoreIndex, row);
        }
        _pendingDeletes.Clear();

        // 3. 还原所有剩余行的原始值
        foreach (var row in _allRows)
        {
            row.RevertToOriginal();
        }

        RecalculatePendingChanges();
        RefreshPage();
        StatusMessage = "已还原全部改动。";
    }

    /// <summary>保存内联编辑改动（复用 IDataEditService.SaveChangesAsync，事务内提交）。</summary>
    [RelayCommand]
    public async Task SaveEditsAsync()
    {
        if (_editService is null || !IsResultEditable || !HasPendingChanges || IsSavingChanges)
            return;

        IsSavingChanges = true;
        try
        {
            var inserts = new List<DataEditRow>();
            var updates = new List<DataEditRow>();
            var deletes = new List<DataEditRow>();

            var tableColumns = _editableTableInfo!.Columns;

            // 新增行：取全部非只读列的当前值生成 INSERT。
            foreach (var r in _allRows.Where(r => r.State == DataRowState.Added))
            {
                var dataRow = new DataEditRow(tableColumns);
                foreach (var col in tableColumns.Where(c => !c.IsReadOnly))
                {
                    var value = r.GetValue(col.Name);
                    if (value is not null)
                    {
                        dataRow[col.Name] = value;
                    }
                }
                inserts.Add(dataRow);
            }

            // 修改行：先写入全部原始值作为 WHERE 基线并快照，再应用新值产生脏标记。
            // （两段写入，避免逐列快照把已写的新值误当作原始值。）
            foreach (var r in _allRows.Where(r => r.State == DataRowState.Modified))
            {
                var dataRow = new DataEditRow(tableColumns);

                for (int i = 0; i < tableColumns.Count; i++)
                {
                    var original = Normalize(r.GetOriginal(tableColumns[i].Name));
                    dataRow.SetCellValueDirect(i, original);
                }
                dataRow.MarkAsSaved();

                foreach (var col in tableColumns)
                {
                    var current = r.GetValue(col.Name);
                    var original = r.GetOriginal(col.Name);
                    if (!Equals(Normalize(original), Normalize(current)))
                    {
                        dataRow[col.Name] = current;
                    }
                }
                updates.Add(dataRow);
            }

            // 删除行：以原始值构造 WHERE 条件。
            foreach (var (r, _) in _pendingDeletes)
            {
                var dataRow = new DataEditRow(tableColumns);
                for (int i = 0; i < tableColumns.Count; i++)
                {
                    var original = Normalize(r.GetOriginal(tableColumns[i].Name));
                    if (original is not null)
                    {
                        dataRow.SetCellValueDirect(i, original);
                    }
                }
                dataRow.MarkAsSaved();
                dataRow.MarkAsDeleted();
                deletes.Add(dataRow);
            }

            if (inserts.Count == 0 && updates.Count == 0 && deletes.Count == 0)
            {
                StatusMessage = "没有需要保存的改动。";
                return;
            }

            StatusMessage = "正在保存改动...";
            var result = await _editService.SaveChangesAsync(
                ConnectionName,
                DatabaseName,
                _editableTableName!,
                _editableSchema,
                inserts,
                updates,
                deletes);

            if (!result.IsSuccess)
            {
                StatusMessage = $"保存失败：{result.ErrorMessage}";
                return;
            }

            // 保存成功：提交所有行状态，清空待删除列表。
            foreach (var (row, _) in _pendingDeletes)
            {
                row.MarkAsSaved();
            }
            _pendingDeletes.Clear();
            foreach (var row in _allRows)
            {
                row.MarkAsSaved();
            }

            RecalculatePendingChanges();
            RefreshPage();
            StatusMessage = $"保存成功，影响 {result.RowCount} 行。建议重新执行查询以获取最新数据（自增列等）。";
        }
        catch (Exception ex)
        {
            StatusMessage = $"保存失败：{ex.Message}";
        }
        finally
        {
            IsSavingChanges = false;
        }
    }

    private void RecalculatePendingChanges()
    {
        HasPendingChanges = _pendingDeletes.Count > 0
            || _allRows.Any(r => r.State == DataRowState.Added || r.State == DataRowState.Modified || r.State == DataRowState.Deleted);
    }

    /// <summary>查询结果的显示值（字符串化）转回存储值：空字符串视为 NULL。</summary>
    internal static object? Normalize(object? value)
        => value is string s && s.Length == 0 ? null : value;

    #endregion

    /// <summary>按当前页码刷新 Rows（当前页切片）与分页状态。</summary>
    private void RefreshPage()
    {
        var maxPage = Math.Max(1, TotalPages);
        var page = Math.Clamp(CurrentPage, 1, maxPage);
        if (page != CurrentPage)
        {
            CurrentPage = page; // 触发 OnCurrentPageChanged 再次进入（幂等）
            return;
        }

        Rows.Clear();
        if (_allRows.Count > 0)
        {
            var start = (page - 1) * PageSize;
            foreach (var row in _allRows.Skip(start).Take(PageSize))
            {
                Rows.Add(row);
            }
        }

        OnPropertyChanged(nameof(TotalRows));
        OnPropertyChanged(nameof(TotalPages));
        OnPropertyChanged(nameof(PageInfo));
        OnPropertyChanged(nameof(CanGoPrevPage));
        OnPropertyChanged(nameof(CanGoNextPage));
        PrevPageCommand.NotifyCanExecuteChanged();
        NextPageCommand.NotifyCanExecuteChanged();
        FirstPageCommand.NotifyCanExecuteChanged();
        LastPageCommand.NotifyCanExecuteChanged();
    }

    partial void OnPageSizeChanged(int value) => RefreshPage();

    partial void OnCurrentPageChanged(int value) => RefreshPage();

    [RelayCommand(CanExecute = nameof(CanGoPrevPage))]
    private void FirstPage() => GoToPage(1);

    [RelayCommand(CanExecute = nameof(CanGoPrevPage))]
    private void PrevPage() => GoToPage(CurrentPage - 1);

    [RelayCommand(CanExecute = nameof(CanGoNextPage))]
    private void NextPage() => GoToPage(CurrentPage + 1);

    [RelayCommand(CanExecute = nameof(CanGoNextPage))]
    private void LastPage() => GoToPage(TotalPages);

    /// <summary>跳转到指定页（自动夹取到有效范围）。</summary>
    public void GoToPage(int page)
    {
        var target = Math.Clamp(page, 1, Math.Max(1, TotalPages));
        CurrentPage = target;
        RefreshPage();
    }

    /// <summary>清除结果集。</summary>
    public void ClearResults()
    {
        Columns.Clear();
        _allRows.Clear();
        Rows.Clear();
        ResetEditingState();
        CurrentPage = 1;
        RefreshPage();
        HasResult = false;
        ShowNoResult = true;
    }
}
