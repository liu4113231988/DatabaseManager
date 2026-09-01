using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DatabaseManager.AppCore.Common;
using DatabaseManager.AppCore.Models;
using DatabaseManager.AppCore.Services;

namespace DatabaseManager.AppCore.ViewModels;

/// <summary>
/// 数据库备份 ViewModel（阶段 5）。
/// 选择连接，配置保存文件夹 / 客户端工具路径 / 是否压缩，执行数据库备份/恢复。
/// 执行经任务中心登记（可脱离本窗口观测/取消）。
/// </summary>
public partial class BackupViewModel : ToolViewModelBase
{
    private readonly IDbConnectionService _connectionService;
    private readonly IBackupService _backupService;
    private readonly ITaskCenterService _taskCenter;
    private TaskRun? _currentRun;

    /// <summary>全部已保存连接。</summary>
    public ObservableCollection<ConnectionItem> Connections { get; } = new();

    [ObservableProperty]
    private ConnectionItem? _selectedConnection;

    [ObservableProperty]
    private string _saveFolder = "Backup";

    [ObservableProperty]
    private string? _clientToolFilePath;

    [ObservableProperty]
    private string? _restoreFilePath;

    [ObservableProperty]
    private bool _zipFile = true;

    public BackupViewModel(IDbConnectionService connectionService, IBackupService backupService, ITaskCenterService taskCenter)
    {
        _connectionService = connectionService;
        _backupService = backupService;
        _taskCenter = taskCenter;
    }

    protected override void OnBusyChanged()
    {
        BackupCommand.NotifyCanExecuteChanged();
        RestoreCommand.NotifyCanExecuteChanged();
        CancelBackupCommand.NotifyCanExecuteChanged();
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

    [RelayCommand(CanExecute = nameof(CanRunBackup))]
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

        var connection = SelectedConnection;
        var saveFolder = SaveFolder;
        var clientTool = ClientToolFilePath;
        var zip = ZipFile;

        var feedbackBuffer = new List<string>();
        void CollectFeedback(string message)
        {
            lock (feedbackBuffer) feedbackBuffer.Add(message);
            _currentRun?.Report(message);
        }

        AppendLog($"连接：{connection.Description}");
        AppendLog($"保存文件夹：{saveFolder}");
        AppendLog("开始备份...");

        // 经任务中心登记：窗口中途关闭后备份仍可观测/取消。
        _currentRun = _taskCenter.Run($"备份 {connection.Description}", "备份", async (run, ct) =>
        {
            try
            {
                var result = await _backupService.BackupAsync(connection, saveFolder, clientTool, zip, CollectFeedback, ct);

                foreach (var line in feedbackBuffer)
                    AppendLog(line);

                StatusMessage = result.IsOK
                    ? $"备份成功，文件：{result.FilePath}"
                    : $"备份失败：{result.Message}";
                AppendLog(StatusMessage);
                run.ResultSummary = StatusMessage;
            }
            catch (OperationCanceledException)
            {
                StatusMessage = "备份已取消。";
                AppendLog(StatusMessage);
                throw;
            }
            catch (Exception ex)
            {
                StatusMessage = $"备份失败：{ex.Message}";
                AppendLog(StatusMessage);
                throw;
            }
            finally
            {
                _currentRun = null;
                IsBusy = false;
            }
        });
    }

    private bool CanRunBackup() => !IsBusy;

    [RelayCommand(CanExecute = nameof(CanRunBackup))]
    private async Task RestoreAsync()
    {
        if (SelectedConnection is null)
        {
            StatusMessage = "请选择连接。";
            return;
        }
        if (string.IsNullOrWhiteSpace(RestoreFilePath) || !File.Exists(RestoreFilePath))
        {
            StatusMessage = "请选择有效的备份文件。";
            return;
        }

        IsBusy = true;
        StatusMessage = string.Empty;
        Logs.Clear();

        var connection = SelectedConnection;
        var restoreFile = RestoreFilePath;
        var clientTool = ClientToolFilePath;

        var feedbackBuffer = new List<string>();
        void CollectFeedback(string message)
        {
            lock (feedbackBuffer) feedbackBuffer.Add(message);
            _currentRun?.Report(message);
        }

        AppendLog($"连接：{connection.Description}");
        AppendLog($"恢复文件：{restoreFile}");
        AppendLog("开始恢复...");

        _currentRun = _taskCenter.Run($"恢复 {connection.Description}", "恢复", async (run, ct) =>
        {
            try
            {
                var result = await _backupService.RestoreAsync(connection, restoreFile, clientTool, CollectFeedback, ct);

                foreach (var line in feedbackBuffer)
                    AppendLog(line);

                StatusMessage = result.IsOK ? "恢复成功。请重新连接并验证数据库对象。" : $"恢复失败：{result.Message}";
                AppendLog(StatusMessage);
                run.ResultSummary = StatusMessage;
            }
            catch (OperationCanceledException)
            {
                StatusMessage = "恢复已取消。";
                AppendLog(StatusMessage);
                throw;
            }
            catch (Exception ex)
            {
                StatusMessage = $"恢复失败：{ex.Message}";
                AppendLog(StatusMessage);
                throw;
            }
            finally
            {
                _currentRun = null;
                IsBusy = false;
            }
        });
    }

    [RelayCommand(CanExecute = nameof(IsBusy))]
    private void CancelBackup()
    {
        if (_currentRun is null)
        {
            return;
        }

        try
        {
            AppendLog("请求取消备份...");
            _taskCenter.Cancel(_currentRun.Id);
        }
        catch (Exception ex)
        {
            StatusMessage = $"取消失败：{ex.Message}";
            AppendLog(StatusMessage);
        }
    }
}
