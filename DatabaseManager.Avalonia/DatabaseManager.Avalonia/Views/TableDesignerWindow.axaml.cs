using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.VisualTree;
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

        if (DesignTabs.SelectedIndex != 0)
        {
            DesignTabs.SelectedIndex = 0;
        }

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
        return FindDescendantDataGrids(this)
            .FirstOrDefault(dg => ReferenceEquals(dg.ItemsSource, _vm.Columns));
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
        var candidates = _vm.Columns.Select(c => c.Name).ToList();
        var picked = await ShowColumnPickerAsync(candidates, "选择主键列", pk.Columns);
        if (picked is not null)
        {
            pk.Columns = picked.ToList();
            _vm.HasChanges = true;
            _vm.StatusMessage = $"已更新主键：{pk.ColumnsDisplay}。";
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

        var pk = _vm.PrimaryKey;
        pk.Columns = pk.Columns.Where(c => c != selected).ToList();
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

    private async void BtnIndexPickColumns_Click(object? sender, RoutedEventArgs e)
    {
        if (_vm is null)
            return;

        var index = FindGridFor(_vm.Indexes)?.SelectedItem as TableDesignIndex;
        if (index is null)
        {
            _vm.StatusMessage = "请先在「索引」标签中选中要编辑的索引。";
            return;
        }

        var picked = await ShowColumnPickerAsync(
            _vm.Columns.Select(c => c.Name).ToList(), $"选择索引 {index.Name} 的列", index.Columns);
        if (picked is null)
            return;

        index.Columns = picked.ToList();
        _vm.HasChanges = true;
        _vm.StatusMessage = $"已更新索引 {index.Name} 的列：{index.ColumnsDisplay}。";
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

    private async void BtnForeignKeyPickColumns_Click(object? sender, RoutedEventArgs e)
    {
        if (_vm is null)
            return;

        var fk = FindGridFor(_vm.ForeignKeys)?.SelectedItem as TableDesignForeignKey;
        if (fk is null)
        {
            _vm.StatusMessage = "请先在「外键」标签中选中要编辑的外键。";
            return;
        }

        var localColumns = fk.Columns.Select(c => c.ColumnName).ToList();
        var picked = await ShowColumnPickerAsync(
            _vm.Columns.Select(c => c.Name).ToList(), $"选择外键 {fk.Name} 的本表列", localColumns);
        if (picked is null)
            return;

        fk.SelectLocalColumns(picked);
        _vm.HasChanges = true;
        _vm.StatusMessage = $"已更新外键 {fk.Name} 的本表列；引用列可在「列映射」单元格中直接修改。";
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
        if (_vm is null || itemsSource is null)
            return null;

        return FindDescendantDataGrids(this)
            .FirstOrDefault(dg => ReferenceEquals(dg.ItemsSource, itemsSource));
    }

    /// <summary>递归枚举视觉树中的全部 DataGrid（不依赖容器层级）。</summary>
    private static IEnumerable<DataGrid> FindDescendantDataGrids(Visual visual)
    {
        foreach (var child in visual.GetVisualChildren())
        {
            if (child is DataGrid grid)
            {
                yield return grid;
            }

            foreach (var descendant in FindDescendantDataGrids(child))
            {
                yield return descendant;
            }
        }
    }

    /// <summary>列选择器：可选、可移除、可上移/下移，返回最终列顺序。</summary>
    private async Task<string[]?> ShowColumnPickerAsync(
        IReadOnlyList<string> candidates, string title, IReadOnlyList<string>? initialSelection = null)
    {
        var dialog = new Window
        {
            Title = title,
            Icon = Icon,
            ShowInTaskbar = false,
            Width = 620,
            Height = 420,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
        };

        var available = new ListBox { SelectionMode = SelectionMode.Multiple, Width = 240 };
        foreach (string candidate in candidates)
        {
            available.Items.Add(candidate);
        }

        var selected = new ListBox { Width = 240 };
        foreach (string name in initialSelection ?? Array.Empty<string>())
        {
            selected.Items.Add(name);
        }

        var addButton = new Button { Content = "添加 →", Width = 96 };
        addButton.Click += (_, _) =>
        {
            foreach (object item in (available.SelectedItems ?? Array.Empty<object>()).Cast<object>().ToList())
            {
                string name = item.ToString() ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(name) && !selected.Items.Contains(name))
                {
                    selected.Items.Add(name);
                }
            }
        };

        var removeButton = new Button { Content = "← 移除", Width = 96 };
        removeButton.Click += (_, _) =>
        {
            foreach (object item in (selected.SelectedItems ?? Array.Empty<object>()).Cast<object>().ToList())
            {
                selected.Items.Remove(item);
            }
        };

        var upButton = new Button { Content = "↑ 上移", Width = 96 };
        upButton.Click += (_, _) => MoveSelectedItem(selected, -1);

        var downButton = new Button { Content = "↓ 下移", Width = 96 };
        downButton.Click += (_, _) => MoveSelectedItem(selected, 1);

        var actions = new StackPanel { Orientation = Orientation.Vertical, Spacing = 6, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0) };
        actions.Children.Add(addButton);
        actions.Children.Add(removeButton);
        actions.Children.Add(upButton);
        actions.Children.Add(downButton);

        var left = new StackPanel { Spacing = 4 };
        left.Children.Add(new TextBlock { Text = "可用列" });
        left.Children.Add(available);
        var right = new StackPanel { Spacing = 4 };
        right.Children.Add(new TextBlock { Text = "已选列（顺序即索引列顺序）" });
        right.Children.Add(selected);

        var body = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8) };
        body.Children.Add(left);
        body.Children.Add(actions);
        body.Children.Add(right);

        var okButton = new Button { Content = "确定", MinWidth = 80, IsDefault = true };
        okButton.Click += (_, _) => dialog.Close(SelectedNames());
        var cancelButton = new Button { Content = "取消", MinWidth = 80, IsCancel = true, Margin = new Thickness(8, 0, 0, 0) };
        cancelButton.Click += (_, _) => dialog.Close(null);

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        buttons.Children.Add(okButton);
        buttons.Children.Add(cancelButton);

        var panel = new global::Avalonia.Controls.DockPanel { Margin = new Thickness(12) };
        global::Avalonia.Controls.DockPanel.SetDock(buttons, global::Avalonia.Controls.Dock.Bottom);
        panel.Children.Add(buttons);
        panel.Children.Add(body);

        dialog.Content = panel;
        return await dialog.ShowDialog<string[]?>(this);

        string[] SelectedNames() => selected.Items.Cast<object>().Select(o => o.ToString() ?? string.Empty).ToArray();
    }

    private static void MoveSelectedItem(ListBox list, int offset)
    {
        int index = list.SelectedIndex;
        int target = index + offset;
        if (index < 0 || target < 0 || target >= list.Items.Count)
        {
            return;
        }

        object current = list.Items[index]!;
        object neighbor = list.Items[target]!;
        list.Items[index] = neighbor;
        list.Items[target] = current;
        list.SelectedIndex = target;
    }
}
