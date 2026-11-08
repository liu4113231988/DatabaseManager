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
using DatabaseManager.Avalonia.Views;

namespace DatabaseManager.Avalonia.Smoke;

internal static class DatabaseManagementSmokeChecks
{
    public static async Task RunAsync(string output, MainWindowViewModel vm, IDbConnectionService connections, ConnectionItem fileConnection)
    {
        var desktop = (IClassicDesktopStyleApplicationLifetime)Application.Current!.ApplicationLifetime!;
        var root = vm.ObjectsExplorer.FindConnectionNode("数据库切换测试")!;
        var node = new DbObjectTreeNode { Name = "report_db", NodeType = DbObjectTreeNodeType.Database, Parent = root };
        Task? action = null;
        var builder = new ObjectTreeContextMenuBuilder(vm, new TreeView(), work => action = work(), connections, new DefaultDdlService(connections));
        var menu = new ContextMenu();
        typeof(ObjectTreeContextMenuBuilder).GetMethod("BuildConnectionMenu", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(builder, [menu, root]);
        var create = menu.Items.OfType<MenuItem>().Single(i => Equals(i.Header, "新建数据库..."));
        if (!create.IsEnabled) throw new InvalidOperationException("Creating a database must be available at connection level.");
        create.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        await Task.Delay(150);
        var input = desktop.Windows.Single(w => w.Title == "新建数据库");
        Capture(input, "database-create-name");
        input.GetVisualDescendants().OfType<TextBox>().Single().Text = "fresh_db";
        input.GetVisualDescendants().OfType<Button>().Single(b => Equals(b.Content, "确定")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        await Task.Delay(150);
        var confirm = desktop.Windows.Single(w => w.Title == "新建数据库");
        if (!confirm.GetVisualDescendants().OfType<TextBlock>().Any(t => t.Text?.Contains("CREATE DATABASE \"fresh_db\";") == true))
            throw new InvalidOperationException("Creation confirmation must display the dialect SQL.");
        confirm.Close();
        await action!;
        if (root.Children.Any(n => n.Name == "fresh_db")) throw new InvalidOperationException("Canceling must not create a database.");

        foreach (bool restore in new[] { false, true })
        {
            var backupVm = new BackupViewModel(connections, new DefaultBackupService(), new DefaultTaskCenterService());
            var window = new BackupWindow(backupVm, node.GetConnectionSnapshot()!, restore);
            window.Show();
            await Task.Delay(200);
            window.GetVisualDescendants().OfType<Button>().Single(b => Equals(b.Content, "刷新连接"))
                .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await Task.Delay(200);
            var selector = window.GetVisualDescendants().OfType<AtomUI.Desktop.Controls.ComboBox>().Single();
            if (backupVm.SelectedConnection?.Database != "report_db" || (selector.SelectedItem as ConnectionItem)?.Database != "report_db"
                || root.Connection!.Database != "app_db" || !backupVm.CanBackup || !backupVm.CanRestore)
                throw new InvalidOperationException("Opening or refreshing backup/restore must preserve the clicked database without mutating the saved default.");
            Capture(window, restore ? "database-restore" : "database-backup");
            if (restore)
            {
                backupVm.RestoreFilePath = "fixture.backup";
                window.GetVisualDescendants().OfType<Button>().Single(b => Equals(b.Content, "恢复备份"))
                    .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                await Task.Delay(200);
                var restoreConfirm = desktop.Windows.Single(w => w.Title == "确认恢复数据库");
                Capture(restoreConfirm, "database-restore-confirm");
                if (!restoreConfirm.GetVisualDescendants().Any(control => (control switch
                    {
                        TextBlock text => text.Text,
                        TextBox text => text.Text,
                        _ => null
                    })?.Contains("report_db") == true))
                    throw new InvalidOperationException("Restore confirmation must identify the clicked database.");
                restoreConfirm.Close();
                await Task.Delay(100);
                if (backupVm.IsBusy) throw new InvalidOperationException("Closing the restore confirmation must cancel the operation.");
            }
            window.Close();
        }
        var propertiesWindow = new DatabasePropertiesWindow(fileConnection, "main", new DatabasePropertiesService());
        propertiesWindow.Show();
        await Task.Delay(250);
        var grid = propertiesWindow.GetVisualDescendants().OfType<DataGrid>().Single();
        if (!grid.IsReadOnly || !grid.ItemsSource!.Cast<DatabaseProperty>().Any(p => p.Name == "文件大小（字节）")
            || !grid.ItemsSource.Cast<DatabaseProperty>().Any(p => p.Name == "数据源" && p.Value == fileConnection.Database))
            throw new InvalidOperationException("File database properties must show real file information in a read-only grid.");
        Capture(propertiesWindow, "database-properties");
        propertiesWindow.Close();

        void Capture(Control control, string name)
        {
            using var bitmap = new RenderTargetBitmap(new PixelSize((int)control.Bounds.Width, (int)control.Bounds.Height), new Vector(96, 96));
            bitmap.Render(control);
            bitmap.Save(Path.Combine(output, name + ".png"));
        }
    }
}
