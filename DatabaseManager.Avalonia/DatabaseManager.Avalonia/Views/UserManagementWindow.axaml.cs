using Avalonia.Controls;
using Avalonia.Interactivity;
using DatabaseInterpreter.Model;
using DatabaseManager.AppCore.Common;
using DatabaseManager.AppCore.Models;
using DatabaseManager.AppCore.Services;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;

namespace DatabaseManager.Avalonia.Views;

/// <summary>
/// 用户与权限管理窗口：用户列表、权限查看、模板化创建/授权/删除（执行前确认）。
/// </summary>
public partial class UserManagementWindow : Window
{
    private readonly IDbUserService _userService;
    private readonly IDbConnectionService _connectionService;

    private enum FormMode { None, CreateUser, Grant }

    private FormMode _mode = FormMode.None;

    public UserManagementWindow()
    {
        InitializeComponent();
    }

    public UserManagementWindow(IDbUserService userService, IDbConnectionService connectionService)
        : this()
    {
        _userService = userService;
        _connectionService = connectionService;

        CmbConnection.ItemsSource = _connectionService.GetConnections()
            .Where(c => _userService.IsSupported(c.DatabaseType))
            .ToList();

        if (CmbConnection.ItemCount == 0)
        {
            TxtGrants.Text = "没有支持用户管理的连接（SQLite 不支持）。";
            BtnRefresh.IsEnabled = false;
        }
    }

    private ConnectionItem? SelectedConnection => CmbConnection.SelectedItem as ConnectionItem;

    private DbUserInfo? SelectedUser => UsersGrid.SelectedItem as DbUserInfo;

    private async void BtnRefresh_Click(object? sender, RoutedEventArgs e) => await RefreshUsersAsync();

    private async Task RefreshUsersAsync()
    {
        if (SelectedConnection is not ConnectionItem connection)
        {
            TxtGrants.Text = "请先选择连接。";
            return;
        }

        BtnRefresh.IsEnabled = false;
        TxtGrants.Text = "正在读取用户列表...";

        try
        {
            var (users, error) = await _userService.GetUsersAsync(connection);
            UsersGrid.ItemsSource = users;

            TxtGrants.Text = error is not null
                ? $"读取失败：{error}\n（部分数据库需要管理员权限才能读取用户列表）"
                : $"共 {users.Count} 个用户。";
        }
        finally
        {
            BtnRefresh.IsEnabled = true;
        }
    }

    private async void BtnShowGrants_Click(object? sender, RoutedEventArgs e)
    {
        if (SelectedConnection is not ConnectionItem connection)
        {
            TxtGrants.Text = "请先选择连接。";
            return;
        }

        if (SelectedUser is not DbUserInfo user)
        {
            TxtGrants.Text = "请先选择用户。";
            return;
        }

        TxtGrants.Text = "正在读取权限...";
        var (text, error) = await _userService.GetGrantsAsync(connection, user.Name, NullIfEmpty(user.Host));

        TxtGrants.Text = error is not null
            ? $"读取权限失败：{error}"
            : $"── {user.Name}{(string.IsNullOrEmpty(user.Host) ? string.Empty : $"@{user.Host}")} 的权限 ──{Environment.NewLine}{text}";
    }

    private void BtnCreateUser_Click(object? sender, RoutedEventArgs e)
    {
        if (SelectedConnection is null)
        {
            TxtGrants.Text = "请先选择连接。";
            return;
        }

        _mode = FormMode.CreateUser;
        FormTitle.Text = "新建用户";
        PanelPassword.IsVisible = true;
        PanelHost.IsVisible = CmbConnection.SelectedItem is ConnectionItem c
                              && Enum.TryParse<DatabaseType>(c.DatabaseType, true, out var t)
                              && t == DatabaseType.MySql;
        PanelPrivilege.IsVisible = false;
        TxtFormUser.Text = string.Empty;
        TxtFormPassword.Text = string.Empty;
        FormPanel.IsVisible = true;
    }

    private void BtnGrant_Click(object? sender, RoutedEventArgs e)
    {
        if (SelectedConnection is null || SelectedUser is null)
        {
            TxtGrants.Text = "请先选择连接与用户。";
            return;
        }

        _mode = FormMode.Grant;
        FormTitle.Text = $"授权给 {SelectedUser.Name}";
        PanelPassword.IsVisible = false;
        PanelHost.IsVisible = false;
        PanelPrivilege.IsVisible = true;
        ComboPrivilege.SelectedIndex = 0;
        TxtFormObject.Text = GetGrantObjectPlaceholder(SelectedConnection.DatabaseType);
        FormPanel.IsVisible = true;
    }

