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
/// 用户与权限管理窗口：主体列表、权限查看，以及用户/角色的创建、删除、成员关系、
/// 授权/撤销与改密码。所有写操作先生成方言 SQL，二次确认后才执行。
/// </summary>
public partial class UserManagementWindow : Window
{
    private readonly IDbUserService _userService = null!;
    private readonly IDbConnectionService _connectionService = null!;

    private enum FormMode { None, CreateUser, CreateRole, Grant, Revoke, GrantRole, RevokeRole, ChangePassword }

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

    private DatabaseType? SelectedDatabaseType => SelectedConnection is { } c
        && Enum.TryParse<DatabaseType>(c.DatabaseType, true, out var type) ? type : null;

    /// <summary>
    /// 由对象树安全主体节点带入上下文：定位连接、刷新列表并选中对应主体。
    /// principalName 允许传入 MySQL 的 'user'@'host' 形式。
    /// </summary>
    public async Task SetContextAsync(ConnectionItem connection, string principalName, bool showGrants = false)
    {
        if (!_userService.IsSupported(connection.DatabaseType))
        {
            TxtGrants.Text = "该连接不支持用户与角色管理。";
            return;
        }

        CmbConnection.SelectedItem = CmbConnection.ItemsSource?
            .OfType<ConnectionItem>()
            .FirstOrDefault(c => string.Equals(c.Name, connection.Name, StringComparison.OrdinalIgnoreCase))
            ?? connection;

        await RefreshUsersAsync();

        var (name, host) = ParsePrincipalLabel(principalName);
        var match = UsersGrid.ItemsSource?
            .OfType<DbUserInfo>()
            .FirstOrDefault(u => string.Equals(u.Name, name, StringComparison.Ordinal)
                                 && (host is null || string.Equals(u.Host, host, StringComparison.Ordinal)));

        if (match is not null)
        {
            UsersGrid.SelectedItem = match;
            if (UsersGrid.Columns.Count > 0)
                UsersGrid.ScrollIntoView(match, UsersGrid.Columns[0]);
            if (showGrants)
                await ShowGrantsForAsync(match);
        }
        else
        {
            TxtGrants.Text = $"列表中未找到主体「{principalName}」，可点击刷新后重试。";
        }
    }

    /// <summary>仅选中连接（供对象树「新建用户/角色」入口在创建前定位上下文）。</summary>
    public void SetConnection(ConnectionItem connection)
    {
        CmbConnection.SelectedItem = CmbConnection.ItemsSource?
            .OfType<ConnectionItem>()
            .FirstOrDefault(c => string.Equals(c.Name, connection.Name, StringComparison.OrdinalIgnoreCase))
            ?? connection;
    }

    public void PrepareCreateUser() => EnterForm(FormMode.CreateUser, "新建用户");

    public void PrepareCreateRole() => EnterForm(FormMode.CreateRole, "新建角色");

    public Task RequestDeleteAsync() => DeleteSelectedPrincipalAsync();

    private static (string Name, string? Host) ParsePrincipalLabel(string label)
    {
        // DbPrincipalCatalog 对 MySQL 生成 'user'@'host'，内部引号已翻倍。
        if (label.Length > 3 && label.StartsWith('\'') && label.EndsWith('\'') && label.Contains("'@'", StringComparison.Ordinal))
        {
            int split = label.IndexOf("'@'", StringComparison.Ordinal);
            return (label[1..split].Replace("''", "'", StringComparison.Ordinal),
                label[(split + 3)..^1].Replace("''", "'", StringComparison.Ordinal));
        }
        return (label, null);
    }

    private async void BtnRefresh_Click(object? sender, RoutedEventArgs e) => await RefreshUsersAsync();

    private async Task RefreshUsersAsync()
    {
        if (SelectedConnection is not ConnectionItem connection)
        {
            TxtGrants.Text = "请先选择连接。";
            return;
        }

        BtnRefresh.IsEnabled = false;
        TxtGrants.Text = "正在读取主体列表...";

        try
        {
            var (users, error) = await _userService.GetUsersAsync(connection);
            UsersGrid.ItemsSource = users;

            TxtGrants.Text = error is not null
                ? $"读取失败：{error}\n（部分数据库需要管理员权限才能读取主体列表）"
                : $"共 {users.Count} 个主体（用户 {users.Count(u => u.Kind != "role")}，角色 {users.Count(u => u.Kind == "role")}）。";
        }
        finally
        {
            BtnRefresh.IsEnabled = true;
        }
    }

    private async void BtnShowGrants_Click(object? sender, RoutedEventArgs e)
    {
        if (SelectedUser is not DbUserInfo user)
        {
            TxtGrants.Text = "请先选择主体。";
            return;
        }

        await ShowGrantsForAsync(user);
    }

