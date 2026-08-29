using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DatabaseManager.AppCore.Common;
using DatabaseManager.AppCore.Models;
using DatabaseManager.AppCore.Services;

namespace DatabaseManager.AppCore.ViewModels;

/// <summary>
/// 脚本预览 ViewModel：展示由结构/数据对比生成的可审阅同步脚本，
/// 支持勾选、保存脚本文件与执行选中脚本（带执行日志）。
/// </summary>
public partial class ScriptPreviewViewModel : ViewModelBase
{
    private readonly ISyncScriptService? _syncScriptService;
    private readonly ITaskCenterService? _taskCenter;

    /// <summary>脚本条目列表（可勾选）。</summary>
    public ObservableCollection<ScriptItem> Scripts { get; } = new();

    /// <summary>执行日志。</summary>
    public ObservableCollection<string> Logs { get; } = new();

    /// <summary>目标连接（执行目标）。</summary>
    public ConnectionItem? TargetConnection { get; set; }

    /// <summary>目标连接显示名。</summary>
    public string TargetDescription => TargetConnection?.Description ?? string.Empty;

    /// <summary>脚本来源说明（如「结构对比 → 目标库」）。</summary>
    [ObservableProperty]
    private string _sourceDescription = string.Empty;

    [ObservableProperty]
    private ScriptItem? _selectedScript;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    /// <summary>由 UI 注入的执行前确认回调（返回 true 表示确认执行）。</summary>
    public Func<Task<bool>>? RequestExecuteConfirm { get; set; }

    /// <summary>当前选中的脚本 SQL 文本（预览区显示；无选中时为空）。</summary>
    public string SelectedScriptText => SelectedScript?.SqlText ?? string.Empty;

    public ScriptPreviewViewModel(ISyncScriptService? syncScriptService, ITaskCenterService? taskCenter = null)
    {
        _syncScriptService = syncScriptService;
        _taskCenter = taskCenter;
    }

    partial void OnSelectedScriptChanged(ScriptItem? value)
    {
        OnPropertyChanged(nameof(SelectedScriptText));
    }

    /// <summary>是否存在任何脚本。</summary>
    public bool HasScripts => Scripts.Count > 0;

    [RelayCommand]
    private void SelectAll()
    {
        foreach (var s in Scripts)
        {
            s.IsSelected = true;
        }
    }

    [RelayCommand]
    private void SelectNone()
    {
        foreach (var s in Scripts)
        {
            s.IsSelected = false;
        }
    }

    /// <summary>执行勾选的脚本（执行前经 UI 确认）。</summary>
    [RelayCommand(CanExecute = nameof(CanExecuteScripts))]
    private async Task ExecuteSelectedAsync()
    {
        if (_syncScriptService is null || TargetConnection is null)
        {
            StatusMessage = "缺少执行服务或目标连接。";
            return;
        }

        var selected = Scripts.Where(s => s.IsSelected).ToList();
        if (selected.Count == 0)
        {
            StatusMessage = "请勾选要执行的脚本。";
            return;
        }

        if (RequestExecuteConfirm is not null && !await RequestExecuteConfirm())
        {
            StatusMessage = "已取消执行。";
            return;
        }

        IsBusy = true;
        StatusMessage = "正在执行脚本...";

        var feedbackBridge = new List<string>();
        void CollectFeedback(string message)
        {
            feedbackBridge.Add(message);
            AppendLog(message);
        }

        // 经任务中心登记：预览窗口中途关闭后脚本仍在执行且可从任务中心取消/观测。
        _taskCenter?.Run($"执行同步脚本（{selected.Count} 项）→ {TargetConnection?.Description}", "脚本执行", async (run, ct) =>
        {
            try
            {
                var result = await _syncScriptService!.ExecuteScriptsAsync(TargetConnection!, selected, CollectFeedback, ct);

                StatusMessage = result.IsSuccess ? result.Message : $"执行失败：{result.Message}";
                AppendLog(StatusMessage);
                run.ResultSummary = StatusMessage;
            }
            catch (OperationCanceledException)
            {
                StatusMessage = "执行已取消。";
                AppendLog(StatusMessage);
                throw;
            }
            catch (Exception ex)
            {
                StatusMessage = $"执行失败：{ex.Message}";
                AppendLog(StatusMessage);
                throw;
            }
            finally
            {
                IsBusy = false;
            }
        });

        if (_taskCenter is null)
        {
            // 无任务中心（极端场景）：退化为内联执行。
            try
            {
                var result = await _syncScriptService!.ExecuteScriptsAsync(TargetConnection!, selected, AppendLog);
                StatusMessage = result.IsSuccess ? result.Message : $"执行失败：{result.Message}";
            }
            catch (Exception ex)
            {
                StatusMessage = $"执行失败：{ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }
    }

    private bool CanExecuteScripts() => !IsBusy && TargetConnection is not null;

    partial void OnIsBusyChanged(bool value)
        => ExecuteSelectedCommand.NotifyCanExecuteChanged();

    /// <summary>把全部脚本（含勾选状态标记）保存到文件（路径由 UI 提供）。</summary>
    public async Task SaveScriptsToFileAsync(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || Scripts.Count == 0)
        {
            return;
        }

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"-- DatabaseManager 导出脚本  {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        if (!string.IsNullOrEmpty(SourceDescription))
        {
            sb.AppendLine($"-- 来源：{SourceDescription}");
        }
        sb.AppendLine();

        foreach (var script in Scripts)
        {
            sb.AppendLine($"-- ========== {(script.IsSelected ? "[已选]" : "[未选]")} {script.Title} ==========");
            if (!string.IsNullOrEmpty(script.Description))
            {
                sb.AppendLine($"-- {script.Description}");
            }
            sb.AppendLine(script.SqlText);
            sb.AppendLine();
        }

        await File.WriteAllTextAsync(filePath, sb.ToString());
        StatusMessage = $"脚本已保存：{filePath}";
        AppendLog(StatusMessage);
    }

    private void AppendLog(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        var time = DateTime.Now.ToString("HH:mm:ss");
        Logs.Add($"[{time}] {message}");
    }
}
