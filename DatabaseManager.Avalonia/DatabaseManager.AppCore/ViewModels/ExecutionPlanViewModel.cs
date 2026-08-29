using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DatabaseManager.AppCore.Common;
using DatabaseManager.AppCore.Models;
using DatabaseManager.AppCore.Services;

namespace DatabaseManager.AppCore.ViewModels;

/// <summary>
/// 执行计划 ViewModel：对当前 SQL 执行 EXPLAIN/SHOWPLAN 并以表格展示计划结果。
/// </summary>
public partial class ExecutionPlanViewModel : ViewModelBase
{
    private readonly IExecutionPlanService _planService;

    /// <summary>执行计划使用的连接（由主窗口设置）。</summary>
    public ConnectionItem? Connection { get; set; }

    /// <summary>要分析的 SQL（由主窗口带入选区或整段文本）。</summary>
    [ObservableProperty]
    private string _sqlText = string.Empty;

    /// <summary>是否实际执行以获取真实计划（MySQL 8.0.18+ / PostgreSQL）。</summary>
    [ObservableProperty]
    private bool _analyze;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _hasResult;

    /// <summary>计划结果列名（窗口据此重建 DataGrid 列）。</summary>
    public ObservableCollection<string> Columns { get; } = new();

    /// <summary>计划结果行。</summary>
    public ObservableCollection<DataRowItem> Rows { get; } = new();

    public ExecutionPlanViewModel(IExecutionPlanService planService)
    {
        _planService = planService;
    }

    [RelayCommand(CanExecute = nameof(CanExecutePlan))]
    private async Task ExecuteAsync()
    {
        if (Connection is null)
        {
            StatusMessage = "请先选择一个连接。";
            return;
        }

        IsBusy = true;
        try
        {
            var result = await _planService.ExplainAsync(Connection, SqlText, Analyze);

            Columns.Clear();
            Rows.Clear();

            if (!result.IsSuccess)
            {
                StatusMessage = result.ErrorMessage ?? "获取执行计划失败。";
                HasResult = false;
                return;
            }

            foreach (var col in result.Columns)
            {
                Columns.Add(col);
            }

            foreach (var row in result.Rows)
            {
                Rows.Add(new DataRowItem(row));
            }

            HasResult = Rows.Count > 0;
            StatusMessage = HasResult
                ? $"已获取执行计划（{Rows.Count} 行，耗时 {result.ElapsedMilliseconds} ms）。"
                : "执行计划为空。";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanExecutePlan() => !IsBusy && SqlText.Length > 0;

    partial void OnSqlTextChanged(string value) => ExecuteCommand.NotifyCanExecuteChanged();
    partial void OnIsBusyChanged(bool value) => ExecuteCommand.NotifyCanExecuteChanged();
}
