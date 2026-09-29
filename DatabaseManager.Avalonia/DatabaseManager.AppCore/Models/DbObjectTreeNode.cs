using System.Collections.ObjectModel;
using DatabaseInterpreter.Model;
using DatabaseManager.AppCore.Services;

namespace DatabaseManager.AppCore.Models;

/// <summary>
/// 对象浏览器树节点（AppCore 领域模型，UI 无关）。
/// 层级结构：连接 → 数据库 → Schema → 类型文件夹（表/视图/存储过程/函数/序列/触发器）→ 具体对象。
/// </summary>
public class DbObjectTreeNode : System.ComponentModel.INotifyPropertyChanged
{
    /// <summary>节点唯一名称（用于 TreeView 定位）。</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>节点显示文本。</summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>节点类型（用于图标选择与右键菜单路由）。</summary>
    public DbObjectTreeNodeType NodeType { get; set; } = DbObjectTreeNodeType.Folder;

    /// <summary>数据库对象类型（当节点对应数据库对象时有效）。</summary>
    public DatabaseObjectType DatabaseObjectType { get; set; } = DatabaseObjectType.None;

    /// <summary>节点关联的数据库对象（表/视图/存储过程等）。</summary>
    public DatabaseObject? DbObject { get; set; }

    /// <summary>所属数据库名（懒加载子节点时用于定位目标库）。</summary>
    public string? DatabaseName { get; set; }

    /// <summary>所属 Schema 名（用于过滤多 Schema 场景）。</summary>
    public string? Schema { get; set; }

    /// <summary>子节点。</summary>
    public ObservableCollection<DbObjectTreeNode> Children { get; } = new();

    /// <summary>父节点（用于向上定位所属数据库）。</summary>
    public DbObjectTreeNode? Parent { get; set; }

    private bool _isLoaded;
    /// <summary>是否已懒加载子节点（用于按需展开加载）。</summary>
    public bool IsLoaded
    {
        get => _isLoaded;
        set
        {
            if (_isLoaded == value) return;
            _isLoaded = value;
            OnPropertyChanged(nameof(IsLoaded));
            OnPropertyChanged(nameof(BadgeText));
            OnPropertyChanged(nameof(HasBadge));
        }
    }

    /// <summary>是否为「占位/假」子节点（用于懒加载前展示 loading 占位）。</summary>
    public bool IsPlaceholder { get; set; }

    private bool _isLoading;
    /// <summary>子级是否正在加载（连接/文件夹级加载指示；再次双击节点可取消）。</summary>
    public bool IsLoading
    {
        get => _isLoading;
        set
        {
            if (_isLoading == value) return;
            _isLoading = value;
            OnPropertyChanged(nameof(IsLoading));
        }
    }

    /// <summary>该节点当前加载操作的取消令牌源（加载期间可取消；完成后置空）。</summary>
    public CancellationTokenSource? LoadCts { get; set; }

    private bool _isVisible = true;
    /// <summary>就地过滤可见性：节点名匹配或拥有匹配后代时可见；清空过滤后恢复 true。</summary>
    public bool IsVisible
    {
        get => _isVisible;
        set
        {
            if (_isVisible == value) return;
            _isVisible = value;
            OnPropertyChanged(nameof(IsVisible));
        }
    }

    /// <summary>关联的连接项（当节点为 Connection 类型时有效）。</summary>
    public ConnectionItem? Connection { get; set; }

    private bool _isConnectionActive;
    /// <summary>
    /// 该连接节点是否已建立连接（用于区分已连接/未连接状态与图标）。
    /// 变化时通知属性变更，供工具栏/上下文菜单等根据节点状态计算可用性。
    /// </summary>
    public bool IsConnectionActive
    {
        get => _isConnectionActive;
        set
        {
            if (_isConnectionActive == value) return;
            _isConnectionActive = value;
            OnPropertyChanged(nameof(IsConnectionActive));
        }
    }

    private string? _connectionState;
    /// <summary>连接状态：null / "Connecting" / "Failed"，用于驱动状态点颜色。</summary>
    public string? ConnectionState
    {
        get => _connectionState;
        set
        {
            if (_connectionState == value) return;
            _connectionState = value;
            OnPropertyChanged(nameof(ConnectionState));
        }
    }

    private string? _colorTag;
    /// <summary>连接颜色标签（hex；仅 Connection 类型节点使用）。</summary>
    public string? ColorTag
    {
        get => _colorTag;
        set
        {
            if (_colorTag == value) return;
            _colorTag = value;
            OnPropertyChanged(nameof(ColorTag));
            OnPropertyChanged(nameof(HasColorTag));
            OnPropertyChanged(nameof(ColorTagBrush));
        }
    }

    /// <summary>是否显示颜色标签点。</summary>
    public bool HasColorTag => !string.IsNullOrEmpty(_colorTag);

