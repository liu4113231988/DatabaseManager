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
/// 阶段 2：加载连接的对象树（数据库 → Schema → 类型 → 对象），支持按需展开加载对象。
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

    /// <summary>加载指定连接下的对象树。</summary>
    public async Task LoadAsync(string connectionName)
    {
        if (string.IsNullOrWhiteSpace(connectionName))
            return;

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

    /// <summary>按需展开：加载某类型文件夹下的具体对象。</summary>
    public async Task LoadFolderChildrenAsync(DbObjectTreeNode folderNode, string connectionName)
    {
        if (folderNode is null || folderNode.IsLoaded)
            return;

        if (folderNode.NodeType != DbObjectTreeNodeType.Folder)
            return;

        // 找到所属数据库节点（文件夹的父级，即数据库节点）。
        var databaseNode = folderNode.Parent as DbObjectTreeNode;
        string databaseName = databaseNode?.Name ?? string.Empty;

        var nodes = await _schemaService.GetDbObjectNodesAsync(
            connectionName,
            databaseName,
            folderNode.DatabaseObjectType);

        foreach (var node in nodes)
        {
            folderNode.Children.Add(node);
        }

        folderNode.IsLoaded = true;
    }
}
