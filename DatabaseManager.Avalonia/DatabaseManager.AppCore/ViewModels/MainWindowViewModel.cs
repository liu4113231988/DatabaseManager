using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DatabaseInterpreter.Model;
using DatabaseManager.AppCore.Common;
using DatabaseManager.AppCore.Models;
using DatabaseManager.AppCore.Services;

namespace DatabaseManager.AppCore.ViewModels;

/// <summary>
/// 主窗口 ViewModel（AppCore 层）。
/// 阶段 2/3：整合对象浏览器与查询编辑器子 ViewModel，负责主界面状态、连接选择联动与连接生命周期。
/// </summary>
public partial class MainWindowViewModel : ViewModelBase
{
    private readonly IDbSchemaService _schemaService;
    private readonly IDbConnectionService _connectionService;
    private readonly IQueryService _queryService;

    /// <summary>主界面左侧"对象浏览器"当前展示的连接集合。</summary>
    public ObservableCollection<ConnectionItem> Connections { get; } = new();

    /// <summary>最近打开的 SQL 脚本文件路径。</summary>
    public ObservableCollection<string> RecentScripts { get; } = new();

    /// <summary>对象浏览器子 ViewModel。</summary>
    public ObjectsExplorerViewModel ObjectsExplorer { get; }

    /// <summary>查询编辑器子 ViewModel。</summary>
    public QueryEditorViewModel QueryEditor { get; }

    /// <summary>数据编辑器子 ViewModel。</summary>
    public DataEditorViewModel DataEditor { get; }

    [ObservableProperty]
    private string _supportedDatabases = string.Empty;

    [ObservableProperty]
    private string _connectionSummary = string.Empty;

    [ObservableProperty]
    private ConnectionItem? _selectedConnection;

    /// <summary>当前连接是否已连接（Connect 后为 true）。</summary>
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
        {
            IsConnected = false;
            return;
        }

        QueryEditor.ConnectionName = connection.Name;
        QueryEditor.OnConnectionChanged();

