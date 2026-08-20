using System;
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
            vm.Initialize();

            // 监听查询结果列变化，动态重建 DataGrid 列。
            _queryEditor.Columns.CollectionChanged += QueryEditor_ColumnsChanged;

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
}
