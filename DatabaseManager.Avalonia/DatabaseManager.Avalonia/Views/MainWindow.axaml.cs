using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
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

    private void MenuRefresh_Click(object? sender, RoutedEventArgs e)
    {
        (DataContext as MainWindowViewModel)?.RefreshConnections();
    }

    private void MenuExit_Click(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    /// <summary>双击对象树节点：若为类型文件夹节点则懒加载其下的具体对象。</summary>
    private async void ObjectsTree_DoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm)
            return;

        if (ObjectsTree.SelectedItem is DbObjectTreeNode node &&
            node.NodeType == DbObjectTreeNodeType.Folder &&
            vm.SelectedConnection is not null)
        {
            await vm.ObjectsExplorer.LoadFolderChildrenAsync(node, vm.SelectedConnection.Name);
        }
    }
}
