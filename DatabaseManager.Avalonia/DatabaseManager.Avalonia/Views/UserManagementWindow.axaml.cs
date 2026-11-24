using Avalonia.Controls;
using Avalonia.Interactivity;
using DatabaseInterpreter.Model;
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

    private FormMode _mode;
    private DbUserInfo? _formUser;
    private string? _databaseName;
    private DbPrincipalKind _principalKind;
    private int _contextVersion;
    private int _refreshVersion;

    public UserManagementWindow()
    {
        InitializeComponent();
    }

    public UserManagementWindow(IDbUserService userService, IDbConnectionService connectionService)
        : this()
    {
        _userService = userService;
        _connectionService = connectionService;
        CmbConnection.SelectionChanged += CmbConnection_SelectionChanged;
        Closed += (_, _) => _contextVersion++;
        BtnRefresh.IsEnabled = false;
    }

    private ConnectionItem? SelectedConnection => CmbConnection.SelectedItem as ConnectionItem;

    private DbUserInfo? SelectedUser => UsersGrid.SelectedItem as DbUserInfo;

    private DatabaseType? SelectedDatabaseType => SelectedConnection is { } c
        && Enum.TryParse<DatabaseType>(c.DatabaseType, true, out var type) ? type : null;

    internal async Task<bool> LoadConnectionsAsync(ConnectionItem? connection = null)
    {
        CmbConnection.SelectedItem = null;
        ResetContext();
        int context = _contextVersion;
        // SQLite 的异步 API 仍可能同步读取本地配置库。
        var connections = await Task.Run(() => _connectionService.GetConnectionsAsync());
        if (context != _contextVersion)
            return false;

        var supported = connections.Where(c => _userService.IsSupported(c.DatabaseType)).ToList();
        CmbConnection.ItemsSource = supported;
        if (connection is null)
        {
            if (supported.Count == 0)
                TxtGrants.Text = "没有支持用户管理的连接（SQLite 不支持）。";
            return true;
        }

        CmbConnection.SelectedItem = supported
            .FirstOrDefault(c => string.Equals(c.Name, connection.Name, StringComparison.OrdinalIgnoreCase));
        if (SelectedConnection is not null)
            return true;

        TxtGrants.Text = "找不到支持用户与权限管理的连接。";
        return false;
    }

    public async Task OpenPrincipalAsync(ConnectionItem connection, DbObjectTreeNode node, PrincipalManagementAction action)
    {
        if (!await LoadConnectionsAsync(connection))
            return;

        _databaseName = node.DatabaseName;
        _principalKind = node.PrincipalKind;
        UpdateScopeLabel();
        if (action is PrincipalManagementAction.NewUser or PrincipalManagementAction.NewRole)
        {
            if (action == PrincipalManagementAction.NewUser)
                EnterForm(FormMode.CreateUser, CreateUserTitle);
            else
                EnterForm(FormMode.CreateRole, "新建角色");
            return;
        }

        int context = _contextVersion;
        if (!await RefreshUsersAsync() || context != _contextVersion || node.NodeType != DbObjectTreeNodeType.Principal)
            return;

        var match = UsersGrid.ItemsSource?.OfType<DbUserInfo>().FirstOrDefault(u => u.MatchesPrincipal(node));
        if (match is null)
        {
            TxtGrants.Text = $"列表中未找到主体「{node.Text}」，请刷新对象树。";
            return;
        }

        UsersGrid.SelectedItem = match;
        UsersGrid.ScrollIntoView(match, UsersGrid.Columns[0]);
        if (action == PrincipalManagementAction.ShowGrants)
            await ShowGrantsForAsync(match);
        else if (action == PrincipalManagementAction.Delete)
            await DeletePrincipalAsync(match);
    }

    private void CmbConnection_SelectionChanged(object? sender, SelectionChangedEventArgs e) => ResetContext();

    private void ResetContext()
    {
        _contextVersion++;
        _databaseName = null;
        _principalKind = DbPrincipalKind.None;
        UsersGrid.ItemsSource = null;
        TxtGrants.Text = string.Empty;
        BtnRefresh.IsEnabled = SelectedConnection is not null;
        CloseForm();
        UpdateScopeLabel();
    }

    private string ScopeLabel => SelectedConnection is { } connection
        ? SelectedDatabaseType == DatabaseType.SqlServer
            ? $"{connection.Name} / {_databaseName ?? "服务器"}"
            : connection.Description
        : string.Empty;

    private void UpdateScopeLabel() => TxtScope.Text = ScopeLabel;

    private async void BtnRefresh_Click(object? sender, RoutedEventArgs e) => await RefreshUsersAsync();

    private async Task<bool> RefreshUsersAsync()
    {
        if (SelectedConnection is not ConnectionItem connection)
        {
            TxtGrants.Text = "请先选择连接。";
            return false;
        }

        int context = _contextVersion;
        int refresh = ++_refreshVersion;
        UsersGrid.ItemsSource = null;
        BtnRefresh.IsEnabled = false;
        TxtGrants.Text = "正在读取主体列表...";

        try
        {
            string? databaseName = _databaseName;
            var principalKind = _principalKind;
            // 数据库驱动可能在异步调用返回之前执行同步 I/O。
            var (users, error) = await Task.Run(() =>
                _userService.GetUsersAsync(connection, databaseName: databaseName, principalKind: principalKind));
            if (context != _contextVersion || refresh != _refreshVersion)
                return false;
            UsersGrid.ItemsSource = users;

            TxtGrants.Text = error is not null
                ? $"读取失败：{error}\n（部分数据库需要管理员权限才能读取主体列表）"
                : $"共 {users.Count} 个主体。";
            return error is null;
        }
        finally
        {
            if (context == _contextVersion && refresh == _refreshVersion)
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

        int context = _contextVersion;
        string? databaseName = _databaseName;
        TxtGrants.Text = "正在读取权限...";
        var (text, error) = await Task.Run(() => _userService.GetGrantsAsync(connection, user.Name, user.Host, databaseName: databaseName));
        if (context != _contextVersion || SelectedUser != user)
            return;

        TxtGrants.Text = error is not null
            ? $"读取权限失败：{error}"
            : $"── {user.Name}{(string.IsNullOrEmpty(user.Host) ? string.Empty : $"@{user.Host}")} 的权限 ──{Environment.NewLine}{text}";
    }

    private void BtnCreateUser_Click(object? sender, RoutedEventArgs e)
        => EnterForm(FormMode.CreateUser, CreateUserTitle);

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

    private string CreateUserTitle => SelectedDatabaseType != DatabaseType.SqlServer ? "新建用户"
        : _databaseName is null ? "新建登录名" : "为同名登录名新建数据库用户";

    private void EnterForm(FormMode mode, string title)
    {
        if (SelectedConnection is null)
        {
            TxtGrants.Text = "请先选择连接。";
            return;
        }
        if ((mode is FormMode.Grant or FormMode.Revoke or FormMode.ChangePassword) && SelectedUser is null)
        {
            TxtGrants.Text = "请先选择主体。";
            return;
        }

        bool isSqlServer = SelectedDatabaseType == DatabaseType.SqlServer;
        if (isSqlServer && _databaseName is null && (mode is FormMode.Grant or FormMode.Revoke))
        {
            TxtGrants.Text = "请从目标数据库的安全主体目录打开管理窗口，再修改对象权限。";
            return;
        }

        if (mode == FormMode.ChangePassword && (SelectedUser?.IsRole == true ||
            (isSqlServer && (_databaseName is not null || SelectedUser?.Attributes != "SQL_LOGIN"))))
        {
            TxtGrants.Text = "该主体不支持在此修改密码。SQL Server 请在服务器范围选择 SQL 登录名。";
            return;
        }

        _mode = mode;
        _formUser = SelectedUser;
        FormTitle.Text = title;
        bool isMySql = SelectedDatabaseType == DatabaseType.MySql;
        bool isPgLike = SelectedDatabaseType is DatabaseType.Postgres or DatabaseType.KingbaseES;
        bool membership = mode is FormMode.GrantRole or FormMode.RevokeRole;

        PanelPassword.IsVisible = mode == FormMode.ChangePassword
            || (mode == FormMode.CreateUser && !(isSqlServer && _databaseName is not null))
            || (mode == FormMode.CreateRole && isPgLike);
        PanelHost.IsVisible = isMySql && (membership || mode is FormMode.CreateUser or FormMode.CreateRole);
        PanelPrivilege.IsVisible = mode is FormMode.Grant or FormMode.Revoke;
        PanelMember.IsVisible = membership;
        PanelMemberHost.IsVisible = isMySql && membership;
        ChkCanLogin.IsVisible = mode == FormMode.CreateRole && isPgLike;
        ChkCanLogin.IsChecked = false;

        PanelFormName.IsVisible = membership || mode is FormMode.CreateUser or FormMode.CreateRole;
        LblFormName.Text = membership ? "角色名:" : "名称:";
        LblFormHost.Text = membership ? "角色主机:" : "主机:";
        TxtFormUser.Text = string.Empty;
        TxtFormPassword.Text = string.Empty;
        TxtFormHost.Text = membership ? string.Empty : "%";
        TxtFormMember.Text = membership ? _formUser?.Name ?? string.Empty : string.Empty;
        TxtFormMemberHost.Text = membership ? _formUser?.Host ?? string.Empty : string.Empty;
        ComboPrivilege.SelectedIndex = 0;
        TxtFormObject.Text = GetGrantObjectPlaceholder(SelectedConnection.DatabaseType);
        FormPanel.IsVisible = true;
    }

    private async void BtnFormConfirm_Click(object? sender, RoutedEventArgs e)
    {
        if (SelectedConnection is not ConnectionItem connection)
            return;

        int context = _contextVersion;
        string? databaseName = _databaseName;
        string scope = ScopeLabel;
        bool hasPassword = PanelPassword.IsVisible;
        string password = TxtFormPassword.Text ?? string.Empty;
        if (hasPassword && SelectedDatabaseType == DatabaseType.Oracle &&
            (password.Length == 0 || password.IndexOfAny(['"', '\r', '\n', '\0']) >= 0))
        {
            await ShowErrorAsync("Oracle 密码不能为空，也不能包含双引号或换行。");
            return;
        }

        string sql;
        string preview;
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
                string? host = NullIfEmpty(TxtFormHost.Text);
                sql = _userService.BuildCreateUserSql(connection.DatabaseType, name, password, host, databaseName is not null);
                preview = hasPassword ? _userService.BuildCreateUserSql(connection.DatabaseType, name, "********", host) : sql;
                confirmTitle = CreateUserTitle;
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
                string? host = NullIfEmpty(TxtFormHost.Text);
                bool canLogin = ChkCanLogin.IsChecked == true;
                sql = _userService.BuildCreateRoleSql(connection.DatabaseType, name, password, canLogin, host, databaseName is not null);
                preview = hasPassword && password.Length > 0
                    ? _userService.BuildCreateRoleSql(connection.DatabaseType, name, "********", canLogin, host, databaseName is not null)
                    : sql;
                confirmTitle = "创建角色";
                break;
            }
            case FormMode.Grant:
            case FormMode.Revoke:
            {
                if (_formUser is not { } user)
                {
                    await ShowErrorAsync("请先选择主体。");
                    return;
                }
                var privilege = (ComboPrivilege.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "SELECT";
                string onObject = (TxtFormObject.Text ?? string.Empty).Trim();
                sql = _mode == FormMode.Grant
                    ? _userService.BuildGrantSql(connection.DatabaseType, user.Name, user.Host, privilege, onObject)
                    : _userService.BuildRevokeSql(connection.DatabaseType, user.Name, user.Host, privilege, onObject);
                preview = sql;
                confirmTitle = _mode == FormMode.Grant ? "授权" : "撤销授权";
                break;
            }
            case FormMode.GrantRole:
            case FormMode.RevokeRole:
            {
                bool isMySql = SelectedDatabaseType == DatabaseType.MySql;
                string role = TxtFormUser.Text ?? string.Empty;
                string member = TxtFormMember.Text ?? string.Empty;
                string? roleHost = isMySql ? TxtFormHost.Text : null;
                string? memberHost = isMySql ? TxtFormMemberHost.Text : null;
                if (string.IsNullOrWhiteSpace(role) || (!isMySql && string.IsNullOrWhiteSpace(member)) ||
                    (isMySql && (string.IsNullOrWhiteSpace(roleHost) || string.IsNullOrWhiteSpace(memberHost))))
                {
                    await ShowErrorAsync("请填写角色名与成员名。MySQL 还需分别填写角色和成员的主机。");
                    return;
                }

                sql = _mode == FormMode.GrantRole
                    ? _userService.BuildGrantRoleSql(connection.DatabaseType, role, member, memberHost, roleHost, databaseName is not null)
                    : _userService.BuildRevokeRoleSql(connection.DatabaseType, role, member, memberHost, roleHost, databaseName is not null);
                preview = sql;
                confirmTitle = _mode == FormMode.GrantRole ? "授予角色" : "撤销角色";
                break;
            }
            case FormMode.ChangePassword:
            {
                if (_formUser is not { } user)
                {
                    await ShowErrorAsync("请先选择主体。");
                    return;
                }
                sql = _userService.BuildAlterPasswordSql(
                    connection.DatabaseType, user.Name, user.Host, password);
                preview = _userService.BuildAlterPasswordSql(connection.DatabaseType, user.Name, user.Host, "********");
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

        await ConfirmAndExecuteAsync(connection, databaseName, scope, context, confirmTitle, sql, preview, hasPassword);
    }

    private void BtnFormCancel_Click(object? sender, RoutedEventArgs e) => CloseForm();

    private void CloseForm()
    {
        FormPanel.IsVisible = false;
        TxtFormPassword.Text = string.Empty;
        _formUser = null;
        _mode = FormMode.None;
    }

    private async void BtnDropUser_Click(object? sender, RoutedEventArgs e)
    {
        if (SelectedUser is { } user)
            await DeletePrincipalAsync(user);
        else
            TxtGrants.Text = "请先选择主体。";
    }

    private async Task DeletePrincipalAsync(DbUserInfo user)
    {
        if (SelectedConnection is not { } connection)
            return;

        int context = _contextVersion;
        string? databaseName = _databaseName;
        string scope = ScopeLabel;
        bool cascade = false;
        if (!user.IsRole && SelectedDatabaseType == DatabaseType.Oracle)
        {
            var answer = await MessageBoxManager
                .GetMessageBoxStandard("删除用户", $"是否级联删除（CASCADE，同时删除其对象）？\n「是」= CASCADE，「否」= 仅删除用户。", ButtonEnum.YesNo, MsBox.Avalonia.Enums.Icon.Warning)
                .ShowWindowDialogAsync(this);
            cascade = answer == ButtonResult.Yes;
        }

        string sql = user.IsRole
            ? _userService.BuildDropRoleSql(connection.DatabaseType, user.Name, user.Host, databaseName is not null, user.Kind == "application role")
            : _userService.BuildDropUserSql(connection.DatabaseType, user.Name, user.Host, cascade, databaseName is not null);
        string title = user.IsRole ? "删除角色" : user.Kind == "login" ? "删除登录名" : "删除用户";
        await ConfirmAndExecuteAsync(connection, databaseName, scope, context, title, sql, sql, hasPassword: false, destructive: true);
    }

    private bool _writeInProgress;

    private async Task ConfirmAndExecuteAsync(ConnectionItem connection, string? databaseName, string scope, int context,
        string title, string sql, string preview, bool hasPassword, bool destructive = false)
    {
        if (_writeInProgress || context != _contextVersion)
            return;

        _writeInProgress = true;
        try
        {
            string prompt = destructive ? "该操作不可撤销，确定执行吗？" : "确定执行吗？";
            var confirm = await MessageBoxManager.GetMessageBoxStandard(title,
                    $"操作范围：{scope}\n\n{preview}\n\n{prompt}", ButtonEnum.YesNo, MsBox.Avalonia.Enums.Icon.Warning)
                .ShowWindowDialogAsync(this);
            if (confirm != ButtonResult.Yes || context != _contextVersion)
                return;

            var (success, error) = await _userService.ExecuteAsync(connection, sql, databaseName: databaseName);
            if (context != _contextVersion)
                return;

            if (success)
            {
                CloseForm();
                await RefreshUsersAsync();
            }
            else
            {
                await ShowErrorAsync(hasPassword ? "执行失败，请检查数据库权限和密码规则。" : $"执行失败：{error}");
            }
        }
        finally
        {
            _writeInProgress = false;
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
