using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Interactivity;
using DatabaseManager.AppCore.Models;
using DatabaseManager.AppCore.ViewModels;

namespace DatabaseManager.Avalonia.Views;

/// <summary>
/// 索引碎片分析窗口（阶段 5）。对应原 WinForms <c>Analysis/frmIndexFragmentation</c>。
/// 分析索引碎片并支持重建选中索引。
/// </summary>
public partial class IndexFragmentationWindow : Window
{
    private readonly IndexFragmentationViewModel? _vm;

    public IndexFragmentationWindow()
    {
        InitializeComponent();
    }

    public IndexFragmentationWindow(IndexFragmentationViewModel vm) : this()
    {
        DataContext = vm;
        _vm = vm;
    }

    protected override async void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        if (_vm is null) return;
        ComboConnection.SelectionChanged += ComboConnection_SelectionChanged;
        await RefreshAsync();
    }

    protected override void OnClosed(EventArgs e)
    {
        _vm?.CancelConnectionRefresh();
        base.OnClosed(e);
        if (_vm is null) return;
        ComboConnection.SelectionChanged -= ComboConnection_SelectionChanged;
    }

    private async Task RefreshAsync()
    {
        if (_vm is null || !await _vm.RefreshConnectionsAsync() || !IsVisible)
            return;

        LoadConnections();
    }

    private void LoadConnections()
    {
        if (_vm is null) return;
        ComboConnection.ItemsSource = _vm.Connections;
        ComboConnection.ItemTemplate = new FuncDataTemplate<ConnectionItem>((item, _) =>
            new TextBlock { Text = item?.Description ?? string.Empty });

        ComboConnection.SelectedItem = _vm.SelectedConnection;
    }

    private void ComboConnection_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_vm is null) return;
        _vm.SelectedConnection = ComboConnection.SelectedItem as ConnectionItem;
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
