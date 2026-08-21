using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using DatabaseInterpreter.Model;
using DatabaseManager.AppCore.Common;
using DatabaseManager.AppCore.Models;
using DatabaseManager.AppCore.Services;
using DatabaseManager.AppCore.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace DatabaseManager.Avalonia.Views;

public partial class MainWindow : Window
{
    private IServiceProvider? _services;
    private QueryEditorViewModel? _queryEditor;
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
            _queryEditor = vm.QueryEditor;
            _dataEditor = vm.DataEditor;
            vm.Initialize();

            // 监听查询结果列变化，动态重建 DataGrid 列。
            _queryEditor.Columns.CollectionChanged += QueryEditor_ColumnsChanged;

            // 监听数据编辑列变化，动态重建可编辑 DataGrid 列。
            _dataEditor.Columns.CollectionChanged += DataEditor_ColumnsChanged;

            // 通过路由事件监听 TreeViewItem 展开，实现点击展开箭头时的按需懒加载（对齐 dbeaver）。
            ObjectsTree.AddHandler(TreeViewItem.ExpandedEvent, ObjectsTree_Item_Expanded);

            // 监听对象树选中变化，更新 Schema 选择器上下文。
            ObjectsTree.SelectionChanged += ObjectsTree_SelectionChanged;
        }
    }

    /// <summary>对象树选中变化时更新当前数据库/Schema 上下文（供 Schema 选择器展示）。</summary>
    private void ObjectsTree_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm)
            return;

        if (ObjectsTree.SelectedItem is DbObjectTreeNode node)
        {
            vm.OnDatabaseNodeSelected(node);
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

    private void QueryEditor_ColumnsChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        // 每次列集合变化（新查询）时，重建 DataGrid 的动态数据列。
        var grid = QueryResultGrid;
        if (grid is null || _queryEditor is null)
            return;

        // 清空全部列后按当前列名重建。
        grid.Columns.Clear();

        for (int i = 0; i < _queryEditor.Columns.Count; i++)
        {
            grid.Columns.Add(new DataGridTextColumn
            {
                Header = _queryEditor.Columns[i],
                Binding = new Binding($"[{i}]"),
                IsReadOnly = true,
            });
        }
    }

    /// <summary>数据编辑列变化时，动态重建可编辑 DataGrid 列。</summary>
    private void DataEditor_ColumnsChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        var grid = DataEditGrid;
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
            vm.OpenScript(files[0].Path?.LocalPath ?? string.Empty);
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
            vm.SaveScript(file.Path?.LocalPath ?? string.Empty);
        }
    }

    /// <summary>打开最近脚本。</summary>
    private void MenuOpenRecent_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm)
            return;

        if (sender is MenuItem item && item.Tag is string path)
        {
            vm.OpenScript(path);
        }
    }

    /// <summary>主工具栏：新建查询。</summary>
    private void ToolNewQuery_Click(object? sender, RoutedEventArgs e)
    {
        (DataContext as MainWindowViewModel)?.NewQuery();
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

    /// <summary>切换到「数据编辑」标签页（索引 1）。</summary>
    private void OpenDataEditorTab()
    {
        if (ContentTabs.Items.Count > 1)
        {
            ContentTabs.SelectedIndex = 1;
        }
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

    /// <summary>处理「删除」按钮：删除数据网格中当前选中的行。</summary>
    private void DataEditorRemove_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm || _dataEditor is null)
            return;

        var selected = DataEditGrid.SelectedItem as DataEditRow;
        if (selected is null)
        {
            _dataEditor.StatusMessage = "请先在网格中选中要删除的行。";
            return;
        }

        _dataEditor.RemoveRowCommand.Execute(selected);
    }
}
