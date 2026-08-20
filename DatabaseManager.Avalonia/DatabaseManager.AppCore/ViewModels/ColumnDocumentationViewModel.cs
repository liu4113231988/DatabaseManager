using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DatabaseManager.AppCore.Common;
using DatabaseManager.AppCore.Models;
using DatabaseManager.AppCore.Services;

namespace DatabaseManager.AppCore.ViewModels;

/// <summary>
/// 数据库文档生成 ViewModel（阶段 5）。
/// 选择连接与要包含的列属性，生成列结构文档（Word）。
/// </summary>
public partial class ColumnDocumentationViewModel : ViewModelBase
{
    private readonly IDbConnectionService _connectionService;
    private readonly IColumnDocumentationService _documentationService;

    /// <summary>全部已保存连接。</summary>
    public ObservableCollection<ConnectionItem> Connections { get; } = new();

    /// <summary>可勾选的列属性。</summary>
    public ObservableCollection<ColumnDocumentationProperty> Properties { get; } = new();

    /// <summary>执行日志。</summary>
    public ObservableCollection<string> Logs { get; } = new();

    [ObservableProperty]
    private ConnectionItem? _selectedConnection;

    [ObservableProperty]
    private string _filePath = string.Empty;

    [ObservableProperty]
    private bool _showTableComment = true;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    public ColumnDocumentationViewModel(
        IDbConnectionService connectionService,
        IColumnDocumentationService documentationService)
    {
        _connectionService = connectionService;
        _documentationService = documentationService;

        foreach (var property in documentationService.GetDefaultProperties())
        {
            Properties.Add(property);
        }
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
    private async Task GenerateAsync()
    {
        if (SelectedConnection is null)
        {
            StatusMessage = "请选择连接。";
            return;
        }

        if (string.IsNullOrWhiteSpace(FilePath))
        {
            StatusMessage = "请设置文档输出文件路径。";
            return;
        }

        var selected = Properties.Where(p => p.IsChecked).ToList();
        if (selected.Count == 0)
        {
            StatusMessage = "请至少勾选一个列属性。";
            return;
        }

        IsBusy = true;
        StatusMessage = string.Empty;

        var feedbackBuffer = new List<string>();
        void CollectFeedback(string message) => feedbackBuffer.Add(message);

        try
        {
            AppendLog($"连接：{SelectedConnection.Description}");
            AppendLog($"输出文件：{FilePath}");
            AppendLog("开始生成文档...");

            var result = await _documentationService.GenerateAsync(
                SelectedConnection, selected, ShowTableComment, FilePath, CollectFeedback);

            foreach (var line in feedbackBuffer)
            {
                AppendLog(line);
            }

            StatusMessage = result.IsOK
                ? $"文档生成成功：{result.FilePath}"
                : $"文档生成失败：{result.Message}";
            AppendLog(StatusMessage);
        }
        catch (Exception ex)
        {
            StatusMessage = $"文档生成失败：{ex.Message}";
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
