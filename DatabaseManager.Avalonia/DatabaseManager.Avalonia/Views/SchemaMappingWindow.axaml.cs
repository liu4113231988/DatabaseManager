using Avalonia.Controls;
using Avalonia.Interactivity;
using DatabaseManager.AppCore.ViewModels;

namespace DatabaseManager.Avalonia.Views;

/// <summary>
/// Schema 映射窗口（阶段 4）。对应原 WinForms <c>frmSchemaMapping</c>。
/// 配置源 Schema → 目标 Schema 的映射，供转换流程使用。
/// </summary>
public partial class SchemaMappingWindow : Window
{
    private readonly ConvertViewModel? _vm;

    public SchemaMappingWindow()
    {
        InitializeComponent();
    }

    public SchemaMappingWindow(ConvertViewModel vm) : this()
    {
        DataContext = vm;
        _vm = vm;
    }

    private void BtnRemoveMapping_Click(object? sender, RoutedEventArgs e)
    {
        // 通过 DataGrid 选中项删除（从可视化树查找绑定了 SchemaMappings 的网格）。
        if (FindMappingGrid() is { SelectedItem: not null } grid &&
            grid.SelectedItem is DatabaseManager.AppCore.ViewModels.SchemaMappingItem item)
        {
            _vm.RemoveSchemaMappingCommand.Execute(item);
        }
        else
        {
            _vm.StatusMessage = "请先选中要删除的映射行。";
        }
    }

    private DataGrid? FindMappingGrid()
    {
        if (_vm is null) return null;
        foreach (var child in VisualChildren)
        {
            if (child is DataGrid dg && ReferenceEquals(dg.ItemsSource, _vm.SchemaMappings))
                return dg;
        }
        return null;
    }

    private void BtnClose_Click(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
