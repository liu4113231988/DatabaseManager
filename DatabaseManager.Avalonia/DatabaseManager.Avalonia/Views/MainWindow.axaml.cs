using Avalonia.Controls;
using DatabaseManager.AppCore.ViewModels;

namespace DatabaseManager.Avalonia.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);

        // 阶段 0：初始化 AppCore 视图模型（枚举受支持数据库、统计连接）
        if (DataContext is MainWindowViewModel vm)
        {
            vm.Initialize();
        }
    }
}
