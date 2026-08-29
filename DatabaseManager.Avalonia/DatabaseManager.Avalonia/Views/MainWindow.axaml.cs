using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using DatabaseInterpreter.Model;
using DatabaseManager.AppCore.Common;
using DatabaseManager.AppCore.Models;
using DatabaseManager.AppCore.Services;
using DatabaseManager.AppCore.ViewModels;
using DatabaseManager.Avalonia.Controls;
using Microsoft.Extensions.DependencyInjection;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;

namespace DatabaseManager.Avalonia.Views;

public partial class MainWindow : Window
{
    private IServiceProvider? _services;
    private QueryTabViewModel? _currentQueryTab;

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
            vm.Initialize();

            // 设置关闭标签页的回调（用于显示未保存提示）
            vm.RequestCloseTab = RequestCloseTabAsync;
            foreach (var tab in vm.QueryTabs)
            {
                tab.RequestDangerousExecution = RequestDangerousExecutionAsync;
                tab.RequestLocateRow = LocateRowInResultGrid;
            }

            vm.QueryTabs.CollectionChanged += (_, args) =>
            {
                if (args.NewItems is null)
                    return;

                foreach (var item in args.NewItems.OfType<QueryTabViewModel>())
                {
                    item.RequestDangerousExecution = RequestDangerousExecutionAsync;
                    item.RequestLocateRow = LocateRowInResultGrid;
                }
            };

            // 监听当前查询标签的列变化，动态重建 DataGrid 列。
            RefreshQueryTabColumnListener();

            // 监听 SelectedQueryTab 变化以切换 DataGrid 列监听目标。
            vm.PropertyChanged += MainWindow_PropertyChanged;

            // 通过路由事件监听 TreeViewItem 展开，实现点击展开箭头时的按需懒加载（对齐 dbeaver）。
            ObjectsTree.AddHandler(TreeViewItem.ExpandedEvent, ObjectsTree_Item_Expanded);

