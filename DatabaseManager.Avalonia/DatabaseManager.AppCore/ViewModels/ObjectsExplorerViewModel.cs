using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using DatabaseInterpreter.Model;
using DatabaseManager.AppCore.Common;
using DatabaseManager.AppCore.Models;
using DatabaseManager.AppCore.Services;

namespace DatabaseManager.AppCore.ViewModels;

/// <summary>
/// 对象浏览器 ViewModel（AppCore 层）。
/// 完整对象浏览：连接 → 数据库 → Schema → 类型文件夹 → 对象 → 表/视图子对象，支持按需懒加载。
/// </summary>
public partial class ObjectsExplorerViewModel : ViewModelBase
{
    private readonly IDbSchemaService _schemaService;

    /// <summary>对象树根节点集合。</summary>
    public ObservableCollection<DbObjectTreeNode> RootNodes { get; } = new();

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    public ObjectsExplorerViewModel(IDbSchemaService schemaService)
    {
        _schemaService = schemaService;
    }

    /// <summary>当前已建立连接的连接名称集合（用于区分各连接节点的连接状态）。</summary>
    private readonly HashSet<string> _activeConnections = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>加载全部已保存连接为对象树根节点（dbeaver 风格：所有连接平铺展示）。</summary>
    public void LoadConnections(IEnumerable<ConnectionItem> connections)
    {
        // 保留已连接的连接节点（避免切换连接列表时丢失连接状态）。
        var activeNames = RootNodes
            .Where(n => n.NodeType == DbObjectTreeNodeType.Connection && n.IsConnectionActive)
            .Select(n => n.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        RootNodes.Clear();
        _activeConnections.Clear();
        _activeConnections.UnionWith(activeNames);

        foreach (var item in connections)
        {
            var node = new DbObjectTreeNode
            {
                Name = item.Name,
                Text = item.Name,
                NodeType = DbObjectTreeNodeType.Connection,
                Connection = item,
                DatabaseObjectType = DatabaseObjectType.None,
                IsConnectionActive = _activeConnections.Contains(item.Name),
            };
            RootNodes.Add(node);
        }
    }

    /// <summary>建立指定连接并加载其对象树（连接节点展开浏览）。</summary>
    public async Task ConnectAsync(DbObjectTreeNode connectionNode)
    {
        if (connectionNode is null || connectionNode.NodeType != DbObjectTreeNodeType.Connection)
            return;

        var connection = connectionNode.Connection;
        if (connection is null)
            return;

        IsLoading = true;
        StatusMessage = $"正在连接 {connection.Name}...";

        try
        {
            var nodes = await _schemaService.GetObjectTreeAsync(connection.Name);
            connectionNode.ClearChildren();
            foreach (var node in nodes)
            {
                connectionNode.AddChild(node);
            }

            connectionNode.IsConnectionActive = true;
            connectionNode.IsLoaded = true;
            _activeConnections.Add(connection.Name);
            StatusMessage = nodes.Count == 0 ? $"已连接 {connection.Name}，暂无数据库。" : $"已连接 {connection.Name}，加载 {nodes.Count} 个数据库。";
        }
        catch (Exception ex)
        {
            connectionNode.IsConnectionActive = false;
            StatusMessage = $"连接失败：{ex.Message}";
            throw;
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>断开指定连接节点，卸载其对象树。</summary>
    public void Disconnect(DbObjectTreeNode connectionNode)
    {
        if (connectionNode is null || connectionNode.NodeType != DbObjectTreeNodeType.Connection)
            return;

        var name = connectionNode.Name;
        connectionNode.ClearChildren();
        connectionNode.IsConnectionActive = false;
        connectionNode.IsLoaded = false;
        _activeConnections.Remove(name);
    }

    /// <summary>判断指定连接是否已连接。</summary>
    public bool IsConnected(string connectionName)
        => _activeConnections.Contains(connectionName);

    /// <summary>根据连接名查找连接根节点。</summary>
    public DbObjectTreeNode? FindConnectionNode(string connectionName)
        => RootNodes.FirstOrDefault(n => n.NodeType == DbObjectTreeNodeType.Connection && string.Equals(n.Name, connectionName, StringComparison.OrdinalIgnoreCase));

    /// <summary>加载指定连接下的对象树（兼容旧调用，加载到对应连接节点下）。</summary>
    public async Task LoadAsync(string connectionName)
    {
        if (string.IsNullOrWhiteSpace(connectionName))
            return;

        // 若已存在对应连接节点则连接之；否则仍走旧逻辑直接加载到根。
        var connNode = FindConnectionNode(connectionName);
        if (connNode is not null)
        {
            await ConnectAsync(connNode);
            return;
        }

        IsLoading = true;
        StatusMessage = $"正在加载 {connectionName} 的对象树...";

        try
        {
            var nodes = await _schemaService.GetObjectTreeAsync(connectionName);
            RootNodes.Clear();
            foreach (var node in nodes)
            {
                RootNodes.Add(node);
            }
            StatusMessage = nodes.Count == 0 ? "该连接下暂无数据库。" : $"已加载 {nodes.Count} 个数据库。";
        }
        catch (Exception ex)
        {
            StatusMessage = $"加载失败：{ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>按需展开：加载某类型文件夹下的具体对象（表/视图/存储过程等）。</summary>
    public async Task LoadFolderChildrenAsync(DbObjectTreeNode folderNode, string connectionName)
    {
        if (folderNode is null || folderNode.IsLoaded)
            return;

        if (folderNode.NodeType != DbObjectTreeNodeType.Folder)
            return;

        var databaseNode = FindAncestor(folderNode, DbObjectTreeNodeType.Database);
        var schemaNode = FindAncestor(folderNode, DbObjectTreeNodeType.Schema);
        string databaseName = databaseNode?.Name ?? folderNode.DatabaseName ?? string.Empty;
        string? schema = schemaNode?.Name ?? folderNode.Schema;

        var nodes = await _schemaService.GetDbObjectNodesAsync(
            connectionName,
            databaseName,
            folderNode.DatabaseObjectType,
            schema);

        // 刷新节点（清空占位符，加入真实对象）。
        folderNode.ClearChildren();
        foreach (var node in nodes)
        {
            folderNode.AddChild(node);
        }

        folderNode.IsLoaded = true;
    }

    /// <summary>按需展开：加载表/视图的子类型文件夹下的具体子对象（列/索引/键/约束/触发器）。</summary>
    public async Task LoadTableChildFolderAsync(DbObjectTreeNode childFolder, string connectionName)
    {
        if (childFolder is null || childFolder.IsLoaded)
            return;

        if (childFolder.NodeType != DbObjectTreeNodeType.ChildFolder)
            return;

        // 找到所属表/视图节点。
        var tableNode = FindAncestor(childFolder, DbObjectTreeNodeType.DbObject);
        var tableOrView = tableNode?.DbObject;
        if (tableOrView is not (Table or View))
            return;

        var databaseNode = FindAncestor(childFolder, DbObjectTreeNodeType.Database);
        string databaseName = databaseNode?.Name ?? childFolder.DatabaseName ?? string.Empty;
        bool isForView = tableOrView is View;

        var childType = GetChildTypeByFolder(childFolder.Name);

        var nodes = await _schemaService.GetTableChildNodesAsync(
            connectionName,
            databaseName,
            childType,
            tableOrView,
            isForView);

        childFolder.ClearChildren();
        foreach (var node in nodes)
        {
            childFolder.AddChild(node);
        }

        // 数量展示（如 Columns (3)）。
        childFolder.Text = nodes.Count > 0 ? $"{childFolder.Name} ({nodes.Count})" : childFolder.Name;
        childFolder.IsLoaded = true;
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

    private static DbObjectChildType GetChildTypeByFolder(string folderName)
        => folderName switch
        {
            "Columns" => DbObjectChildType.Column,
            "Triggers" => DbObjectChildType.Trigger,
            "Indexes" => DbObjectChildType.Index,
            "Keys" => DbObjectChildType.PrimaryKey,
            "Constraints" => DbObjectChildType.Constraint,
            _ => DbObjectChildType.None,
        };
}
