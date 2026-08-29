using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DatabaseManager.AppCore.Common;
using DatabaseManager.AppCore.Services;

namespace DatabaseManager.AppCore.ViewModels;

/// <summary>
/// 查询历史 ViewModel：浏览/搜索历史执行记录，复制或插入到当前编辑器。
/// </summary>
public partial class QueryHistoryViewModel : ViewModelBase
{
    private readonly IQueryHistoryService _historyService;

    public ObservableCollection<QueryHistoryEntry> Entries { get; } = new();

    [ObservableProperty]
    private QueryHistoryEntry? _selectedEntry;

    /// <summary>按连接名或 SQL 内容过滤。</summary>
    [ObservableProperty]
    private string _filterText = string.Empty;

    /// <summary>由窗口注入：把 SQL 插入到当前编辑器。</summary>
    public Action<string>? InsertToEditorRequested { get; set; }

    /// <summary>当前选中记录的 SQL 全文。</summary>
    public string SelectedSqlText => SelectedEntry?.SqlText ?? string.Empty;

    public QueryHistoryViewModel(IQueryHistoryService historyService)
    {
        _historyService = historyService;
    }

    partial void OnSelectedEntryChanged(QueryHistoryEntry? value)
        => OnPropertyChanged(nameof(SelectedSqlText));

    [RelayCommand]
    private void Refresh()
    {
        Entries.Clear();

        var filter = FilterText?.Trim() ?? string.Empty;
        var items = _historyService.GetRecent(500);

        foreach (var entry in items)
        {
            if (filter.Length > 0
                && !(entry.ConnectionName?.Contains(filter, StringComparison.OrdinalIgnoreCase) ?? false)
                && !(entry.SqlText?.Contains(filter, StringComparison.OrdinalIgnoreCase) ?? false))
            {
                continue;
            }

            Entries.Add(entry);
        }
    }

    [RelayCommand]
    private void Clear()
    {
        _historyService.Clear();
        Refresh();
    }

    [RelayCommand]
    private void InsertToEditor()
    {
        if (SelectedEntry is not null && InsertToEditorRequested is not null)
        {
            InsertToEditorRequested(SelectedEntry.SqlText);
        }
    }
}
