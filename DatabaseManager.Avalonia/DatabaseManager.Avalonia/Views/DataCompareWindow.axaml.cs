using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Interactivity;
using DatabaseManager.AppCore.Models;
using DatabaseManager.AppCore.ViewModels;

namespace DatabaseManager.Avalonia.Views;

/// <summary>
/// 数据对比窗口（阶段 4）。对应原 WinForms <c>frmDataCompare</c>。
/// 对比两个同类型数据库的表数据差异，查看明细并生成同步脚本。
/// </summary>
public partial class DataCompareWindow : Window
{
    private readonly DataCompareViewModel? _vm;

    public DataCompareWindow()
    {
        InitializeComponent();
    }

    public DataCompareWindow(DataCompareViewModel vm) : this()
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
        ComboMode.SelectionChanged += ComboMode_SelectionChanged;
        _vm.PropertyChanged += Vm_PropertyChanged;

        Refresh();
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        if (_vm is null) return;
        _vm.PropertyChanged -= Vm_PropertyChanged;
    }

    private void Refresh()
    {
        if (_vm is null) return;
        _vm.RefreshConnections();
        LoadConnections();
        LoadModes();
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

    private void LoadModes()
    {
        if (_vm is null) return;
        ComboMode.ItemsSource = _vm.Modes;
        ComboMode.ItemTemplate = new FuncDataTemplate<DataCompareModeOption>((item, _) =>
            new TextBlock { Text = item.DisplayName });

        ComboMode.SelectedItem = _vm.SelectedMode;
    }

    private void ComboSource_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        => _vm.SourceConnection = ComboSource.SelectedItem as ConnectionItem;

    private void ComboTarget_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        => _vm.TargetConnection = ComboTarget.SelectedItem as ConnectionItem;

    private void ComboMode_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        => _vm.SelectedMode = ComboMode.SelectedItem as DataCompareModeOption;

    private void Vm_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        // 明细列变化时，动态重建 DataGrid 列（绑定 [i] 索引器）。
        if (e.PropertyName == nameof(DataCompareViewModel.DetailColumns))
        {
            RebuildDetailColumns();
        }
    }

    private void RebuildDetailColumns()
    {
        var grid = DetailGrid;
        if (grid is null)
            return;

        grid.Columns.Clear();

        for (int i = 0; i < _vm.DetailColumns.Count; i++)
        {
            grid.Columns.Add(new DataGridTextColumn
            {
                Header = _vm.DetailColumns[i],
                Binding = new Binding($"[{i}]"),
                IsReadOnly = true,
            });
        }
    }

    private void BtnRefresh_Click(object? sender, RoutedEventArgs e)
    {
        Refresh();
    }

    private void BtnClose_Click(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
