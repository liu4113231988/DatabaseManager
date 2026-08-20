using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Interactivity;
using DatabaseManager.AppCore.Models;
using DatabaseManager.AppCore.ViewModels;

namespace DatabaseManager.Avalonia.Views;

/// <summary>
/// 数据库诊断窗口（阶段 4）。对应原 WinForms <c>frmDiagnose</c>。
/// 对单个数据库执行表 / 脚本诊断，展示检出结果与日志。
/// </summary>
public partial class DiagnoseWindow : Window
{
    private readonly DiagnoseViewModel _vm;

    public DiagnoseWindow(DiagnoseViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
        _vm = vm;
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);

        ComboConnection.SelectionChanged += ComboConnection_SelectionChanged;
        ComboDiagnoseType.SelectionChanged += ComboDiagnoseType_SelectionChanged;

        Refresh();
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        ComboConnection.SelectionChanged -= ComboConnection_SelectionChanged;
        ComboDiagnoseType.SelectionChanged -= ComboDiagnoseType_SelectionChanged;
    }

    private void Refresh()
    {
        _vm.RefreshConnections();
        LoadConnections();
        LoadDiagnoseTypes();
    }

    private void LoadConnections()
    {
        ComboConnection.ItemsSource = _vm.Connections;
        ComboConnection.ItemTemplate = new FuncDataTemplate<ConnectionItem>((item, _) =>
            new TextBlock { Text = item.Description });

        ComboConnection.SelectedItem = _vm.SelectedConnection;
    }

    private void LoadDiagnoseTypes()
    {
        ComboDiagnoseType.ItemsSource = _vm.DiagnoseTypes;
        ComboDiagnoseType.ItemTemplate = new FuncDataTemplate<DiagnoseTypeOption>((item, _) =>
            new TextBlock { Text = item.DisplayName });

        ComboDiagnoseType.SelectedItem = _vm.SelectedDiagnoseType;
    }

    private void ComboConnection_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        => _vm.SelectedConnection = ComboConnection.SelectedItem as ConnectionItem;

    private void ComboDiagnoseType_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        => _vm.SelectedDiagnoseType = ComboDiagnoseType.SelectedItem as DiagnoseTypeOption;

    private void BtnRefresh_Click(object? sender, RoutedEventArgs e)
    {
        Refresh();
    }

    private void BtnClose_Click(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
