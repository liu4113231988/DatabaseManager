using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DatabaseManager.AppCore.Common;
using DatabaseManager.AppCore.Models;
using DatabaseManager.AppCore.Services;

namespace DatabaseManager.AppCore.ViewModels;

/// <summary>
/// 元数据搜索 ViewModel（P0：对应 DBeaver 的 DB Metadata Search / Open Database Object）。
/// 按关键字模糊搜索表/视图/列/存储过程/函数/序列，结果可请求「在对象树中定位」或「生成 SELECT」。
/// </summary>
public partial class SearchViewModel : ViewModelBase
{
    private readonly IDbSchemaService _schemaService;

    public SearchViewModel(IDbSchemaService schemaService)
    {
        _schemaService = schemaService;
    }

    /// <summary>可选连接列表（仅已活动的连接，由主窗口注入）。</summary>
    public ObservableCollection<string> Connections { get; } = new();

    [ObservableProperty]
    private string? _selectedConnectionName;

    [ObservableProperty]
    private string _keyword = string.Empty;

    /// <summary>结果集合。</summary>
    public ObservableCollection<SearchResultItem> Results { get; } = new();

    [ObservableProperty]
    private SearchResultItem? _selectedResult;

    [ObservableProperty]
    private bool _isSearching;

    [ObservableProperty]
    private string _statusMessage = "输入关键字后回车或点击「搜索」。";

    /// <summary>搜索是否可用（有活动连接且不在搜索中）。</summary>
    public bool CanSearch => !IsSearching && Connections.Count > 0;

    /// <summary>是否有选中的结果（「在树中定位」按钮可用性）。</summary>
    public bool HasSelected => SelectedResult is not null;

    /// <summary>选中结果是否为表/视图（「生成 SELECT」按钮可用性）。</summary>
    public bool HasSelectedTableOrView =>
        SelectedResult is { Kind: SearchObjectKind.Table or SearchObjectKind.View };

    partial void OnSelectedResultChanged(SearchResultItem? value)
    {
        OnPropertyChanged(nameof(HasSelected));
        OnPropertyChanged(nameof(HasSelectedTableOrView));
    }

    partial void OnIsSearchingChanged(bool value)
    {
        OnPropertyChanged(nameof(CanSearch));
    }

    /// <summary>打开窗口时由主窗口注入活动连接列表与默认连接。</summary>
    public void SetConnections(IEnumerable<string> activeConnectionNames, string defaultConnectionName)
    {
        Connections.Clear();

        foreach (var name in activeConnectionNames)
        {
            Connections.Add(name);
        }

        SelectedConnectionName = !string.IsNullOrEmpty(defaultConnectionName) && Connections.Contains(defaultConnectionName)
            ? defaultConnectionName
            : Connections.FirstOrDefault();
    }

    /// <summary>执行元数据搜索。</summary>
    [RelayCommand]
    public async Task SearchAsync()
    {
        Results.Clear();
        SelectedResult = null;

        var keyword = Keyword?.Trim() ?? string.Empty;
        if (keyword.Length == 0)
        {
            StatusMessage = "请输入搜索关键字。";
            return;
        }

        if (string.IsNullOrWhiteSpace(SelectedConnectionName))
        {
            StatusMessage = "请先在对象浏览器中连接一个连接。";
            return;
        }

        IsSearching = true;
        StatusMessage = $"正在搜索「{keyword}」...";

        try
        {
            var items = await _schemaService.SearchMetadataAsync(SelectedConnectionName, keyword);

            foreach (var item in items)
            {
                Results.Add(item);
            }

            StatusMessage = Results.Count == 0
                ? $"未找到与「{keyword}」匹配的对象。"
                : $"搜索完成，共 {Results.Count} 条。双击结果可在对象树中定位；表/视图可直接生成 SELECT。";
        }
        catch (Exception ex)
        {
            StatusMessage = $"搜索失败：{ex.Message}";
        }
        finally
        {
            IsSearching = false;
        }
    }
}
