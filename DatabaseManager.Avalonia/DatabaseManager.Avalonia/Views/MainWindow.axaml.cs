using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using DatabaseInterpreter.Model;
using DatabaseManager.AppCore.Common;
using DatabaseManager.AppCore.Models;
using DatabaseManager.AppCore.Services;
using DatabaseManager.AppCore.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;

namespace DatabaseManager.Avalonia.Views;

public partial class MainWindow : Window
{
    private IServiceProvider? _services;
    private QueryTabViewModel? _currentQueryTab;
    private DataEditorViewModel? _dataEditor;

    public MainWindow()
    {
        InitializeComponent();
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);

        _services = (Application.Current as App)?.Services;

        if (DataContext is MainWindowViewModel vm)
        {
            _dataEditor = vm.DataEditor;
            vm.Initialize();

            // 设置关闭标签页的回调（用于显示未保存提示）
            vm.RequestCloseTab = RequestCloseTabAsync;

            // 监听数据编辑列变化，动态重建可编辑 DataGrid 列。
            _dataEditor.Columns.CollectionChanged += DataEditor_ColumnsChanged;

            // 监听当前查询标签的列变化，动态重建 DataGrid 列。
            RefreshQueryTabColumnListener();

            // 监听 SelectedQueryTab 变化以切换 DataGrid 列监听目标。
            vm.PropertyChanged += MainWindow_PropertyChanged;

            // 通过路由事件监听 TreeViewItem 展开，实现点击展开箭头时的按需懒加载（对齐 dbeaver）。
            ObjectsTree.AddHandler(TreeViewItem.ExpandedEvent, ObjectsTree_Item_Expanded);

            // 监听对象树选中变化，更新 Schema 选择器上下文。
            ObjectsTree.SelectionChanged += ObjectsTree_SelectionChanged;
        }
    }

    /// <summary>SelectedQueryTab 属性变化时切换 DataGrid 列监听目标。</summary>
    private void MainWindow_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainWindowViewModel.SelectedQueryTab))
        {
            RefreshQueryTabColumnListener();
        }
    }

    /// <summary>对象树选中变化时更新当前数据库/Schema 上下文（供 Schema 选择器展示）。</summary>
    private void ObjectsTree_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm)
            return;

        if (ObjectsTree.SelectedItem is DbObjectTreeNode node)
        {
            // 更新当前数据库/Schema 上下文
            if (node.NodeType == DbObjectTreeNodeType.Database)
            {
                vm.CurrentDatabase = node.Name;
                vm.CurrentSchema = string.Empty;
                vm.SchemaSelectorVisible = false;
            }
            else if (node.NodeType == DbObjectTreeNodeType.Schema)
            {
                vm.CurrentDatabase = node.DatabaseName ?? vm.CurrentDatabase;
                vm.CurrentSchema = node.Name;
                vm.SchemaSelectorVisible = true;
            }
        }
    }

    /// <summary>对象树节点展开时按需懒加载子级。</summary>
    private async void ObjectsTree_Item_Expanded(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm)
            return;

        if (e.Source is not TreeViewItem item || item.DataContext is not DbObjectTreeNode node)
            return;

        // 连接节点展开：若尚未连接或对象树已被卸载则自动建立连接并加载对象树。
        if (node.NodeType == DbObjectTreeNodeType.Connection)
        {
            if (!node.IsConnectionActive || node.Children.Count == 0)
            {
                await vm.ConnectConnectionNodeAsync(node);
            }
            return;
        }

        // 找到所属连接节点，以确定使用的连接。
        var connectionNode = FindConnectionNode(node);
        if (connectionNode is null || connectionNode.Connection is null)
            return;
        string connectionName = connectionNode.Name;

        switch (node.NodeType)
        {
            case DbObjectTreeNodeType.Folder:
                await vm.ObjectsExplorer.LoadFolderChildrenAsync(node, connectionName);
                break;
            case DbObjectTreeNodeType.ChildFolder:
                await vm.ObjectsExplorer.LoadTableChildFolderAsync(node, connectionName);
                break;
        }
    }

    /// <summary>向上查找节点所属的连接根节点。</summary>
    private static DbObjectTreeNode? FindConnectionNode(DbObjectTreeNode node)
    {
        var current = node.Parent;
        while (current is not null)
        {
            if (current.NodeType == DbObjectTreeNodeType.Connection)
                return current;
            current = current.Parent;
        }
        return null;
    }

    /// <summary>数据编辑列变化时，动态重建可编辑 DataGrid 列。</summary>
    private void DataEditor_ColumnsChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        var grid = this.FindControl<DataGrid>("DataEditGrid");
        if (grid is null || _dataEditor is null)
            return;

        grid.Columns.Clear();

        foreach (var col in _dataEditor.Columns)
        {
            var isReadOnly = col.IsReadOnly || _dataEditor.IsView;
            grid.Columns.Add(new DataGridTextColumn
            {
                Header = col.Name,
                // 双向绑定以支持单元格编辑写入到 DataEditRow。
                Binding = new Binding($"Item[{col.Name}]")
                {
                    Mode = BindingMode.TwoWay,
                },
                IsReadOnly = isReadOnly,
            });
        }
    }

    private async void MenuNewConnection_Click(object? sender, RoutedEventArgs e)
    {
        if (_services is null || DataContext is not MainWindowViewModel)
            return;

        var connVm = _services.GetRequiredService<ConnectionManagerViewModel>();
        var dialog = new ConnectWindow(connVm) { DataContext = connVm };
        await dialog.ShowDialog<object?>(this);

        (DataContext as MainWindowViewModel)?.RefreshConnections();
    }

    private async void MenuConnectionManager_Click(object? sender, RoutedEventArgs e)
        => await OpenConnectionManagerAsync();

    /// <summary>打开连接管理窗口，关闭后刷新连接列表。</summary>
    private async Task OpenConnectionManagerAsync()
    {
        if (_services is null)
            return;

        var connVm = _services.GetRequiredService<ConnectionManagerViewModel>();
        var window = new ConnectionManagerWindow(connVm);
        await window.ShowDialog<object?>(this);

        (DataContext as MainWindowViewModel)?.RefreshConnections();
    }

    /// <summary>打开元数据搜索窗口（P0：DB Metadata Search / Open Database Object）。</summary>
    private async void MenuSearch_Click(object? sender, RoutedEventArgs e)
    {
        if (_services is null || DataContext is not MainWindowViewModel vm)
            return;

        // 仅提供已活动的连接供搜索；默认选中当前查询标签使用的连接。
        var activeNames = vm.ObjectsExplorer.RootNodes
            .Where(n => n.NodeType == DbObjectTreeNodeType.Connection && n.IsConnectionActive)
            .Select(n => n.Name)
            .ToList();

        if (activeNames.Count == 0)
        {
            vm.QueryEditor.StatusMessage = "请先在对象浏览器中连接一个连接，再使用元数据搜索。";
            return;
        }

        var searchVm = _services.GetRequiredService<SearchViewModel>();
        var defaultConnection = vm.SelectedQueryTab?.ConnectionName;
        searchVm.SetConnections(
            activeNames,
            string.IsNullOrEmpty(defaultConnection) ? activeNames[0] : defaultConnection);

        var window = new SearchWindow(searchVm);
        await window.ShowDialog<object?>(this);

        var result = window.SelectedItemResult;
        if (result is null)
            return;

        var locatedNode = await LocateNodeInTreeAsync(result);

        if (window.GenerateSelectRequested)
        {
            if (locatedNode?.DbObject is Table or View)
            {
                vm.GenerateSelectScript(locatedNode);
                return;
            }

            // 树中未找到对应节点（尚未加载等）时，按搜索结果直接构造对象生成 SELECT。
            DatabaseObject obj = result.Kind == SearchObjectKind.View
                ? new View { Name = result.Name, Schema = result.Schema }
                : new Table { Name = result.Name, Schema = result.Schema };

            vm.GenerateSelectScript(new DbObjectTreeNode
            {
                Name = result.Name,
                Text = result.FullName,
                NodeType = DbObjectTreeNodeType.DbObject,
                DbObject = obj,
                DatabaseName = result.DatabaseName,
                Schema = result.Schema,
            });
        }
        else if (locatedNode is null)
        {
            SetQueryStatus($"未能在对象树中定位「{result.DisplayText}」，请确认该连接已展开加载。");
        }
    }

    /// <summary>
    /// 在对象树中定位搜索结果对应的节点：逐级展开（触发懒加载）并选中目标。
    /// 返回定位到的节点；失败时返回 null 并给出状态提示。
    /// </summary>
    private async Task<DbObjectTreeNode?> LocateNodeInTreeAsync(SearchResultItem item)
    {
        if (DataContext is not MainWindowViewModel vm)
            return null;

        var connectionNode = vm.ObjectsExplorer.FindConnectionNode(item.ConnectionName);
        if (connectionNode is null || !connectionNode.IsConnectionActive)
        {
            SetQueryStatus($"连接「{item.ConnectionName}」未激活，无法定位。");
            return null;
        }

        await ExpandContainerAsync(connectionNode);

        // 数据库节点
        var dbNode = connectionNode.Children.FirstOrDefault(c =>
            c.NodeType == DbObjectTreeNodeType.Database &&
            string.Equals(c.Name, item.DatabaseName, StringComparison.OrdinalIgnoreCase));

        if (dbNode is null)
        {
            SetQueryStatus($"未在对象树中找到数据库「{item.DatabaseName}」。");
            return null;
        }

        await ExpandContainerAsync(dbNode);

        // 多 Schema 数据库（Postgres/Kingbase 等）存在 Schema 层；单层结构直接是类型文件夹。
        var schemaParent = dbNode;
        if (!string.IsNullOrEmpty(item.Schema))
        {
            var schemaNode = dbNode.Children.FirstOrDefault(c =>
                c.NodeType == DbObjectTreeNodeType.Schema &&
                string.Equals(c.Name, item.Schema, StringComparison.OrdinalIgnoreCase));

            if (schemaNode is not null)
            {
                await ExpandContainerAsync(schemaNode);
                schemaParent = schemaNode;
            }
        }

        // 类型文件夹（Tables / Views / Procedures / Functions / Sequences）
        var folderName = item.Kind switch
        {
            SearchObjectKind.Table => "Tables",
            SearchObjectKind.View => "Views",
            SearchObjectKind.Procedure => "Procedures",
            SearchObjectKind.Function => "Functions",
            SearchObjectKind.Sequence => "Sequences",
            _ => "Tables",
        };

        var folderNode = schemaParent.Children.FirstOrDefault(c =>
            c.NodeType == DbObjectTreeNodeType.Folder &&
            string.Equals(c.Name, folderName, StringComparison.OrdinalIgnoreCase));

        if (folderNode is null)
        {
            SetQueryStatus($"未找到类型文件夹「{folderName}」。");
            return null;
        }

        // 懒加载文件夹内容（已加载时内部会跳过）。
        try
        {
            await vm.ObjectsExplorer.LoadFolderChildrenAsync(folderNode, connectionNode.Name);
        }
        catch
        {
            // 加载失败时继续尝试用现有子节点匹配。
        }

        await ExpandContainerAsync(folderNode);

        // 对象节点（优先 名称+Schema 匹配，退化为仅名称匹配）。
        var objectNode = folderNode.Children.FirstOrDefault(c =>
            c.NodeType == DbObjectTreeNodeType.DbObject &&
            string.Equals(c.Name, item.Name, StringComparison.OrdinalIgnoreCase) &&
            (string.IsNullOrEmpty(item.Schema) ||
             string.Equals(c.Schema, item.Schema, StringComparison.OrdinalIgnoreCase)))
            ?? folderNode.Children.FirstOrDefault(c =>
                c.NodeType == DbObjectTreeNodeType.DbObject &&
                string.Equals(c.Name, item.Name, StringComparison.OrdinalIgnoreCase));

        if (objectNode is null)
        {
            SetQueryStatus($"未在对象树中找到「{item.DisplayText}」。");
            return null;
        }

        var target = objectNode;
        await ExpandContainerAsync(objectNode);

        // 列结果：继续深入 Columns 子文件夹定位列子节点。
        if (item.Kind == SearchObjectKind.Column)
        {
            var columnsFolder = objectNode.Children.FirstOrDefault(c =>
                c.NodeType == DbObjectTreeNodeType.ChildFolder &&
                string.Equals(c.Name, "Columns", StringComparison.OrdinalIgnoreCase));

            if (columnsFolder is not null)
            {
                try
                {
                    await vm.ObjectsExplorer.LoadTableChildFolderAsync(columnsFolder, connectionNode.Name);
                }
                catch
                {
                    // 忽略加载失败。
                }

                await ExpandContainerAsync(columnsFolder);

                target = columnsFolder.Children.FirstOrDefault(c =>
                    c.NodeType == DbObjectTreeNodeType.ChildObject &&
                    string.Equals(c.Name, item.Name, StringComparison.OrdinalIgnoreCase)) ?? objectNode;
            }
        }

        ObjectsTree.SelectedItem = target;

        if (ObjectsTree.ContainerFromItem(target) is TreeViewItem targetContainer)
        {
            targetContainer.BringIntoView();
        }

        return target;
    }

    /// <summary>等待 TreeViewItem 容器生成并展开（容器可能因虚拟化延迟出现，轮询等待）。</summary>
    private async Task<TreeViewItem?> ExpandContainerAsync(DbObjectTreeNode node)
    {
        TreeViewItem? container = null;

        for (var attempt = 0; attempt < 30; attempt++)
        {
            container = ObjectsTree.ContainerFromItem(node) as TreeViewItem;

            if (container is not null)
            {
                container.IsExpanded = true;
                break;
            }

            await Task.Delay(20);
        }

        return container;
    }

    /// <summary>向当前查询标签写入状态提示。</summary>
    private void SetQueryStatus(string message)
    {
        if (DataContext is MainWindowViewModel vm && vm.SelectedQueryTab is not null)
        {
            vm.SelectedQueryTab.StatusMessage = message;
        }
    }

    /// <summary>打开数据库转换窗口（阶段 4）。</summary>
    private async void MenuConvert_Click(object? sender, RoutedEventArgs e)
    {
        if (_services is null)
            return;

        var convertVm = _services.GetRequiredService<ConvertViewModel>();
        var window = new ConvertWindow(convertVm);
        await window.ShowDialog<object?>(this);
    }

    /// <summary>打开结构对比窗口（阶段 4）。</summary>
    private async void MenuSchemaCompare_Click(object? sender, RoutedEventArgs e)
    {
        if (_services is null)
            return;

        var compareVm = _services.GetRequiredService<SchemaCompareViewModel>();
        var window = new SchemaCompareWindow(compareVm);
        await window.ShowDialog<object?>(this);
    }

    /// <summary>打开数据对比窗口（阶段 4）。</summary>
    private async void MenuDataCompare_Click(object? sender, RoutedEventArgs e)
    {
        if (_services is null)
            return;

        var dataCompareVm = _services.GetRequiredService<DataCompareViewModel>();
        var window = new DataCompareWindow(dataCompareVm);
        await window.ShowDialog<object?>(this);
    }

    /// <summary>打开数据库诊断窗口（阶段 4）。</summary>
    private async void MenuDiagnose_Click(object? sender, RoutedEventArgs e)
    {
        if (_services is null)
            return;

        var diagnoseVm = _services.GetRequiredService<DiagnoseViewModel>();
        var window = new DiagnoseWindow(diagnoseVm);
        await window.ShowDialog<object?>(this);
    }

    /// <summary>打开数据库优化窗口（阶段 4）。</summary>
    private async void MenuOptimize_Click(object? sender, RoutedEventArgs e)
    {
        if (_services is null)
            return;

        var optimizeVm = _services.GetRequiredService<OptimizeViewModel>();
        var window = new OptimizeWindow(optimizeVm);
        await window.ShowDialog<object?>(this);
    }

    /// <summary>打开依赖分析窗口（阶段 4）。</summary>
    private async void MenuDependency_Click(object? sender, RoutedEventArgs e)
    {
        if (_services is null)
            return;

        var dependencyVm = _services.GetRequiredService<DependencyViewModel>();
        var window = new DependencyWindow(dependencyVm);
        await window.ShowDialog<object?>(this);
    }

    /// <summary>打开统计窗口（阶段 5）。</summary>
    private async void MenuStatistic_Click(object? sender, RoutedEventArgs e)
    {
        if (_services is null)
            return;

        var statisticVm = _services.GetRequiredService<StatisticViewModel>();
        var window = new StatisticWindow(statisticVm);
        await window.ShowDialog<object?>(this);
    }

    /// <summary>打开索引碎片分析窗口（阶段 5）。</summary>
    private async void MenuIndexFragmentation_Click(object? sender, RoutedEventArgs e)
    {
        if (_services is null)
            return;

        var fragVm = _services.GetRequiredService<IndexFragmentationViewModel>();
        var window = new IndexFragmentationWindow(fragVm);
        await window.ShowDialog<object?>(this);
    }

    /// <summary>打开数据库备份窗口（阶段 5）。</summary>
    private async void MenuBackup_Click(object? sender, RoutedEventArgs e)
    {
        if (_services is null)
            return;

        var backupVm = _services.GetRequiredService<BackupViewModel>();
        var window = new BackupWindow(backupVm);
        await window.ShowDialog<object?>(this);
    }

    /// <summary>打开代码生成窗口（阶段 5）。</summary>
    private async void MenuCodeGenerate_Click(object? sender, RoutedEventArgs e)
    {
        if (_services is null)
            return;

        var codeGenVm = _services.GetRequiredService<CodeGenerateViewModel>();
        var window = new CodeGenerateWindow(codeGenVm);
        await window.ShowDialog<object?>(this);
    }

    /// <summary>打开文档生成窗口（阶段 5）。</summary>
    private async void MenuColumnDocumentation_Click(object? sender, RoutedEventArgs e)
    {
        if (_services is null)
            return;

        var docVm = _services.GetRequiredService<ColumnDocumentationViewModel>();
        var window = new ColumnDocumentationWindow(docVm);
        await window.ShowDialog<object?>(this);
    }

    /// <summary>打开数据导出窗口（阶段 6 / M6）。</summary>
    private async void MenuExport_Click(object? sender, RoutedEventArgs e)
    {
        if (_services is null)
            return;

        var exportVm = _services.GetRequiredService<ExportViewModel>();
        var window = new ExportWindow(exportVm);
        await window.ShowDialog<object?>(this);
    }

    /// <summary>打开数据导入窗口（阶段 6 / M6）。</summary>
    private async void MenuImport_Click(object? sender, RoutedEventArgs e)
    {
        if (_services is null)
            return;

        var importVm = _services.GetRequiredService<ImportViewModel>();
        var window = new ImportWindow(importVm);
        await window.ShowDialog<object?>(this);
    }

    /// <summary>打开脚本文件对话框并加载到查询编辑器。</summary>
    private async void MenuOpenScript_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm)
            return;

        var storage = StorageProvider;
        var files = await storage.OpenFilePickerAsync(new global::Avalonia.Platform.Storage.FilePickerOpenOptions
        {
            Title = "打开 SQL 脚本",
            AllowMultiple = false,
            FileTypeFilter = new[] { new global::Avalonia.Platform.Storage.FilePickerFileType("SQL 脚本") { Patterns = new[] { "*.sql" } } },
        });

        if (files.Count > 0)
        {
            // 打开脚本到当前选中的查询标签页
            if (vm.SelectedQueryTab is not null)
            {
                vm.SelectedQueryTab.SqlText = File.ReadAllText(files[0].Path?.LocalPath ?? string.Empty);
                vm.SelectedQueryTab.StatusMessage = $"已打开 {Path.GetFileName(files[0].Path?.LocalPath)}。";
            }
        }
    }

    /// <summary>保存当前 SQL 到脚本文件。</summary>
    private async void MenuSaveScript_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm)
            return;

        var storage = StorageProvider;
        var file = await storage.SaveFilePickerAsync(new global::Avalonia.Platform.Storage.FilePickerSaveOptions
        {
            Title = "保存 SQL 脚本",
            SuggestedFileName = "query.sql",
            DefaultExtension = "sql",
            FileTypeChoices = new[] { new global::Avalonia.Platform.Storage.FilePickerFileType("SQL 脚本") { Patterns = new[] { "*.sql" } } },
        });

        if (file is not null)
        {
            // 保存当前标签页 SQL 到文件
            if (vm.SelectedQueryTab is not null)
            {
                File.WriteAllText(file.Path?.LocalPath ?? string.Empty, vm.SelectedQueryTab.SqlText);
                vm.SelectedQueryTab.StatusMessage = $"已保存到 {Path.GetFileName(file.Path?.LocalPath)}。";
            }
        }
    }

    /// <summary>打开最近脚本。</summary>
    private void MenuOpenRecent_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm)
            return;

        if (sender is MenuItem item && item.Tag is string path)
        {
            // 打开最近脚本到当前标签页
            if (vm.SelectedQueryTab is not null && File.Exists(path))
            {
                vm.SelectedQueryTab.SqlText = File.ReadAllText(path);
                vm.SelectedQueryTab.StatusMessage = $"已打开 {Path.GetFileName(path)}。";
            }
        }
    }

    /// <summary>主工具栏：新建查询。</summary>
    private void ToolNewQuery_Click(object? sender, RoutedEventArgs e)
    {
        (DataContext as MainWindowViewModel)?.NewQuery();
    }

    /// <summary>主工具栏：执行当前查询标签的 SQL。</summary>
    private async void ToolExecute_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm && vm.SelectedQueryTab is not null)
        {
            await vm.SelectedQueryTab.ExecuteAsync();
        }
    }

    private void MenuRefresh_Click(object? sender, RoutedEventArgs e)
    {
        (DataContext as MainWindowViewModel)?.RefreshConnections();
    }

    private void MenuExit_Click(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    /// <summary>双击对象树节点：连接节点建立连接；类型文件夹懒加载具体对象；表/视图生成 SELECT 脚本。</summary>
    private async void ObjectsTree_DoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm)
            return;

        if (ObjectsTree.SelectedItem is not DbObjectTreeNode node)
            return;

        // 连接节点：双击连接/断开。
        if (node.NodeType == DbObjectTreeNodeType.Connection)
        {
            if (node.IsConnectionActive)
            {
                vm.DisconnectConnectionNode(node);
            }
            else
            {
                await vm.ConnectConnectionNodeAsync(node);
                // 连接后自动展开连接节点以浏览对象。
                if (ObjectsTree.ContainerFromItem(node) is TreeViewItem tvi)
                {
                    tvi.IsExpanded = true;
                }
            }
            return;
        }

        // 找到所属连接节点以确定连接。
        var connectionNode = FindConnectionNode(node);
        if (connectionNode is null || !connectionNode.IsConnectionActive)
            return;
        string connectionName = connectionNode.Name;

        switch (node.NodeType)
        {
            case DbObjectTreeNodeType.Folder:
                await vm.ObjectsExplorer.LoadFolderChildrenAsync(node, connectionName);
                break;
            case DbObjectTreeNodeType.ChildFolder:
                await vm.ObjectsExplorer.LoadTableChildFolderAsync(node, connectionName);
                break;
            case DbObjectTreeNodeType.DbObject when node.DbObject is Table or View:
                vm.GenerateSelectScript(node);
                break;
        }
    }

    /// <summary>对象树右键菜单：使用 ObjectTreeContextMenuBuilder 按节点类型分发构建。</summary>
    private void ObjectsTree_ContextRequested(object? sender, ContextRequestedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm)
            return;

        if (ObjectsTree.SelectedItem is not DbObjectTreeNode node)
            return;

        // 使用构建器模式按节点类型分发右键菜单（P2增强：含Compare/Migrate回调）
        var connectionService = _services?.GetService<IDbConnectionService>();
        var builder = new ObjectTreeContextMenuBuilder(
            vm,
            ObjectsTree,
            asyncAction: async (action) => action(),
            connectionService: connectionService,
            openConnectionManager: () => _ = OpenConnectionManagerAsync(),
            openTableDesigner: (n, isNew) => _ = isNew ? OpenNewTableDesignerAsync(n) : OpenTableDesignerAsync(n),
            openDataEditorTab: OpenDataEditorTab,
            openExportWindow: (n) => _ = OpenExportWindowForTableAsync(n),
            openImportWindow: (n) => _ = OpenImportWindowForTableAsync(n),
            openSchemaCompare: (n) => _ = OpenSchemaCompareForNodeAsync(n),
            openDataCompare: (n) => _ = OpenDataCompareForNodeAsync(n),
            openConvert: (n) => _ = OpenConvertForNodeAsync(n));

        builder.BuildAndShow(node, e);
    }

    /// <summary>P2: 为节点打开结构对比窗口。</summary>
    private async Task OpenSchemaCompareForNodeAsync(DbObjectTreeNode node)
    {
        if (_services is null) return;
        
        var compareVm = _services.GetRequiredService<SchemaCompareViewModel>();
        var window = new SchemaCompareWindow(compareVm);
        await window.ShowDialog<object?>(this);
    }

    /// <summary>P2: 为节点打开数据对比窗口。</summary>
    private async Task OpenDataCompareForNodeAsync(DbObjectTreeNode node)
    {
        if (_services is null) return;
        
        var dataCompareVm = _services.GetRequiredService<DataCompareViewModel>();
        var window = new DataCompareWindow(dataCompareVm);
        await window.ShowDialog<object?>(this);
    }

    /// <summary>P2: 为节点打开数据库转换窗口。</summary>
    private async Task OpenConvertForNodeAsync(DbObjectTreeNode node)
    {
        if (_services is null) return;
        
        var convertVm = _services.GetRequiredService<ConvertViewModel>();
        var window = new ConvertWindow(convertVm);
        await window.ShowDialog<object?>(this);
    }

    /// <summary>打开导出窗口并预填表信息（P1：表节点右键导出数据）。</summary>
    private async Task OpenExportWindowForTableAsync(DbObjectTreeNode node)
    {
        if (_services is null || node.DbObject is not Table)
            return;

        var exportVm = _services.GetRequiredService<ExportViewModel>();
        
        // 预填充连接和表信息
        var connectionNode = FindConnectionNode(node);
        if (connectionNode?.Connection is not null)
        {
            exportVm.RefreshConnections();
            // 选中对应连接
            var conn = exportVm.Connections.FirstOrDefault(c => 
                string.Equals(c.Id, connectionNode.Connection.Id, StringComparison.OrdinalIgnoreCase));
            if (conn is not null)
            {
                exportVm.SelectedConnection = conn;
            }
        }

        var window = new ExportWindow(exportVm);
        await window.ShowDialog<object?>(this);
    }

    /// <summary>打开导入窗口并预填表信息（P1：表节点右键导入数据）。</summary>
    private async Task OpenImportWindowForTableAsync(DbObjectTreeNode node)
    {
        if (_services is null || node.DbObject is not Table)
            return;

        var importVm = _services.GetRequiredService<ImportViewModel>();
        
        // 预填充连接和表信息
        var connectionNode = FindConnectionNode(node);
        if (connectionNode?.Connection is not null)
        {
            importVm.RefreshConnections();
            // 选中对应连接
            var conn = importVm.Connections.FirstOrDefault(c => 
                string.Equals(c.Id, connectionNode.Connection.Id, StringComparison.OrdinalIgnoreCase));
            if (conn is not null)
            {
                importVm.SelectedConnection = conn;
            }
        }

        var window = new ImportWindow(importVm);
        await window.ShowDialog<object?>(this);
    }

    /// <summary>切换到「数据编辑」标签页（预留）。</summary>
    private void OpenDataEditorTab()
    {
        // 数据编辑功能将在后续版本集成到多标签页架构
    }

    /// <summary>刷新当前查询标签的 DataGrid 列监听（切换标签时动态重建列）。</summary>
    private void RefreshQueryTabColumnListener()
    {
        if (DataContext is not MainWindowViewModel currentVm)
            return;

        // 移除旧监听
        if (_currentQueryTab is not null)
        {
            _currentQueryTab.Columns.CollectionChanged -= QueryTabColumns_CollectionChanged;
        }

        // 指向当前选中的查询标签
        _currentQueryTab = currentVm.SelectedQueryTab;

        if (_currentQueryTab is not null)
        {
            _currentQueryTab.Columns.CollectionChanged += QueryTabColumns_CollectionChanged;
            // 立即触发一次列重建
            QueryTabColumns_CollectionChanged(_currentQueryTab.Columns, null!);
        }
    }

    /// <summary>查询标签列变化时，动态重建对应 DataGrid 的数据列。</summary>
    private void QueryTabColumns_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        if (_currentQueryTab is null) return;

        // 由于 DataGrid 在 DataTemplate 内部，需要在 TabControl 的 Visual Tree 中查找
        var tabControl = this.FindControl<TabControl>("QueryTabsControl");
        if (tabControl is null) return;

        // 使用更全面的递归查找方法（支持所有控件类型）
        DataGrid? grid = FindDataGridInVisualTree(tabControl);
        
        if (grid is null) return;

        // 清空全部列后按当前列名重建。
        grid.Columns.Clear();

        for (int i = 0; i < _currentQueryTab.Columns.Count; i++)
        {
            grid.Columns.Add(new DataGridTextColumn
            {
                Header = _currentQueryTab.Columns[i],
                Binding = new Binding($"[{i}]"),
                IsReadOnly = true,
            });
        }
    }

    /// <summary>在 Visual Tree 中查找指定名称的 DataGrid。</summary>
    private static DataGrid? FindDataGridInVisualTree(Control parent)
    {
        // 使用 GetVisualDescendants 遍历所有子控件
        foreach (var descendant in parent.GetVisualDescendants())
        {
            if (descendant is DataGrid { Name: "QueryResultGrid" } targetGrid)
            {
                return targetGrid;
            }
        }
        
        return null;
    }

    /// <summary>打开表设计器（修改已有表结构）。</summary>
    private async Task OpenTableDesignerAsync(DbObjectTreeNode node)
    {
        if (_services is null || DataContext is not MainWindowViewModel vm)
            return;

        if (node?.DbObject is not Table table)
            return;

        var connectionNode = FindConnectionNode(node);
        if (connectionNode is null || connectionNode.Connection is null)
        {
            vm.QueryEditor.StatusMessage = "请先连接对应连接。";
            return;
        }

        var designerVm = _services.GetRequiredService<TableDesignerViewModel>();
        bool ok = await designerVm.LoadAsync(
            connectionNode.Name,
            node.DatabaseName ?? vm.CurrentDatabase,
            table.Name,
            node.Schema,
            isNew: false);

        if (!ok)
        {
            vm.QueryEditor.StatusMessage = designerVm.StatusMessage;
            return;
        }

        var window = new TableDesignerWindow(designerVm);
        await window.ShowDialog<object?>(this);
    }

    /// <summary>打开表设计器（在 Tables 文件夹上新建表）。</summary>
    private async Task OpenNewTableDesignerAsync(DbObjectTreeNode folderNode)
    {
        if (_services is null || DataContext is not MainWindowViewModel vm)
            return;

        if (folderNode?.NodeType != DbObjectTreeNodeType.Folder || folderNode.DatabaseObjectType != DatabaseObjectType.Table)
            return;

        var connectionNode = FindConnectionNode(folderNode);
        if (connectionNode is null || connectionNode.Connection is null)
        {
            vm.QueryEditor.StatusMessage = "请先连接对应连接。";
            return;
        }

        var designerVm = _services.GetRequiredService<TableDesignerViewModel>();
        bool ok = await designerVm.LoadAsync(
            connectionNode.Name,
            folderNode.DatabaseName ?? vm.CurrentDatabase,
            "NewTable",
            folderNode.Schema,
            isNew: true);

        if (!ok)
        {
            vm.QueryEditor.StatusMessage = designerVm.StatusMessage;
            return;
        }

        var window = new TableDesignerWindow(designerVm);
        await window.ShowDialog<object?>(this);

        // 新建/修改后刷新节点，展示最新表结构。
        await vm.RefreshNodeAsync(folderNode);
    }

    /// <summary>标签页头部右键菜单事件处理。</summary>
    private void TabHeader_ContextRequested(object? sender, ContextRequestedEventArgs e)
    {
        // 只响应鼠标右键触发的请求
        if (sender is Border border && border.Tag is QueryTabViewModel tab)
        {
            var menu = new ContextMenu();
            
            var closeItem = new MenuItem { Header = "关闭", Tag = tab };
            closeItem.Click += CloseTab_Click;
            menu.Items.Add(closeItem);
            
            var closeOtherItem = new MenuItem { Header = "关闭其他", Tag = tab };
            closeOtherItem.Click += CloseOtherTabs_Click;
            menu.Items.Add(closeOtherItem);
            
            var closeAllItem = new MenuItem { Header = "关闭所有" };
            closeAllItem.Click += CloseAllTabs_Click;
            menu.Items.Add(closeAllItem);
            
            menu.Items.Add(new Separator());
            
            var copyTitleItem = new MenuItem { Header = "复制标签标题", Tag = tab };
            copyTitleItem.Click += CopyTabTitle_Click;
            menu.Items.Add(copyTitleItem);
            
            // 在鼠标位置打开菜单
            menu.Open(this);
            e.Handled = true;
        }
    }

    /// <summary>关闭查询标签页。</summary>
    private void CloseTab_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is QueryTabViewModel tab && DataContext is MainWindowViewModel vm)
        {
            vm.CloseQueryTab(tab);
        }
        else if (sender is MenuItem menuItem && menuItem.Tag is QueryTabViewModel tab2 && DataContext is MainWindowViewModel vm2)
        {
            // 右键菜单触发的关闭
            vm2.CloseQueryTab(tab2);
        }
    }

    /// <summary>关闭除当前标签外的所有其他标签。</summary>
    private void CloseOtherTabs_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { Tag: QueryTabViewModel currentTab } && DataContext is MainWindowViewModel vm)
        {
            // 收集需要关闭的标签（排除当前标签）
            var tabsToClose = vm.QueryTabs.Where(t => t != currentTab).ToList();
            foreach (var tab in tabsToClose)
            {
                vm.CloseQueryTab(tab);
            }
        }
    }

    /// <summary>关闭所有标签页。</summary>
    private void CloseAllTabs_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            // 复制列表以避免遍历时修改
            var allTabs = vm.QueryTabs.ToList();
            foreach (var tab in allTabs)
            {
                vm.CloseQueryTab(tab);
            }
        }
    }

    /// <summary>复制标签标题到剪贴板。</summary>
    private async void CopyTabTitle_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { Tag: QueryTabViewModel tab })
        {
            await Clipboard.SetTextAsync(tab.Title);
        }
    }

    /// <summary>处理「删除」按钮：删除数据网格中当前选中的行。</summary>
    private void DataEditorRemove_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm || _dataEditor is null)
            return;

        var selected = this.FindControl<DataGrid>("DataEditGrid")?.SelectedItem as DataEditRow;
        if (selected is null)
        {
            _dataEditor.StatusMessage = "请先在网格中选中要删除的行。";
            return;
        }

        _dataEditor.RemoveRowCommand.Execute(selected);
    }

    /// <summary>请求关闭标签页的回调（显示未保存提示对话框）。</summary>
    private async Task<bool> RequestCloseTabAsync(QueryTabViewModel tab)
    {
        // 简化版：直接返回 true 允许关闭（后续可集成 MsBox.Avalonia 实现完整对话框）
        // TODO: 集成 MsBox.Avalonia 实现完整的"保存/不保存/取消"对话框
        if (tab.IsModified)
        {
            // 暂时自动标记为已保存，允许关闭
            tab.MarkAsSaved();
        }
        return true;
    }

    /// <summary>主窗口快捷键处理（对齐 DBeaver 快捷键）。</summary>
    private void MainWindow_KeyDown(object? sender, KeyEventArgs e)
    {
        // 如果焦点在文本输入控件（如 TextBox），不拦截回车等键
        if (e.Key == Key.Enter && FocusManager.GetFocusedElement() is TextBox)
            return;

        if (DataContext is not MainWindowViewModel vm)
            return;

        // 检查修饰键
        bool ctrl = e.KeyModifiers.HasFlag(KeyModifiers.Control);

        switch (e.Key)
        {
            case Key.F5:
                // F5：执行当前查询
                e.Handled = true;
                ToolExecute_Click(sender, e);
                break;

            case Key.N when ctrl:
                // Ctrl+N：新建查询
                e.Handled = true;
                vm.NewQuery();
                break;

            case Key.W when ctrl:
                // Ctrl+W：关闭当前标签页
                e.Handled = true;
                if (vm.SelectedQueryTab is not null)
                {
                    vm.CloseQueryTab(vm.SelectedQueryTab);
                }
                break;

            case Key.S when ctrl:
                // Ctrl+S：保存当前脚本
                e.Handled = true;
                MenuSaveScript_Click(sender, e);
                break;

            case Key.O when ctrl:
                // Ctrl+O：打开脚本
                e.Handled = true;
                MenuOpenScript_Click(sender, e);
                break;

            case Key.D when ctrl && e.KeyModifiers.HasFlag(KeyModifiers.Shift):
                // Ctrl+Shift+D：元数据搜索并定位（对齐 DBeaver Open Database Object）
                e.Handled = true;
                MenuSearch_Click(sender, e);
                break;

            case Key.H when ctrl:
                // Ctrl+H：元数据搜索（对齐 DBeaver Search）
                e.Handled = true;
                MenuSearch_Click(sender, e);
                break;

            case Key.F4:
                // F4：刷新对象树（对齐 DBeaver）
                e.Handled = true;
                vm.RefreshConnections();
                break;

            case Key.Delete:
                // Delete：如果焦点在对象树，不处理（由右键菜单处理）
                break;
        }
    }
}
