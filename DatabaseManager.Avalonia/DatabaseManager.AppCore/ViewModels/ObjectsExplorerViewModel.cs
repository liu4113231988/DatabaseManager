using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Threading;
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

    /// <summary>加载全部已保存连接为对象树根节点。配置了分组的连接归入「📁 分组」文件夹节点，其余平铺展示。</summary>
    public void LoadConnections(IEnumerable<ConnectionItem>? connections)
    {
        // 增量更新：复用已有连接节点，避免重建导致 TreeView 展开状态被重置（整棵树折叠）。
        if (connections is null)
            return;

        var newList = connections.ToList();
        bool hasAnyGroup = newList.Any(c => !string.IsNullOrWhiteSpace(c.Group));

        // 1) 建立旧节点索引（按连接名，不区分大小写）
        var existingMap = new Dictionary<string, DbObjectTreeNode>(StringComparer.OrdinalIgnoreCase);
        foreach (var n in RootNodes)
        {
            if (n.NodeType == DbObjectTreeNodeType.Connection && !string.IsNullOrEmpty(n.Name))
            {
                existingMap[n.Name] = n;
            }
        }

        // 2) 同步 active 集合（以复用后的节点状态为准）
        _activeConnections.Clear();
        foreach (var kv in existingMap)
        {
            if (kv.Value.IsConnectionActive)
            {
                _activeConnections.Add(kv.Key);
            }
        }

        // 3) 按新列表顺序重建 RootNodes（复用旧节点，新增则创建）；
        //    启用分组时：分组连接挂到「📁 分组」节点下，未分组连接保持平铺在前。
        RootNodes.Clear();

        var groupNodes = new Dictionary<string, DbObjectTreeNode>(StringComparer.OrdinalIgnoreCase);

        DbObjectTreeNode GetGroupNode(string group)
        {
            if (!groupNodes.TryGetValue(group, out var folder))
            {
                folder = new DbObjectTreeNode
                {
                    Name = group,
                    Text = $"📁 {group}",
                    NodeType = DbObjectTreeNodeType.Folder,
                };
                groupNodes[group] = folder;
                RootNodes.Add(folder);
            }

            return folder;
        }

        foreach (var item in newList)
        {
            if (existingMap.TryGetValue(item.Name, out var existing))
            {
                // 连接相关属性被修改：断开连接并折叠节点（下次展开时按新属性重新连接）；
                // 未修改：保留 Children / IsLoaded / IsExpanded 等状态，RootNodes 重建容器后
                // 由 TreeViewItem.IsExpanded 与节点 IsExpanded 的双向绑定恢复展开状态。
                if (IsConnectionProfileChanged(existing.Connection, item))
                {
                    Disconnect(existing);
                    existing.IsExpanded = false;
                }

                existing.Connection = item;
                existing.Text = item.Name;
                existing.ColorTag = item.ColorTag;
                existing.IsConnectionActive = _activeConnections.Contains(item.Name);
                AttachConnectionNode(existing, item, hasAnyGroup, GetGroupNode);
                existingMap.Remove(item.Name);
            }
            else
            {
                // 新增连接
                var node = new DbObjectTreeNode
                {
                    Name = item.Name,
                    Text = item.Name,
                    NodeType = DbObjectTreeNodeType.Connection,
                    Connection = item,
                    DatabaseObjectType = DatabaseObjectType.None,
                    ColorTag = item.ColorTag,
                    IsConnectionActive = _activeConnections.Contains(item.Name),
                };
                AttachConnectionNode(node, item, hasAnyGroup, GetGroupNode);
            }
        }

        // 4) 旧列表中剩下的是已被删除的连接，从 active 集合中清理
        foreach (var kv in existingMap)
        {
            _activeConnections.Remove(kv.Key);
        }
    }

    /// <summary>
    /// 比较连接相关属性（数据库类型/服务器/端口/数据库/认证方式/SSL 等）是否被修改。
    /// 名称、优先级等不影响实际连接的展示属性不参与比较。
    /// </summary>
    private static bool IsConnectionProfileChanged(ConnectionItem? oldItem, ConnectionItem newItem)
        => oldItem is not null && (
            !string.Equals(oldItem.DatabaseType, newItem.DatabaseType, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(oldItem.Server, newItem.Server, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(oldItem.Port, newItem.Port, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(oldItem.Database, newItem.Database, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(oldItem.ServerVersion, newItem.ServerVersion, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(oldItem.UserId, newItem.UserId, StringComparison.Ordinal)
            || !string.Equals(oldItem.Password, newItem.Password, StringComparison.Ordinal)
            || oldItem.IntegratedSecurity != newItem.IntegratedSecurity
            || oldItem.UseSsl != newItem.UseSsl);

    /// <summary>建立指定连接并加载其对象树（连接节点展开浏览）。加载中再次触发可经节点 LoadCts 取消。</summary>
    public async Task ConnectAsync(DbObjectTreeNode connectionNode)
    {
        if (connectionNode is null || connectionNode.NodeType != DbObjectTreeNodeType.Connection)
            return;

        var connection = connectionNode.Connection;
        if (connection is null)
            return;

        IsLoading = true;
        connectionNode.IsLoading = true;
        StatusMessage = $"正在连接 {connection.Name}...";
        connectionNode.LoadCts = new CancellationTokenSource();

        try
        {
            var nodes = await _schemaService.GetObjectTreeAsync(connection.Name, connectionNode.LoadCts.Token);
            connectionNode.ClearChildren();
            foreach (var node in nodes)
            {
                connectionNode.AddChild(node);
            }

            connectionNode.IsConnectionActive = true;
            connectionNode.IsLoaded = true;
            _activeConnections.Add(connection.Name);
            ClearPrefetchCache(connection.Name);

            // 过滤激活时，重连后的新节点默认可见，需按当前关键字重新应用过滤。
            ReapplyFilterIfActive(connectionNode);

            StatusMessage = nodes.Count == 0 ? $"已连接 {connection.Name}，暂无数据库。" : $"已连接 {connection.Name}，加载 {nodes.Count} 个数据库。";
        }
        catch (OperationCanceledException)
        {
            connectionNode.IsConnectionActive = false;
            StatusMessage = $"连接 {connection.Name} 已取消。";
        }
        catch (Exception ex)
        {
            connectionNode.IsConnectionActive = false;
            StatusMessage = $"连接失败：{ex.Message}";
            throw;
        }
        finally
        {
            connectionNode.IsLoading = false;
            connectionNode.LoadCts?.Dispose();
            connectionNode.LoadCts = null;
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
        ClearPrefetchCache(name);
    }

    /// <summary>把连接节点挂到根集合或所属分组节点下（启用分组且有分组名时归组）。</summary>
    private void AttachConnectionNode(
        DbObjectTreeNode node,
        ConnectionItem item,
        bool hasAnyGroup,
        Func<string, DbObjectTreeNode> getGroupNode)
    {
        if (hasAnyGroup && !string.IsNullOrWhiteSpace(item.Group))
        {
            getGroupNode(item.Group.Trim()).AddChild(node);
        }
        else
        {
            RootNodes.Add(node);
        }
    }

    /// <summary>判断指定连接是否已连接。</summary>
    public bool IsConnected(string connectionName)
        => _activeConnections.Contains(connectionName);

    /// <summary>根据连接名查找连接根节点（含分组文件夹内的连接节点）。</summary>
    public DbObjectTreeNode? FindConnectionNode(string connectionName)
    {
        foreach (var node in RootNodes)
        {
            if (node.NodeType == DbObjectTreeNodeType.Connection
                && string.Equals(node.Name, connectionName, StringComparison.OrdinalIgnoreCase))
            {
                return node;
            }

            foreach (var child in node.Children)
            {
                if (child.NodeType == DbObjectTreeNodeType.Connection
                    && string.Equals(child.Name, connectionName, StringComparison.OrdinalIgnoreCase))
                {
                    return child;
                }
            }
        }

        return null;
    }

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
                ReapplyFilterIfActive(node);
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

    /// <summary>兄弟类型文件夹的预取缓存：key = 连接|库|schema|类型。值可为空列表（表示已取回且为空）。</summary>
    private readonly ConcurrentDictionary<string, IReadOnlyList<DbObjectTreeNode>> _prefetchCache =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>预取查询的并发上限（避免展开文件夹时对服务器形成连接风暴）。</summary>
    private static readonly SemaphoreSlim PrefetchGate = new(4);

    /// <summary>按需加载数据库下的 Schema 或类型文件夹，避免跨数据库引擎首次连接时建立额外连接。</summary>
    public async Task LoadDatabaseChildrenAsync(DbObjectTreeNode databaseNode, string connectionName)
    {
        if (databaseNode is null || databaseNode.IsLoaded || databaseNode.NodeType != DbObjectTreeNodeType.Database)
            return;

        databaseNode.IsLoading = true;
        databaseNode.LoadCts = new CancellationTokenSource();

        try
        {
            var nodes = await _schemaService.GetDatabaseChildrenAsync(
                connectionName,
                databaseNode.Name,
                databaseNode.LoadCts.Token);

            databaseNode.ClearChildren();
            foreach (var node in nodes)
            {
                databaseNode.AddChild(node);
            }
            databaseNode.IsLoaded = true;
            ReapplyFilterIfActive(databaseNode);
        }
        catch (OperationCanceledException)
        {
            databaseNode.ClearChildren();
            databaseNode.AddChild(new DbObjectTreeNode
            {
                Name = "_Cancelled_",
                Text = "（已取消加载，再次展开可重试）",
                NodeType = DbObjectTreeNodeType.Folder,
                IsPlaceholder = true,
                IsLoaded = true,
            });
            StatusMessage = $"加载数据库 {databaseNode.Name} 已取消。";
        }
        finally
        {
            databaseNode.IsLoading = false;
            databaseNode.LoadCts?.Dispose();
            databaseNode.LoadCts = null;
        }
    }

    /// <summary>按需展开：一次性全量加载某类型文件夹下的具体对象（表/视图/存储过程等，不再分页）。
    /// 加载完成后并行预取同 Schema 下其余类型文件夹（放入缓存，展开时零等待）。加载中再次双击可取消。</summary>
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

        // 命中预取缓存：立即填充，不发起查询。
        var cacheKey = BuildPrefetchKey(connectionName, databaseName, schema, folderNode.DatabaseObjectType);
        if (_prefetchCache.TryRemove(cacheKey, out var cached))
        {
            FillFolder(folderNode, cached);
            PrefetchSiblingFoldersAsync(folderNode, connectionName, databaseName, schema);
            return;
        }

        folderNode.IsLoading = true;
        folderNode.LoadCts = new CancellationTokenSource();

        try
        {
            var nodes = await _schemaService.GetDbObjectNodesAsync(
                connectionName,
                databaseName,
                folderNode.DatabaseObjectType,
                schema,
                folderNode.LoadCts.Token);

            FillFolder(folderNode, nodes);
            ReapplyFilterIfActive(folderNode);
        }
        catch (OperationCanceledException)
        {
            folderNode.ClearChildren();
            folderNode.AddChild(new DbObjectTreeNode
            {
                Name = "_Cancelled_",
                Text = "（已取消加载，再次展开可重试）",
                NodeType = DbObjectTreeNodeType.Folder,
                IsPlaceholder = true,
                IsLoaded = true,
            });
            folderNode.IsLoaded = false;
            StatusMessage = $"加载 {folderNode.Name} 已取消。";
            return;
        }
        finally
        {
            folderNode.IsLoading = false;
            folderNode.LoadCts?.Dispose();
            folderNode.LoadCts = null;
        }

        // 主文件夹就绪后，后台并行预取同层其余类型文件夹（对齐 SSMS 展开即可见全部类型的体验）。
        PrefetchSiblingFoldersAsync(folderNode, connectionName, databaseName, schema);
    }

    /// <summary>填充文件夹子节点：一次性全量加入（含空状态占位），不再懒分页。</summary>
    private void FillFolder(DbObjectTreeNode folderNode, IReadOnlyList<DbObjectTreeNode> nodes)
    {
        folderNode.ClearChildren();

        if (nodes.Count == 0)
        {
            // 空状态：无对象时显示占位提示（而非空白），便于用户感知可“新建”
            folderNode.AddChild(new DbObjectTreeNode
            {
                Name = "_Empty_",
                Text = "（空）",
                NodeType = folderNode.NodeType,
                IsPlaceholder = true,
                IsLoaded = true,
            });
        }
        else
        {
            foreach (var node in nodes)
            {
                folderNode.AddChild(node);
            }
        }

        folderNode.IsLoaded = true;
    }

    /// <summary>后台并行预取同层（同库同 Schema）其余未加载的类型文件夹，结果放入缓存供展开时命中。</summary>
    private void PrefetchSiblingFoldersAsync(DbObjectTreeNode folderNode, string connectionName, string databaseName, string? schema)
    {
        var parent = folderNode.Parent;
        if (parent is null)
            return;

        foreach (var sibling in parent.Children)
        {
            if (sibling == folderNode
                || sibling.NodeType != DbObjectTreeNodeType.Folder
                || sibling.IsLoaded
                || sibling.DatabaseObjectType == DatabaseObjectType.None)
            {
                continue;
            }

            var siblingType = sibling.DatabaseObjectType;
            var key = BuildPrefetchKey(connectionName, databaseName, schema, siblingType);
            if (_prefetchCache.ContainsKey(key))
            {
                continue;
            }

            _ = Task.Run(async () =>
            {
                await PrefetchGate.WaitAsync();
                try
                {
                    if (_prefetchCache.ContainsKey(key))
                    {
                        return;
                    }

                    var nodes = await _schemaService.GetDbObjectNodesAsync(connectionName, databaseName, siblingType, schema);
                    _prefetchCache[key] = nodes;
                }
                catch
                {
                    // 预取失败静默：用户展开该文件夹时走正常加载路径并能看到错误。
                }
                finally
                {
                    PrefetchGate.Release();
                }
            });
        }
    }

    private static string BuildPrefetchKey(string connectionName, string databaseName, string? schema, DatabaseObjectType type)
        => $"{connectionName}|{databaseName}|{schema ?? string.Empty}|{type}";

    /// <summary>清理指定连接的预取缓存（断开/重连后避免陈旧数据）。</summary>
    private void ClearPrefetchCache(string connectionName)
    {
        var prefix = connectionName + "|";
        foreach (var key in _prefetchCache.Keys)
        {
            if (key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                _prefetchCache.TryRemove(key, out _);
            }
        }
    }

    #region 就地过滤

    /// <summary>当前就地过滤关键字（null 表示未启用过滤）。</summary>
    private string? _filterKeyword;

    /// <summary>进入过滤前各节点的展开状态快照（清空过滤后恢复；仅记录一次）。</summary>
    private readonly Dictionary<DbObjectTreeNode, bool> _preFilterExpansion = new();

    /// <summary>就地过滤：节点名匹配或拥有匹配后代时可见，其余隐藏；自动展开匹配父级。
    /// 空关键字清除过滤并恢复展开状态。未加载的子级不触发懒加载（避免连接风暴）。</summary>
    public void ApplyTreeFilter(string? keyword)
    {
        _filterKeyword = string.IsNullOrWhiteSpace(keyword) ? null : keyword.Trim();

        if (_filterKeyword is null)
        {
            ClearTreeFilter();
            return;
        }

        int matchCount = 0;
        foreach (var root in RootNodes)
        {
            ApplyFilterToNode(root, _filterKeyword, ref matchCount);
        }

        StatusMessage = $"就地过滤「{_filterKeyword}」：{matchCount} 个匹配节点（未加载的子级展开后参与过滤）。";
    }

    /// <summary>递归计算节点子树的可见性；返回该子树是否存在可见节点。</summary>
    private bool ApplyFilterToNode(DbObjectTreeNode node, string keyword, ref int matchCount)
    {
        bool selfMatch = !string.IsNullOrEmpty(node.Name)
            && node.Name.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0;

        bool childMatch = false;
        foreach (var child in node.Children)
        {
            if (ApplyFilterToNode(child, keyword, ref matchCount))
            {
                childMatch = true;
            }
        }

        bool visible = selfMatch || childMatch;
        node.IsVisible = visible;

        if (visible)
        {
            // 只在首次进入过滤时快照展开状态，避免过滤期间的自动展开覆盖原始值。
            if (!_preFilterExpansion.ContainsKey(node))
            {
                _preFilterExpansion[node] = node.IsExpanded;
            }

            if (childMatch)
            {
                node.IsExpanded = true;
            }
        }

        if (selfMatch)
        {
            matchCount++;
        }

        return visible;
    }

    /// <summary>清除就地过滤：恢复全部节点可见性与过滤前的展开状态。</summary>
    public void ClearTreeFilter()
    {
        _filterKeyword = null;

        foreach (var entry in _preFilterExpansion)
        {
            entry.Key.IsExpanded = entry.Value;
        }

        _preFilterExpansion.Clear();

        foreach (var root in RootNodes)
        {
            ResetVisibility(root);
        }
    }

    private void ResetVisibility(DbObjectTreeNode node)
    {
        node.IsVisible = true;
        foreach (var child in node.Children)
        {
            ResetVisibility(child);
        }
    }

    /// <summary>过滤激活时对刚加载完的子树重应用过滤（新加载节点默认可见，需重算）。</summary>
    private void ReapplyFilterIfActive(DbObjectTreeNode subtreeRoot)
    {
        if (_filterKeyword is null)
        {
            return;
        }

        int matchCount = 0;
        ApplyFilterToNode(subtreeRoot, _filterKeyword, ref matchCount);
    }

    #endregion

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

        childFolder.IsLoading = true;
        childFolder.LoadCts = new CancellationTokenSource();

        try
        {
            var nodes = await _schemaService.GetTableChildNodesAsync(
                connectionName,
                databaseName,
                childType,
                tableOrView,
                isForView,
                childFolder.LoadCts.Token);

            childFolder.ClearChildren();
            foreach (var node in nodes)
            {
                childFolder.AddChild(node);
            }

            // 空状态：无子对象时显示占位
            if (nodes.Count == 0)
            {
                childFolder.AddChild(new DbObjectTreeNode
                {
                    Name = "_Empty_",
                    Text = "（空）",
                    NodeType = DbObjectTreeNodeType.ChildFolder,
                    IsPlaceholder = true,
                    IsLoaded = true,
                });
            }

            // 数量展示（如 Columns (3)）。
            childFolder.Text = nodes.Count > 0 ? $"{childFolder.Name} ({nodes.Count})" : childFolder.Name;
            childFolder.IsLoaded = true;
            ReapplyFilterIfActive(childFolder);
        }
        catch (OperationCanceledException)
        {
            childFolder.ClearChildren();
            StatusMessage = $"加载 {childFolder.Name} 已取消。";
        }
        finally
        {
            childFolder.IsLoading = false;
            childFolder.LoadCts?.Dispose();
            childFolder.LoadCts = null;
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