        await ObjectsExplorer.LoadAsync(connection.Name);
    }

    /// <summary>初始化：枚举受支持的数据库类型，并加载已保存连接与最近脚本。</summary>
    public void Initialize()
    {
        SupportedDatabases = string.Join(", ", _schemaService.GetSupportedDatabaseTypes());
        RefreshConnections();
        RefreshRecentScripts();
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

    /// <summary>连接选中的连接（建立连接、加载对象树）。</summary>
    [RelayCommand]
    public async Task ConnectAsync()
    {
        var connection = SelectedConnection;
        if (connection is null)
        {
            QueryEditor.StatusMessage = "请先选择一个连接。";
            return;
        }

        QueryEditor.StatusMessage = $"正在连接 {connection.Name}...";

        try
        {
            await ObjectsExplorer.LoadAsync(connection.Name);
            IsConnected = true;

            // 记录当前数据库。
            CurrentDatabase = connection.Database ?? string.Empty;
            CurrentSchema = string.Empty;
            SchemaSelectorVisible = false;

            QueryEditor.ConnectionName = connection.Name;
            QueryEditor.OnConnectionChanged();
            QueryEditor.StatusMessage = $"已连接到 {connection.Name}。";
        }
        catch (Exception ex)
        {
            IsConnected = false;
            QueryEditor.StatusMessage = $"连接失败：{ex.Message}";
        }
    }

    /// <summary>断开当前连接（释放事务连接，卸载对象树）。</summary>
    [RelayCommand]
    public void Disconnect()
    {
        var connection = SelectedConnection;
        if (connection is not null)
        {
            _queryService.CloseConnection(connection.Name);
            QueryEditor.ConnectionName = connection.Name;
            QueryEditor.OnConnectionChanged();
            QueryEditor.StatusMessage = $"已断开 {connection.Name}。";
        }

        ObjectsExplorer.RootNodes.Clear();
        DataEditor.Clear();
        IsConnected = false;
        CurrentDatabase = string.Empty;
        CurrentSchema = string.Empty;
        SchemaSelectorVisible = false;
        ConnectionSummary = $"{Connections.Count} 个已保存连接";
    }

    /// <summary>重连当前连接。</summary>
    [RelayCommand]
    public async Task ReconnectAsync()
    {
        if (SelectedConnection is null)
            return;

        // 先断开，再连接。
        Disconnect();
        await ConnectAsync();
    }

    /// <summary>当用户在对象树中选中数据库节点时，更新当前数据库上下文。</summary>
    public void OnDatabaseNodeSelected(DbObjectTreeNode? node)
    {
        if (node is null)
            return;

        if (node.NodeType == DbObjectTreeNodeType.Database)
        {
            CurrentDatabase = node.Name;
            CurrentSchema = string.Empty;
            SchemaSelectorVisible = false;
        }
        else if (node.NodeType == DbObjectTreeNodeType.Schema)
        {
            CurrentDatabase = node.DatabaseName ?? CurrentDatabase;
            CurrentSchema = node.Name;
            SchemaSelectorVisible = true;
        }
        else
        {
            // 其他节点：向上取数据库/Schema。
            var dbNode = FindAncestor(node, DbObjectTreeNodeType.Database);
            var schemaNode = FindAncestor(node, DbObjectTreeNodeType.Schema);
            if (dbNode is not null) CurrentDatabase = dbNode.Name;
            if (schemaNode is not null)
            {
                CurrentSchema = schemaNode.Name;
                SchemaSelectorVisible = true;
            }
        }
    }

    /// <summary>新建查询（清空 SQL 编辑器）。</summary>
    [RelayCommand]
    public void NewQuery()
    {
        QueryEditor.SqlText = string.Empty;
        QueryEditor.StatusMessage = "新建查询。";
    }

    /// <summary>打开一个 SQL 脚本文件到查询编辑器。</summary>
    [RelayCommand]
    public void OpenScript(string? filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            QueryEditor.StatusMessage = "脚本文件不存在。";
            return;
        }

        try
        {
            QueryEditor.SqlText = File.ReadAllText(filePath);
            QueryEditor.StatusMessage = $"已打开 {Path.GetFileName(filePath)}。";
            AddRecentScript(filePath);
        }
        catch (Exception ex)
        {
            QueryEditor.StatusMessage = $"打开失败：{ex.Message}";
        }
    }

    /// <summary>将当前 SQL 保存到指定文件。</summary>
    [RelayCommand]
    public void SaveScript(string? filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            QueryEditor.StatusMessage = "未指定保存路径。";
            return;
        }

        try
        {
            var dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }

            File.WriteAllText(filePath, QueryEditor.SqlText);
            AddRecentScript(filePath);
            QueryEditor.StatusMessage = $"已保存到 {Path.GetFileName(filePath)}。";
        }
        catch (Exception ex)
        {
            QueryEditor.StatusMessage = $"保存失败：{ex.Message}";
        }
    }

    private void RefreshRecentScripts()
    {
        RecentScripts.Clear();
        var recent = _recentScriptPaths;
        foreach (var path in recent)
        {
            RecentScripts.Add(path);
        }
    }

    private void AddRecentScript(string filePath)
    {
        var normalized = Path.GetFullPath(filePath);

        _recentScriptPaths.RemoveAll(p => string.Equals(Path.GetFullPath(p), normalized, StringComparison.OrdinalIgnoreCase));
        _recentScriptPaths.Insert(0, normalized);

        // 最多保留 10 条。
        while (_recentScriptPaths.Count > 10)
        {
            _recentScriptPaths.RemoveAt(_recentScriptPaths.Count - 1);
        }

        RefreshRecentScripts();
    }

    /// <summary>最近打开的 SQL 脚本文件路径（内存态）。</summary>
    private readonly List<string> _recentScriptPaths = new();

    /// <summary>根据选中的表/视图生成 SELECT 脚本并填充到查询编辑器。</summary>
    public void GenerateSelectScript(DbObjectTreeNode node)
    {
        if (node?.DbObject is not (Table or View))
            return;

        QueryEditor.SqlText = BuildSelectSql(node.DbObject);
        QueryEditor.StatusMessage = $"已生成 {node.DbObject.Name} 的查询脚本，点击「执行」运行。";
    }

    /// <summary>在数据编辑器中打开指定表/视图进行查看/编辑。</summary>
    public async Task<bool> OpenDataEditor(DbObjectTreeNode node)
    {
        if (node?.DbObject is not Table and not View)
            return false;

        if (SelectedConnection is null)
        {
            QueryEditor.StatusMessage = "请先选择一个连接。";
            return false;
        }

        var table = node.DbObject as Table ?? (DatabaseObject)(node.DbObject as View)!;
        bool isView = node.DbObject is View;
        bool ok = await DataEditor.LoadAsync(
            SelectedConnection.Name,
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

        if (node.NodeType == DbObjectTreeNodeType.Folder && SelectedConnection is not null)
        {
            await ObjectsExplorer.LoadFolderChildrenAsync(node, SelectedConnection.Name);
        }
        else if (node.NodeType == DbObjectTreeNodeType.ChildFolder && SelectedConnection is not null)
        {
            await ObjectsExplorer.LoadTableChildFolderAsync(node, SelectedConnection.Name);
        }
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