            // 监听对象树选中变化，更新 Schema 选择器上下文。
            ObjectsTree.SelectionChanged += ObjectsTree_SelectionChanged;
        }

        // 任务中心接线：状态栏计数 + 完成/失败 Toast 通知。
        _taskCenter = _services?.GetService<ITaskCenterService>();
        if (_taskCenter is not null)
        {
            ToastHost.ItemsSource = _toasts;
            _taskCenter.TaskFinished += TaskCenter_TaskFinished;
            _taskCenter.RunsChanged += TaskCenter_RunsChanged;
            UpdateRunningTaskCount();
        }

        // 恢复窗口布局（大小/位置/左栏宽度；位置钳制到可见屏幕）。
        RestoreWindowLayout();

        // 应用持久化的主题外观与字体缩放。
        _appSettings = _services?.GetService<IAppSettingsService>();
        if (_appSettings is { } appSettings)
        {
            ApplyThemeMode(appSettings.Settings.ThemeMode);
            ApplyFontScale(appSettings.Settings.FontScale);
        }
    }

    private IAppSettingsService? _appSettings;

    /// <summary>应用主题外观（跟随系统/亮色/深色/高对比）并持久化。</summary>
    private void ApplyThemeMode(string mode)
    {
        if (Application.Current is not { } app)
        {
            return;
        }

        app.RequestedThemeVariant = mode switch
        {
            ThemeModes.Light => ThemeVariant.Light,
            ThemeModes.Dark => ThemeVariant.Dark,
            ThemeModes.HighContrast => AppThemeVariants.HighContrast,
            _ => ThemeVariant.Default,
        };

        if (_appSettings is { } settings && !string.Equals(settings.Settings.ThemeMode, mode, StringComparison.Ordinal))
        {
            settings.Settings.ThemeMode = mode;
            settings.Save();
        }
    }

    /// <summary>应用主工作区字体缩放并持久化。</summary>
    private void ApplyFontScale(double scale)
    {
        RootScaler.LayoutTransform = scale is > 0.5 and < 2 && Math.Abs(scale - 1.0) > 0.001
            ? new ScaleTransform(scale, scale)
            : null;

        if (_appSettings is { } settings && Math.Abs(settings.Settings.FontScale - scale) > 0.001)
        {
            settings.Settings.FontScale = scale;
            settings.Save();
        }
    }

    /// <summary>外观菜单（跟随系统/亮色/深色/高对比）。</summary>
    private void Appearance_Click(object? sender, RoutedEventArgs e)
    {
        var menu = new ContextMenu();
        foreach (var (label, mode) in new[]
                 {
                     ("跟随系统", ThemeModes.System),
                     ("亮色", ThemeModes.Light),
                     ("深色", ThemeModes.Dark),
                     ("高对比", ThemeModes.HighContrast),
                 })
        {
            var item = new MenuItem { Header = label, Tag = mode };
            item.Click += (_, _) => ApplyThemeMode(mode);
            menu.Items.Add(item);
        }

        menu.Open(AppearanceButton);
    }

    /// <summary>字体缩放菜单（90/100/110/125%）。</summary>
    private void FontScale_Click(object? sender, RoutedEventArgs e)
    {
        var menu = new ContextMenu();
        foreach (var scale in new[] { 0.9, 1.0, 1.1, 1.25 })
        {
            var captured = scale;
            var item = new MenuItem { Header = $"{scale * 100:0}%", Tag = scale };
            item.Click += (_, _) => ApplyFontScale(captured);
            menu.Items.Add(item);
        }

        menu.Open(FontScaleButton);
    }

    private ITaskCenterService? _taskCenter;
    private readonly System.Collections.ObjectModel.ObservableCollection<ToastItem> _toasts = new();

    private void TaskCenter_TaskFinished(TaskRun run)
    {
        var (title, accent) = run.State switch
        {
            TaskRunState.Completed => ("任务完成", "#169B62"),
            TaskRunState.Failed => ("任务失败", "#C44545"),
            _ => ("任务已取消", "#D97706"),
        };

        ShowToast(title, $"{run.Title}\n{run.ResultSummary}", accent);
        UpdateRunningTaskCount();
    }

    private void TaskCenter_RunsChanged() => UpdateRunningTaskCount();

    private void UpdateRunningTaskCount()
    {
        var count = _taskCenter?.RunningCount ?? 0;
        RunningTasksText.Text = count > 0 ? $"任务：{count} 运行中" : "任务：无运行中";
    }

    private void ShowToast(string title, string message, string accentColor)
    {
        var toast = new ToastItem { Title = title, Message = message, Accent = accentColor };

        Dispatcher.UIThread.Post(() =>
        {
            _toasts.Insert(0, toast);
            while (_toasts.Count > 3)
            {
                _toasts.RemoveAt(_toasts.Count - 1);
            }

            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(4) };
            timer.Tick += (_, _) =>
            {
                _toasts.Remove(toast);
                timer.Stop();
            };
            timer.Start();
        });
    }

    /// <summary>打开任务中心窗口。</summary>
    private void MenuTaskCenter_Click(object? sender, RoutedEventArgs e)
    {
        OpenTaskCenter();
    }

    /// <summary>对象搜索框回车：深度搜索元数据并在搜索窗口展示。</summary>
    private async void TreeFilterBox_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            await RunTreeFilterSearchAsync();
        }
    }

    private async void TreeFilterSearch_Click(object? sender, RoutedEventArgs e)
    {
        await RunTreeFilterSearchAsync();
    }

    /// <summary>深度搜索当前活动连接的元数据（复用元数据搜索窗口，结果可定位回树）。</summary>
    private async Task RunTreeFilterSearchAsync()
    {
        if (_services is null || DataContext is not MainWindowViewModel vm)
            return;

        var keyword = TreeFilterBox.Text?.Trim();
        if (string.IsNullOrEmpty(keyword))
        {
            return;
        }

        var activeNames = vm.ObjectsExplorer.RootNodes
            .Where(n => n.NodeType == DbObjectTreeNodeType.Connection && n.IsConnectionActive && !string.IsNullOrEmpty(n.Name))
            .Select(n => n.Name!)
            .ToList();

        if (activeNames.Count == 0)
        {
            vm.QueryEditor.StatusMessage = "请先在对象浏览器中连接一个连接，再搜索对象。";
            return;
        }

        var preferred = vm.SelectedQueryTab?.ConnectionName;
        var searchVm = _services.GetRequiredService<SearchViewModel>();
        searchVm.SetConnections(
            activeNames,
            !string.IsNullOrEmpty(preferred) && activeNames.Contains(preferred) ? preferred : activeNames[0]);
        searchVm.Keyword = keyword;

        var window = new SearchWindow(searchVm);
        searchVm.SearchCommand.Execute(null); // 打开即搜索
        await window.ShowDialog<object?>(this);

        var result = window.SelectedItemResult;
        if (result is null)
        {
            return;
        }

        await LocateNodeInTreeAsync(result);
    }

    private void RunningTasks_Click(object? sender, RoutedEventArgs e)
    {
        OpenTaskCenter();
    }

    private void OpenTaskCenter()
    {
        if (_services is null)
        {
            return;
        }

        var vm = _services.GetRequiredService<TaskCenterViewModel>();
        new TaskCenterWindow(vm).Show(this); // 非模态：任务运行时可随时查看
    }

    /// <summary>恢复窗口布局：大小/位置/最大化状态与左栏宽度（位置钳制到可见屏幕，多显示器安全）。</summary>
    private void RestoreWindowLayout()
    {
        if (_services?.GetService<IAppSettingsService>() is not { } settings)
        {
            return;
        }

        var ws = settings.Settings.Workspace;

        if (ws.WindowWidth >= 400 && ws.WindowHeight >= 300)
        {
            Width = ws.WindowWidth;
            Height = ws.WindowHeight;
        }

        if (ws.LeftPanelWidth is > 260 and < 800)
        {
            MainContentGrid.ColumnDefinitions[0].Width = new GridLength(ws.LeftPanelWidth);
        }

        if (ws.WindowX >= 0 && ws.WindowY >= 0)
        {
            Position = new PixelPoint((int)ws.WindowX, (int)ws.WindowY);
            ClampWindowToVisibleScreen();
        }

        if (string.Equals(ws.WindowState, "Maximized", StringComparison.OrdinalIgnoreCase))
        {
            WindowState = global::Avalonia.Controls.WindowState.Maximized;
        }
    }

    /// <summary>把窗口位置钳制到所在屏幕的工作区内。</summary>
    private void ClampWindowToVisibleScreen()
    {
        try
        {
            var screen = Screens.ScreenFromPoint(Position) ?? Screens.Primary;
            if (screen is null)
            {
                return;
            }

            var area = screen.WorkingArea;
            int w = (int)(Width * RenderScaling);
            int h = (int)(Height * RenderScaling);
            int x = Math.Clamp(Position.X, area.X, Math.Max(area.X, area.X + area.Width - w));
            int y = Math.Clamp(Position.Y, area.Y, Math.Max(area.Y, area.Y + area.Height - h));
            Position = new PixelPoint(x, y);
        }
        catch
        {
            // 屏幕信息不可用时跳过钳制。
        }
    }

    /// <summary>保存窗口布局与左栏宽度。</summary>
    private void SaveWindowLayout()
    {
        if (_services?.GetService<IAppSettingsService>() is not { } settings)
        {
            return;
        }

        var ws = settings.Settings.Workspace;

        if (WindowState == global::Avalonia.Controls.WindowState.Normal)
        {
            ws.WindowX = Position.X;
            ws.WindowY = Position.Y;
            ws.WindowWidth = Width;
            ws.WindowHeight = Height;
        }

        ws.WindowState = WindowState == global::Avalonia.Controls.WindowState.Maximized ? "Maximized" : "Normal";
        var leftColumn = MainContentGrid.ColumnDefinitions[0].Width;
        ws.LeftPanelWidth = leftColumn.IsAbsolute ? leftColumn.Value : 400;

        settings.Save();
    }

    /// <summary>窗口真实关闭后：捕获查询标签会话（SQL 草稿）与窗口布局。</summary>
    protected override void OnClosed(EventArgs e)
    {
        try
        {
            if (DataContext is MainWindowViewModel vm)
            {
                vm.CaptureSession();
            }

            SaveWindowLayout();

            if (_taskCenter is not null)
            {
                _taskCenter.TaskFinished -= TaskCenter_TaskFinished;
                _taskCenter.RunsChanged -= TaskCenter_RunsChanged;
            }
        }
        finally
        {
            base.OnClosed(e);
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

    /// <summary>
    /// 主窗口整体关闭拦截：存在未保存数据修改或未保存 SQL 的标签时弹三选确认。
    /// 「是」= 保存全部数据修改后退出（SQL 文本不逐个弹文件对话框，退出即丢失请先手动保存）；
    /// 「否」= 放弃全部并退出；「取消」= 留在应用。
    /// </summary>
    protected override async void OnClosing(WindowClosingEventArgs e)
    {
        base.OnClosing(e);

        if (e.Cancel || _closingConfirmed || DataContext is not MainWindowViewModel vm)
        {
            return;
        }

        // 存在运行中的后台任务时先确认（退出会尝试取消它们）。
        var taskCenter = _taskCenter ?? _services?.GetService<ITaskCenterService>();
        if (taskCenter is { HasRunning: true })
        {
            e.Cancel = true;

            var runningBox = MessageBoxManager.GetMessageBoxStandard(
                title: "后台任务正在运行",
                text: $"有 {taskCenter.RunningCount} 个后台任务正在运行（转换/导入导出/统计等）。\n\n「是」取消任务并退出；「否」返回应用（可在任务中心查看进度）。",
                ButtonEnum.YesNo,
                MsBox.Avalonia.Enums.Icon.Warning);
            var runningResult = await runningBox.ShowWindowDialogAsync(this);

            if (runningResult != ButtonResult.Yes)
            {
                return;
            }

            foreach (var run in taskCenter.Runs.Where(r => r.State == TaskRunState.Running).ToList())
            {
                taskCenter.Cancel(run.Id);
            }

            // 等待任务退出运行态（最多 3 秒），随后直接退出（跳过其余确认）。
            for (int i = 0; i < 30 && taskCenter.HasRunning; i++)
            {
                await Task.Delay(100);
            }

            _closingConfirmed = true;
            Close();
            return;
        }

        var dataChangedTabs = vm.QueryTabs.Where(t => t.HasPendingChanges).ToList();
        var modifiedTabs = vm.QueryTabs.Where(t => t.IsModified).ToList();

        if (dataChangedTabs.Count == 0 && modifiedTabs.Count == 0)
        {
            return;
        }

        var messageParts = new List<string>();
        if (dataChangedTabs.Count > 0)
        {
            messageParts.Add($"{dataChangedTabs.Count} 个标签的结果集有未保存的数据修改");
        }
        if (modifiedTabs.Count > 0)
        {
            messageParts.Add($"{modifiedTabs.Count} 个标签的 SQL 文本尚未保存到文件");
        }

        e.Cancel = true;

        var box = MessageBoxManager.GetMessageBoxStandard(
            title: "未保存的更改",
            text: $"有 {string.Join("，", messageParts)}。\n\n「是」保存数据修改并退出；「否」放弃全部并退出；「取消」留在应用。",
            ButtonEnum.YesNoCancel,
            MsBox.Avalonia.Enums.Icon.Warning);
        var result = await box.ShowWindowDialogAsync(this);

        if (result == ButtonResult.Cancel)
        {
            return;
        }

        if (result == ButtonResult.Yes)
        {
            // 逐个保存数据修改；任一保存失败（含用户取消）则留在应用。
            foreach (var tab in dataChangedTabs)
            {
                await tab.SaveEditsAsync();
                if (tab.HasPendingChanges)
                {
                    return;
                }
            }
        }

        _closingConfirmed = true;
        Close();
    }

    /// <summary>主窗口关闭确认已通过（避免二次弹窗）。</summary>
    private bool _closingConfirmed;

    /// <summary>保存刷新后在结果网格中滚动并选中指定行。</summary>
    private void LocateRowInResultGrid(QueryResultRow row)
    {
        if (FindDataGridInVisualTree(this) is { } grid)
        {
            grid.ScrollIntoView(row, null);
            grid.SelectedItem = row;
        }
    }

    /// <summary>当前查询标签的可编辑状态变化时，重建结果网格列（只读/可编辑切换）。</summary>
    private void CurrentQueryTab_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (_currentQueryTab is null) return;

        if (e.PropertyName is nameof(QueryTabViewModel.IsResultEditable)
            or nameof(QueryTabViewModel.HasResult))
        {
            QueryTabColumns_CollectionChanged(_currentQueryTab.Columns, null!);
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

        // 搜索高亮：短暂高亮目标节点，2秒后自动清除
        target.IsHighlighted = true;
        _ = Task.Delay(2000).ContinueWith(_ =>
        {
            global::Avalonia.Threading.Dispatcher.UIThread.Post(() => target.IsHighlighted = false);
        });

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

    /// <summary>打开图像查看器（工具菜单）。</summary>
    private async void MenuImageViewer_Click(object? sender, RoutedEventArgs e)
    {
        if (_services is null)
            return;

        var imgVm = _services.GetRequiredService<ImageViewerViewModel>();
        var window = new ImageViewerWindow(imgVm);
        await window.ShowDialog<object?>(this);
    }

    /// <summary>打开 JSON 查看器（工具菜单）。</summary>
    private async void MenuJsonViewer_Click(object? sender, RoutedEventArgs e)
    {
        if (_services is null)
            return;

        var jsonVm = _services.GetRequiredService<JsonViewerViewModel>();
        var window = new JsonViewerWindow(jsonVm);
        await window.ShowDialog<object?>(this);
    }

    /// <summary>显示「关于」对话框。</summary>
    private async void MenuAbout_Click(object? sender, RoutedEventArgs e)
    {
        var title = "关于 DatabaseManager";
        var version = typeof(Program).Assembly.GetName().Version?.ToString() ?? "1.0.0";
        var msg =
@"DatabaseManager (Avalonia 版)
================================
版本: " + version + @"
框架: .NET 8 + Avalonia 11
许可证: 开源
================================
特性:
- 跨平台数据库管理工具（Windows/macOS/Linux）
- 支持 SQL Server / MySQL / Oracle / Postgres / SQLite
- 连接管理 / 对象浏览 / 查询执行 / 数据编辑
- 表设计 / 结构对比 / 数据对比 / 数据库转换
- 导入导出(CSV/Excel) / 备份 / 诊断 / 优化 / 统计

感谢使用！";
        var box = MessageBoxManager.GetMessageBoxStandard(title, msg, ButtonEnum.Ok, MsBox.Avalonia.Enums.Icon.Info);
        await box.ShowWindowDialogAsync(this);
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
                vm.TrackRecentScript(files[0].Path?.LocalPath ?? string.Empty);
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
                vm.TrackRecentScript(path);
            }
        }
    }

    /// <summary>主工具栏：新建查询。</summary>
    private void ToolNewQuery_Click(object? sender, RoutedEventArgs e)
    {
        (DataContext as MainWindowViewModel)?.NewQuery();
    }

    /// <summary>主工具栏：执行当前查询标签的 SQL（有选区时仅执行选区）。</summary>
    private async void ToolExecute_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm && vm.SelectedQueryTab is not null)
        {
            // 同步当前数据库上下文（内联编辑定位目标表需要）。
            if (!string.IsNullOrEmpty(vm.CurrentDatabase))
            {
                vm.SelectedQueryTab.DatabaseName = vm.CurrentDatabase;
            }

            // 若编辑器有选中文本则仅执行选区
            var tabControl = this.FindControl<TabControl>("QueryTabsControl");
            var editor = tabControl is not null ? FindSqlEditorInVisualTree(tabControl) : null;
            var selectedText = editor?.GetSelectedText()?.Trim();
            if (!string.IsNullOrEmpty(selectedText))
            {
                await vm.SelectedQueryTab.ExecuteWithSqlAsync(selectedText);
            }
            else
            {
                await vm.SelectedQueryTab.ExecuteAsync();
            }

            if (vm.SelectedQueryTab.LastErrorLine is > 0 && editor is not null)
                editor.GoToLine(vm.SelectedQueryTab.LastErrorLine.Value);
        }
    }

    /// <summary>取消当前正在执行的 SQL。</summary>
    private void ToolCancelExecution_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel { SelectedQueryTab: not null } vm)
            vm.SelectedQueryTab.CancelExecutionCommand.Execute(null);
    }

    /// <summary>在可视树中查找当前标签页的 SqlEditor。</summary>
    private static SqlEditor? FindSqlEditorInVisualTree(Control parent)
    {
        foreach (var descendant in parent.GetVisualDescendants())
        {
            if (descendant is SqlEditor editor)
                return editor;
        }
        return null;
    }

    /// <summary>美化当前 SQL（有选区则仅美化选区）。</summary>
    private void ToolFormat_Click(object? sender, RoutedEventArgs e)
    {
        var tabControl = this.FindControl<TabControl>("QueryTabsControl");
        var editor = tabControl is not null ? FindSqlEditorInVisualTree(tabControl) : null;
        editor?.Format();
    }

    /// <summary>把 SQL 插入到当前查询编辑器光标处（脚本库/查询历史共用）。</summary>
    private void InsertSqlToCurrentEditor(string sql)
    {
        if (string.IsNullOrEmpty(sql))
        {
            return;
        }

        var tabControl = this.FindControl<TabControl>("QueryTabsControl");
        var editor = tabControl is not null ? FindSqlEditorInVisualTree(tabControl) : null;
        if (editor is not null)
        {
            editor.InsertAtCaret(sql);
        }
        else if (DataContext is MainWindowViewModel { SelectedQueryTab: not null } vm)
        {
            vm.SelectedQueryTab.SqlText += (vm.SelectedQueryTab.SqlText.Length > 0 ? Environment.NewLine : string.Empty) + sql;
        }
    }

    /// <summary>打开查询历史窗口。</summary>
    private void MenuQueryHistory_Click(object? sender, RoutedEventArgs e)
    {
        if (_services is null)
        {
            return;
        }

        var vm = _services.GetRequiredService<QueryHistoryViewModel>();
        vm.InsertToEditorRequested = InsertSqlToCurrentEditor;
        new QueryHistoryWindow(vm).ShowDialog(this);
    }

    /// <summary>打开脚本库窗口。</summary>
    private void MenuScriptLibrary_Click(object? sender, RoutedEventArgs e)
    {
        if (_services is null)
        {
            return;
        }

        var vm = _services.GetRequiredService<ScriptLibraryViewModel>();
        vm.InsertToEditorRequested = InsertSqlToCurrentEditor;
        new ScriptLibraryWindow(vm).ShowDialog(this);
    }

    /// <summary>把当前编辑器中的 SQL 保存为脚本库条目。</summary>
    private void MenuSaveToLibrary_Click(object? sender, RoutedEventArgs e)
    {
        if (_services is null)
        {
            return;
        }

        var tabControl = this.FindControl<TabControl>("QueryTabsControl");
        var editor = tabControl is not null ? FindSqlEditorInVisualTree(tabControl) : null;
        var sql = editor?.GetSelectedText();
        if (string.IsNullOrWhiteSpace(sql) && DataContext is MainWindowViewModel { SelectedQueryTab: not null } vm)
        {
            sql = vm.SelectedQueryTab.SqlText;
        }

        if (string.IsNullOrWhiteSpace(sql))
        {
            return;
        }

        var libraryVm = _services.GetRequiredService<ScriptLibraryViewModel>();
        libraryVm.InsertToEditorRequested = InsertSqlToCurrentEditor;
        libraryVm.BeginNewWithSql(sql!);
        new ScriptLibraryWindow(libraryVm).ShowDialog(this);
    }

    /// <summary>获取当前 SQL 的执行计划（有选区时仅分析选区）。</summary>
    private void ToolExplain_Click(object? sender, RoutedEventArgs e)
    {
        if (_services is null || DataContext is not MainWindowViewModel vm)
        {
            return;
        }

        if (vm.SelectedConnection is null)
        {
            _ = MessageBoxManager.GetMessageBoxStandard(
                "执行计划", "请先连接一个数据库。", ButtonEnum.Ok, MsBox.Avalonia.Enums.Icon.Warning)
                .ShowWindowDialogAsync(this);
            return;
        }

        var tabControl = this.FindControl<TabControl>("QueryTabsControl");
        var editor = tabControl is not null ? FindSqlEditorInVisualTree(tabControl) : null;
        var sql = editor?.GetSelectedText()?.Trim();
        if (string.IsNullOrWhiteSpace(sql) && vm.SelectedQueryTab is not null)
        {
            sql = vm.SelectedQueryTab.SqlText;
        }

        if (string.IsNullOrWhiteSpace(sql))
        {
            return;
        }

        var planVm = _services.GetRequiredService<ExecutionPlanViewModel>();
        planVm.Connection = vm.SelectedConnection;
        planVm.SqlText = sql!;
        new ExecutionPlanWindow(planVm).ShowDialog(this);
    }

    /// <summary>导出当前查询结果集为 CSV / JSON 文件。</summary>
    private async void ToolExportResults_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel { SelectedQueryTab: not null } vm)
        {
            return;
        }

        var file = await StorageProvider.SaveFilePickerAsync(new global::Avalonia.Platform.Storage.FilePickerSaveOptions
        {
            Title = "导出查询结果",
            SuggestedFileName = $"query-result-{DateTime.Now:yyyyMMdd-HHmmss}.csv",
            DefaultExtension = "csv",
            FileTypeChoices = new[]
            {
                new global::Avalonia.Platform.Storage.FilePickerFileType("CSV 文件") { Patterns = new[] { "*.csv" } },
                new global::Avalonia.Platform.Storage.FilePickerFileType("JSON 文件") { Patterns = new[] { "*.json" } },
            },
        });

        if (file is null)
        {
            return;
        }

        var path = file.Path?.LocalPath ?? string.Empty;
        var format = path.EndsWith(".json", StringComparison.OrdinalIgnoreCase) ? "JSON" : "CSV";
        await vm.SelectedQueryTab.ExportResultsAsync(path, format);
    }

    /// <summary>查询结果内联编辑：新增一行并滚动定位到该行。</summary>
    private void QueryAddRow_Click(object? sender, RoutedEventArgs e)
    {
        if (_currentQueryTab is null || DataContext is not MainWindowViewModel vm)
            return;

        // 执行前同步数据库上下文。
        if (string.IsNullOrEmpty(_currentQueryTab.DatabaseName) && !string.IsNullOrEmpty(vm.CurrentDatabase))
        {
            _currentQueryTab.DatabaseName = vm.CurrentDatabase;
        }

        _currentQueryTab.AddRowForEdit(out var newRow);

        if (newRow is not null && FindDataGridInVisualTree(this) is { } grid)
        {
            grid.ScrollIntoView(newRow, null);
            grid.SelectedItem = newRow;
        }
    }

    /// <summary>查询结果内联编辑：删除选中的行（保存时生效）。</summary>
    private void QueryRemoveRow_Click(object? sender, RoutedEventArgs e)
    {
        if (_currentQueryTab is null)
            return;

        if (FindDataGridInVisualTree(this)?.SelectedItem is QueryResultRow selected)
        {
            _currentQueryTab.RemoveRowForEdit(selected);
        }
    }

    /// <summary>查询结果内联编辑：还原全部未保存改动。</summary>
    private void QueryRevert_Click(object? sender, RoutedEventArgs e)
    {
        _currentQueryTab?.RevertEdits();
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

        // 加载中：再次双击 = 取消加载（连接/文件夹/子文件夹通用）。
        if (node.IsLoading)
        {
            node.LoadCts?.Cancel();
            return;
        }

        // 「加载更多」占位节点：双击续接下一批子节点。
        if (node.IsLoadMore)
        {
            await vm.ObjectsExplorer.LoadMoreAsync(node);
            return;
        }

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

        // 关键修复：Avalonia 中右键单击不会改变 TreeView.SelectedItem。
        // 若继续使用 SelectedItem，菜单会作用到"上一次左键选中的其他节点"上（功能错位），
        // 且未选中任何节点时（首次打开对象树即右击）会完全无菜单。故从右键命中的树项解析目标节点。
        var node = ResolveContextNode(e);
        if (node is null)
            return;

        // 使用构建器模式按节点类型分发右键菜单（P2增强：含Compare/Migrate回调）
        var connectionService = _services?.GetService<IDbConnectionService>();
        var ddlService = _services?.GetService<IDdlService>();
        var builder = new ObjectTreeContextMenuBuilder(
            vm,
            ObjectsTree,
            asyncAction: async (action) => action(),
            connectionService: connectionService,
            ddlService: ddlService,
            openConnectionManager: () => _ = OpenConnectionManagerAsync(),
            openTableDesigner: (n, isNew) => _ = isNew ? OpenNewTableDesignerAsync(n) : OpenTableDesignerAsync(n),

            openExportWindow: (n) => _ = OpenExportWindowForTableAsync(n),
            openImportWindow: (n) => _ = OpenImportWindowForTableAsync(n),
            openSchemaCompare: (n) => _ = OpenSchemaCompareForNodeAsync(n),
            openDataCompare: (n) => _ = OpenDataCompareForNodeAsync(n),
            openConvert: (n) => _ = OpenConvertForNodeAsync(n));

        builder.BuildAndShow(node, e);
    }

    /// <summary>
    /// 解析用户正在右键点击的目标树节点。
    /// Avalonia（与 WPF 一致）中右键单击不会改变 TreeView.SelectedItem，
    /// 因此需从 ContextRequested 事件的原始触发元素沿可视化树向上查找命中的 TreeViewItem，
    /// 取其 DataContext（DbObjectTreeNode）作为菜单作用目标；找不到时回退到当前选中节点。
    /// </summary>
    private DbObjectTreeNode? ResolveContextNode(ContextRequestedEventArgs e)
    {
        // 从右键事件源（Source 为引发事件的最底层 Interactive 控件）向上遍历可视化树，
        // 命中 TreeViewItem 后取其 DataContext（DbObjectTreeNode）作为菜单作用目标。
        var current = e.Source as Visual;
        while (current is not null)
        {
            if (current is TreeViewItem treeViewItem && treeViewItem.DataContext is DbObjectTreeNode node)
            {
                // 同步选中到被右键的节点，保证后续操作与界面高亮状态一致。
                ObjectsTree.SelectedItem = treeViewItem;
                return node;
            }
            current = current.GetVisualParent();
        }

        // 回退：未命中任何树项（如右键空白区）时使用当前选中节点。
        return ObjectsTree.SelectedItem as DbObjectTreeNode;
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

    /// <summary>刷新当前查询标签的 DataGrid 列监听（切换标签时动态重建列）。</summary>
    private void RefreshQueryTabColumnListener()
    {
        if (DataContext is not MainWindowViewModel currentVm)
            return;

        // 移除旧监听
        if (_currentQueryTab is not null)
        {
            _currentQueryTab.Columns.CollectionChanged -= QueryTabColumns_CollectionChanged;
            _currentQueryTab.PropertyChanged -= CurrentQueryTab_PropertyChanged;
        }

        // 指向当前选中的查询标签
        _currentQueryTab = currentVm.SelectedQueryTab;

        if (_currentQueryTab is not null)
        {
            _currentQueryTab.Columns.CollectionChanged += QueryTabColumns_CollectionChanged;
            _currentQueryTab.PropertyChanged += CurrentQueryTab_PropertyChanged;
            // 立即触发一次列重建
            QueryTabColumns_CollectionChanged(_currentQueryTab.Columns, null!);
        }
    }

    /// <summary>查询标签列变化时，动态重建对应 DataGrid 的数据列。</summary>
    private void QueryTabColumns_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        if (_currentQueryTab is null) return;

        // 由于 DataGrid 在 DataTemplate 内部，需要在 TabControl 的 Visual Tree 中查找。
        // TabControl 切换标签会重新实例化 DataTemplate 内容，因此对所有已物化的结果网格逐个重建。
        var tabControl = this.FindControl<TabControl>("QueryTabsControl");
        if (tabControl is null) return;

        foreach (var descendant in tabControl.GetVisualDescendants())
        {
            if (descendant is DataGrid { Name: "QueryResultGrid" } grid)
            {
                RebuildResultGridColumns(grid, grid.DataContext as QueryTabViewModel ?? _currentQueryTab);
            }
        }
    }

    /// <summary>结果网格挂载到视觉树时基于自身 DataContext 重建列（覆盖切换标签后 DataTemplate 重新实例化的场景）。</summary>
    private void QueryResultGrid_AttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        if (sender is DataGrid grid && grid.DataContext is QueryTabViewModel tabVm)
        {
            RebuildResultGridColumns(grid, tabVm);
        }
    }

    /// <summary>按查询标签的列集合重建指定结果网格的数据列。</summary>
    private static void RebuildResultGridColumns(DataGrid grid, QueryTabViewModel tabVm)
    {
        grid.Columns.Clear();

        for (int i = 0; i < tabVm.Columns.Count; i++)
        {
            // 内联编辑模式：非只读列（映射到表的非自增/计算/二进制列）开放双向编辑；否则只读。
            bool editableColumn = tabVm.IsColumnEditable(i);
            bool isPrimaryKey = tabVm.IsPrimaryKeyColumn(i);

            grid.Columns.Add(new DataGridTextColumn
            {
                // 主键列头加 🔑 标识，便于用户识别编辑定位依据。
                Header = isPrimaryKey ? $"🔑 {tabVm.Columns[i]}" : tabVm.Columns[i],
                // 使用 Values[i] 绑定，避免直接索引器路径解析在不同 Avalonia 版本/主题下的兼容性问题
                Binding = new Binding($"Values[{i}]")
                {
                    Mode = editableColumn ? BindingMode.TwoWay : BindingMode.OneWay,
                },
                IsReadOnly = !editableColumn,
                CanUserResize = true,
                Width = DataGridLength.Auto,
                MinWidth = 40,
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

    /// <summary>关闭查询标签页（带未保存修改提示，用户取消则不关闭）。</summary>
    private async void CloseTab_Click(object? sender, RoutedEventArgs e)
    {
        QueryTabViewModel? tab = sender switch
        {
            Button { Tag: QueryTabViewModel t } => t,
            MenuItem { Tag: QueryTabViewModel t } => t,
            _ => null,
        };

        if (tab is null || DataContext is not MainWindowViewModel vm)
            return;

        await vm.CloseQueryTabAsync(tab);
    }

    /// <summary>关闭除当前标签外的所有其他标签（逐个带未保存提示，任一取消则停止后续）。</summary>
    private async void CloseOtherTabs_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { Tag: QueryTabViewModel currentTab } && DataContext is MainWindowViewModel vm)
        {
            // 收集需要关闭的标签（排除当前标签）
            var tabsToClose = vm.QueryTabs.Where(t => t != currentTab).ToList();
            foreach (var tab in tabsToClose)
            {
                if (!await vm.CloseQueryTabAsync(tab))
                    break;
            }
        }
    }

    /// <summary>关闭所有标签页（逐个带未保存提示，取消任一则停止后续）。</summary>
    private async void CloseAllTabs_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            // 复制列表以避免遍历时修改
            var allTabs = vm.QueryTabs.ToList();
            foreach (var tab in allTabs)
            {
                if (!await vm.CloseQueryTabAsync(tab))
                    break;
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

    /// <summary>请求确认丢弃未保存改动的回调（保留接口，当前无数据编辑 Tab，直接允许）。</summary>
    private Task<bool> RequestDiscardDataChangesAsync() => Task.FromResult(true);

    /// <summary>对可能写入数据或改变结构的 SQL 请求二次确认。</summary>
    private async Task<bool> RequestDangerousExecutionAsync(string sql)
    {
        var preview = sql.Trim();
        if (preview.Length > 160)
            preview = $"{preview[..160]}...";

        var box = MessageBoxManager.GetMessageBoxStandard(
            title: "确认执行危险 SQL",
            text: $"该语句可能修改数据或数据库结构，是否继续执行？\n\n{preview}",
            ButtonEnum.YesNo,
            MsBox.Avalonia.Enums.Icon.Warning);
        return await box.ShowWindowDialogAsync(this) == ButtonResult.Yes;
    }

    /// <summary>请求关闭标签页的回调：有未保存修改时弹出三选一（保存/不保存/取消）对话框。</summary>
    private async Task<bool> RequestCloseTabAsync(QueryTabViewModel tab)
    {
        if (tab.HasPendingChanges)
        {
            var dataBox = MessageBoxManager.GetMessageBoxStandard(
                title: "未保存的数据修改",
                text: $"「{tab.Title}」的结果集有未保存的数据修改，是否保存后关闭？",
                ButtonEnum.YesNoCancel,
                MsBox.Avalonia.Enums.Icon.Warning);
            var dataResult = await dataBox.ShowWindowDialogAsync(this);
            if (dataResult == ButtonResult.Yes)
            {
                await tab.SaveEditsAsync();
                if (tab.HasPendingChanges)
                    return false;
            }
            else if (dataResult != ButtonResult.No)
            {
                return false;
            }
        }

        if (!tab.IsModified)
            return true;

        var box = MessageBoxManager.GetMessageBoxStandard(
            title: "未保存的更改",
            text: $"「{tab.Title}」有未保存的更改，是否保存？",
            ButtonEnum.YesNoCancel,
            MsBox.Avalonia.Enums.Icon.Warning);

        var result = await box.ShowWindowDialogAsync(this);
        switch (result)
        {
            case ButtonResult.Yes:
                await SaveQueryTabContentAsync(tab);
                return !tab.IsModified;
            case ButtonResult.No:
                tab.MarkAsSaved();
                return true;
            default:
                return false;
        }
    }

    /// <summary>保存指定查询标签的 SQL 内容；取消或未选中文件时不清除修改标记。</summary>
    private async Task SaveQueryTabContentAsync(QueryTabViewModel tab)
    {
        var storage = StorageProvider;
        var file = await storage.SaveFilePickerAsync(new global::Avalonia.Platform.Storage.FilePickerSaveOptions
        {
            Title = "保存 SQL 脚本",
            SuggestedFileName = "query.sql",
            DefaultExtension = "sql",
            FileTypeChoices = new[] { new global::Avalonia.Platform.Storage.FilePickerFileType("SQL 脚本") { Patterns = new[] { "*.sql" } } },
        });

        if (file is null)
            return;

        var path = file.Path?.LocalPath ?? string.Empty;
        File.WriteAllText(path, tab.SqlText);
        tab.MarkAsSaved();
        tab.StatusMessage = $"已保存到 {Path.GetFileName(path)}。";
    }

    /// <summary>主窗口快捷键处理（对齐 DBeaver 快捷键）。</summary>
    private async void MainWindow_KeyDown(object? sender, KeyEventArgs e)
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
                // Ctrl+W：关闭当前标签页（带未保存修改提示）
                e.Handled = true;
                if (vm.SelectedQueryTab is not null)
                {
                    await vm.CloseQueryTabAsync(vm.SelectedQueryTab);
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

            case Key.F when ctrl && e.KeyModifiers.HasFlag(KeyModifiers.Shift):
                // Ctrl+Shift+F：美化 SQL
                e.Handled = true;
                ToolFormat_Click(sender, e);
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

/// <summary>Toast 通知条目（仅主窗口 UI 使用）。</summary>
public sealed class ToastItem
{
    public string Title { get; init; } = string.Empty;

    public string Message { get; init; } = string.Empty;

    public string Accent { get; init; } = "#52657F";
}
