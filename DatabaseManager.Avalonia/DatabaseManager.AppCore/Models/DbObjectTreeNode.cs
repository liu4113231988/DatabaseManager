using System.Collections.ObjectModel;
using DatabaseInterpreter.Model;

namespace DatabaseManager.AppCore.Models;

/// <summary>
/// 对象浏览器树节点（AppCore 领域模型，UI 无关）。
/// 层级结构：连接 → 数据库 → Schema → 类型文件夹（表/视图/存储过程/函数/序列/触发器）→ 具体对象。
/// </summary>
public class DbObjectTreeNode
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

    /// <summary>是否已懒加载子节点（用于按需展开加载）。</summary>
    public bool IsLoaded { get; set; }

    /// <summary>是否为「占位/假」子节点（用于懒加载前展示 loading 占位）。</summary>
    public bool IsPlaceholder { get; set; }

    /// <summary>关联的连接项（当节点为 Connection 类型时有效）。</summary>
    public ConnectionItem? Connection { get; set; }

    /// <summary>该连接节点是否已建立连接（用于区分已连接/未连接状态与图标）。</summary>
    public bool IsConnectionActive { get; set; }

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
