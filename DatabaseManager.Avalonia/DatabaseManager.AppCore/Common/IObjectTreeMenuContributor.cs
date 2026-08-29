using DatabaseManager.AppCore.Models;
using DatabaseManager.AppCore.ViewModels;

namespace DatabaseManager.AppCore.Common;

/// <summary>
/// 对象树右键菜单扩展点：外部代码实现本接口并调用 <see cref="ObjectTreeMenuRegistry.Register"/>
/// 即可为对象树节点追加自定义菜单项，无需修改内置的 <c>ObjectTreeContextMenuBuilder</c>。
/// </summary>
public interface IObjectTreeMenuContributor
{
    /// <summary>排序（小的在前；内置菜单始终在扩展菜单之前）。</summary>
    int Order => 100;

    /// <summary>是否适用于该节点（按 NodeType / DatabaseObjectType / DbObject 过滤）。</summary>
    bool AppliesTo(DbObjectTreeNode node);

    /// <summary>向菜单追加菜单项（在内置菜单之后调用）。</summary>
    void Contribute(ObjectTreeMenuContext context);
}

/// <summary>菜单构建上下文：向 <see cref="MenuItems"/> 追加 MenuItem / Separator 即可。</summary>
public sealed class ObjectTreeMenuContext
{
    /// <summary>菜单项集合（追加 MenuItem 或 Separator）。</summary>
    public System.Collections.IList MenuItems { get; }

    /// <summary>右键命中的树节点。</summary>
    public DbObjectTreeNode Node { get; }

    /// <summary>主窗口 ViewModel（连接上下文、生成脚本等）。</summary>
    public MainWindowViewModel ViewModel { get; }

    /// <summary>执行异步动作的封送器（菜单点击处理用）。</summary>
    public Action<Func<Task>> RunAsync { get; }

    public ObjectTreeMenuContext(System.Collections.IList menuItems, DbObjectTreeNode node, MainWindowViewModel viewModel, Action<Func<Task>> runAsync)
    {
        MenuItems = menuItems;
        Node = node;
        ViewModel = viewModel;
        RunAsync = runAsync;
    }
}

/// <summary>
/// 对象树菜单贡献者注册表：静态登记（App 启动时注册一次即可，线程安全）。
/// </summary>
public static class ObjectTreeMenuRegistry
{
    private static readonly List<IObjectTreeMenuContributor> Contributors = new();
    private static readonly object Lock = new();

    /// <summary>登记一个菜单贡献者（重复登记同一实例会被忽略）。</summary>
    public static void Register(IObjectTreeMenuContributor contributor)
    {
        lock (Lock)
        {
            if (!Contributors.Contains(contributor))
            {
                Contributors.Add(contributor);
            }
        }
    }

    /// <summary>取按 Order 排序、适用于该节点的贡献者（供构建器调用）。</summary>
    public static IReadOnlyList<IObjectTreeMenuContributor> GetContributors(DbObjectTreeNode node)
    {
        lock (Lock)
        {
            return Contributors.Where(c => c.AppliesTo(node)).OrderBy(c => c.Order).ToList();
        }
    }
}
