using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Interactivity;
using DatabaseManager.AppCore.Models;
using DatabaseManager.AppCore.ViewModels;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;

namespace DatabaseManager.Avalonia.Views;

/// <summary>
/// 数据库备份窗口（阶段 5）。对应原 WinForms <c>frmBackupSetting</c> / <c>frmBackupSettingRedefine</c>。
/// 选择连接与备份配置，执行数据库备份。
/// </summary>
public partial class BackupWindow : Window
{
    private readonly BackupViewModel? _vm;
    private readonly bool _restoreMode;

    public BackupWindow()
    {
        InitializeComponent();
    }

    public BackupWindow(BackupViewModel vm, ConnectionItem? targetConnection = null, bool restoreMode = false) : this()
    {
        DataContext = vm;
        _vm = vm;
        _restoreMode = restoreMode;
        if (targetConnection is not null) vm.SetTargetConnection(targetConnection);
        if (restoreMode)
        {
            TitleText.Text = Title = "数据库恢复";
            (BackupButton.ButtonType, RestoreButton.ButtonType) = (RestoreButton.ButtonType, BackupButton.ButtonType);
        }
    }

    protected override async void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        if (_vm is null) return;
        ComboConnection.SelectionChanged += ComboConnection_SelectionChanged;
        if (await RefreshAsync() && _restoreMode)
        {
            _vm.StatusMessage = $"恢复目标：{_vm.SelectedConnection?.Description}。请选择备份文件并确认目标。";
            RestoreFileBox.Focus();
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        _vm?.CancelConnectionRefresh();
        base.OnClosed(e);
        if (_vm is null) return;
        ComboConnection.SelectionChanged -= ComboConnection_SelectionChanged;
    }

    private async Task<bool> RefreshAsync()
    {
        if (_vm is null || !await _vm.RefreshConnectionsAsync() || !IsVisible)
            return false;

        LoadConnections();
        return true;
    }

    private void LoadConnections()
    {
        if (_vm is null) return;
        ComboConnection.ItemsSource = _vm.Connections;
        ComboConnection.ItemTemplate = new FuncDataTemplate<ConnectionItem>((item, _) =>
            new TextBlock { Text = item?.Description ?? string.Empty });

        ComboConnection.SelectedItem = _vm.SelectedConnection;
    }

    private void ComboConnection_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_vm is null) return;
        _vm.SelectedConnection = ComboConnection.SelectedItem as ConnectionItem;
    }

    private async void BtnBrowseFolder_Click(object? sender, RoutedEventArgs e)
    {
        if (_vm is null) return;
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
        if (_vm is null) return;
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

    private async void BtnBrowseRestoreFile_Click(object? sender, RoutedEventArgs e)
    {
        // DuckDB 的备份产物是 EXPORT DATABASE 目录，需要选择文件夹而非文件。
        if (_vm?.SelectedConnection is { } connection &&
            string.Equals(connection.DatabaseType, "DuckDB", StringComparison.OrdinalIgnoreCase))
        {
            var folders = await StorageProvider.OpenFolderPickerAsync(new global::Avalonia.Platform.Storage.FolderPickerOpenOptions
            {
                Title = "选择 DuckDB 备份目录（EXPORT DATABASE 产物）",
                AllowMultiple = false,
            });
            if (folders.Count > 0)
                _vm.RestoreFilePath = folders[0].Path?.LocalPath ?? string.Empty;
            return;
        }

        var files = await StorageProvider.OpenFilePickerAsync(new global::Avalonia.Platform.Storage.FilePickerOpenOptions
        {
            Title = "选择备份文件",
            AllowMultiple = false,
            FileTypeFilter = new[] { new global::Avalonia.Platform.Storage.FilePickerFileType("备份文件") { Patterns = new[] { "*.bak", "*.sql", "*.dump", "*.backup", "*.dmp", "*.db", "*.sqlite", "*" } } },
        });
        if (files.Count > 0)
            _vm!.RestoreFilePath = files[0].Path?.LocalPath ?? string.Empty;
    }

    private async void BtnRestore_Click(object? sender, RoutedEventArgs e)
    {
        if (_vm?.SelectedConnection is null || string.IsNullOrWhiteSpace(_vm.RestoreFilePath))
            return;

        var box = MessageBoxManager.GetMessageBoxStandard(
            "确认恢复数据库",
            $"恢复会覆盖目标数据库「{_vm.SelectedConnection.Database}」，此操作不可撤销。\n\n请确认备份文件来源可靠，并已停止相关业务写入。是否继续？",
            ButtonEnum.YesNo,
            MsBox.Avalonia.Enums.Icon.Warning);
        if (await box.ShowWindowDialogAsync(this) == ButtonResult.Yes)
            await _vm.RestoreCommand.ExecuteAsync(null);
    }

    private async void BtnRefresh_Click(object? sender, RoutedEventArgs e)
    {
        await RefreshAsync();
    }

    private void BtnClose_Click(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
