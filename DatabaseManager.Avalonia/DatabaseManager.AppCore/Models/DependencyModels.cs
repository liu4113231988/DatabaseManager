namespace DatabaseManager.AppCore.Models;

/// <summary>
/// 依赖方向选项（UI 友好）：哪些对象依赖于它 / 它依赖于哪些对象。
/// </summary>
public sealed record DependencyDirectionOption(string DisplayName, bool DependOnThis);

/// <summary>
/// 依赖关系节点（UI 友好）。描述一个被引用/引用对象。
/// </summary>
public class DependencyNode
{
    /// <summary>对象类型。</summary>
    public string ObjectType { get; }

    /// <summary>Schema。</summary>
    public string Schema { get; }

    /// <summary>对象名。</summary>
    public string ObjectName { get; }

    /// <summary>展示名。</summary>
    public string DisplayName { get; }

    /// <summary>子依赖节点（懒加载）。</summary>
    public IReadOnlyList<DependencyNode>? Children { get; set; }

    public DependencyNode(string objectType, string schema, string objectName, IReadOnlyList<DependencyNode>? children = null)
    {
        ObjectType = objectType;
        Schema = schema ?? string.Empty;
        ObjectName = objectName ?? string.Empty;
        Children = children;
        DisplayName = string.IsNullOrEmpty(Schema) ? ObjectName : $"{Schema}.{ObjectName}";
    }
}
