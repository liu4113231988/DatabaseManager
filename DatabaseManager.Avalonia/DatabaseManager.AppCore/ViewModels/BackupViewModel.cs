using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DatabaseManager.AppCore.Common;
using DatabaseManager.AppCore.Models;
using DatabaseManager.AppCore.Services;

namespace DatabaseManager.AppCore.ViewModels;

/// <summary>
/// 数据库备份 ViewModel（阶段 5）。
/// 选择连接，配置保存文件夹 / 客户端工具路径 / 是否压缩，执行数据库备份。
/// </summary>
public partial class BackupViewModel : ViewModelBase
{
    private readonly IDbConnectionService _connectionService;
    private readonly IBackupService _backupService;

    /// <summary>全部已保存连接。</summary>
    public ObservableCollection<ConnectionItem> Connections { get; } = new();

    /// <summary>执行日志。</summary>
    public ObservableCollection<string> Logs { get; } = new();

    [ObservableProperty]
    private ConnectionItem? _selectedConnection;

    [ObservableProperty]
    private string _saveFolder = "Backup";

    [ObservableProperty]
    private string? _clientToolFilePath;

    [ObservableProperty]
    private bool _zipFile = true;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    public BackupViewModel(IDbConnectionService connectionService, IBackupService backupService)
    {
        _connectionService = connectionService;
        _backupService = backupService;
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
    private async Task BackupAsync()
    {
        if (SelectedConnection is null)
        {
            StatusMessage = "请选择连接。";
            return;
        }

        if (string.IsNullOrWhiteSpace(SaveFolder))
        {
            StatusMessage = "请设置备份保存文件夹。";
            return;
        }

        IsBusy = true;
        StatusMessage = string.Empty;
        Logs.Clear();

        var feedbackBuffer = new List<string>();
        void CollectFeedback(string message) => feedbackBuffer.Add(message);

        try
        {
            AppendLog($"连接：{SelectedConnection.Description}");
            AppendLog($"保存文件夹：{SaveFolder}");
            AppendLog("开始备份...");

            var result = await _backupService.BackupAsync(
                SelectedConnection, SaveFolder, ClientToolFilePath, ZipFile, CollectFeedback);

            foreach (var line in feedbackBuffer)
            {
                AppendLog(line);
            }

            StatusMessage = result.IsOK
                ? $"备份成功，文件：{result.FilePath}"
                : $"备份失败：{result.Message}";
            AppendLog(StatusMessage);
        }
        catch (Exception ex)
        {
            StatusMessage = $"备份失败：{ex.Message}";
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