    /// <summary>颜色标签对应的画刷（解析失败时返回透明，不渲染可见点）。</summary>
    public Avalonia.Media.IBrush ColorTagBrush
    {
        get
        {
            try
            {
                if (!string.IsNullOrEmpty(_colorTag))
                {
                    var color = Avalonia.Media.Color.Parse(_colorTag);
                    return new Avalonia.Media.SolidColorBrush(color);
                }
            }
            catch
            {
                // 无效颜色字符串按无色处理。
            }

            return Avalonia.Media.Brushes.Transparent;
        }
    }

    private bool _isExpanded;
    /// <summary>节点展开状态（与 TreeViewItem.IsExpanded 双向绑定，容器重建后据此恢复展开）。</summary>
    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (_isExpanded == value) return;
            _isExpanded = value;
            OnPropertyChanged(nameof(IsExpanded));
        }
    }

    /// <summary>徽标文本（如 "Tables (20)" 的计数部分），仅 Folder 且已加载时有值；不计占位节点。</summary>
    public string BadgeText
    {
        get
        {
            if (NodeType != DbObjectTreeNodeType.Folder || !IsLoaded || Children.Count == 0)
            {
                return string.Empty;
            }

            if (Children[0].IsPlaceholder)
            {
                return string.Empty;
            }

            int count = 0;
            foreach (var child in Children)
            {
                if (!child.IsPlaceholder)
                {
                    count++;
                }
            }

            return count > 0 ? $"({count})" : string.Empty;
        }
    }

    /// <summary>是否显示徽标。</summary>
    public bool HasBadge => !string.IsNullOrEmpty(BadgeText);

    private bool _isHighlighted;
    /// <summary>是否高亮（搜索定位时短暂高亮）。</summary>
    public bool IsHighlighted
    {
        get => _isHighlighted;
        set
        {
            if (_isHighlighted == value) return;
            _isHighlighted = value;
            OnPropertyChanged(nameof(IsHighlighted));
        }
    }

    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(name));

    /// <summary>通知徽标相关属性已变化（供「加载更多」续接后刷新计数）。</summary>
    public void RefreshBadge()
    {
        OnPropertyChanged(nameof(BadgeText));
        OnPropertyChanged(nameof(HasBadge));
    }

    public DbObjectTreeNode()
    {
        Children.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(BadgeText));
            OnPropertyChanged(nameof(HasBadge));
        };
    }

    /// <summary>向父节点注册子节点（自动维护 Parent 引用）。</summary>
    public void AddChild(DbObjectTreeNode child)
    {
        child.Parent = this;
        Children.Add(child);
    }

    /// <summary>清空并释放所有子节点。</summary>
    public void ClearChildren()
    {
        foreach (var child in Children)
        {
            child.Parent = null;
        }
        Children.Clear();
        IsLoaded = false;
    }

    /// <summary>查找指定子节点（按名称与类型）。</summary>
    public DbObjectTreeNode? FindChild(string name, DbObjectTreeNodeType? nodeType = null)
        => Children.FirstOrDefault(c =>
            c.Name == name && (nodeType is null || c.NodeType == nodeType));

    /// <summary>
    /// 生成拖拽到 SQL 编辑器时插入的文本（默认模式）。
    /// 表/视图自动包含 schema 限定名并按目标数据库方言正确引用标识符；
    /// 列名直接返回名称（由用户自行添加限定前缀）。
    /// </summary>
    public string GetDragDropSqlText() => GetDragDropSqlText(DragTextMode.Default);

    /// <summary>
    /// 按修饰键模式生成拖拽插入文本。
    /// Ctrl → 纯名称无引号；Shift+表 → SELECT 模板；Shift+列 → column AS alias。
    /// </summary>
    public string GetDragDropSqlText(DragTextMode mode)
    {
        if (string.IsNullOrWhiteSpace(Name))
            return string.Empty;

        var dbType = GetDatabaseType();

        // Ctrl → 纯名称模式，跳过 schema 和引号
        if (mode == DragTextMode.PlainName)
        {
            return Name;
        }

        // Shift 修饰的特殊模板
        if (mode == DragTextMode.SelectTemplate)
        {
            // 只有表/视图生成 SELECT 模板，其他降级为默认
            if (NodeType == DbObjectTreeNodeType.DbObject
                && DatabaseObjectType is DatabaseObjectType.Table or DatabaseObjectType.View)
            {
                var qualified = BuildQualifiedName(dbType);
                return $"SELECT * FROM {qualified};";
            }
            // 非表/视图节点 fallback 到默认
            mode = DragTextMode.Default;
        }

        if (mode == DragTextMode.ColumnAlias)
        {
            // 只有列节点生成 alias 格式，其他降级为默认
            if (NodeType == DbObjectTreeNodeType.ChildObject)
            {
                var quoted = QuoteIdentifier(Name, dbType);
                return $"{quoted} AS {Name}";
            }
            mode = DragTextMode.Default;
        }

        // 默认模式
        return mode switch
        {
            DragTextMode.Default => BuildQualifiedOrPlain(dbType),
            _ => BuildQualifiedOrPlain(dbType),
        };
    }

    /// <summary>默认模式下的文本：表/视图带 schema 限定 + 引号，其余直接引号名称。</summary>
    private string BuildQualifiedOrPlain(DatabaseType dbType)
    {
        switch (NodeType)
        {
            case DbObjectTreeNodeType.DbObject:
                if (DatabaseObjectType is DatabaseObjectType.Table or DatabaseObjectType.View)
                    return BuildQualifiedName(dbType);
                return QuoteIdentifier(Name, dbType);

            case DbObjectTreeNodeType.ChildObject:
                return QuoteIdentifier(Name, dbType);

            default:
                return QuoteIdentifier(Name, dbType);
        }
    }

    /// <summary>按数据库方言构建带 schema 的引号限定名。</summary>
    private string BuildQualifiedName(DatabaseType dbType)
    {
        var schema = Schema ?? Parent?.Schema;
        if (!string.IsNullOrWhiteSpace(schema))
        {
            return $"{QuoteIdentifier(schema, dbType)}.{QuoteIdentifier(Name, dbType)}";
        }
        return QuoteIdentifier(Name, dbType);
    }

    /// <summary>向上追溯到连接节点。</summary>
    public DbObjectTreeNode? FindConnectionNode()
    {
        var current = this;
        while (current is not null)
        {
            if (current.NodeType == DbObjectTreeNodeType.Connection)
                return current;
            current = current.Parent;
        }
        return null;
    }

    /// <summary>获取所属连接的数据库类型（解析为 DatabaseType 枚举）；无法确定时返回 Unknown。</summary>
    public DatabaseType GetDatabaseType()
    {
        var connNode = FindConnectionNode();
        if (connNode?.Connection is not null)
            return ConnectionHelper.ParseDatabaseType(connNode.Connection.DatabaseType);
        return DatabaseType.Unknown;
    }

    /// <summary>
    /// 按数据库方言给标识符加引号。
    /// 未知方言或包含特殊字符时保守处理：仅当标识符不是纯字母数字下划线时才添加引号。
    /// </summary>
    public static string QuoteIdentifier(string identifier, DatabaseType dbType)
    {
        if (string.IsNullOrWhiteSpace(identifier)) return identifier;

        // 纯标识符（字母/数字/下划线开头字母/下划线）在多数数据库中可省略引号
        bool needsQuote = !IsPlainIdentifier(identifier);
        if (!needsQuote && dbType == DatabaseType.Unknown)
            return identifier;

        var (open, close) = dbType switch
        {
            DatabaseType.MySql => ("`", "`"),
            DatabaseType.SqlServer or DatabaseType.Sqlite or DatabaseType.DM => ("[", "]"),
            // Postgres, KingbaseES, Oracle, DuckDB 及其他统一用双引号
            _ => ("\"", "\""),
        };

        // 内部出现结束符时做转义（双写）
        var escaped = identifier.Replace(close, close + close);
        return open + escaped + close;
    }

    private static bool IsPlainIdentifier(string identifier)
    {
        if (string.IsNullOrEmpty(identifier)) return false;
        if (!char.IsLetter(identifier[0]) && identifier[0] != '_') return false;
        for (int i = 1; i < identifier.Length; i++)
        {
            if (!char.IsLetterOrDigit(identifier[i]) && identifier[i] != '_')
                return false;
        }
        return true;
    }
}

