using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using DatabaseManager.AppCore.Common;
using DatabaseManager.AppCore.Models;
using DatabaseManager.AppCore.Services;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;

namespace DatabaseManager.Avalonia.Views;

/// <summary>
/// 会话与锁监控窗口：查看活动会话与阻塞链，支持终止会话（按数据库类型适配）。
/// </summary>
public partial class SessionMonitorWindow : Window
{
    private readonly IDbSessionService _sessionService;
    private readonly IDbConnectionService _connectionService;
    private readonly DispatcherTimer _autoRefreshTimer;

    public SessionMonitorWindow()
    {
        InitializeComponent();
    }

    public SessionMonitorWindow(IDbSessionService sessionService, IDbConnectionService connectionService)
        : this()
    {
        _sessionService = sessionService;
        _connectionService = connectionService;

        var connections = _connectionService.GetConnections()
            .Where(c => _sessionService.IsSupported(c.DatabaseType))
            .ToList();
        CmbConnection.ItemsSource = connections;

        if (connections.Count == 0)
        {
            TxtSummary.Text = "没有支持会话监控的连接（SQLite 不支持）。";
            BtnRefresh.IsEnabled = false;
        }

        _autoRefreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(10) };
        _autoRefreshTimer.Tick += async (_, _) => await RefreshAsync();
        ChkAutoRefresh.IsCheckedChanged += (_, _) => UpdateAutoRefreshTimer();
        TxtInterval.TextChanged += (_, _) => UpdateAutoRefreshTimer();
    }

    private void UpdateAutoRefreshTimer()
    {
        int seconds = int.TryParse(TxtInterval.Text, out int s) ? Math.Clamp(s, 3, 600) : 10;
        _autoRefreshTimer.Interval = TimeSpan.FromSeconds(seconds);

        if (ChkAutoRefresh.IsChecked == true && CmbConnection.SelectedItem is ConnectionItem)
        {
            if (!_autoRefreshTimer.IsEnabled)
            {
                _autoRefreshTimer.Start();
            }
        }
        else
        {
            _autoRefreshTimer.Stop();
        }
    }

    private async void BtnRefresh_Click(object? sender, RoutedEventArgs e) => await RefreshAsync();

    private async Task RefreshAsync()
    {
        if (CmbConnection.SelectedItem is not ConnectionItem connection)
        {
            TxtSummary.Text = "请先选择连接。";
            return;
        }

        BtnRefresh.IsEnabled = false;
        TxtSummary.Text = "正在读取会话...";

        try
        {
            var snapshot = await _sessionService.GetSnapshotAsync(connection);

            SessionsGrid.ItemsSource = snapshot.Sessions;
            LocksGrid.ItemsSource = snapshot.Locks;

            TxtSummary.Text = snapshot.Error is not null
                ? $"会话 {snapshot.Sessions.Count} 个；{snapshot.Error}"
                : $"会话 {snapshot.Sessions.Count} 个，锁/阻塞 {snapshot.Locks.Count} 条。";
        }
        catch (Exception ex)
        {
            TxtSummary.Text = $"读取失败：{ex.Message}";
        }
        finally
        {
            BtnRefresh.IsEnabled = true;
        }
    }

    private async void BtnKill_Click(object? sender, RoutedEventArgs e)
    {
        if (CmbConnection.SelectedItem is not ConnectionItem connection)
        {
            await ShowErrorAsync("请先选择连接。");
            return;
        }

        if (SessionsGrid.SelectedItem is not DbSessionInfo session
            || string.IsNullOrWhiteSpace(session.SessionId))
        {
            await ShowErrorAsync("请先在会话列表中选择要终止的会话。");
            return;
        }

        var confirm = await DialogHelper.ShowConfirmAsync(
            "终止会话",
            $"确定要终止会话 {session.SessionId}（用户：{session.User}）吗？该操作会回滚其未完成事务，不可撤销。");

        if (confirm != true)
        {
            return;
        }

        var (success, error) = await _sessionService.KillSessionAsync(connection, session.SessionId);
        if (success)
        {
            await ShowInfoAsync($"已终止会话 {session.SessionId}。");
            await RefreshAsync();
        }
        else
        {
            await ShowErrorAsync($"终止失败：{error}");
        }
    }

    private void BtnClose_Click(object? sender, RoutedEventArgs e)
    {
        _autoRefreshTimer.Stop();
        Close();
    }

    protected override void OnClosed(EventArgs e)
    {
        _autoRefreshTimer.Stop();
        base.OnClosed(e);
    }

    private Task ShowInfoAsync(string message)
        => MessageBoxManager.GetMessageBoxStandard("提示", message, ButtonEnum.Ok, MsBox.Avalonia.Enums.Icon.Info)
            .ShowWindowDialogAsync(this);

    private Task ShowErrorAsync(string message)
        => MessageBoxManager.GetMessageBoxStandard("错误", message, ButtonEnum.Ok, MsBox.Avalonia.Enums.Icon.Error)
            .ShowWindowDialogAsync(this);
}
