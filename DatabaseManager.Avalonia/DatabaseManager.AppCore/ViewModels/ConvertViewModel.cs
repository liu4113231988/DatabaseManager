using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DatabaseManager.AppCore.Common;
using DatabaseManager.AppCore.Models;
using DatabaseManager.AppCore.Services;

namespace DatabaseManager.AppCore.ViewModels;

/// <summary>
/// 数据库转换 ViewModel（阶段 4）。
/// 跨库结构/数据转换：选择源/目标连接、转换模式与选项，执行转换并展示反馈日志。
/// </summary>
public partial class ConvertViewModel : ViewModelBase
{
    private readonly IDbConnectionService _connectionService;
    private readonly IConvertService _convertService;

    /// <summary>全部已保存连接（源/目标下拉共用）。</summary>
    public ObservableCollection<ConnectionItem> Connections { get; } = new();

    /// <summary>可用的转换模式。</summary>
    public IReadOnlyList<ConvertModeOption> Modes { get; }

    /// <summary>转换日志。</summary>
    public ObservableCollection<string> Logs { get; } = new();

    [ObservableProperty]
    private ConnectionItem? _sourceConnection;

    [ObservableProperty]
    private ConnectionItem? _targetConnection;

    [ObservableProperty]
    private ConvertModeOption? _selectedMode;

    [ObservableProperty]
    private bool _executeOnTargetServer = true;

    [ObservableProperty]
    private bool _useTransaction;

    [ObservableProperty]
    private bool _bulkCopy;

    [ObservableProperty]
    private bool _continueWhenErrorOccurs;

    [ObservableProperty]
    private bool _createSchemaIfNotExists;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    public ConvertViewModel(IDbConnectionService connectionService, IConvertService convertService)
    {
        _connectionService = connectionService;
        _convertService = convertService;

        Modes = new[]
        {
            new ConvertModeOption(ConvertMode.Schema, "仅结构 (Schema)"),
            new ConvertModeOption(ConvertMode.Data, "仅数据 (Data)"),
            new ConvertModeOption(ConvertMode.SchemaAndData, "结构 + 数据"),
        };

        SelectedMode = Modes.Last();
    }

    /// <summary>加载已保存的连接并刷新源/目标选择。</summary>
    public void RefreshConnections()
    {
        var previousSourceId = SourceConnection?.Id;
        var previousTargetId = TargetConnection?.Id;

        Connections.Clear();
        foreach (var item in _connectionService.GetConnections())
        {
            Connections.Add(item);
        }

        SourceConnection = FindConnection(previousSourceId) ?? Connections.FirstOrDefault();
        TargetConnection = FindConnection(previousTargetId) ?? Connections.Skip(1).FirstOrDefault();
    }

    private ConnectionItem? FindConnection(string? id)
        => Connections.FirstOrDefault(c => c.Id == id);

    [RelayCommand]
    private async Task ExecuteAsync()
    {
        if (SelectedMode is null)
        {
            StatusMessage = "请选择转换模式。";
            return;
        }

        if (SourceConnection is null || TargetConnection is null)
        {
            StatusMessage = "请选择源连接和目标连接。";
            return;
        }

        IsBusy = true;
        StatusMessage = string.Empty;
        Logs.Clear();

        try
        {
            var options = new ConvertOptions
            {
                ExecuteScriptOnTargetServer = ExecuteOnTargetServer,
                UseTransaction = UseTransaction,
                BulkCopy = BulkCopy,
                ContinueWhenErrorOccurs = ContinueWhenErrorOccurs,
                CreateSchemaIfNotExists = CreateSchemaIfNotExists,
            };

            // 转换过程反馈在后台线程触发，这里先收集到临时缓冲，
            // await 回到 UI 线程后一次性刷新到 Logs，避免跨线程修改 UI 集合。
            var feedbackBuffer = new List<string>();
            void CollectFeedback(string message) => feedbackBuffer.Add(message);

            AppendLog($"源：{SourceConnection.Description}");
            AppendLog($"目标：{TargetConnection.Description}");
            AppendLog($"模式：{SelectedMode.DisplayName}");
            AppendLog("开始转换...");

            var result = await _convertService.ConvertAsync(
                SourceConnection,
                TargetConnection,
                SelectedMode.Value,
                options,
                CollectFeedback);

            foreach (var line in feedbackBuffer)
            {
                AppendLog(line);
            }

            if (result.IsCanceled)
            {
                StatusMessage = "转换已取消。";
            }
            else
            {
                StatusMessage = result.Message;
                AppendLog(result.Message);
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"转换失败：{ex.Message}";
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

/// <summary>转换模式下拉选项。</summary>
public sealed record ConvertModeOption(string Value, string DisplayName);
