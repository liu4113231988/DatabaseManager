using Avalonia.Controls;
using Avalonia.Interactivity;
using DatabaseManager.AppCore.ViewModels;

namespace DatabaseManager.Avalonia.Views;

/// <summary>查询历史窗口：浏览历史执行记录并插入到编辑器。</summary>
public partial class QueryHistoryWindow : Window
{
    private readonly QueryHistoryViewModel? _vm;

    public QueryHistoryWindow()
    {
        InitializeComponent();
    }

    public QueryHistoryWindow(QueryHistoryViewModel vm) : this()
    {
        DataContext = vm;
        _vm = vm;
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        _vm?.RefreshCommand.Execute(null);
    }

    private void BtnClose_Click(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
