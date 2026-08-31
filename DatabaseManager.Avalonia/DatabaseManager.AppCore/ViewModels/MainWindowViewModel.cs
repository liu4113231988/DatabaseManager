using System.Collections.ObjectModel;
using System.Linq;
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
    private readonly IDataEditService _dataEditService;
    private readonly IQueryHistoryService? _historyService;
    private readonly IScriptLibraryService? _scriptLibraryService;
    private readonly IAppSettingsService? _appSettingsService;

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

    /// <summary>主内容区当前模式（0=查询，1=数据编辑），切换子选项卡使用。</summary>
    [ObservableProperty]
    private int _contentMode;

    /// <summary>最近打开的 SQL 脚本文件路径。</summary>
    public ObservableCollection<string> RecentScripts { get; } = new();

    /// <summary>请求关闭标签页的回调（用于未保存提示）。</summary>
    public Func<QueryTabViewModel, Task<bool>>? RequestCloseTab { get; set; }

    /// <summary>请求确认丢弃数据编辑器未保存改动的回调（切换表/断开连接时）。</summary>
    public Func<Task<bool>>? RequestDiscardDataChanges { get; set; }

    public MainWindowViewModel(
        IDbSchemaService schemaService,
        IDbConnectionService connectionService,
        IQueryService queryService,
        IDataEditService dataEditService,
        ObjectsExplorerViewModel objectsExplorer,
        QueryEditorViewModel queryEditor,
        IQueryHistoryService? historyService = null,
        IScriptLibraryService? scriptLibraryService = null,
        IAppSettingsService? appSettingsService = null)
    {
        _schemaService = schemaService;
        _connectionService = connectionService;
        _queryService = queryService;
        _dataEditService = dataEditService;
        _historyService = historyService;
        _scriptLibraryService = scriptLibraryService;
        _appSettingsService = appSettingsService;
        ObjectsExplorer = objectsExplorer;
        QueryEditor = queryEditor;
    }

    /// <summary>初始化：枚举受支持的数据库类型，加载已保存连接，并恢复上次会话的查询标签。</summary>
    public void Initialize()
    {
        SupportedDatabases = string.Join(", ", _schemaService.GetSupportedDatabaseTypes());
        RefreshConnections();
        RefreshRecentScripts();

        RestoreSession();
    }

    /// <summary>恢复上次会话打开的查询标签（含未保存的 SQL 草稿）；无可恢复内容时新建一个空标签。</summary>
    private void RestoreSession()
    {
        var tabs = _appSettingsService?.Settings.Workspace.Tabs;
        var restored = 0;

        if (tabs is { Count: > 0 })
        {
            foreach (var tab in tabs)
            {
                if (string.IsNullOrWhiteSpace(tab.SqlText) && string.IsNullOrEmpty(tab.ConnectionName))
                {
                    continue;
                }

                RestoreQueryTab(tab);
                restored++;
            }

            if (restored > 0)
            {
                SelectedQueryTab = QueryTabs[^1];
            }
        }

        if (restored == 0)
        {
            NewQuery();
        }
    }

    /// <summary>按会话快照恢复一个查询标签（SQL 以草稿形式恢复，不自动标记为已修改）。</summary>
    public QueryTabViewModel RestoreQueryTab(QueryTabState state)
    {
        var newTab = new QueryTabViewModel(_queryService, _dataEditService, title: state.Title, historyService: _historyService);

        if (!string.IsNullOrEmpty(state.ConnectionName))
        {
            newTab.ConnectionName = state.ConnectionName;
        }
        else if (SelectedConnection is not null)
        {
            newTab.ConnectionName = SelectedConnection.Name;
        }

        if (!string.IsNullOrEmpty(state.DatabaseName))
        {
            newTab.DatabaseName = state.DatabaseName;
        }

        newTab.SqlText = state.SqlText ?? string.Empty;
        newTab.MarkAsSaved();

        QueryTabs.Add(newTab);
        return newTab;
    }

    /// <summary>捕获当前会话（打开的查询标签 + SQL 草稿），供退出时持久化。</summary>
    public void CaptureSession()
    {
        if (_appSettingsService is null)
        {
            return;
        }

        _appSettingsService.Settings.Workspace.Tabs = QueryTabs
            .Select(t => new QueryTabState
            {
                Title = t.Title.EndsWith(" *", StringComparison.Ordinal) ? t.Title[..^2] : t.Title,
                SqlText = t.SqlText,
                ConnectionName = t.ConnectionName,
                DatabaseName = t.DatabaseName,
            })
            .ToList();

        _appSettingsService.Save();
    }

    /// <summary>刷新左侧对象浏览器的连接列表。</summary>
    public void RefreshConnections()
    {
        if (_connectionService is null || ObjectsExplorer is null)
            return;

        Connections.Clear();

        var items = _connectionService.GetConnections() ?? new List<ConnectionItem>();
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
        var newTab = new QueryTabViewModel(_queryService, _dataEditService, historyService: _historyService);

        // 如果有当前连接，自动设置到新标签
        if (SelectedConnection is not null)
        {
            newTab.ConnectionName = SelectedConnection.Name;
        }
        newTab.DatabaseName = CurrentDatabase;

        QueryTabs.Add(newTab);
        SelectedQueryTab = newTab;
    }

    /// <summary>打开一个新查询标签并填充 SQL（供全库数据搜索等工具窗口回调）。</summary>
    public QueryTabViewModel OpenSqlInNewTab(string connectionName, string sql, string? databaseName = null)
    {
        var newTab = new QueryTabViewModel(_queryService, _dataEditService, historyService: _historyService)
        {
            ConnectionName = connectionName,
            DatabaseName = databaseName ?? CurrentDatabase,
            SqlText = sql,
        };
        newTab.MarkAsSaved();

        QueryTabs.Add(newTab);
        SelectedQueryTab = newTab;
        return newTab;
    }

    /// <summary>关闭指定的查询标签页（带未保存提示）。返回是否真正关闭（false=用户取消）。</summary>
    public async Task<bool> CloseQueryTabAsync(QueryTabViewModel tab)
    {
        if (!QueryTabs.Contains(tab))
            return true;

        // 检查是否有未保存的修改
        if (tab.IsModified)
        {
            // 通过回调请求 UI 层显示确认对话框
            if (RequestCloseTab is not null)
            {
                var canClose = await RequestCloseTab(tab);
                if (!canClose)
                    return false;
            }
            // 如果没有设置回调，直接关闭（静默模式）
        }

        QueryTabs.Remove(tab);

        // 如果关闭的是当前选中标签，自动切换
        if (SelectedQueryTab == tab && QueryTabs.Count > 0)
        {
            SelectedQueryTab = QueryTabs[^1];
        }

        return true;
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

        // 仅当对象树标记为已连接时才通知查询服务为已连接
        if (connectionNode.IsConnectionActive)
        {
            _queryService.NotifyConnected(connectionNode.Connection.Name);
        }
    }

    /// <summary>断开指定的连接节点（dbeaver 风格：右键断开）。</summary>
    public void DisconnectConnectionNode(DbObjectTreeNode? connectionNode)
    {
        if (connectionNode?.NodeType == DbObjectTreeNodeType.Connection && connectionNode.Connection is not null)
        {
            var name = connectionNode.Connection.Name;
            _queryService.CloseConnection(name);
            // 将使用该连接的查询标签标记为已断开，避免用户误以为仍可执行
            foreach (var tab in QueryTabs.Where(t => string.Equals(t.ConnectionName, name, StringComparison.OrdinalIgnoreCase)))
            {
                tab.StatusMessage = $"连接 '{name}' 已断开，请重新连接后再执行。";
            }
        }
        else if (connectionNode is null)
        {
            // 来自菜单的 Disconnect（按 SelectedConnection）：已在 CloseConnection 中处理
        }

        ObjectsExplorer.Disconnect(connectionNode);
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

    /// <summary>连接当前选中的连接（工具栏/菜单快捷操作）。</summary>
    [RelayCommand]
    public async Task Connect()
    {
        if (SelectedConnection is null)
        {
            QueryEditor.StatusMessage = "请先选择一个连接。";
            return;
        }

        var node = ObjectsExplorer.FindConnectionNode(SelectedConnection.Name);
        if (node is null)
        {
            QueryEditor.StatusMessage = $"连接「{SelectedConnection.Name}」未在对象树中定位。";
            return;
        }

        await ConnectConnectionNodeAsync(node);
    }

    /// <summary>断开当前选中的连接（工具栏/菜单快捷操作）。</summary>
    [RelayCommand]
    public void Disconnect()
    {
        if (SelectedConnection is null)
            return;

        var node = ObjectsExplorer.FindConnectionNode(SelectedConnection.Name);
        if (node is not null)
        {
            DisconnectConnectionNode(node);
        }
        else
        {
            // 节点未找到时仍需标记查询服务为断开
            _queryService.CloseConnection(SelectedConnection.Name);
            foreach (var tab in QueryTabs.Where(t => string.Equals(t.ConnectionName, SelectedConnection.Name, StringComparison.OrdinalIgnoreCase)))
            {
                tab.StatusMessage = $"连接 '{SelectedConnection.Name}' 已断开，请重新连接后再执行。";
            }
            IsConnected = false;
            CurrentDatabase = string.Empty;
            CurrentSchema = string.Empty;
            SchemaSelectorVisible = false;
        }
    }

    /// <summary>重连当前选中的连接（工具栏/菜单快捷操作）。</summary>
    [RelayCommand]
    public async Task Reconnect()
    {
        if (SelectedConnection is null)
            return;

        var node = ObjectsExplorer.FindConnectionNode(SelectedConnection.Name);
        if (node is null)
            return;

        DisconnectConnectionNode(node);
        await ConnectConnectionNodeAsync(node);
    }

    /// <summary>断开全部活动连接（对应 DBeaver 的 Database &gt; Disconnect All）。</summary>
    [RelayCommand]
    public void DisconnectAll()
    {
        var activeNodes = ObjectsExplorer.RootNodes
            .Where(n => n.NodeType == DbObjectTreeNodeType.Connection && n.IsConnectionActive)
            .ToList();

        foreach (var node in activeNodes)
        {
            DisconnectConnectionNode(node);
        }
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
        if (_scriptLibraryService is null)
        {
            return;
        }

        foreach (var path in _scriptLibraryService.GetRecentFiles())
        {
            RecentScripts.Add(path);
        }
    }

    /// <summary>记录最近打开的脚本文件并刷新菜单。</summary>
    public void TrackRecentScript(string path)
    {
        if (_scriptLibraryService is null || string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        _scriptLibraryService.AddRecentFile(path);
        RefreshRecentScripts();
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

    /// <summary>新建查询标签页并填入 SQL（用于新建对象模板 / 查看对象定义）。</summary>
    public void NewObjectDefinitionQuery(string sql, string? connectionName, string? databaseName)
    {
        var newTab = new QueryTabViewModel(_queryService, _dataEditService, historyService: _historyService);

        if (!string.IsNullOrEmpty(connectionName))
        {
            newTab.ConnectionName = connectionName;
        }
        else if (SelectedConnection is not null)
        {
            newTab.ConnectionName = SelectedConnection.Name;
        }
        newTab.DatabaseName = string.IsNullOrEmpty(databaseName) ? CurrentDatabase : databaseName;

        QueryTabs.Add(newTab);
        SelectedQueryTab = newTab;

        newTab.SqlText = sql;
        ContentMode = 0; // 切换到查询子选项卡
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
    public string? FindNodeConnectionName(DbObjectTreeNode node)
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
