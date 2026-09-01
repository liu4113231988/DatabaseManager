using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DatabaseManager.AppCore.Common;
using DatabaseManager.AppCore.Models;
using DatabaseManager.AppCore.Services;

namespace DatabaseManager.AppCore.ViewModels;

/// <summary>
/// 数据库统计 ViewModel（阶段 5）。
/// 选择连接与统计类型（表记录数 / 列内容最大长度），执行并查看结果。
/// 执行经任务中心登记（可脱离本窗口观测/取消）。
/// </summary>
public partial class StatisticViewModel : ToolViewModelBase
{
    private readonly IDbConnectionService _connectionService;
    private readonly IStatisticService _statisticService;
    private readonly ITaskCenterService _taskCenter;

    /// <summary>全部已保存连接。</summary>
    public ObservableCollection<ConnectionItem> Connections { get; } = new();

    /// <summary>统计类型选项。</summary>
    public ObservableCollection<StatisticTypeOption> StatisticTypes { get; } = new()
    {
        StatisticTypeOption.RecordCount,
        StatisticTypeOption.ColumnLength,
    };

    /// <summary>记录数统计结果。</summary>
    public ObservableCollection<RecordCountItem> RecordCounts { get; } = new();

    /// <summary>列长度统计结果。</summary>
    public ObservableCollection<ColumnLengthItem> ColumnLengths { get; } = new();

    [ObservableProperty]
    private ConnectionItem? _selectedConnection;

    [ObservableProperty]
    private StatisticTypeOption? _selectedStatisticType;

    public StatisticViewModel(IDbConnectionService connectionService, IStatisticService statisticService, ITaskCenterService taskCenter)
    {
        _connectionService = connectionService;
        _statisticService = statisticService;
        _taskCenter = taskCenter;
        SelectedStatisticType = StatisticTypes.FirstOrDefault();
    }

    /// <summary>加载已保存连接并刷新选择。</summary>
    public void RefreshConnections()
    {
        var previousId = SelectedConnection?.Id;

        Connections.Clear();
        foreach (var item in _connectionService.GetConnections())
        {
            Connections.Add(item);
        }

        SelectedConnection = Connections.FirstOrDefault(c => c.Id == previousId) ?? Connections.FirstOrDefault();
    }

    partial void OnSelectedStatisticTypeChanged(StatisticTypeOption? value)
    {
        ClearResults();
    }

    /// <summary>请求取消正在执行的统计。</summary>
    [RelayCommand(CanExecute = nameof(IsBusy))]
    private void CancelExecute()
    {
        if (_currentRun is not { State: TaskRunState.Running } run)
        {
            return;
        }

        StatusMessage = "正在取消统计...";
        _taskCenter.Cancel(run.Id);
    }

    protected override void OnBusyChanged() => CancelExecuteCommand.NotifyCanExecuteChanged();

    [RelayCommand]
    private async Task ExecuteAsync()
    {
        if (SelectedConnection is null)
        {
            StatusMessage = "请选择连接。";
            return;
        }

        var statType = SelectedStatisticType;
        if (statType is null)
        {
            StatusMessage = "请选择统计类型。";
            return;
        }

        IsBusy = true;
        StatusMessage = string.Empty;
        ClearResults();

        var connection = SelectedConnection;
        var typeName = statType.DisplayName;

        var feedbackBuffer = new List<string>();
        void CollectFeedback(string message)
        {
            feedbackBuffer.Add(message);
            _currentRun?.Report(message);
        }

        AppendLog($"连接：{connection.Description}");
        AppendLog($"统计类型：{typeName}");
        AppendLog("开始统计...");

        // 经任务中心登记：窗口中途关闭后统计仍可观测/取消。
        _currentRun = _taskCenter.Run($"统计 {connection.Description}（{typeName}）", "统计", async (run, ct) =>
        {
            try
            {
                if (statType.Value == StatisticTypeOption.RecordCount.Value)
                {
                    var results = await _statisticService.CountTableRecordsAsync(connection, CollectFeedback, ct);

                    foreach (var line in feedbackBuffer)
                    {
                        AppendLog(line);
                    }

                    foreach (var item in results)
                    {
                        RecordCounts.Add(item);
                    }

                    StatusMessage = $"统计完成，共 {results.Count} 张表。";
                }
                else
                {
                    var results = await _statisticService.GetTableColumnLengthsAsync(connection, CollectFeedback, ct);

                    foreach (var line in feedbackBuffer)
                    {
                        AppendLog(line);
                    }

                    foreach (var item in results)
                    {
                        ColumnLengths.Add(item);
                    }

                    StatusMessage = $"统计完成，共 {results.Count} 个字符列。";
                }

                AppendLog(StatusMessage);
                run.ResultSummary = StatusMessage;
            }
            finally
            {
                _currentRun = null;
                IsBusy = false;
            }
        });
    }

    private TaskRun? _currentRun;

    private void ClearResults()
    {
        RecordCounts.Clear();
        ColumnLengths.Clear();
    }
}
