using System.IO;
using DatabaseManager.AppCore.Models;
using Newtonsoft.Json;

namespace DatabaseManager.AppCore.Services;

/// <summary>
/// 任务中心服务实现：内存保留最近 100 条任务；结束的任务追加持久化到
/// Profiles\task-history.json（保留最近 200 条，仿 query-history 的锁 + 静默容错模式）。
/// 必须在 UI 线程调用：TaskRun 的属性/集合更新依赖调用线程即 UI 线程。
/// </summary>
public class DefaultTaskCenterService : ITaskCenterService
{
    private const int MaxRuns = 100;
    private const int MaxHistoryEntries = 200;
    private static readonly object HistoryFileLock = new();

    private readonly string _historyFilePath;
    private readonly List<TaskRun> _runs = new();

    public event Action<TaskRun>? TaskFinished;

    public event Action? RunsChanged;

    public DefaultTaskCenterService()
    {
        var dir = Path.Combine(AppContext.BaseDirectory, "Profiles");
        Directory.CreateDirectory(dir);
        _historyFilePath = Path.Combine(dir, "task-history.json");
    }

    public IReadOnlyList<TaskRun> Runs => _runs;

    public int RunningCount => _runs.Count(r => r.State == TaskRunState.Running);

    public bool HasRunning => RunningCount > 0;

    public TaskRun Run(string title, string category, Func<TaskRun, CancellationToken, Task> work)
    {
        var run = Register(title, category);
        _ = ExecuteAsync(run, work);
        return run;
    }

    public TaskRun Register(string title, string category)
    {
        var reporter = new Progress<string>();
        var run = new TaskRun(title, category, reporter);

        _runs.Insert(0, run);
        while (_runs.Count > MaxRuns)
        {
            _runs.RemoveAt(_runs.Count - 1);
        }

        run.AppendLog("任务已登记。");
        RunsChanged?.Invoke();

        return run;
    }

    private async Task ExecuteAsync(TaskRun run, Func<TaskRun, CancellationToken, Task> work)
    {
        try
        {
            await work(run, run.Cts.Token);

            if (run.Cts.IsCancellationRequested)
            {
                Finalize(run, TaskRunState.Cancelled, "任务已取消。");
            }
            else
            {
                Finalize(run, TaskRunState.Completed,
                    string.IsNullOrEmpty(run.ResultSummary) ? "任务完成。" : run.ResultSummary);
            }
        }
        catch (OperationCanceledException)
        {
            Finalize(run, TaskRunState.Cancelled, "任务已取消。");
        }
        catch (Exception ex)
        {
            Finalize(run, TaskRunState.Failed, $"任务失败：{ex.Message}");
        }
    }

    private void Finalize(TaskRun run, TaskRunState state, string summary)
    {
        run.FinishedAt = DateTime.Now;
        run.State = state;
        run.ResultSummary = summary;
        run.AppendLog(summary);

        PersistHistory(run);

        TaskFinished?.Invoke(run);
        RunsChanged?.Invoke();
    }

    public void Cancel(string taskId)
    {
        var run = _runs.FirstOrDefault(r => r.Id == taskId);
        if (run is { State: TaskRunState.Running })
        {
            run.AppendLog("已请求取消...");
            run.Cts.Cancel();
        }
    }

    public IReadOnlyList<TaskHistoryEntry> GetHistory()
    {
        lock (HistoryFileLock)
        {
            return LoadHistory();
        }
    }

    private void PersistHistory(TaskRun run)
    {
        try
        {
            lock (HistoryFileLock)
            {
                var entries = LoadHistory();
                entries.Insert(0, new TaskHistoryEntry
                {
                    Title = run.Title,
                    Category = run.Category,
                    State = run.State.ToString(),
                    StartedAt = run.StartedAt,
                    FinishedAt = run.FinishedAt,
                    ResultSummary = run.ResultSummary,
                });

                if (entries.Count > MaxHistoryEntries)
                {
                    entries.RemoveRange(MaxHistoryEntries, entries.Count - MaxHistoryEntries);
                }

                File.WriteAllText(_historyFilePath, JsonConvert.SerializeObject(entries, Formatting.Indented));
            }
        }
        catch
        {
            // 历史写入失败（磁盘只读等）静默忽略，任务状态仍在内存中可见。
        }
    }

    private List<TaskHistoryEntry> LoadHistory()
    {
        try
        {
            if (!File.Exists(_historyFilePath))
            {
                return new List<TaskHistoryEntry>();
            }

            return JsonConvert.DeserializeObject<List<TaskHistoryEntry>>(File.ReadAllText(_historyFilePath))
                   ?? new List<TaskHistoryEntry>();
        }
        catch
        {
            return new List<TaskHistoryEntry>();
        }
    }
}
