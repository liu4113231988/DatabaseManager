using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Interactivity;
using DatabaseManager.AppCore.Models;
using DatabaseManager.AppCore.ViewModels;

namespace DatabaseManager.Avalonia.Views;

/// <summary>
/// 依赖分析窗口（阶段 4）。对应原 WinForms <c>frmDbObjectDependency</c> / <c>frmTableDependency</c>。
/// 指定数据库对象并分析其依赖关系。
/// </summary>
public partial class DependencyWindow : Window
{
    private readonly DependencyViewModel? _vm;

    public DependencyWindow()
    {
        InitializeComponent();
    }

    public DependencyWindow(DependencyViewModel vm) : this()
    {
        DataContext = vm;
        _vm = vm;
    }

    protected override void OnOpened(EventArgs e)
    {
        if (_vm is null) return;
        base.OnOpened(e);

        ComboConnection.SelectionChanged += ComboConnection_SelectionChanged;
        ComboObjectType.SelectionChanged += ComboObjectType_SelectionChanged;
        ComboDirection.SelectionChanged += ComboDirection_SelectionChanged;

        Refresh();
    }

    protected override void OnClosed(EventArgs e)
    {
        if (_vm is null) return;
        base.OnClosed(e);

        ComboConnection.SelectionChanged -= ComboConnection_SelectionChanged;
        ComboObjectType.SelectionChanged -= ComboObjectType_SelectionChanged;
        ComboDirection.SelectionChanged -= ComboDirection_SelectionChanged;
    }

    private void Refresh()
    {
        if (_vm is null) return;
        _vm.RefreshConnections();
        LoadConnections();
        LoadObjectTypes();
        LoadDirections();
    }

    private void LoadConnections()
    {
        if (_vm is null) return;
        ComboConnection.ItemsSource = _vm.Connections;
        ComboConnection.ItemTemplate = new FuncDataTemplate<ConnectionItem>((item, _) =>
            new TextBlock { Text = item.Description });

        ComboConnection.SelectedItem = _vm.SelectedConnection;
    }

    private void LoadObjectTypes()
    {
        if (_vm is null) return;
        ComboObjectType.ItemsSource = _vm.ObjectTypes;
        ComboObjectType.SelectedItem = _vm.SelectedObjectType;
    }

    private void LoadDirections()
    {
        if (_vm is null) return;
        ComboDirection.ItemsSource = _vm.Directions;
        ComboDirection.ItemTemplate = new FuncDataTemplate<DependencyDirectionOption>((item, _) =>
            new TextBlock { Text = item.DisplayName });

        ComboDirection.SelectedItem = _vm.SelectedDirection;
    }

    private void ComboConnection_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        => _vm.SelectedConnection = ComboConnection.SelectedItem as ConnectionItem;

    private void ComboObjectType_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        => _vm.SelectedObjectType = ComboObjectType.SelectedItem as string ?? "Table";

    private void ComboDirection_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        => _vm.SelectedDirection = ComboDirection.SelectedItem as DependencyDirectionOption;

    private void BtnRefresh_Click(object? sender, RoutedEventArgs e)
    {
        Refresh();
    }

    private void BtnClose_Click(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