    private async Task ShowGrantsForAsync(DbUserInfo user)
    {
        if (SelectedConnection is not ConnectionItem connection)
        {
            TxtGrants.Text = "请先选择连接。";
            return;
        }

        TxtGrants.Text = "正在读取权限...";
        var (text, error) = await _userService.GetGrantsAsync(connection, user.Name, NullIfEmpty(user.Host));

        TxtGrants.Text = error is not null
            ? $"读取权限失败：{error}"
            : $"── {user.Name}{(string.IsNullOrEmpty(user.Host) ? string.Empty : $"@{user.Host}")} 的权限 ──{Environment.NewLine}{text}";
    }

    private void BtnCreateUser_Click(object? sender, RoutedEventArgs e)
        => EnterForm(FormMode.CreateUser, "新建用户");

    private void BtnCreateRole_Click(object? sender, RoutedEventArgs e)
        => EnterForm(FormMode.CreateRole, "新建角色");

    private void BtnGrant_Click(object? sender, RoutedEventArgs e)
        => EnterForm(FormMode.Grant, $"授权给 {SelectedUser?.Name ?? "选中主体"}");

    private void BtnRevoke_Click(object? sender, RoutedEventArgs e)
        => EnterForm(FormMode.Revoke, $"从 {SelectedUser?.Name ?? "选中主体"} 撤销权限");

    private void BtnGrantRole_Click(object? sender, RoutedEventArgs e)
        => EnterForm(FormMode.GrantRole, "授予角色");

    private void BtnRevokeRole_Click(object? sender, RoutedEventArgs e)
        => EnterForm(FormMode.RevokeRole, "撤销角色");

    private void BtnChangePassword_Click(object? sender, RoutedEventArgs e)
        => EnterForm(FormMode.ChangePassword, $"修改 {SelectedUser?.Name ?? "选中主体"} 的密码");

