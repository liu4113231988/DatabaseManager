using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DatabaseManager.AppCore.Common;
using DatabaseManager.AppCore.Models;
using DatabaseManager.AppCore.Services;

namespace DatabaseManager.AppCore.ViewModels;

/// <summary>
/// 任务中心 ViewModel：查看本会话全部后台任务（运行中/已完成/失败/取消），
/// 取消运行中的任务、查看任务日志与跨会话历史。
/// </summary>
public partial class TaskCenterViewModel : ViewModelBase
{
    private readonly ITaskCenterService _taskCenter;

    /// <summary>本会话任务列表（最新在前）。</summary>
    public ObservableCollection<TaskRun> Runs { get; } = new();

    /// <summary>跨会话历史。</summary>
    public ObservableCollection<TaskHistoryEntry> History { get; } = new();

    [ObservableProperty]
    private TaskRun? _selectedRun;

    [ObservableProperty]
    private bool _hasHistory;

    /// <summary>当前选中任务的日志（只读展示）。</summary>
    public string SelectedRunLogs => SelectedRun is null
        ? string.Empty
        : string.Join(Environment.NewLine, SelectedRun.Logs);

    public TaskCenterViewModel(ITaskCenterService taskCenter)
    {
        _taskCenter = taskCenter;
    }

    partial void OnSelectedRunChanged(TaskRun? value)
        => OnPropertyChanged(nameof(SelectedRunLogs));

    /// <summary>刷新任务列表与历史（窗口打开时与手动刷新时调用）。</summary>
    [RelayCommand]
    private void Refresh()
    {
        Runs.Clear();
        foreach (var run in _taskCenter.Runs)
        {
            Runs.Add(run);
        }

        History.Clear();
        foreach (var entry in _taskCenter.GetHistory())
        {
            History.Add(entry);
        }

        HasHistory = History.Count > 0;
        OnPropertyChanged(nameof(SelectedRunLogs));
    }

    [RelayCommand]
    private void Cancel(TaskRun? run)
    {
        if (run is { State: TaskRunState.Running })
        {
            _taskCenter.Cancel(run.Id);
        }
    }

    [RelayCommand]
    private void CancelSelected()
    {
        Cancel(SelectedRun);
    }
}
