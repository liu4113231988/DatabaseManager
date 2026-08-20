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
    private readonly IndexFragmentationViewModel _vm;

    public IndexFragmentationWindow(IndexFragmentationViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
        _vm = vm;
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        ComboConnection.SelectionChanged += ComboConnection_SelectionChanged;
        Refresh();
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        ComboConnection.SelectionChanged -= ComboConnection_SelectionChanged;
    }

    private void Refresh()
    {
        _vm.RefreshConnections();
        LoadConnections();
    }

    private void LoadConnections()
    {
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
