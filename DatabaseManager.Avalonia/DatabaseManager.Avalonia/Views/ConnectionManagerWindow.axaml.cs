using Avalonia.Controls;
using Avalonia.Interactivity;
using DatabaseManager.AppCore.Models;
using DatabaseManager.AppCore.ViewModels;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;

namespace DatabaseManager.Avalonia.Views;

/// <summary>
/// 连接管理窗口（对应原 WinForms <c>frmDbConnectionManage</c>）。
/// 按数据库类型展示连接列表，支持新增 / 编辑 / 删除 / 刷新。
/// </summary>
public partial class ConnectionManagerWindow : Window
{
    private readonly ConnectionManagerViewModel _vm;

    public ConnectionManagerWindow(ConnectionManagerViewModel vm)
    {
        InitializeComponent();

        _vm = vm;
        DataContext = _vm;

        ComboDatabaseType.ItemsSource = _vm.DatabaseTypes;
        ComboDatabaseType.SelectedItem = _vm.SelectedDatabaseType;

        _vm.Refresh();
    }

    private void ComboDatabaseType_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        _vm.SelectedDatabaseType = ComboDatabaseType.SelectedItem as string ?? string.Empty;
        _vm.Refresh();
    }

    private void BtnRefresh_Click(object? sender, RoutedEventArgs e)
    {
        _vm.SelectedDatabaseType = ComboDatabaseType.SelectedItem as string ?? string.Empty;
        _vm.Refresh();
    }

    private async void BtnAdd_Click(object? sender, RoutedEventArgs e)
    {
        var item = _vm.CreateNew();
        item.DatabaseType = ComboDatabaseType.SelectedItem as string ?? _vm.SelectedDatabaseType;

        var dialog = new ConnectWindow(_vm, item) { DataContext = _vm };
        var result = await dialog.ShowDialog<object?>(this);
        if (dialog.Result is not null)
        {
            _vm.Refresh();
        }
    }

    private async void BtnEdit_Click(object? sender, RoutedEventArgs e)
    {
        var selected = _vm.SelectedConnection;
        if (selected is null)
        {
            await ShowInfoAsync("请先选择一条连接。");
            return;
        }

        var item = new ConnectionItem
        {
            Id = selected.Id,
            AccountId = selected.AccountId,
            DatabaseType = selected.DatabaseType,
            Name = selected.Name,
            Server = selected.Server,
            Port = selected.Port,
            ServerVersion = selected.ServerVersion,
            Database = selected.Database,
            IntegratedSecurity = selected.IntegratedSecurity,
            UserId = selected.UserId,
            Password = selected.Password,
            IsDba = selected.IsDba,
            UseSsl = selected.UseSsl,
            RememberPassword = selected.RememberPassword,
            Priority = selected.Priority,
            Group = selected.Group,
            ColorTag = selected.ColorTag,
            KingbaseCompatibilityMode = selected.KingbaseCompatibilityMode,
        };

        var dialog = new ConnectWindow(_vm, item) { DataContext = _vm };
        var result = await dialog.ShowDialog<object?>(this);
        if (dialog.Result is not null)
        {
            _vm.Refresh();
        }
    }

    private async void BtnDelete_Click(object? sender, RoutedEventArgs e)
    {
        var selected = _vm.SelectedConnection;
        if (selected is null)
        {
            await ShowInfoAsync("请先选择一条连接。");
            return;
        }

        var confirm = await MessageBoxManager.GetMessageBoxStandard(
            "确认删除",
            $"确定删除连接“{selected.Name}”吗？",
            ButtonEnum.YesNo,
            MsBox.Avalonia.Enums.Icon.Question).ShowWindowDialogAsync(this);

        if (confirm == ButtonResult.Yes)
        {
            await _vm.DeleteAsync(new[] { selected });
        }
    }

    private void BtnClose_Click(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    private async Task ShowInfoAsync(string message)
        => await MessageBoxManager.GetMessageBoxStandard("提示", message, ButtonEnum.Ok, MsBox.Avalonia.Enums.Icon.Info)
            .ShowWindowDialogAsync(this);
}
