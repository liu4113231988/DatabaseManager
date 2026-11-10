using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using DatabaseManager.AppCore.Common;
using DatabaseManager.AppCore.Models;
using DatabaseManager.AppCore.Services;
using DatabaseManager.AppCore.ViewModels;

namespace DatabaseManager.Avalonia.Smoke;

internal static class DatabaseDeletionSmokeChecks
{
    public static async Task RunAsync(string output, MainWindowViewModel vm, IDbConnectionService connections)
    {
        var desktop = (IClassicDesktopStyleApplicationLifetime)Application.Current!.ApplicationLifetime!;
        var owner = desktop.MainWindow!;
        var oldContent = owner.Content;
        var parent = vm.ObjectsExplorer.FindConnectionNode("数据库切换测试")!;
        var node = new DbObjectTreeNode { Name = "app_db", NodeType = DbObjectTreeNodeType.Database, DatabaseName = "app_db" };
        parent.AddChild(node);
        var tree = new TreeView { ItemsSource = new[] { node }, Margin = new Thickness(20) };
        owner.Content = tree;
        owner.Width = 640;
        owner.Height = 480;
        DbObjectTreeNode? backupTarget = null;
        DbObjectTreeNode? propertiesTarget = null;
        bool restoreMode = false;
        var builder = new ObjectTreeContextMenuBuilder(vm, tree, action => _ = action(), connections, new DefaultDdlService(connections),
            openBackup: (target, restore) => { backupTarget = target; restoreMode = restore; },
            openDatabaseProperties: target => propertiesTarget = target);
        var build = typeof(ObjectTreeContextMenuBuilder).GetMethod("BuildDatabaseMenu", BindingFlags.Instance | BindingFlags.NonPublic)!;
        try
        {
            var menu = new ContextMenu();
            build.Invoke(builder, [menu, node]);
            var delete = menu.Items.OfType<MenuItem>().Single(i => i.Header?.ToString()?.StartsWith("删除数据库") == true);
            if (!delete.IsEnabled) throw new InvalidOperationException("User database deletion must be enabled.");
            menu.Open(tree);
            await Task.Delay(250);
            Capture(menu, "database-context-menu");
            menu.Close();

            menu.Items.OfType<MenuItem>().Single(i => Equals(i.Header, "备份数据库..."))
                .RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            if (backupTarget != node || restoreMode) throw new InvalidOperationException("Backup menu must route to the clicked database.");
            menu.Items.OfType<MenuItem>().Single(i => Equals(i.Header, "恢复数据库..."))
                .RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            if (backupTarget != node || !restoreMode) throw new InvalidOperationException("Restore menu must select restore mode for the clicked database.");
            menu.Items.OfType<MenuItem>().Single(i => Equals(i.Header, "数据库属性..."))
                .RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            if (propertiesTarget != node) throw new InvalidOperationException("Properties must use the clicked database.");

            // 使用 Delete 快捷键的同一个入口验证名称不匹配和取消；不执行真实删库。
            var pending = builder.DeleteAsync(node);
            await Task.Delay(200);
            var dialog = desktop.Windows.Single(w => w.Title == "删除数据库");
            Capture(dialog, "database-delete-confirm");
            dialog.GetVisualDescendants().OfType<TextBox>().Single().Text = "wrong_database";
            dialog.GetVisualDescendants().OfType<Button>().Single(b => Equals(b.Content, "确定"))
                .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await pending;
            if (!vm.QueryEditor.StatusMessage.Contains("不匹配") || !parent.Children.Contains(node))
                throw new InvalidOperationException("Mismatched confirmation must preserve the database.");
            pending = builder.DeleteAsync(node);
            await Task.Delay(100);
            desktop.Windows.Single(w => w.Title == "删除数据库").Close();
            await pending;
            if (!parent.Children.Contains(node)) throw new InvalidOperationException("Closing confirmation must cancel deletion.");

            var systemMenu = new ContextMenu();
            var systemNode = new DbObjectTreeNode { Name = "postgres", NodeType = DbObjectTreeNodeType.Database, Parent = parent };
            build.Invoke(builder, [systemMenu, systemNode]);
            if (systemMenu.Items.OfType<MenuItem>().Single(i => i.Header?.ToString()?.StartsWith("删除数据库") == true).IsEnabled)
                throw new InvalidOperationException("System database deletion must be disabled.");
        }
        finally
        {
            parent.Children.Remove(node);
            owner.Content = oldContent;
        }

        void Capture(Control control, string name)
        {
            using var bitmap = new RenderTargetBitmap(new PixelSize((int)control.Bounds.Width, (int)control.Bounds.Height), new Vector(96, 96));
            bitmap.Render(control);
            bitmap.Save(Path.Combine(output, name + ".png"), new PngBitmapEncoderOptions());
        }
    }
}
