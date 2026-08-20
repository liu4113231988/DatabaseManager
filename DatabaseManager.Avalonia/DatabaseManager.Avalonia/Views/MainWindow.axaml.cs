using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using DatabaseInterpreter.Model;
using DatabaseManager.AppCore.Models;
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
        if (DataContext is not MainWindowViewModel vm || vm.SelectedConnection is null)
            return;

        if (e.Source is not TreeViewItem item || item.DataContext is not DbObjectTreeNode node)
            return;

        switch (node.NodeType)
        {
            case DbObjectTreeNodeType.Folder:
                await vm.ObjectsExplorer.LoadFolderChildrenAsync(node, vm.SelectedConnection.Name);
                break;
            case DbObjectTreeNodeType.ChildFolder:
                await vm.ObjectsExplorer.LoadTableChildFolderAsync(node, vm.SelectedConnection.Name);
                break;
        }
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

    private void MenuRefresh_Click(object? sender, RoutedEventArgs e)
    {
        (DataContext as MainWindowViewModel)?.RefreshConnections();
    }

    private void MenuExit_Click(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    /// <summary>双击对象树节点：类型文件夹懒加载具体对象；表/视图生成 SELECT 脚本。</summary>
    private async void ObjectsTree_DoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm)
            return;

        if (ObjectsTree.SelectedItem is not DbObjectTreeNode node)
            return;

        if (vm.SelectedConnection is null)
            return;

        switch (node.NodeType)
        {
            case DbObjectTreeNodeType.Folder:
                await vm.ObjectsExplorer.LoadFolderChildrenAsync(node, vm.SelectedConnection.Name);
                break;
            case DbObjectTreeNodeType.ChildFolder:
                await vm.ObjectsExplorer.LoadTableChildFolderAsync(node, vm.SelectedConnection.Name);
                break;
            case DbObjectTreeNodeType.DbObject when node.DbObject is Table or View:
                vm.GenerateSelectScript(node);
                break;
        }
    }

    /// <summary>对象树右键菜单：新建查询/查看数据(SELECT)/生成脚本/刷新。</summary>
    private void ObjectsTree_ContextRequested(object? sender, ContextRequestedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm)
            return;

        if (ObjectsTree.SelectedItem is not DbObjectTreeNode node)
            return;

        var menu = new ContextMenu();

        var newQuery = new MenuItem { Header = "新建查询" };
        newQuery.Click += (_, _) => vm.NewQuery();
        menu.Items.Add(newQuery);

        if (node.DbObject is Table or View)
        {
            var select = new MenuItem { Header = "查看数据 (SELECT)" };
            select.Click += (_, _) => vm.GenerateSelectScript(node);
            menu.Items.Add(select);

            var editData = new MenuItem { Header = "编辑数据" };
            editData.Click += async (_, _) =>
            {
                await vm.OpenDataEditor(node);
                OpenDataEditorTab();
            };
            menu.Items.Add(editData);

            // 表：设计表（新建表时在 Tables 文件夹上提供）。
            if (node.DbObject is Table)
            {
                var design = new MenuItem { Header = "设计表" };
                design.Click += async (_, _) => await OpenTableDesignerAsync(node);
                menu.Items.Add(design);
            }
        }
        else if (node.NodeType == DbObjectTreeNodeType.Folder && node.DatabaseObjectType == DatabaseObjectType.Table)
        {
            // Tables 文件夹：新建表。
            var newTable = new MenuItem { Header = "新建表" };
            newTable.Click += async (_, _) => await OpenNewTableDesignerAsync(node);
            menu.Items.Add(newTable);
        }

        // 刷新（针对可懒加载的文件夹/子文件夹）。
        if (node.NodeType is DbObjectTreeNodeType.Folder or DbObjectTreeNodeType.ChildFolder)
        {
            var refresh = new MenuItem { Header = "刷新" };
            refresh.Click += async (_, _) => await vm.RefreshNodeAsync(node);
            menu.Items.Add(refresh);
        }

        menu.Open(ObjectsTree);
        e.Handled = true;
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

        if (vm.SelectedConnection is null)
        {
            vm.QueryEditor.StatusMessage = "请先选择一个连接。";
            return;
        }

        var designerVm = _services.GetRequiredService<TableDesignerViewModel>();
        bool ok = await designerVm.LoadAsync(
            vm.SelectedConnection.Name,
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

        if (vm.SelectedConnection is null)
        {
            vm.QueryEditor.StatusMessage = "请先选择一个连接。";
            return;
        }

        var designerVm = _services.GetRequiredService<TableDesignerViewModel>();
        bool ok = await designerVm.LoadAsync(
            vm.SelectedConnection.Name,
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
