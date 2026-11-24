using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using DatabaseManager.AppCore.Common;
using DatabaseManager.AppCore.Models;
using DatabaseManager.AppCore.Services;

namespace DatabaseManager.AppCore.ViewModels;

/// <summary>
/// 工具类 ViewModel 基类：统一 IsBusy / StatusMessage / 日志集合 / 取消令牌源 的管理与释放，
/// 供转换、导入导出、备份、统计等耗时操作 VM 复用（消除逐份重复的样板代码）。
/// </summary>
public abstract partial class ToolViewModelBase : ViewModelBase
{
    private bool _isBusy;
    private CancellationTokenSource? _connectionRefresh;

    /// <summary>是否正在执行（驱动按钮禁用态与取消按钮）。</summary>
    public bool IsBusy
    {
        get => _isBusy;
        set
        {
            if (SetProperty(ref _isBusy, value))
            {
                OnBusyChanged();
            }
        }
    }

    /// <summary>IsBusy 变化通知（派生类用于刷新取消按钮的 CanExecute）。</summary>
    protected virtual void OnBusyChanged()
    {
    }

    /// <summary>状态栏消息。</summary>
    [ObservableProperty]
    private string _statusMessage = string.Empty;

    /// <summary>执行日志。</summary>
    public ObservableCollection<string> Logs { get; } = new();

    protected async Task<bool> RefreshConnectionListAsync(IDbConnectionService service, ObservableCollection<ConnectionItem> connections,
        Action restoreSelection, CancellationToken cancellationToken)
    {
        if (IsBusy)
            return false;

        using var refresh = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _connectionRefresh = refresh;
        IsBusy = true;
        const string loadingMessage = "正在读取连接...";
        StatusMessage = loadingMessage;
        try
        {
            var items = await service.GetConnectionsAsync(refresh.Token);
            refresh.Token.ThrowIfCancellationRequested();
            connections.Clear();
            foreach (var item in items)
                connections.Add(item);
            restoreSelection();
            if (StatusMessage == loadingMessage)
                StatusMessage = items.Count == 0 ? "没有已保存的连接。" : string.Empty;
            return true;
        }
        catch (OperationCanceledException) when (refresh.IsCancellationRequested)
        {
            StatusMessage = "已取消读取连接。";
            return false;
        }
        catch (Exception ex)
        {
            StatusMessage = $"读取连接失败：{ex.Message}";
            return false;
        }
        finally
        {
            _connectionRefresh = null;
            IsBusy = false;
        }
    }

    public void CancelConnectionRefresh() => _connectionRefresh?.Cancel();

    /// <summary>当前执行对应的取消令牌源（无任务时为 null）。</summary>
    protected CancellationTokenSource? BusyCts { get; private set; }

    /// <summary>当前执行的取消令牌（无任务时为 CancellationToken.None）。</summary>
    protected CancellationToken BusyToken => BusyCts?.Token ?? CancellationToken.None;

    /// <summary>清空日志。</summary>
    protected void ClearLogs() => Logs.Clear();

    /// <summary>追加一条带时间戳的日志（须在 UI 线程调用）。</summary>
    protected void AppendLog(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;

        var time = DateTime.Now.ToString("HH:mm:ss");
        Logs.Add($"[{time}] {message}");
    }

    /// <summary>批量追加缓冲的日志行（后台反馈先收集、await 回 UI 线程后一次刷入）。</summary>
    protected void AppendLogs(IEnumerable<string> messages)
    {
        foreach (var message in messages)
        {
            AppendLog(message);
        }
    }

    /// <summary>开始一次执行：置忙碌态并创建取消令牌源。返回取消令牌。</summary>
    protected CancellationToken BeginBusy()
    {
        IsBusy = true;
        BusyCts = new CancellationTokenSource();
        return BusyCts.Token;
    }

    /// <summary>结束一次执行：释放并清空取消令牌源，恢复空闲态。</summary>
    protected void EndBusy()
    {
        BusyCts?.Dispose();
        BusyCts = null;
        IsBusy = false;
    }

    /// <summary>请求取消当前执行（无执行中任务时忽略）。</summary>
    protected void CancelBusy()
    {
        if (BusyCts is { IsCancellationRequested: false })
        {
            StatusMessage = "正在取消...";
            BusyCts.Cancel();
        }
    }
}
