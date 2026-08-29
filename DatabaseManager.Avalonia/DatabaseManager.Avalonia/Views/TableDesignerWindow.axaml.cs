using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using DatabaseManager.AppCore.Models;
using DatabaseManager.AppCore.ViewModels;

namespace DatabaseManager.Avalonia.Views;

public partial class TableDesignerWindow : Window
{
    private TableDesignerViewModel? _vm;

    public TableDesignerWindow()
    {
        InitializeComponent();
    }

    public TableDesignerWindow(TableDesignerViewModel vm) : this()
    {
        DataContext = vm;
        _vm = vm;
    }

    private void BtnClose_Click(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    #region 列操作

    private void BtnRemoveColumn_Click(object? sender, RoutedEventArgs e)
    {
        if (_vm is null)
            return;

        // 定位到列 Tab（索引 0）并读取选中列。
        if (DesignTabs.SelectedIndex != 0)
        {
            DesignTabs.SelectedIndex = 0;
        }

        // 通过当前活动 DataGrid 的选中项删除。
        var grid = FindColumnGrid();
        if (grid is null || grid.SelectedItem is not TableDesignColumn column)
        {
            _vm.StatusMessage = "请先在「列」标签中选中要删除的列。";
            return;
        }

        _vm.RemoveColumnCommand.Execute(column);
    }

    private DataGrid? FindColumnGrid()
    {
        if (_vm is null) return null;
        // 在窗口内查找列 DataGrid。
        foreach (var control in VisualChildren)
        {
            if (control is TabControl tabs)
            {
                foreach (var item in tabs.Items)
                {
                    if (item is TabItem ti && ti.Content is Grid grid)
                    {
                        foreach (var child in grid.Children)
                        {
                            if (child is DataGrid dg && dg.ItemsSource == _vm.Columns)
                                return dg;
                        }
                    }
                }
            }
        }
        return null;
    }

    #endregion

    #region 主键操作

    private async void BtnPkAddColumn_Click(object? sender, RoutedEventArgs e)
    {
        if (_vm is null)
            return;

        if (_vm.PrimaryKey is null)
        {
            _vm.PrimaryKey = new TableDesignKey { Name = $"PK_{_vm.TableName}" };
        }

        var pk = _vm.PrimaryKey;

        // 仅选择尚未加入主键的列。
        var candidates = _vm.Columns
            .Where(c => !pk.Columns.Contains(c.Name, StringComparer.OrdinalIgnoreCase))
            .Select(c => c.Name)
            .ToList();

        if (candidates.Count == 0)
        {
            _vm.StatusMessage = "所有列均已在主键中。";
            return;
        }

        var picked = await ShowPickColumnDialogAsync(candidates, "选择要加入主键的列");
        if (picked is not null && picked.Length > 0)
        {
            foreach (var name in picked)
            {
                if (!pk.Columns.Contains(name, StringComparer.OrdinalIgnoreCase))
                    pk.Columns.Add(name);
            }
            _vm.HasChanges = true;
            _vm.StatusMessage = $"已更新主键：{string.Join(", ", pk.Columns)}。";
        }
    }

    private void BtnPkRemoveColumn_Click(object? sender, RoutedEventArgs e)
    {
        if (_vm?.PrimaryKey is null)
            return;

        if (PkColumnList.SelectedItem is not string selected)
        {
            _vm.StatusMessage = "请先在主键列列表中选中要移除的列。";
            return;
        }

        _vm.PrimaryKey.Columns.RemoveAll(c => string.Equals(c, selected, StringComparison.OrdinalIgnoreCase));
        _vm.HasChanges = true;
        _vm.StatusMessage = $"已从主键移除 {selected}。";
    }

    private void BtnPkClear_Click(object? sender, RoutedEventArgs e)
    {
        if (_vm is null)
            return;

        _vm.PrimaryKey = null;
        _vm.HasChanges = true;
        _vm.StatusMessage = "已清除主键定义。";
    }

    #endregion

    #region 索引操作

    private void BtnRemoveIndex_Click(object? sender, RoutedEventArgs e)
    {
        if (_vm is null)
            return;

        var index = ShowIndexGrid()?.SelectedItem as TableDesignIndex;
        if (index is null)
        {
            _vm.StatusMessage = "请先在「索引」标签中选中要删除的索引。";
            return;
        }

        _vm.RemoveIndexCommand.Execute(index);
    }

    private DataGrid? ShowIndexGrid()
    {
        if (_vm is null) return null;
        return FindGridFor(_vm.Indexes);
    }

    #endregion

    #region 外键操作

    private void BtnRemoveForeignKey_Click(object? sender, RoutedEventArgs e)
    {
        if (_vm is null)
            return;

        var fk = FindGridFor(_vm.ForeignKeys)?.SelectedItem as TableDesignForeignKey;
        if (fk is null)
        {
            _vm.StatusMessage = "请先在「外键」标签中选中要删除的外键。";
            return;
        }

        _vm.RemoveForeignKeyCommand.Execute(fk);
    }

    #endregion

    #region 约束操作

    private void BtnRemoveConstraint_Click(object? sender, RoutedEventArgs e)
    {
        if (_vm is null)
            return;

        var c = FindGridFor(_vm.Constraints)?.SelectedItem as TableDesignConstraint;
        if (c is null)
        {
            _vm.StatusMessage = "请先在「约束」标签中选中要删除的约束。";
            return;
        }

        _vm.RemoveConstraintCommand.Execute(c);
    }

    #endregion

    private DataGrid? FindGridFor(System.Collections.IEnumerable? itemsSource)
    {
        if (_vm is null) return null;
        if (itemsSource is null)
            return null;

        foreach (var item in DesignTabs.Items)
        {
            if (item is TabItem ti && ti.Content is Grid grid)
            {
                foreach (var child in grid.Children)
                {
                    if (child is DataGrid dg && ReferenceEquals(dg.ItemsSource, itemsSource))
                        return dg;
                }
            }
        }

        return null;
    }

    /// <summary>简单多选列对话框（选择要加入主键的列）。</summary>
    private async Task<string[]?> ShowPickColumnDialogAsync(System.Collections.Generic.List<string> candidates, string title)
    {
        var dialog = new Window
        {
            Title = title,
            Width = 320,
            Height = 400,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
        };

        var listBox = new ListBox { SelectionMode = SelectionMode.Multiple };
        foreach (var c in candidates)
        {
            listBox.Items.Add(c);
        }

        var okButton = new Button { Content = "确定", Margin = new Thickness(0, 8, 0, 0) };
        string[]? result = null;

        okButton.Click += (_, _) =>
        {
            result = listBox.SelectedItems.Cast<object>().Select(o => o.ToString() ?? string.Empty).ToArray();
            dialog.Close(result);
        };

        var cancelButton = new Button { Content = "取消", Margin = new Thickness(8, 8, 0, 0) };
        cancelButton.Click += (_, _) => dialog.Close();

        var buttons = new StackPanel { Orientation = Orientation.Horizontal };
        buttons.Children.Add(okButton);
        buttons.Children.Add(cancelButton);

        var panel = new global::Avalonia.Controls.DockPanel { Margin = new Thickness(12) };
        global::Avalonia.Controls.DockPanel.SetDock(buttons, global::Avalonia.Controls.Dock.Bottom);
        panel.Children.Add(buttons);
        panel.Children.Add(listBox);

        dialog.Content = panel;

        var r = await dialog.ShowDialog<string[]?>(this);
        return r ?? result;
    }
}
