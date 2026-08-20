using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Interactivity;
using DatabaseManager.AppCore.Models;
using DatabaseManager.AppCore.ViewModels;

namespace DatabaseManager.Avalonia.Views;

/// <summary>
/// 数据库统计窗口（阶段 5）。对应原 WinForms <c>frmStatistic</c>。
/// 选择连接与统计类型，统计表记录数或列内容最大长度。
/// </summary>
public partial class StatisticWindow : Window
{
    private readonly StatisticViewModel _vm;

    public StatisticWindow(StatisticViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
        _vm = vm;
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);

        ComboConnection.SelectionChanged += ComboConnection_SelectionChanged;
        ComboStatisticType.SelectionChanged += ComboStatisticType_SelectionChanged;

        Refresh();
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);

        ComboConnection.SelectionChanged -= ComboConnection_SelectionChanged;
        ComboStatisticType.SelectionChanged -= ComboStatisticType_SelectionChanged;
    }

    private void Refresh()
    {
        _vm.RefreshConnections();
        LoadConnections();
        LoadStatisticTypes();
    }

    private void LoadConnections()
    {
        ComboConnection.ItemsSource = _vm.Connections;
        ComboConnection.ItemTemplate = new FuncDataTemplate<ConnectionItem>((item, _) =>
            new TextBlock { Text = item.Description });

        ComboConnection.SelectedItem = _vm.SelectedConnection;
    }

    private void LoadStatisticTypes()
    {
        ComboStatisticType.ItemsSource = _vm.StatisticTypes;
        ComboStatisticType.ItemTemplate = new FuncDataTemplate<StatisticTypeOption>((item, _) =>
            new TextBlock { Text = item.DisplayName });

        ComboStatisticType.SelectedItem = _vm.SelectedStatisticType;
    }

    private void ComboConnection_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        => _vm.SelectedConnection = ComboConnection.SelectedItem as ConnectionItem;

    private void ComboStatisticType_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        => _vm.SelectedStatisticType = ComboStatisticType.SelectedItem as StatisticTypeOption;

    private void BtnRefresh_Click(object? sender, RoutedEventArgs e)
    {
        Refresh();
    }

    private void BtnClose_Click(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
