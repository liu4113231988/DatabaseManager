using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DatabaseInterpreter.Model;
using DatabaseManager.AppCore.Common;
using DatabaseManager.AppCore.Models;
using DatabaseManager.AppCore.Services;

namespace DatabaseManager.AppCore.ViewModels;

/// <summary>
/// 主窗口 ViewModel（AppCore 层）。
/// 阶段 1：注入连接服务，展示受支持数据库类型、已保存连接数量；负责主界面状态展示。
/// 阶段 7（DBeaver 对齐）：支持多查询标签页管理。
/// </summary>
public partial class MainWindowViewModel : ViewModelBase
{
    private readonly IDbSchemaService _schemaService;
    private readonly IDbConnectionService _connectionService;
    private readonly IQueryService _queryService;

    /// <summary>主界面左侧"对象浏览器"当前展示的连接集合。</summary>
    public ObservableCollection<ConnectionItem> Connections { get; } = new();

    /// <summary>查询标签页集合（对齐 DBeaver 多标签设计）。</summary>
    public ObservableCollection<QueryTabViewModel> QueryTabs { get; } = new();

    [ObservableProperty]
    private string _supportedDatabases = string.Empty;

    [ObservableProperty]
    private string _connectionSummary = string.Empty;

    [ObservableProperty]
    private ConnectionItem? _selectedConnection;

    [ObservableProperty]
    private QueryTabViewModel? _selectedQueryTab;

    /// <summary>对象浏览器子 ViewModel。</summary>
    public ObjectsExplorerViewModel ObjectsExplorer { get; }

    /// <summary>查询编辑器子 ViewModel（向后兼容，指向当前选中的标签）。</summary>
    public QueryEditorViewModel QueryEditor { get; }

    /// <summary>数据编辑器子 ViewModel。</summary>
    public DataEditorViewModel DataEditor { get; }

    [ObservableProperty]
    private bool _isConnected;

    /// <summary>当前使用的数据库名（用于 Schema 选择器展示）。</summary>
    [ObservableProperty]
    private string _currentDatabase = string.Empty;

    /// <summary>当前使用的 Schema（多 Schema 数据库如 public）。</summary>
    [ObservableProperty]
    private string _currentSchema = string.Empty;

    /// <summary>是否允许使用 Schema 选择器。</summary>
    [ObservableProperty]
    private bool _schemaSelectorVisible;

    /// <summary>最近打开的 SQL 脚本文件路径。</summary>
    public ObservableCollection<string> RecentScripts { get; } = new();

    /// <summary>请求关闭标签页的回调（用于未保存提示）。</summary>
    public Func<QueryTabViewModel, Task<bool>>? RequestCloseTab { get; set; }

    public MainWindowViewModel(
        IDbSchemaService schemaService,
        IDbConnectionService connectionService,
        IQueryService queryService,
        ObjectsExplorerViewModel objectsExplorer,
        QueryEditorViewModel queryEditor,
        DataEditorViewModel dataEditor)
    {
        _schemaService = schemaService;
        _connectionService = connectionService;
        _queryService = queryService;
        ObjectsExplorer = objectsExplorer;
        QueryEditor = queryEditor;
        DataEditor = dataEditor;
    }

    /// <summary>初始化：枚举受支持的数据库类型，并加载已保存连接。</summary>
    public void Initialize()
    {
        SupportedDatabases = string.Join(", ", _schemaService.GetSupportedDatabaseTypes());
        RefreshConnections();
        RefreshRecentScripts();

        // 默认打开一个查询标签页
        NewQuery();
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

        // 将全部连接加载为对象树根节点。
        ObjectsExplorer.LoadConnections(items);

        ConnectionSummary = $"{Connections.Count} 个已保存连接";
    }

    /// <summary>新建查询标签页（对齐 DBeaver）。</summary>
    [RelayCommand]
    public void NewQuery()
    {
        var newTab = new QueryTabViewModel(_queryService);
        
        // 如果有当前连接，自动设置到新标签
        if (SelectedConnection is not null)
        {
            newTab.ConnectionName = SelectedConnection.Name;
        }
        
        QueryTabs.Add(newTab);
        SelectedQueryTab = newTab;
    }

    /// <summary>关闭指定的查询标签页（带未保存提示）。</summary>
    public async Task CloseQueryTabAsync(QueryTabViewModel tab)
    {
        if (!QueryTabs.Contains(tab))
            return;

        // 检查是否有未保存的修改
        if (tab.IsModified)
        {
            // 通过回调请求 UI 层显示确认对话框
            if (RequestCloseTab is not null)
            {
                var canClose = await RequestCloseTab(tab);
                if (!canClose)
                    return;
            }
            // 如果没有设置回调，直接关闭（静默模式）
        }

        QueryTabs.Remove(tab);

        // 如果关闭的是当前选中标签，自动切换
        if (SelectedQueryTab == tab && QueryTabs.Count > 0)
        {
            SelectedQueryTab = QueryTabs[^1];
        }
    }

    /// <summary>同步版本的关闭方法（用于非异步场景）。</summary>
    public void CloseQueryTab(QueryTabViewModel tab)
    {
        // 注意：此方法不显示未保存提示，直接关闭
        // 需要提示请使用 CloseQueryTabAsync
        if (QueryTabs.Contains(tab))
        {
            QueryTabs.Remove(tab);
            
            if (SelectedQueryTab == tab && QueryTabs.Count > 0)
            {
                SelectedQueryTab = QueryTabs[^1];
            }
        }
    }

