using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DatabaseManager.AppCore.Common;
using DatabaseManager.AppCore.Models;
using DatabaseManager.AppCore.Services;

namespace DatabaseManager.AppCore.ViewModels;

/// <summary>
/// 数据库优化 ViewModel（阶段 4）。
/// 选择连接并执行数据库优化，查看优化前后的数据长度。
/// </summary>
public partial class OptimizeViewModel : ToolViewModelBase
{
    private readonly IDbConnectionService _connectionService;
    private readonly IOptimizeService _optimizeService;

    /// <summary>全部已保存连接。</summary>
    public ObservableCollection<ConnectionItem> Connections { get; } = new();

    /// <summary>优化结果。</summary>
    public ObservableCollection<OptimizeResultItem> Results { get; } = new();

    [ObservableProperty]
    private ConnectionItem? _selectedConnection;

    public OptimizeViewModel(IDbConnectionService connectionService, IOptimizeService optimizeService)
    {
        _connectionService = connectionService;
        _optimizeService = optimizeService;
    }

    /// <summary>加载已保存连接并刷新选择。</summary>
    public Task<bool> RefreshConnectionsAsync(CancellationToken cancellationToken = default)
    {
        var previousId = SelectedConnection?.Id;

        return RefreshConnectionListAsync(_connectionService, Connections, () =>
        {
            SelectedConnection = Connections.FirstOrDefault(c => c.Id == previousId) ?? Connections.FirstOrDefault();
        }, cancellationToken);
    }

    [RelayCommand]
    private async Task ExecuteAsync()
    {
        if (SelectedConnection is null)
        {
            StatusMessage = "请选择连接。";
            return;
        }

        IsBusy = true;
        StatusMessage = string.Empty;
        Results.Clear();
        Logs.Clear();

        var feedbackBuffer = new List<string>();
        void CollectFeedback(string message) => feedbackBuffer.Add(message);

        try
        {
            AppendLog($"连接：{SelectedConnection.Description}");
            AppendLog("开始优化...");

            var results = await _optimizeService.OptimizeAsync(SelectedConnection, CollectFeedback);

            foreach (var line in feedbackBuffer)
            {
                AppendLog(line);
            }

            foreach (var result in results)
            {
                Results.Add(result);
            }

            var okCount = results.Count(r => r.IsOK);
            var failCount = results.Count - okCount;
            if (results.Count > 0 && okCount == 0)
            {
                StatusMessage = $"优化失败，共 {results.Count} 个对象全部失败。";
                AppendLog(StatusMessage);
            }
            else if (failCount > 0)
            {
                StatusMessage = $"优化完成，共处理 {results.Count} 个对象（成功 {okCount}，失败 {failCount}）。";
                AppendLog(StatusMessage);
            }
            else
            {
                StatusMessage = $"优化完成，共处理 {results.Count} 个对象。";
                AppendLog(StatusMessage);
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"优化失败：{ex.Message}";
            AppendLog(StatusMessage);
        }
        finally
        {
            IsBusy = false;
        }
    }

}
