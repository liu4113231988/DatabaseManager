using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using DatabaseManager.AppCore.Common;
using DatabaseManager.AppCore.Models;
using DatabaseManager.AppCore.Services;

namespace DatabaseManager.AppCore.ViewModels;

/// <summary>
/// 主窗口 ViewModel（AppCore 层）。
/// 阶段 2：整合对象浏览器与查询编辑器子 ViewModel，负责主界面状态与连接选择联动。
/// </summary>
public partial class MainWindowViewModel : ViewModelBase
{
    private readonly IDbSchemaService _schemaService;
    private readonly IDbConnectionService _connectionService;

    /// <summary>主界面左侧"对象浏览器"当前展示的连接集合。</summary>
    public ObservableCollection<ConnectionItem> Connections { get; } = new();

    /// <summary>对象浏览器子 ViewModel。</summary>
    public ObjectsExplorerViewModel ObjectsExplorer { get; }

    /// <summary>查询编辑器子 ViewModel。</summary>
    public QueryEditorViewModel QueryEditor { get; }

    [ObservableProperty]
    private string _supportedDatabases = string.Empty;

    [ObservableProperty]
    private string _connectionSummary = string.Empty;

    [ObservableProperty]
    private ConnectionItem? _selectedConnection;

    public MainWindowViewModel(
        IDbSchemaService schemaService,
        IDbConnectionService connectionService,
        ObjectsExplorerViewModel objectsExplorer,
        QueryEditorViewModel queryEditor)
    {
        _schemaService = schemaService;
        _connectionService = connectionService;
        ObjectsExplorer = objectsExplorer;
        QueryEditor = queryEditor;

        PropertyChanged += MainWindowViewModel_PropertyChanged;
    }

    private void MainWindowViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        // 选中连接变化时，联动刷新对象树与查询目标连接。
        if (e.PropertyName == nameof(SelectedConnection))
        {
            OnSelectedConnectionChanged();
        }
    }

    private async void OnSelectedConnectionChanged()
    {
        var connection = SelectedConnection;
        if (connection is null)
            return;

        QueryEditor.ConnectionName = connection.Name;
        await ObjectsExplorer.LoadAsync(connection.Name);
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