    private void EnterForm(FormMode mode, string title)
    {
        if (SelectedConnection is null)
        {
            TxtGrants.Text = "请先选择连接。";
            return;
        }
        if (mode is FormMode.Grant or FormMode.Revoke or FormMode.ChangePassword && SelectedUser is null)
        {
            TxtGrants.Text = "请先选择主体。";
            return;
        }

        _mode = mode;
        FormTitle.Text = title;
        bool isMySql = SelectedDatabaseType == DatabaseType.MySql;
        bool isPgLike = SelectedDatabaseType is DatabaseType.Postgres or DatabaseType.KingbaseES;

        PanelPassword.IsVisible = mode is FormMode.CreateUser or FormMode.CreateRole or FormMode.ChangePassword;
        PanelHost.IsVisible = isMySql && (mode is FormMode.CreateUser or FormMode.CreateRole or FormMode.ChangePassword);
        PanelPrivilege.IsVisible = mode is FormMode.Grant or FormMode.Revoke;
        PanelMember.IsVisible = mode is FormMode.GrantRole or FormMode.RevokeRole;
        ChkCanLogin.IsVisible = mode == FormMode.CreateRole && isPgLike;

        LblFormName.Text = mode is FormMode.GrantRole or FormMode.RevokeRole ? "角色名:" : "名称:";
        TxtFormUser.Text = mode is FormMode.GrantRole or FormMode.RevokeRole
            ? string.Empty
            : SelectedUser?.Name ?? string.Empty;
        TxtFormPassword.Text = string.Empty;
        TxtFormHost.Text = "%";
        TxtFormMember.Text = SelectedUser?.Name ?? string.Empty;
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

        switch (_mode)
        {
            case FormMode.CreateUser:
            {
                string name = (TxtFormUser.Text ?? string.Empty).Trim();
                if (name.Length == 0)
                {
                    await ShowErrorAsync("请填写用户名。");
                    return;
                }
                sql = _userService.BuildCreateUserSql(
                    connection.DatabaseType, name, TxtFormPassword.Text ?? string.Empty, NullIfEmpty(TxtFormHost.Text));
                confirmTitle = "创建用户";
                break;
            }
            case FormMode.CreateRole:
            {
                string name = (TxtFormUser.Text ?? string.Empty).Trim();
                if (name.Length == 0)
                {
                    await ShowErrorAsync("请填写角色名。");
                    return;
                }
                sql = _userService.BuildCreateRoleSql(
                    connection.DatabaseType, name, TxtFormPassword.Text ?? string.Empty,
                    ChkCanLogin.IsChecked == true, NullIfEmpty(TxtFormHost.Text));
                confirmTitle = "创建角色";
                break;
            }
            case FormMode.Grant:
            case FormMode.Revoke:
            {
                if (SelectedUser is null)
                {
                    await ShowErrorAsync("请先选择主体。");
                    return;
                }
                var privilege = (ComboPrivilege.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "SELECT";
                string onObject = (TxtFormObject.Text ?? string.Empty).Trim();
                sql = _mode == FormMode.Grant
                    ? _userService.BuildGrantSql(connection.DatabaseType, SelectedUser.Name, NullIfEmpty(SelectedUser.Host), privilege, onObject)
                    : _userService.BuildRevokeSql(connection.DatabaseType, SelectedUser.Name, NullIfEmpty(SelectedUser.Host), privilege, onObject);
                confirmTitle = _mode == FormMode.Grant ? "授权" : "撤销授权";
                break;
            }
            case FormMode.GrantRole:
            case FormMode.RevokeRole:
            {
                string role = (TxtFormUser.Text ?? string.Empty).Trim();
                string member = (TxtFormMember.Text ?? string.Empty).Trim();
                if (role.Length == 0 || member.Length == 0)
                {
                    await ShowErrorAsync("请填写角色名与成员名。");
                    return;
                }
                string? memberHost = SelectedDatabaseType == DatabaseType.MySql ? "%" : null;
                sql = _mode == FormMode.GrantRole
                    ? _userService.BuildGrantRoleSql(connection.DatabaseType, role, member, memberHost)
                    : _userService.BuildRevokeRoleSql(connection.DatabaseType, role, member, memberHost);
                confirmTitle = _mode == FormMode.GrantRole ? "授予角色" : "撤销角色";
                break;
            }
            case FormMode.ChangePassword:
            {
                if (SelectedUser is null)
                {
                    await ShowErrorAsync("请先选择主体。");
                    return;
                }
                sql = _userService.BuildAlterPasswordSql(
                    connection.DatabaseType, SelectedUser.Name, NullIfEmpty(SelectedUser.Host), TxtFormPassword.Text ?? string.Empty);
                confirmTitle = "修改密码";
                break;
            }
            default:
                return;
        }

        if (string.IsNullOrWhiteSpace(sql))
        {
            await ShowErrorAsync("当前数据库类型不支持该操作，或对象格式无效。MySQL 使用 db.table 或 *.*；PostgreSQL/SQL Server 使用 schema.table 或 schema.*；Oracle 使用 schema.table。");
            return;
        }

        var confirm = await DialogHelper.ShowConfirmAsync(confirmTitle, $"将执行以下 SQL：{Environment.NewLine}{Environment.NewLine}{sql}{Environment.NewLine}{Environment.NewLine}确定执行吗？");
        if (confirm != true)
            return;

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

    private async void BtnDropUser_Click(object? sender, RoutedEventArgs e) => await DeleteSelectedPrincipalAsync();

    private async Task DeleteSelectedPrincipalAsync()
    {
        if (SelectedConnection is not ConnectionItem connection)
        {
            TxtGrants.Text = "请先选择连接。";
            return;
        }

        if (SelectedUser is not DbUserInfo user)
        {
            TxtGrants.Text = "请先选择主体。";
            return;
        }

        bool isRole = string.Equals(user.Kind, "role", StringComparison.OrdinalIgnoreCase);
        bool cascade = false;
        if (!isRole && SelectedDatabaseType == DatabaseType.Oracle)
        {
            var answer = await MessageBoxManager
                .GetMessageBoxStandard("删除用户", $"是否级联删除（CASCADE，同时删除其对象）？\n「是」= CASCADE，「否」= 仅删除用户。", ButtonEnum.YesNo, MsBox.Avalonia.Enums.Icon.Warning)
                .ShowWindowDialogAsync(this);
            cascade = answer == ButtonResult.Yes;
        }

        string sql = isRole
            ? _userService.BuildDropRoleSql(connection.DatabaseType, user.Name, NullIfEmpty(user.Host))
            : _userService.BuildDropUserSql(connection.DatabaseType, user.Name, NullIfEmpty(user.Host), cascade);

        var confirm = await DialogHelper.ShowConfirmAsync(
            isRole ? "删除角色" : "删除用户",
            $"{sql}{Environment.NewLine}{Environment.NewLine}该操作不可撤销，确定执行吗？");

        if (confirm != true)
            return;

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
                DatabaseType.Postgres or DatabaseType.KingbaseES => "public.*",
                DatabaseType.SqlServer => "dbo.*",
                DatabaseType.Oracle => "SCHEMA.TABLE",
                _ => "*.*",
            }
            : "*.*";

    private Task ShowErrorAsync(string message)
        => MessageBoxManager.GetMessageBoxStandard("错误", message, ButtonEnum.Ok, MsBox.Avalonia.Enums.Icon.Error)
            .ShowWindowDialogAsync(this);
}
