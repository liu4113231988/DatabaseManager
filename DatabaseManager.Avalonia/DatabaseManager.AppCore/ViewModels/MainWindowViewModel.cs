using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using DatabaseManager.AppCore.Common;
using DatabaseManager.AppCore.Models;
using DatabaseManager.AppCore.Services;

namespace DatabaseManager.AppCore.ViewModels;

/// <summary>
/// 主窗口 ViewModel（AppCore 层）。
/// 阶段 1：注入连接服务，展示受支持数据库类型、已保存连接数量；负责主界面状态展示。
/// </summary>
public partial class MainWindowViewModel : ViewModelBase
{
    private readonly IDbSchemaService _schemaService;
    private readonly IDbConnectionService _connectionService;

    /// <summary>主界面左侧"对象浏览器"当前展示的连接集合。</summary>
    public ObservableCollection<ConnectionItem> Connections { get; } = new();

    [ObservableProperty]
    private string _supportedDatabases = string.Empty;

    [ObservableProperty]
    private string _connectionSummary = string.Empty;

    [ObservableProperty]
    private ConnectionItem? _selectedConnection;

    public MainWindowViewModel(IDbSchemaService schemaService, IDbConnectionService connectionService)
    {
        _schemaService = schemaService;
        _connectionService = connectionService;
    }

    /// <summary>初始化：枚举受支持的数据库类型，并加载已保存连接。</summary>
    public void Initialize()
    {
        SupportedDatabases = string.Join(", ", _schemaService.GetSupportedDatabaseTypes());
        RefreshConnections();
    }

    /// <summary>刷新左侧对象浏览器的连接列表。</summary>
    public void RefreshConnections()
    {
        Connections.Clear();

        var items = _connectionService.GetConnections();
        foreach (var item in items)
        {
            Connections.Add(item);
        }

        ConnectionSummary = $"{Connections.Count} 个已保存连接";
    }

    public void TestSelectedConnection()
    {
        // 连接测试在 ConnectWindow 中交互式进行；此处预留快速入口。
    }
}