    /// <summary>连接指定的连接节点（dbeaver 风格：双击或右键连接）。</summary>
    public async Task ConnectConnectionNodeAsync(DbObjectTreeNode connectionNode)
    {
        if (connectionNode?.NodeType != DbObjectTreeNodeType.Connection || connectionNode.Connection is null)
            return;

        SelectedConnection = connectionNode.Connection;
        IsConnected = true;
        CurrentDatabase = connectionNode.Connection.Database ?? string.Empty;
        CurrentSchema = string.Empty;
        SchemaSelectorVisible = false;

        // 同步连接名到当前选中的查询标签
        SyncConnectionToCurrentTab(connectionNode.Connection.Name);

        await ObjectsExplorer.LoadAsync(connectionNode.Connection.Name);
    }

    /// <summary>断开指定的连接节点（dbeaver 风格：右键断开）。</summary>
    public void DisconnectConnectionNode(DbObjectTreeNode? connectionNode)
    {
        if (connectionNode?.NodeType == DbObjectTreeNodeType.Connection && connectionNode.Connection is not null)
        {
            _queryService.CloseConnection(connectionNode.Connection.Name);
        }

        ObjectsExplorer.Disconnect(connectionNode);
        DataEditor.Clear();
        IsConnected = false;
        CurrentDatabase = string.Empty;
        CurrentSchema = string.Empty;
        SchemaSelectorVisible = false;
        ConnectionSummary = $"{Connections.Count} 个已保存连接";
    }

    /// <summary>重连指定连接节点。</summary>
    public async Task ReconnectConnectionNodeAsync(DbObjectTreeNode connectionNode)
    {
        if (connectionNode?.NodeType != DbObjectTreeNodeType.Connection)
            return;

        DisconnectConnectionNode(connectionNode);
        await ConnectConnectionNodeAsync(connectionNode);
    }

    /// <summary>将连接名同步到当前选中的查询标签页。</summary>
    private void SyncConnectionToCurrentTab(string connectionName)
    {
        if (SelectedQueryTab is not null)
        {
            SelectedQueryTab.ConnectionName = connectionName;
        }
        // 同时更新 QueryEditor 以保持向后兼容
        QueryEditor.ConnectionName = connectionName;
    }

    public void TestSelectedConnection()
    {
        // 连接测试在 ConnectWindow 中交互式进行；此处预留快速入口。
    }

    private void RefreshRecentScripts()
    {
        RecentScripts.Clear();
        // 可从配置文件加载最近脚本列表
    }

    /// <summary>根据选中的表/视图生成 SELECT 脚本并填充到查询编辑器。</summary>
    public void GenerateSelectScript(DbObjectTreeNode node)
    {
        if (node?.DbObject is not (Table or View))
            return;

        var sql = BuildSelectSql(node.DbObject);
        
        // 填充到当前选中的标签页
        if (SelectedQueryTab is not null)
        {
            SelectedQueryTab.SqlText = sql;
            SelectedQueryTab.StatusMessage = $"已生成 {node.DbObject.Name} 的查询脚本，点击「执行」运行。";
        }
        
        // 向后兼容
        QueryEditor.SqlText = sql;
        QueryEditor.StatusMessage = $"已生成 {node.DbObject.Name} 的查询脚本，点击「执行」运行。";
    }

    /// <summary>在数据编辑器中打开指定表/视图进行查看/编辑。</summary>
    public async Task<bool> OpenDataEditor(DbObjectTreeNode node)
    {
        if (node?.DbObject is not Table and not View)
            return false;

        var connectionName = FindNodeConnectionName(node);
        if (string.IsNullOrEmpty(connectionName))
        {
            QueryEditor.StatusMessage = "请先连接对应连接。";
            return false;
        }

        var table = node.DbObject as Table ?? (DatabaseObject)(node.DbObject as View)!;
        bool isView = node.DbObject is View;
        bool ok = await DataEditor.LoadAsync(
            connectionName,
            node.DatabaseName ?? CurrentDatabase,
            table.Name,
            node.Schema,
            isView);

        if (ok)
        {
            QueryEditor.StatusMessage = $"已打开数据编辑：{table.Name}。";
        }

        return ok;
    }

    /// <summary>刷新指定节点（重新懒加载其子节点）。</summary>
    public async Task RefreshNodeAsync(DbObjectTreeNode node)
    {
        if (node is null)
            return;

        var connectionName = FindNodeConnectionName(node);
        if (string.IsNullOrEmpty(connectionName))
            return;

        if (node.NodeType == DbObjectTreeNodeType.Folder)
        {
            await ObjectsExplorer.LoadFolderChildrenAsync(node, connectionName);
        }
        else if (node.NodeType == DbObjectTreeNodeType.ChildFolder)
        {
            await ObjectsExplorer.LoadTableChildFolderAsync(node, connectionName);
        }
    }

    /// <summary>向上查找节点所属连接名称。</summary>
    private string? FindNodeConnectionName(DbObjectTreeNode node)
    {
        var current = node.Parent;
        while (current is not null)
        {
            if (current.NodeType == DbObjectTreeNodeType.Connection)
                return current.Name;
            current = current.Parent;
        }
        return null;
    }

    /// <summary>向上查找指定类型的祖先节点。</summary>
    private static DbObjectTreeNode? FindAncestor(DbObjectTreeNode node, DbObjectTreeNodeType type)
    {
        var current = node.Parent;
        while (current is not null)
        {
            if (current.NodeType == type)
                return current;
            current = current.Parent;
        }
        return null;
    }

    private static string BuildSelectSql(DatabaseObject dbObj)
    {
        string name = dbObj.Name;
        if (!string.IsNullOrEmpty(dbObj.Schema))
        {
            name = $"{dbObj.Schema}.{name}";
        }
        return $"SELECT * FROM {name};";
    }
}
