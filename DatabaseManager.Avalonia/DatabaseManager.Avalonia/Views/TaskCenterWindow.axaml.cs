using Avalonia.Controls;
using Avalonia.Interactivity;
using DatabaseManager.AppCore.ViewModels;

namespace DatabaseManager.Avalonia.Views;

/// <summary>任务中心窗口：查看/取消本会话后台任务与跨会话历史。</summary>
public partial class TaskCenterWindow : Window
{
    private readonly TaskCenterViewModel? _vm;

    public TaskCenterWindow()
    {
        InitializeComponent();
    }

    public TaskCenterWindow(TaskCenterViewModel vm) : this()
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
