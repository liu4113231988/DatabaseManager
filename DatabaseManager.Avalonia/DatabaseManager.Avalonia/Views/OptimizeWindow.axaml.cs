using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Interactivity;
using DatabaseManager.AppCore.Models;
using DatabaseManager.AppCore.ViewModels;

namespace DatabaseManager.Avalonia.Views;

/// <summary>
/// 数据库优化窗口（阶段 4）。对应原 WinForms <c>frmOpitimizeResult</c>。
/// 对单个数据库执行优化，展示优化前后数据长度。
/// </summary>
public partial class OptimizeWindow : Window
{
    private readonly OptimizeViewModel? _vm;

    public OptimizeWindow()
    {
        InitializeComponent();
    }

    public OptimizeWindow(OptimizeViewModel vm) : this()
    {
        DataContext = vm;
        _vm = vm;
    }

    protected override void OnOpened(EventArgs e)
    {
        if (_vm is null) return;
        base.OnOpened(e);
        ComboConnection.SelectionChanged += ComboConnection_SelectionChanged;
        Refresh();
    }

    protected override void OnClosed(EventArgs e)
    {
        if (_vm is null) return;
        base.OnClosed(e);
        ComboConnection.SelectionChanged -= ComboConnection_SelectionChanged;
    }

    private void Refresh()
    {
        if (_vm is null) return;
        _vm.RefreshConnections();
        LoadConnections();
    }

    private void LoadConnections()
    {
        if (_vm is null) return;
        ComboConnection.ItemsSource = _vm.Connections;
        ComboConnection.ItemTemplate = new FuncDataTemplate<ConnectionItem>((item, _) =>
            new TextBlock { Text = item.Description });

        ComboConnection.SelectedItem = _vm.SelectedConnection;
    }

    private void ComboConnection_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        => _vm.SelectedConnection = ComboConnection.SelectedItem as ConnectionItem;

    private void BtnRefresh_Click(object? sender, RoutedEventArgs e)
    {
        Refresh();
    }

    private void BtnClose_Click(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
