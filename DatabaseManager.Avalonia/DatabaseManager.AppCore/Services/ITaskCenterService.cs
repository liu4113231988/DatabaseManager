using DatabaseManager.AppCore.Models;

namespace DatabaseManager.AppCore.Services;

/// <summary>
/// 任务中心服务：统一登记/观测/取消耗时的后台任务（转换、导入导出、备份、统计、脚本执行等），
/// 使任务脱离创建它的模态窗口独立可见、可取消，完成/失败时发出通知事件。
/// 所有方法要求在 UI 线程调用（内部保证 TaskRun 属性更新发生在 UI 线程）。
/// </summary>
public interface ITaskCenterService
{
    /// <summary>本会话的全部任务（最新在前，含运行中与已结束，上限 100 条）。</summary>
    IReadOnlyList<TaskRun> Runs { get; }

    /// <summary>当前运行中的任务数。</summary>
    int RunningCount { get; }

    /// <summary>
    /// 登记并启动一个后台任务。work 内部应通过 <see cref="TaskRun.Report"/> 回报进度，
    /// 并正确响应取消令牌；正常返回视为完成，抛出 OperationCanceledException 视为取消，其余异常视为失败。
    /// </summary>
    TaskRun Run(string title, string category, Func<TaskRun, CancellationToken, Task> work);

    /// <summary>仅登记一个不归本服务驱动生命周期的任务占位（很少使用）。</summary>
    TaskRun Register(string title, string category);

    /// <summary>请求取消指定任务。</summary>
    void Cancel(string taskId);

    /// <summary>是否存在运行中的任务。</summary>
    bool HasRunning { get; }

    /// <summary>任务结束（完成/失败/取消）时触发（UI 线程），供 Toast 通知与状态栏计数。</summary>
    event Action<TaskRun>? TaskFinished;

    /// <summary>任务集合变化时触发（UI 线程），供状态栏计数刷新。</summary>
    event Action? RunsChanged;

    /// <summary>读取跨会话历史（最近 200 条，最新在前）。</summary>
    IReadOnlyList<TaskHistoryEntry> GetHistory();
}

/// <summary>任务历史条目（跨会话持久化）。</summary>
public class TaskHistoryEntry
{
    public string Title { get; set; } = string.Empty;

    public string Category { get; set; } = string.Empty;

    /// <summary>状态：Completed / Failed / Cancelled。</summary>
    public string State { get; set; } = string.Empty;

    public DateTime StartedAt { get; set; }

    public DateTime? FinishedAt { get; set; }

    public string ResultSummary { get; set; } = string.Empty;
}
