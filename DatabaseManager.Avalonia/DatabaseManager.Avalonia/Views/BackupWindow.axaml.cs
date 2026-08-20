using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Interactivity;
using DatabaseManager.AppCore.Models;
using DatabaseManager.AppCore.ViewModels;

namespace DatabaseManager.Avalonia.Views;

/// <summary>
/// 数据库备份窗口（阶段 5）。对应原 WinForms <c>frmBackupSetting</c> / <c>frmBackupSettingRedefine</c>。
/// 选择连接与备份配置，执行数据库备份。
/// </summary>
public partial class BackupWindow : Window
{
    private readonly BackupViewModel _vm;

    public BackupWindow(BackupViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
        _vm = vm;
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        ComboConnection.SelectionChanged += ComboConnection_SelectionChanged;
        Refresh();
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        ComboConnection.SelectionChanged -= ComboConnection_SelectionChanged;
    }

    private void Refresh()
    {
        _vm.RefreshConnections();
        LoadConnections();
    }

    private void LoadConnections()
    {
        ComboConnection.ItemsSource = _vm.Connections;
        ComboConnection.ItemTemplate = new FuncDataTemplate<ConnectionItem>((item, _) =>
            new TextBlock { Text = item.Description });

        ComboConnection.SelectedItem = _vm.SelectedConnection;
    }

    private void ComboConnection_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        => _vm.SelectedConnection = ComboConnection.SelectedItem as ConnectionItem;

    private async void BtnBrowseFolder_Click(object? sender, RoutedEventArgs e)
    {
        var dialog = await StorageProvider.OpenFolderPickerAsync(new global::Avalonia.Platform.Storage.FolderPickerOpenOptions
        {
            Title = "选择备份保存文件夹",
            AllowMultiple = false,
        });

        if (dialog.Count > 0)
        {
            _vm.SaveFolder = dialog[0].Path?.LocalPath ?? string.Empty;
        }
    }

    private async void BtnBrowseTool_Click(object? sender, RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new global::Avalonia.Platform.Storage.FilePickerOpenOptions
        {
            Title = "选择数据库客户端备份工具",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new global::Avalonia.Platform.Storage.FilePickerFileType("可执行程序") { Patterns = new[] { "*.exe" } },
                new global::Avalonia.Platform.Storage.FilePickerFileType("所有文件") { Patterns = new[] { "*" } },
            },
        });

        if (files.Count > 0)
        {
            _vm.ClientToolFilePath = files[0].Path?.LocalPath ?? string.Empty;
        }
    }

    private void BtnRefresh_Click(object? sender, RoutedEventArgs e)
    {
        Refresh();
    }

    private void BtnClose_Click(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
