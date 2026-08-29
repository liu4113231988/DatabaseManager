using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Interactivity;
using DatabaseManager.AppCore.ViewModels;

namespace DatabaseManager.Avalonia.Views;

/// <summary>执行计划窗口：展示 EXPLAIN/SHOWPLAN 结果（列动态重建）。</summary>
public partial class ExecutionPlanWindow : Window
{
    private readonly ExecutionPlanViewModel? _vm;

    public ExecutionPlanWindow()
    {
        InitializeComponent();
    }

    public ExecutionPlanWindow(ExecutionPlanViewModel vm) : this()
    {
        DataContext = vm;
        _vm = vm;
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        if (_vm is null) return;

        _vm.Columns.CollectionChanged += (_, _) => RebuildColumns();

        // 打开时自动获取一次执行计划。
        _vm.ExecuteCommand.Execute(null);
    }

    private void RebuildColumns()
    {
        if (_vm is null)
        {
            return;
        }

        PlanGrid.Columns.Clear();

        for (int i = 0; i < _vm.Columns.Count; i++)
        {
            PlanGrid.Columns.Add(new DataGridTextColumn
            {
                Header = _vm.Columns[i],
                Binding = new Binding($"[{i}]"),
                IsReadOnly = true,
            });
        }
    }

    private void BtnClose_Click(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
