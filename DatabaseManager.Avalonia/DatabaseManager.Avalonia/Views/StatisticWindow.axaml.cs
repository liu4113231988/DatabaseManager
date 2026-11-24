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
    private readonly StatisticViewModel? _vm;

    public StatisticWindow()
    {
        InitializeComponent();
    }

    public StatisticWindow(StatisticViewModel vm) : this()
    {
        DataContext = vm;
        _vm = vm;
    }

    protected override async void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        if (_vm is null) return;

        ComboConnection.SelectionChanged += ComboConnection_SelectionChanged;
        ComboStatisticType.SelectionChanged += ComboStatisticType_SelectionChanged;

        await RefreshAsync();
    }

    protected override void OnClosed(EventArgs e)
    {
        _vm?.CancelConnectionRefresh();
        base.OnClosed(e);
        if (_vm is null) return;

        ComboConnection.SelectionChanged -= ComboConnection_SelectionChanged;
        ComboStatisticType.SelectionChanged -= ComboStatisticType_SelectionChanged;
    }

    private async Task RefreshAsync()
    {
        if (_vm is null || !await _vm.RefreshConnectionsAsync() || !IsVisible)
            return;

        LoadConnections();
        LoadStatisticTypes();
    }

    private void LoadConnections()
    {
        if (_vm is null) return;
        ComboConnection.ItemsSource = _vm.Connections;
        ComboConnection.ItemTemplate = new FuncDataTemplate<ConnectionItem>((item, _) =>
            new TextBlock { Text = item?.Description ?? string.Empty });

        ComboConnection.SelectedItem = _vm.SelectedConnection;
    }

    private void LoadStatisticTypes()
    {
        if (_vm is null) return;
        ComboStatisticType.ItemsSource = _vm.StatisticTypes;
        ComboStatisticType.ItemTemplate = new FuncDataTemplate<StatisticTypeOption>((item, _) =>
            new TextBlock { Text = item?.DisplayName ?? string.Empty });

        ComboStatisticType.SelectedItem = _vm.SelectedStatisticType;
    }

    private void ComboConnection_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_vm is null) return;
        _vm.SelectedConnection = ComboConnection.SelectedItem as ConnectionItem;
    }

    private void ComboStatisticType_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_vm is null) return;
        _vm.SelectedStatisticType = ComboStatisticType.SelectedItem as StatisticTypeOption;
    }

    private async void BtnRefresh_Click(object? sender, RoutedEventArgs e)
    {
        await RefreshAsync();
    }

    private void BtnClose_Click(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
