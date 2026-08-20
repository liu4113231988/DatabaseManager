using Avalonia.Controls;
using Avalonia.Interactivity;
using DatabaseManager.AppCore.Models;
using DatabaseManager.AppCore.ViewModels;

namespace DatabaseManager.Avalonia.Views;

/// <summary>
/// 数据库转换窗口（阶段 4）。对应原 WinForms <c>frmConvert</c>。
/// 选择源/目标连接与转换模式，执行跨库结构/数据转换并展示反馈日志。
/// </summary>
public partial class ConvertWindow : Window
{
    private readonly ConvertViewModel _vm;

    public ConvertWindow(ConvertViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
        _vm = vm;
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);

        ComboSource.SelectionChanged += ComboSource_SelectionChanged;
        ComboTarget.SelectionChanged += ComboTarget_SelectionChanged;
        ComboMode.SelectionChanged += ComboMode_SelectionChanged;

        Refresh();
    }

    private void Refresh()
    {
        _vm.RefreshConnections();
        LoadConnections();
        LoadModes();
    }

    private void LoadConnections()
    {
        ComboSource.ItemsSource = _vm.Connections;
        ComboSource.ItemTemplate = new FuncDataTemplate<ConnectionItem>((item, _) =>
            new TextBlock { Text = item.Description });

        ComboTarget.ItemsSource = _vm.Connections;
        ComboTarget.ItemTemplate = new FuncDataTemplate<ConnectionItem>((item, _) =>
            new TextBlock { Text = item.Description });

        // 同步 VM 当前选中的连接。
        ComboSource.SelectedItem = _vm.SourceConnection;
        ComboTarget.SelectedItem = _vm.TargetConnection;
    }

    private void LoadModes()
    {
        ComboMode.ItemsSource = _vm.Modes;
        ComboMode.ItemTemplate = new FuncDataTemplate<ConvertModeOption>((item, _) =>
            new TextBlock { Text = item.DisplayName });

        ComboMode.SelectedItem = _vm.SelectedMode;
    }

    private void ComboSource_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        => _vm.SourceConnection = ComboSource.SelectedItem as ConnectionItem;

    private void ComboTarget_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        => _vm.TargetConnection = ComboTarget.SelectedItem as ConnectionItem;

    private void ComboMode_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        => _vm.SelectedMode = ComboMode.SelectedItem as ConvertModeOption;

    private void BtnRefresh_Click(object? sender, RoutedEventArgs e)
    {
        Refresh();
    }

    private void BtnClose_Click(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
