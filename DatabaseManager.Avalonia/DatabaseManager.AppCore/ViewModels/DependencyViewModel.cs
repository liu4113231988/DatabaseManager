using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DatabaseManager.AppCore.Common;
using DatabaseManager.AppCore.Models;
using DatabaseManager.AppCore.Services;

namespace DatabaseManager.AppCore.ViewModels;

/// <summary>
/// 依赖分析 ViewModel（阶段 4）。
/// 指定数据库对象并分析其依赖关系（谁依赖它 / 它依赖谁）。
/// </summary>
public partial class DependencyViewModel : ViewModelBase
{
    private readonly IDbConnectionService _connectionService;
    private readonly IDependencyService _dependencyService;

    /// <summary>全部已保存连接。</summary>
    public ObservableCollection<ConnectionItem> Connections { get; } = new();

    /// <summary>对象类型选项。</summary>
    public ObservableCollection<string> ObjectTypes { get; } = new()
    {
        "Table",
        "View",
        "Function",
        "Procedure",
    };

    /// <summary>依赖方向选项。</summary>
    public ObservableCollection<DependencyDirectionOption> Directions { get; } = new()
    {
        new("依赖此对象的对象 (Incoming)", true),
        new("此对象依赖的对象 (Outgoing)", false),
    };

    /// <summary>依赖关系树。</summary>
    public ObservableCollection<DependencyNode> Nodes { get; } = new();

    /// <summary>执行日志。</summary>
    public ObservableCollection<string> Logs { get; } = new();

    [ObservableProperty]
    private ConnectionItem? _selectedConnection;

    [ObservableProperty]
    private string _selectedObjectType = "Table";

    [ObservableProperty]
    private string _schema = string.Empty;

    [ObservableProperty]
    private string _objectName = string.Empty;

    [ObservableProperty]
    private DependencyDirectionOption? _selectedDirection;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    public DependencyViewModel(IDbConnectionService connectionService, IDependencyService dependencyService)
    {
        _connectionService = connectionService;
        _dependencyService = dependencyService;
        SelectedDirection = Directions.FirstOrDefault();
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

        if (string.IsNullOrWhiteSpace(ObjectName))
        {
            StatusMessage = "请输入要分析的对象名。";
            return;
        }

        if (SelectedDirection is null)
        {
            StatusMessage = "请选择依赖方向。";
            return;
        }

        IsBusy = true;
        StatusMessage = string.Empty;
        Nodes.Clear();

        try
        {
            AppendLog($"连接：{SelectedConnection.Description}");
            AppendLog($"对象：{Schema}.{ObjectName}（{SelectedObjectType}）");
            AppendLog($"方向：{SelectedDirection.DisplayName}");
            AppendLog("开始分析依赖关系...");

            var nodes = await _dependencyService.FetchAsync(
                SelectedConnection, SelectedObjectType,
                string.IsNullOrWhiteSpace(Schema) ? null : Schema,
                ObjectName.Trim(), SelectedDirection.DependOnThis);

            foreach (var node in nodes)
            {
                Nodes.Add(node);
            }

            StatusMessage = $"分析完成，共 {nodes.Count} 个依赖对象。";
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

    private void AppendLog(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;

        var time = DateTime.Now.ToString("HH:mm:ss");
        Logs.Add($"[{time}] {message}");
    }
}
