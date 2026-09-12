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
        vm.ConfirmAnalyze = async () => await MsBox.Avalonia.MessageBoxManager.GetMessageBoxStandard(
            "实际执行 SQL", "ANALYZE 会执行当前 SQL，并可能修改数据。是否继续？",
            MsBox.Avalonia.Enums.ButtonEnum.YesNo).ShowWindowDialogAsync(this) == MsBox.Avalonia.Enums.ButtonResult.Yes;
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
    protected override void OnClosed(EventArgs e)
    {
        _vm?.Cancel();
        base.OnClosed(e);
    }
    private void LocateSql_Click(object? sender, RoutedEventArgs e)
    {
        var name = _vm?.SelectedNode?.ObjectName;
        var sql = SqlInput.Text ?? "";
        if (string.IsNullOrWhiteSpace(name)) { if (_vm is not null) _vm.StatusMessage = "当前节点未提供对象名，无法可靠定位 SQL。"; return; }
        int index = sql.IndexOf(name, StringComparison.OrdinalIgnoreCase);
        if (index < 0) { _vm!.StatusMessage = "SQL 中未找到节点对象名称。"; return; }
        SqlInput.Focus(); SqlInput.SelectionStart = index; SqlInput.SelectionEnd = index + name.Length;
        _vm!.StatusMessage = "已定位对象名称首次出现的位置（计划未提供精确源码范围）。";
    }
}
