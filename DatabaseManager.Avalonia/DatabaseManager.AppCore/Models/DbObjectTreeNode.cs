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

    /// <summary>子节点。</summary>
    public ObservableCollection<DbObjectTreeNode> Children { get; } = new();

    /// <summary>父节点（用于向上定位所属数据库）。</summary>
    public DbObjectTreeNode? Parent { get; set; }

    /// <summary>是否已懒加载子节点（用于按需展开加载）。</summary>
    public bool IsLoaded { get; set; }

    /// <summary>向父节点注册子节点（自动维护 Parent 引用）。</summary>
    public void AddChild(DbObjectTreeNode child)
    {
        child.Parent = this;
        Children.Add(child);
    }
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

    /// <summary>具体数据库对象。</summary>
    DbObject,
}