    private async void BtnFormConfirm_Click(object? sender, RoutedEventArgs e)
    {
        if (SelectedConnection is not ConnectionItem connection)
            return;

        string sql;
        string confirmTitle;

        if (_mode == FormMode.CreateUser)
        {
            string userName = TxtFormUser.Text?.Trim() ?? string.Empty;
            if (userName.Length == 0)
            {
                await ShowErrorAsync("请填写用户名。");
                return;
            }

            sql = _userService.BuildCreateUserSql(
                connection.DatabaseType, userName, TxtFormPassword.Text ?? string.Empty, NullIfEmpty(TxtFormHost.Text));
            confirmTitle = "创建用户";
        }
        else if (_mode == FormMode.Grant && SelectedUser is not null)
        {
            var privilege = (ComboPrivilege.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "SELECT";
            sql = _userService.BuildGrantSql(
                connection.DatabaseType, SelectedUser.Name, NullIfEmpty(SelectedUser.Host),
                privilege, TxtFormObject.Text?.Trim() ?? "*.*");
            confirmTitle = "授权";
        }
        else
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(sql))
        {
            await ShowErrorAsync("授权对象格式无效。MySQL 使用 db.table 或 *.*；PostgreSQL/SQL Server 使用 schema.table 或 schema.*；Oracle 使用 schema.table。");
            return;
        }

        var confirm = await DialogHelper.ShowConfirmAsync(confirmTitle, $"将执行以下 SQL：{Environment.NewLine}{Environment.NewLine}{sql}{Environment.NewLine}{Environment.NewLine}确定执行吗？");
        if (confirm != true)
        {
            return;
        }

        var (success, error) = await _userService.ExecuteAsync(connection, sql);
        if (success)
        {
            TxtGrants.Text = $"执行成功：{sql}";
            FormPanel.IsVisible = false;
            _mode = FormMode.None;
            await RefreshUsersAsync();
        }
        else
        {
            await ShowErrorAsync($"执行失败：{error}");
        }
    }

    private void BtnFormCancel_Click(object? sender, RoutedEventArgs e)
    {
        FormPanel.IsVisible = false;
        _mode = FormMode.None;
    }

    private async void BtnDropUser_Click(object? sender, RoutedEventArgs e)
    {
        if (SelectedConnection is not ConnectionItem connection)
        {
            TxtGrants.Text = "请先选择连接。";
            return;
        }

        if (SelectedUser is not DbUserInfo user)
        {
            TxtGrants.Text = "请先选择用户。";
            return;
        }

        bool cascade = false;
        if (Enum.TryParse<DatabaseType>(connection.DatabaseType, true, out var dbType) && dbType == DatabaseType.Oracle)
        {
            var answer = await MessageBoxManager
                .GetMessageBoxStandard("删除用户", $"是否级联删除（CASCADE，同时删除其对象）？\n「是」= CASCADE，「否」= 仅删除用户。", ButtonEnum.YesNo, MsBox.Avalonia.Enums.Icon.Warning)
                .ShowWindowDialogAsync(this);
            cascade = answer == ButtonResult.Yes;
        }

        string sql = _userService.BuildDropUserSql(connection.DatabaseType, user.Name, NullIfEmpty(user.Host), cascade);

        var confirm = await DialogHelper.ShowConfirmAsync(
            "删除用户",
            $"将执行以下 SQL：{Environment.NewLine}{Environment.NewLine}{sql}{Environment.NewLine}{Environment.NewLine}该操作不可撤销，确定执行吗？");

        if (confirm != true)
        {
            return;
        }

        var (success, error) = await _userService.ExecuteAsync(connection, sql);
        if (success)
        {
            TxtGrants.Text = $"执行成功：{sql}";
            await RefreshUsersAsync();
        }
        else
        {
            await ShowErrorAsync($"删除失败：{error}");
        }
    }

    private void BtnClose_Click(object? sender, RoutedEventArgs e) => Close();

    private static string? NullIfEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string GetGrantObjectPlaceholder(string databaseType)
        => Enum.TryParse<DatabaseType>(databaseType, true, out var dbType)
            ? dbType switch
            {
                DatabaseType.Postgres => "public.*",
                DatabaseType.SqlServer => "dbo.*",
                DatabaseType.Oracle => "SCHEMA.TABLE",
                _ => "*.*",
            }
            : "*.*";

    private Task ShowErrorAsync(string message)
        => MessageBoxManager.GetMessageBoxStandard("错误", message, ButtonEnum.Ok, MsBox.Avalonia.Enums.Icon.Error)
            .ShowWindowDialogAsync(this);
}
