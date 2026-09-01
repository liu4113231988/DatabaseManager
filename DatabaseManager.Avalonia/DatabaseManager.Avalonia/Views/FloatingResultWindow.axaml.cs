using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using DatabaseManager.Avalonia.Controls;
using DatabaseManager.AppCore.ViewModels;

namespace DatabaseManager.Avalonia.Views;

/// <summary>
/// 浮动查询结果窗口：绑定同一个 QueryTabViewModel（行/筛选/分页实时同步），
/// 关闭时回调恢复主窗口停靠的结果区。
/// </summary>
public partial class FloatingResultWindow : Window
{
    private readonly QueryTabViewModel _tab;
    private readonly Action _onDockBack;

    public FloatingResultWindow(QueryTabViewModel tab, Action onDockBack)
    {
        InitializeComponent();

        _tab = tab;
        _onDockBack = onDockBack;

        DataContext = tab;
        Title = $"查询结果（浮动）- {tab.Title}";
        Closed += (_, _) => _onDockBack();
    }

    private void FloatingGrid_AttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        if (sender is DataGrid grid)
        {
            ResultGridHelper.RebuildColumns(grid, _tab);
        }
    }

    private void DockBack_Click(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
