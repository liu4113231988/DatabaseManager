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
    private readonly DiagnoseViewModel? _vm;

    public DiagnoseWindow()
    {
        InitializeComponent();
    }

    public DiagnoseWindow(DiagnoseViewModel vm) : this()
    {
        DataContext = vm;
        _vm = vm;
    }

    protected override async void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        if (_vm is null) return;

        ComboConnection.SelectionChanged += ComboConnection_SelectionChanged;
        ComboDiagnoseType.SelectionChanged += ComboDiagnoseType_SelectionChanged;

        await RefreshAsync();
    }

    protected override void OnClosed(EventArgs e)
    {
        _vm?.CancelConnectionRefresh();
        base.OnClosed(e);
        if (_vm is null) return;
        ComboConnection.SelectionChanged -= ComboConnection_SelectionChanged;
        ComboDiagnoseType.SelectionChanged -= ComboDiagnoseType_SelectionChanged;
    }

    private async Task RefreshAsync()
    {
        if (_vm is null || !await _vm.RefreshConnectionsAsync() || !IsVisible)
            return;

        LoadConnections();
        LoadDiagnoseTypes();
    }

    private void LoadConnections()
    {
        if (_vm is null) return;
        ComboConnection.ItemsSource = _vm.Connections;
        ComboConnection.ItemTemplate = new FuncDataTemplate<ConnectionItem>((item, _) =>
            new TextBlock { Text = item?.Description ?? string.Empty });

        ComboConnection.SelectedItem = _vm.SelectedConnection;
    }

    private void LoadDiagnoseTypes()
    {
        if (_vm is null) return;
        ComboDiagnoseType.ItemsSource = _vm.DiagnoseTypes;
        ComboDiagnoseType.ItemTemplate = new FuncDataTemplate<DiagnoseTypeOption>((item, _) =>
            new TextBlock { Text = item?.DisplayName ?? string.Empty });

        ComboDiagnoseType.SelectedItem = _vm.SelectedDiagnoseType;
    }

    private void ComboConnection_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_vm is null) return;
        _vm.SelectedConnection = ComboConnection.SelectedItem as ConnectionItem;
    }

    private void ComboDiagnoseType_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_vm is null) return;
        _vm.SelectedDiagnoseType = ComboDiagnoseType.SelectedItem as DiagnoseTypeOption;
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
