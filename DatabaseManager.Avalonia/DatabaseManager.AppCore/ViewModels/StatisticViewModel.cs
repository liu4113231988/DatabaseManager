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
/// </summary>
public partial class StatisticViewModel : ViewModelBase
{
    private readonly IDbConnectionService _connectionService;
    private readonly IStatisticService _statisticService;

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

    /// <summary>执行日志。</summary>
    public ObservableCollection<string> Logs { get; } = new();

    [ObservableProperty]
    private ConnectionItem? _selectedConnection;

    [ObservableProperty]
    private StatisticTypeOption? _selectedStatisticType;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    public StatisticViewModel(IDbConnectionService connectionService, IStatisticService statisticService)
    {
        _connectionService = connectionService;
        _statisticService = statisticService;
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

        var feedbackBuffer = new List<string>();
        void CollectFeedback(string message) => feedbackBuffer.Add(message);

        try
        {
            AppendLog($"连接：{SelectedConnection.Description}");
            AppendLog($"统计类型：{statType.DisplayName}");
            AppendLog("开始统计...");

            if (statType.Value == StatisticTypeOption.RecordCount.Value)
            {
                var results = await _statisticService.CountTableRecordsAsync(SelectedConnection, CollectFeedback);

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
                var results = await _statisticService.GetTableColumnLengthsAsync(SelectedConnection, CollectFeedback);

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
        }
        catch (Exception ex)
        {
            StatusMessage = $"统计失败：{ex.Message}";
            AppendLog(StatusMessage);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ClearResults()
    {
        RecordCounts.Clear();
        ColumnLengths.Clear();
    }

    private void AppendLog(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;

        var time = DateTime.Now.ToString("HH:mm:ss");
        Logs.Add($"[{time}] {message}");
    }
}
