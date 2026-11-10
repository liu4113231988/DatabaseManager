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
    private bool _isDeletingDatabase;
    private bool _isCreatingDatabase;

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

    /// <summary>当前选中连接在对象树中对应的节点（用于按节点状态计算工具栏按钮可用性）。</summary>
    [ObservableProperty]
    private DbObjectTreeNode? _selectedConnectionNode;

    [ObservableProperty]
    private QueryTabViewModel? _selectedQueryTab;

    partial void OnSelectedQueryTabChanged(QueryTabViewModel? oldValue, QueryTabViewModel? newValue)
    {
        if (oldValue is not null) oldValue.PropertyChanged -= OnQueryTabContextChanged;
        if (newValue is not null)
        {
            newValue.PropertyChanged += OnQueryTabContextChanged;
            RefreshQueryDatabases(newValue);
        }
        RefreshQueryEditorContext();
    }

    private void OnQueryTabContextChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(QueryTabViewModel.ConnectionName) or nameof(QueryTabViewModel.DatabaseName)
            or nameof(QueryTabViewModel.IsExecuting)) RefreshQueryEditorContext();
    }

    /// <summary>刷新标签头共享事务提示：≥2 个标签共用同一连接且该连接存在活动事务时标记。</summary>
    public void RefreshSharedTransactionIndicators()
    {
        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var tab in QueryTabs)
        {
            if (string.IsNullOrWhiteSpace(tab.ConnectionName)) continue;
            counts[tab.ConnectionName] = counts.TryGetValue(tab.ConnectionName, out var n) ? n + 1 : 1;
        }

        foreach (var tab in QueryTabs)
        {
            tab.IsSharedTransaction = !string.IsNullOrWhiteSpace(tab.ConnectionName)
                && counts.TryGetValue(tab.ConnectionName, out var count) && count >= 2
                && _queryService.IsTransactionActive(tab.ConnectionName);
        }
    }

    /// <summary>后台标签执行也可能开启/结束共享事务，执行完成时重新计算提示。</summary>
    private void OnTabTransactionContextChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(QueryTabViewModel.IsExecuting) or nameof(QueryTabViewModel.ConnectionName))
            RefreshSharedTransactionIndicators();
    }

    private void RefreshQueryEditorContext()
    {
        QueryEditor.ConnectionName = SelectedQueryTab?.ConnectionName ?? string.Empty;
        QueryEditor.DatabaseName = SelectedQueryTab?.DatabaseName ?? string.Empty;
        QueryEditor.OnConnectionChanged();
    }

    /// <summary>查询工具使用标签的上下文快照，不受对象浏览器选择变化影响。</summary>
    public ConnectionItem? GetSelectedQueryConnection()
    {
        var tab = SelectedQueryTab;
        var saved = Connections.FirstOrDefault(c => string.Equals(c.Name, tab?.ConnectionName, StringComparison.OrdinalIgnoreCase));
        if (tab is null || saved is null) return null;
        var connection = saved.Clone();
        if (!string.IsNullOrWhiteSpace(tab.DatabaseName)) connection.Database = tab.DatabaseName;
        return connection;
    }

    public void RefreshQueryDatabases(QueryTabViewModel tab)
    {
        var connection = Connections.FirstOrDefault(c =>
            string.Equals(c.Name, tab.ConnectionName, StringComparison.OrdinalIgnoreCase));
        if (connection?.DatabaseType is not (DatabaseTypeNames.SqlServer or DatabaseTypeNames.MySql or DatabaseTypeNames.Postgres or DatabaseTypeNames.KingbaseES))
        {
            tab.AvailableDatabases.Clear();
            return;
        }
        var node = ObjectsExplorer.FindConnectionNode(tab.ConnectionName);
        var names = node?.Children.Where(n => n.NodeType == DbObjectTreeNodeType.Database).Select(n => n.Name)
            ?? Enumerable.Empty<string>();
        var databases = names.Append(tab.DatabaseName).Where(n => !string.IsNullOrWhiteSpace(n)).Distinct().OrderBy(n => n).ToArray();
        if (tab.AvailableDatabases.SequenceEqual(databases)) return;
        tab.AvailableDatabases.Clear();
        foreach (var name in databases)
            tab.AvailableDatabases.Add(name);
    }

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

    /// <summary>专注模式：折叠菜单/工具栏/状态栏与对象浏览器，仅保留查询编辑区（UI 层按此应用显隐，状态持久化到 app-settings.json）。</summary>
    [ObservableProperty]
    private bool _focusMode;

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
        QueryEditor.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(QueryEditorViewModel.StatusMessage)
                && SelectedQueryTab is { } tab && tab.ConnectionName == QueryEditor.ConnectionName)
                tab.StatusMessage = QueryEditor.StatusMessage;
            if (e.PropertyName == nameof(QueryEditorViewModel.IsTransactionActive))
                RefreshSharedTransactionIndicators();
        };
        QueryTabs.CollectionChanged += (_, args) =>
        {
            if (args.OldItems is not null)
                foreach (QueryTabViewModel tab in args.OldItems) tab.PropertyChanged -= OnTabTransactionContextChanged;
            if (args.NewItems is not null)
                foreach (QueryTabViewModel tab in args.NewItems) tab.PropertyChanged += OnTabTransactionContextChanged;
            RefreshSharedTransactionIndicators();
        };
        System.Diagnostics.Trace.WriteLine("startup|viewmodel-resolved");
    }

    /// <summary>连接被选中时同步对象树节点，便于工具栏按节点状态计算可用性。</summary>
    partial void OnSelectedConnectionChanged(ConnectionItem? value)
    {
        // 解绑旧节点的属性变更
        if (SelectedConnectionNode is not null)
        {
            SelectedConnectionNode.PropertyChanged -= OnSelectedConnectionNodePropertyChanged;
        }

        SelectedConnectionNode = value is null
            ? null
            : ObjectsExplorer.FindConnectionNode(value.Name);

        CurrentDatabase = value?.Database ?? string.Empty;
        IsConnected = SelectedConnectionNode?.IsConnectionActive == true;
        CurrentSchema = string.Empty;
        SchemaSelectorVisible = false;

        // 订阅新节点的 IsConnectionActive 变化
        if (SelectedConnectionNode is not null)
        {
            SelectedConnectionNode.PropertyChanged += OnSelectedConnectionNodePropertyChanged;
        }

        RaiseCommandStateChanged();
    }

    /// <summary>当前选中连接节点的 IsConnectionActive 变更时刷新命令可用性。</summary>
    private void OnSelectedConnectionNodePropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(DbObjectTreeNode.IsConnectionActive))
        {
            RaiseCommandStateChanged();
        }
    }

    /// <summary>通知工具栏命令可用性相关属性已变化（CanConnect/CanReconnect/CanDisconnect/CanDisconnectAll）。</summary>
    private void RaiseCommandStateChanged()
    {
        OnPropertyChanged(nameof(CanConnect));
        OnPropertyChanged(nameof(CanReconnect));
        OnPropertyChanged(nameof(CanDisconnect));
        OnPropertyChanged(nameof(CanDisconnectAll));
        RefreshSharedTransactionIndicators();
    }

    /// <summary>已选中连接且该连接未建立连接时可用（用于工具栏「连接」按钮）。</summary>
    public bool CanConnect => SelectedConnectionNode is not null && !SelectedConnectionNode.IsConnectionActive;

    /// <summary>已选中连接且该连接已建立连接时可用（用于工具栏「重连」按钮）。</summary>
    public bool CanReconnect => SelectedConnectionNode is not null && SelectedConnectionNode.IsConnectionActive;

    /// <summary>已选中连接且该连接已建立连接时可用（用于工具栏「断开」按钮）。</summary>
    public bool CanDisconnect => SelectedConnectionNode is not null && SelectedConnectionNode.IsConnectionActive;

    /// <summary>至少存在一个已建立连接的节点时可用（用于工具栏「断开全部」按钮）。</summary>
    public bool CanDisconnectAll => HasActiveConnection();

    /// <summary>判断对象树中是否存在已激活的连接节点。</summary>
    private bool HasActiveConnection()
    {
        foreach (var node in ObjectsExplorer.RootNodes)
        {
            if (node.NodeType == DbObjectTreeNodeType.Connection && node.IsConnectionActive)
                return true;

            foreach (var child in node.Children)
            {
                if (child.NodeType == DbObjectTreeNodeType.Connection && child.IsConnectionActive)
                    return true;
            }
        }
        return false;
    }

    /// <summary>初始化：枚举受支持的数据库类型，加载已保存连接，并恢复上次会话的查询标签。</summary>
    public void Initialize()
    {
        SupportedDatabases = string.Join(", ", _schemaService.GetSupportedDatabaseTypes());
        RefreshConnections();
        System.Diagnostics.Trace.WriteLine("startup|connections-end");
        RefreshRecentScripts();
        System.Diagnostics.Trace.WriteLine("startup|recent-scripts-end");

        RestoreSession();
        System.Diagnostics.Trace.WriteLine("startup|session-end");
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
            DatabaseName = databaseName ?? (string.Equals(connectionName, SelectedConnection?.Name, StringComparison.OrdinalIgnoreCase)
                ? CurrentDatabase : Connections.FirstOrDefault(c => string.Equals(c.Name, connectionName, StringComparison.OrdinalIgnoreCase))?.Database) ?? string.Empty,
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
        IsConnected = connectionNode.IsConnectionActive;
        CurrentDatabase = connectionNode.Connection.Database ?? string.Empty;
        CurrentSchema = string.Empty;
        SchemaSelectorVisible = false;

        var targetTab = SelectedQueryTab;
        try
        {
            await ObjectsExplorer.LoadAsync(connectionNode.Connection.Name);
            if (connectionNode.IsConnectionActive)
            {
                _queryService.NotifyConnected(connectionNode.Connection.Name);
                InitializeQueryTabConnection(targetTab, connectionNode.Connection.Name, connectionNode.Connection.Database);
            }
            foreach (var tab in QueryTabs.Where(t => string.Equals(t.ConnectionName, connectionNode.Connection.Name, StringComparison.OrdinalIgnoreCase)))
                RefreshQueryDatabases(tab);
        }
        finally
        {
            if (SelectedConnection == connectionNode.Connection) IsConnected = connectionNode.IsConnectionActive;
            RefreshQueryEditorContext();
            RaiseCommandStateChanged();
        }
    }

    /// <summary>断开指定的连接节点（dbeaver 风格：右键断开）。</summary>
    public async Task DisconnectConnectionNodeAsync(DbObjectTreeNode? connectionNode)
    {
        if (connectionNode?.NodeType == DbObjectTreeNodeType.Connection && connectionNode.Connection is not null)
        {
            var name = connectionNode.Connection.Name;
            foreach (var tab in QueryTabs.Where(t => string.Equals(t.ConnectionName, name, StringComparison.OrdinalIgnoreCase)))
                tab.StatusMessage = $"正在断开连接 '{name}'，等待当前查询结束...";
            await _queryService.CloseConnectionAsync(name);
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

        if (connectionNode is not null) ObjectsExplorer.Disconnect(connectionNode);
        if (connectionNode == SelectedConnectionNode)
        {
            IsConnected = false;
            CurrentSchema = string.Empty;
            SchemaSelectorVisible = false;
        }
        ConnectionSummary = $"{Connections.Count} 个已保存连接";
        RaiseCommandStateChanged();
        RefreshQueryEditorContext();
    }

    /// <summary>重连指定连接节点。</summary>
    public async Task ReconnectConnectionNodeAsync(DbObjectTreeNode connectionNode)
    {
        if (connectionNode?.NodeType != DbObjectTreeNodeType.Connection)
            return;

        await DisconnectConnectionNodeAsync(connectionNode);
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
    public async Task DisconnectAsync()
    {
        var connection = SelectedConnection;
        if (connection is null)
            return;

        var node = ObjectsExplorer.FindConnectionNode(connection.Name);
        if (node is not null)
        {
            await DisconnectConnectionNodeAsync(node);
        }
        else
        {
            // 节点未找到时仍需标记查询服务为断开
            await _queryService.CloseConnectionAsync(connection.Name);
            foreach (var tab in QueryTabs.Where(t => string.Equals(t.ConnectionName, connection.Name, StringComparison.OrdinalIgnoreCase)))
            {
                tab.StatusMessage = $"连接 '{connection.Name}' 已断开，请重新连接后再执行。";
            }
            if (SelectedConnection == connection)
            {
                IsConnected = false;
                CurrentSchema = string.Empty;
                SchemaSelectorVisible = false;
            }
            RaiseCommandStateChanged();
            RefreshQueryEditorContext();
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

        await DisconnectConnectionNodeAsync(node);
        await ConnectConnectionNodeAsync(node);
    }

    /// <summary>断开全部活动连接（对应 DBeaver 的 Database &gt; Disconnect All）。</summary>
    [RelayCommand]
    public async Task DisconnectAllAsync()
    {
        var activeNodes = ObjectsExplorer.RootNodes
            .SelectMany(n => n.NodeType == DbObjectTreeNodeType.Connection ? new[] { n } : n.Children.AsEnumerable())
            .Where(n => n.NodeType == DbObjectTreeNodeType.Connection && n.IsConnectionActive)
            .ToList();

        await Task.WhenAll(activeNodes.Select(DisconnectConnectionNodeAsync));
    }

    /// <summary>仅为发起连接时尚未绑定的标签初始化上下文，保留所有已有绑定。</summary>
    private void InitializeQueryTabConnection(QueryTabViewModel? tab, string connectionName, string? databaseName)
    {
        if (tab is not null && QueryTabs.Contains(tab) && string.IsNullOrWhiteSpace(tab.ConnectionName)
            && !tab.IsExecuting && !tab.IsSavingChanges && !tab.HasPendingChanges && !tab.HasResult)
        {
            tab.ConnectionName = connectionName;
            tab.DatabaseName = databaseName ?? string.Empty;
            RefreshQueryDatabases(tab);
        }
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

    /// <summary>根据选中的表/视图生成 SELECT 脚本并在新查询标签页中打开（避免覆盖当前编辑内容）。</summary>
    public void GenerateSelectScript(DbObjectTreeNode node)
    {
        if (node?.DbObject is not (Table or View))
            return;

        var sql = BuildSelectSql(node.DbObject);
        OpenNodeSqlInNewTab(
            node,
            sql,
            $"已生成 {node.DbObject.Name} 的查询脚本，点击「执行」运行。");
    }

    /// <summary>新建查询标签页并填入 SQL（用于对象树右键菜单的「查询数据/编辑数据/生成 SQL」等场景）。</summary>
    public QueryTabViewModel OpenNodeSqlInNewTab(DbObjectTreeNode node, string sql, string? statusMessage = null)
    {
        var connectionName = FindNodeConnectionName(node);
        var newTab = new QueryTabViewModel(_queryService, _dataEditService, historyService: _historyService);

        if (!string.IsNullOrEmpty(connectionName))
        {
            newTab.ConnectionName = connectionName;
        }
        else if (SelectedConnection is not null)
        {
            newTab.ConnectionName = SelectedConnection.Name;
        }
        newTab.DatabaseName = string.IsNullOrEmpty(node.DatabaseName) ? CurrentDatabase : node.DatabaseName;

        QueryTabs.Add(newTab);
        SelectedQueryTab = newTab;

        newTab.SqlText = sql;
        if (!string.IsNullOrEmpty(statusMessage))
        {
            newTab.StatusMessage = statusMessage;
        }
        return newTab;
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


    public async Task<bool> CreateDatabaseAsync(DbObjectTreeNode node, string databaseName, IDdlService ddlService)
    {
        var connectionNode = node.FindConnectionNode();
        if (_isCreatingDatabase || connectionNode?.Connection is not { } connection) return false;
        _isCreatingDatabase = true;
        try
        {
            var result = await ddlService.CreateDatabaseAsync(connection.Name, databaseName);
            if (!result.IsSuccess)
            {
                QueryEditor.StatusMessage = $"新建数据库失败：{result.ErrorMessage}";
                return false;
            }
            bool wasActive = connectionNode.IsConnectionActive;
            try
            {
                // 仅刷新元数据，不通过重连回滚现有查询事务。
                await ObjectsExplorer.LoadAsync(connection.Name);
                _queryService.NotifyConnected(connection.Name);
                connectionNode.IsExpanded = true;
                if (SelectedConnectionNode == connectionNode) IsConnected = connectionNode.IsConnectionActive;
                foreach (var tab in QueryTabs.Where(t => string.Equals(t.ConnectionName, connection.Name, StringComparison.OrdinalIgnoreCase)))
                    RefreshQueryDatabases(tab);
                QueryEditor.StatusMessage = $"已创建数据库：{databaseName}。";
            }
            catch (Exception ex)
            {
                connectionNode.IsConnectionActive = wasActive;
                QueryEditor.StatusMessage = $"数据库 {databaseName} 已创建，但刷新对象树失败：{ex.Message}。请刷新连接。";
            }
            return true;
        }
        finally { _isCreatingDatabase = false; }
    }

    /// <summary>删除数据库并清理浏览器/查询上下文；调用前由界面完成名称确认。</summary>
    public async Task<bool> DeleteDatabaseAsync(DbObjectTreeNode node, IDdlService ddlService)
    {
        var parent = node.Parent;
        if (_isDeletingDatabase || node.NodeType != DbObjectTreeNodeType.Database
            || parent?.Connection is not { } connection || !parent.Children.Contains(node))
            return false;
        var tabs = QueryTabs.Where(t => string.Equals(t.ConnectionName, connection.Name, StringComparison.OrdinalIgnoreCase)).ToArray();
        if (tabs.Any(t => t.IsExecuting || t.IsSavingChanges || t.HasPendingChanges)
            || _queryService.IsTransactionActive(connection.Name) || QueryEditor.IsTransactionBusy)
        {
            QueryEditor.StatusMessage = "请先结束查询、保存或还原数据修改，并提交或回滚当前事务后再删除数据库。";
            return false;
        }
        DdlExecuteResult result;
        _isDeletingDatabase = true;
        try
        {
            result = await ddlService.DropDatabaseAsync(connection.Name, node.Name);
        }
        finally { _isDeletingDatabase = false; }
        if (!result.IsSuccess)
        {
            QueryEditor.StatusMessage = $"删除数据库失败：{result.ErrorMessage}";
            return false;
        }

        tabs = QueryTabs.Where(t => string.Equals(t.ConnectionName, connection.Name, StringComparison.OrdinalIgnoreCase)).ToArray();
        var affectedTabs = tabs.Where(t => string.Equals(
            string.IsNullOrEmpty(t.DatabaseName) ? connection.Database : t.DatabaseName, node.Name, StringComparison.Ordinal)).ToArray();
        var maintenance = DefaultDdlService.GetMaintenanceDatabase(connection.DatabaseType);
        bool wasDefault = string.Equals(connection.Database, node.Name, StringComparison.Ordinal);
        // 已保存配置不在此处重写；对象浏览器本次会话的默认查询目标改用维护库。
        if (wasDefault) connection.Database = maintenance;
        parent.Children.Remove(node);
        if (SelectedConnection == connection && CurrentDatabase == node.Name)
        {
            CurrentDatabase = maintenance;
            CurrentSchema = string.Empty;
            SchemaSelectorVisible = false;
        }
        foreach (var tab in affectedTabs)
        {
            // 保留 SQL/结果，但解除执行绑定，避免把原 SQL 自动改投到维护库。
            tab.ConnectionName = string.Empty;
            tab.DatabaseName = string.Empty;
            tab.StatusMessage = $"数据库 '{node.Name}' 已删除，请重新选择连接和数据库。";
        }
        foreach (var tab in tabs) RefreshQueryDatabases(tab);
        QueryEditor.StatusMessage = $"已删除数据库：{node.Name}。"
            + (wasDefault ? "当前会话已切换至维护库；请编辑已保存连接的默认数据库。" : string.Empty);
        return true;
    }

    /// <summary>刷新指定节点（重新懒加载其子节点）。</summary>
    public async Task RefreshNodeAsync(DbObjectTreeNode node)
    {
        if (node is null)
            return;

        // 数据库/Schema 节点的子级在连接时随整棵树一次性构建（无按节点单独加载入口），
        // 刷新时定位所属连接节点并重连，重建对象树。
        if (node.NodeType is DbObjectTreeNodeType.Database or DbObjectTreeNodeType.Schema)
        {
            var connectionNode = FindAncestor(node, DbObjectTreeNodeType.Connection);
            if (connectionNode is not null)
            {
                await ReconnectConnectionNodeAsync(connectionNode);
            }
            return;
        }

        var connectionName = FindNodeConnectionName(node);
        if (string.IsNullOrEmpty(connectionName))
            return;

        ObjectsExplorer.ClearPrefetchCache(connectionName);
        node.IsLoaded = false;
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
