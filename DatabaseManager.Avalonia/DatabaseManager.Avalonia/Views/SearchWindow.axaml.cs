using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using DatabaseManager.AppCore.Models;
using DatabaseManager.AppCore.ViewModels;

namespace DatabaseManager.Avalonia.Views;

/// <summary>
/// 元数据搜索窗口（对应 DBeaver 的 DB Metadata Search / Open Database Object）。
/// 对话框结果模式：双击结果行或「在树中定位」→ 关闭后由主窗口在对象树中定位；
/// 「生成 SELECT」→ 关闭后由主窗口定位并填充查询标签。
/// </summary>
public partial class SearchWindow : Window
{
    private readonly SearchViewModel _vm;

    /// <summary>选中的结果项（关闭对话框后由主窗口读取处理）。</summary>
    public SearchResultItem? SelectedItemResult { get; private set; }

    /// <summary>是否请求为选中结果生成 SELECT（仅表/视图有效）。</summary>
    public bool GenerateSelectRequested { get; private set; }

    public SearchWindow(SearchViewModel vm)
    {
        InitializeComponent();

        _vm = vm;
        DataContext = _vm;

        // 打开时聚焦关键字输入框，便于直接输入搜索
        Opened += (_, _) => TxtKeyword.Focus();
    }

    /// <summary>关键字输入回车触发搜索。</summary>
    private void TxtKeyword_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            _ = _vm.SearchAsync();
        }
    }

    /// <summary>双击结果行 = 在树中定位。</summary>
    private void ResultsGrid_DoubleTapped(object? sender, TappedEventArgs e)
    {
        CloseWithResult(generateSelect: false);
    }

    private void BtnLocate_Click(object? sender, RoutedEventArgs e)
    {
        CloseWithResult(generateSelect: false);
    }

    private void BtnGenerateSelect_Click(object? sender, RoutedEventArgs e)
    {
        CloseWithResult(generateSelect: true);
    }

    /// <summary>记录结果并关闭对话框（定位/查询由主窗口继续处理）。</summary>
    private void CloseWithResult(bool generateSelect)
    {
        if (_vm.SelectedResult is null)
            return;

        SelectedItemResult = _vm.SelectedResult;
        GenerateSelectRequested =
            generateSelect &&
            SelectedItemResult.Kind is SearchObjectKind.Table or SearchObjectKind.View;

        Close();
    }
}
