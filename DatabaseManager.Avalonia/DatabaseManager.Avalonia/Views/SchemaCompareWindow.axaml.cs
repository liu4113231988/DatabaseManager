using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Interactivity;
using DatabaseManager.AppCore.Models;
using DatabaseManager.AppCore.ViewModels;

namespace DatabaseManager.Avalonia.Views;

/// <summary>
/// 结构对比窗口（阶段 4）。对应原 WinForms <c>frmSchemaCompare</c>。
/// 选择源/目标连接与对象类型，对比两个同类型库的结构差异并展示差异树。
/// </summary>
public partial class SchemaCompareWindow : Window
{
    private readonly SchemaCompareViewModel? _vm;

    public SchemaCompareWindow()
    {
        InitializeComponent();
    }

    public SchemaCompareWindow(SchemaCompareViewModel vm) : this()
    {
        DataContext = vm;
        _vm = vm;
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        if (_vm is null) return;

        ComboSource.SelectionChanged += ComboSource_SelectionChanged;
        ComboTarget.SelectionChanged += ComboTarget_SelectionChanged;
        ComboObjectType.SelectionChanged += ComboObjectType_SelectionChanged;

        Refresh();
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        if (_vm is null) return;
        ComboSource.SelectionChanged -= ComboSource_SelectionChanged;
        ComboTarget.SelectionChanged -= ComboTarget_SelectionChanged;
        ComboObjectType.SelectionChanged -= ComboObjectType_SelectionChanged;
    }

    private void Refresh()
    {
        if (_vm is null) return;
        _vm.RefreshConnections();
        LoadConnections();
        LoadObjectTypes();
    }

    private void LoadConnections()
    {
        if (_vm is null) return;
        ComboSource.ItemsSource = _vm.Connections;
        ComboSource.ItemTemplate = new FuncDataTemplate<ConnectionItem>((item, _) =>
            new TextBlock { Text = item.Description });

        ComboTarget.ItemsSource = _vm.Connections;
        ComboTarget.ItemTemplate = new FuncDataTemplate<ConnectionItem>((item, _) =>
            new TextBlock { Text = item.Description });

        ComboSource.SelectedItem = _vm.SourceConnection;
        ComboTarget.SelectedItem = _vm.TargetConnection;
    }

    private void LoadObjectTypes()
    {
        if (_vm is null) return;
        ComboObjectType.ItemsSource = _vm.ObjectTypes;
        ComboObjectType.ItemTemplate = new FuncDataTemplate<ObjectTypeOption>((item, _) =>
            new TextBlock { Text = item.DisplayName });

        ComboObjectType.SelectedItem = _vm.SelectedObjectType;
    }

    private void ComboSource_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        => _vm.SourceConnection = ComboSource.SelectedItem as ConnectionItem;

    private void ComboTarget_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        => _vm.TargetConnection = ComboTarget.SelectedItem as ConnectionItem;

    private void ComboObjectType_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        => _vm.SelectedObjectType = ComboObjectType.SelectedItem as ObjectTypeOption;

    private void BtnRefresh_Click(object? sender, RoutedEventArgs e)
    {
        Refresh();
    }

    private void BtnClose_Click(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
