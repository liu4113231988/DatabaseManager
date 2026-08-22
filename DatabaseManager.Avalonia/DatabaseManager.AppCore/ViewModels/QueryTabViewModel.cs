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
/// </summary>
public partial class QueryTabViewModel : ViewModelBase
{
    private readonly IQueryService _queryService;
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

    [ObservableProperty]
    private bool _isModified;

    /// <summary>原始标题（不含修改标记）。</summary>
    private string _baseTitle = "查询";

    /// <summary>是否正在保存中（防止保存时触发修改标记）。</summary>
    private bool _isSaving;

    private readonly List<RowData> _allRows = new();

    /// <summary>查询结果列名。</summary>
    public ObservableCollection<string> Columns { get; } = new();

    /// <summary>当前页的行数据（绑定到结果表格；全量数据在 _allRows 中分页切片）。</summary>
    public ObservableCollection<RowData> Rows { get; } = new();

    [ObservableProperty]
    private int _pageSize = 50;

    [ObservableProperty]
    private int _currentPage = 1;

    /// <summary>每页大小可选项（供下拉选择）。</summary>
    public int[] PageSizeOptions { get; } = { 50, 100, 200, 500, 1000 };

    /// <summary>总行数。</summary>
    public int TotalRows => _allRows.Count;

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

    public QueryTabViewModel(IQueryService queryService, string? title = null)
    {
        _queryService = queryService;
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
            _allRows.Add(new RowData(row));
        }

        StatusMessage = $"查询完成，返回 {result.RowCount} 行，耗时 {result.ElapsedMilliseconds} ms。";
        HasResult = true;
        ShowNoResult = false;

        // 新结果集回到第一页并切片显示
        CurrentPage = 1;
        RefreshPage();
    }

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
        CurrentPage = 1;
        RefreshPage();
        HasResult = false;
        ShowNoResult = true;
    }
}
