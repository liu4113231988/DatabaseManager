using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace DatabaseManager.AppCore.Models;

/// <summary>后台任务状态。</summary>
public enum TaskRunState
{
    Running = 0,
    Completed = 1,
    Failed = 2,
    Cancelled = 3,
}

/// <summary>
/// 任务中心的一条后台任务（转换/导入导出/备份/统计/脚本执行等）。
/// 属性只在 UI 线程更新（由 <see cref="Services.ITaskCenterService"/> 保证），
/// 后台工作通过 <see cref="Report"/>/<see cref="Log"/> 回报进度与日志（内部经 Progress 封送）。
/// </summary>
public partial class TaskRun : ObservableObject
{
    public string Id { get; } = Guid.NewGuid().ToString("N");

    public string Title { get; }

    public string Category { get; }

    public DateTime StartedAt { get; } = DateTime.Now;

    public DateTime? FinishedAt { get; internal set; }

    [ObservableProperty]
    private TaskRunState _state = TaskRunState.Running;

    [ObservableProperty]
    private string _progressText = string.Empty;

    [ObservableProperty]
    private string _resultSummary = string.Empty;

    /// <summary>取消令牌源（任务执行期间可取消）。</summary>
    public CancellationTokenSource Cts { get; } = new();

    /// <summary>任务日志（UI 线程追加，上限 500 行，绑定到任务中心窗口）。</summary>
    public ObservableCollection<string> Logs { get; } = new();

    /// <summary>状态显示文本。</summary>
    public string StateText => State switch
    {
        TaskRunState.Running => "运行中",
        TaskRunState.Completed => "已完成",
        TaskRunState.Failed => "失败",
        TaskRunState.Cancelled => "已取消",
        _ => State.ToString(),
    };

    /// <summary>开始时间显示文本。</summary>
    public string StartedAtText => StartedAt.ToString("HH:mm:ss");

    /// <summary>耗时显示文本（运行中按当前时间估算）。</summary>
    public string DurationText
    {
        get
        {
            var end = FinishedAt ?? DateTime.Now;
            var elapsed = end - StartedAt;
            return elapsed.TotalMinutes >= 1
                ? $"{(int)elapsed.TotalMinutes}m{elapsed.Seconds:00}s"
                : $"{elapsed.TotalSeconds:0.0}s";
        }
    }

    public TaskRun(string title, string category)
    {
        Title = title;
        Category = category;
    }

    /// <summary>回报进度文本并追加日志（可从后台线程调用，自动封送到 UI 线程）。</summary>
    public void Report(string message)
    {
        _progressReporter.Report(message ?? string.Empty);
    }

    /// <summary>仅追加日志（可从后台线程调用）。</summary>
    public void Log(string message)
    {
        _progressReporter.Report(message ?? string.Empty);
    }

    /// <summary>在 UI 线程上追加日志（供服务内部使用）。</summary>
    internal void AppendLog(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        Logs.Add($"[{DateTime.Now:HH:mm:ss}] {message}");
        while (Logs.Count > MaxLogLines)
        {
            Logs.RemoveAt(0);
        }
    }

    internal void NotifyProgress(string message)
    {
        ProgressText = message;
        AppendLog(message);
    }

    private const int MaxLogLines = 500;

    /// <summary>经 UI 线程封送的进度回报器（在创建任务的 UI 线程上构造）。</summary>
    private readonly IProgress<string> _progressReporter;

    internal TaskRun(string title, string category, Progress<string> reporter)
    {
        Title = title;
        Category = category;
        _progressReporter = reporter;
        reporter.ProgressChanged += (_, message) => NotifyProgress(message);
    }
}
