using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DatabaseManager.AppCore.Common;
using DatabaseManager.AppCore.Models;
using DatabaseManager.AppCore.Services;

namespace DatabaseManager.AppCore.ViewModels;

/// <summary>
/// 索引碎片分析 ViewModel（阶段 5）。
/// 选择连接并分析索引碎片，支持重建选中的索引。
/// </summary>
public partial class IndexFragmentationViewModel : ViewModelBase
{
    private readonly IDbConnectionService _connectionService;
    private readonly IIndexFragmentationService _indexFragmentationService;

    /// <summary>全部已保存连接。</summary>
    public ObservableCollection<ConnectionItem> Connections { get; } = new();

    /// <summary>索引碎片结果。</summary>
    public ObservableCollection<IndexFragmentationItem> Results { get; } = new();

    /// <summary>执行日志。</summary>
    public ObservableCollection<string> Logs { get; } = new();

    [ObservableProperty]
    private ConnectionItem? _selectedConnection;

    [ObservableProperty]
    private IndexFragmentationItem? _selectedResult;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    public IndexFragmentationViewModel(
        IDbConnectionService connectionService,
        IIndexFragmentationService indexFragmentationService)
    {
        _connectionService = connectionService;
        _indexFragmentationService = indexFragmentationService;
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

    [RelayCommand]
    private async Task AnalyzeAsync()
    {
        if (SelectedConnection is null)
        {
            StatusMessage = "请选择连接。";
            return;
        }

        IsBusy = true;
        StatusMessage = string.Empty;
        Results.Clear();

        var feedbackBuffer = new List<string>();
        void CollectFeedback(string message) => feedbackBuffer.Add(message);

        try
        {
            AppendLog($"连接：{SelectedConnection.Description}");
            AppendLog("开始分析索引碎片...");

            var results = await _indexFragmentationService.GetIndexFragmentationsAsync(
                SelectedConnection, CollectFeedback);

            foreach (var line in feedbackBuffer)
            {
                AppendLog(line);
            }

            foreach (var result in results)
            {
                Results.Add(result);
            }

            StatusMessage = $"分析完成，共 {results.Count} 个碎片索引。";
            AppendLog(StatusMessage);
        }
        catch (Exception ex)
        {
            StatusMessage = $"分析失败：{ex.Message}";
            AppendLog(StatusMessage);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task RebuildAsync()
    {
        if (SelectedConnection is null)
        {
            StatusMessage = "请选择连接。";
            return;
        }

        if (SelectedResult is null)
        {
            StatusMessage = "请先在结果中选中要重建的索引。";
            return;
        }

        IsBusy = true;
        StatusMessage = string.Empty;

        try
        {
            AppendLog($"开始重建索引：{SelectedResult.DisplayTableName}.{SelectedResult.IndexName} ...");

            var (isOK, message) = await _indexFragmentationService.RebuildIndexAsync(
                SelectedConnection, SelectedResult);

            StatusMessage = isOK
                ? $"索引 {SelectedResult.IndexName} 重建成功。"
                : $"索引重建失败：{message}";
            AppendLog(StatusMessage);
        }
        catch (Exception ex)
        {
            StatusMessage = $"重建失败：{ex.Message}";
            AppendLog(StatusMessage);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void AppendLog(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;

        var time = DateTime.Now.ToString("HH:mm:ss");
        Logs.Add($"[{time}] {message}");
    }
}