/// <summary>拖拽修饰键模式（由 UI 层把 Avalonia KeyModifiers 翻译过来）。</summary>
public enum DragTextMode
{
    /// <summary>默认：按数据库方言生成带引号的限定名（表/视图自动加 schema）。</summary>
    Default,

    /// <summary>Ctrl 修饰：纯名称，不加引号、不带 schema。</summary>
    PlainName,

    /// <summary>Shift + 表/视图：生成 SELECT * FROM 限定名; 模板。</summary>
    SelectTemplate,

    /// <summary>Shift + 列：生成 column AS alias 格式（其他节点降级为 Default）。</summary>
    ColumnAlias,
}

/// <summary>对象树节点类型。</summary>
public enum DbObjectTreeNodeType
{
    /// <summary>连接（顶层）。</summary>
    Connection,

    /// <summary>数据库。</summary>
    Database,

    /// <summary>Schema。</summary>
    Schema,

    /// <summary>类型文件夹（表/视图等）。</summary>
    Folder,

    /// <summary>具体数据库对象（表/视图/存储过程等）。</summary>
    DbObject,

    /// <summary>表/视图的子类型文件夹（列/索引/键/约束/触发器）。</summary>
    ChildFolder,

    /// <summary>表/视图的子对象（列/索引/键/约束/触发器）。</summary>
    ChildObject,
}

/// <summary>表/视图子对象类型（用于图标与右键菜单路由）。</summary>
public enum DbObjectChildType
{
    None,
    Column,
    PrimaryKey,
    ForeignKey,
    Index,
    Constraint,
    Trigger,
}
