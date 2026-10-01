using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using DatabaseInterpreter.Core;
using DatabaseInterpreter.Model;
using DatabaseManager.AppCore.Models;
using DatabaseManager.AppCore.Services;
using DatabaseManager.AppCore.ViewModels;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;

namespace DatabaseManager.Avalonia.Views;

/// <summary>
/// 连接配置对话框（对应原 WinForms <c>frmDbConnect</c>/<c>frmAccountInfo</c>）。
/// 用于新增 / 编辑一条数据库连接：填写账号信息、测试连接、选择数据库并保存为 Profile。
/// </summary>
public partial class ConnectWindow : Window
{
    private const string PasswordAuthentication = "Password";
    private const string WindowsAuthentication = "Windows 身份验证";
    private readonly ConnectionManagerViewModel _vm;
    private readonly IConnectionVisualService? _visualService;
    private readonly bool _isAdd;
    private readonly ConnectionItem _working;
    private DatabaseType _previousDatabaseType;

    /// <summary>保存成功后返回的连接项。</summary>
    public ConnectionItem? Result { get; private set; }

    public ConnectWindow(ConnectionManagerViewModel vm, ConnectionItem? connection = null)
    {
        InitializeComponent();

        // 使用字符串选项，与 SelectedItem 的读取和配置恢复保持一致。
        ComboAuthentication.ItemsSource = new[] { PasswordAuthentication, WindowsAuthentication };
        ComboAuthentication.SelectedItem = PasswordAuthentication;
        ComboAuthentication.SelectionChanged += (_, _) => UpdateAuthenticationFields();

        _vm = vm;
        _isAdd = connection is null;
        _working = connection ?? ConnectionItem.New(string.Empty);
        _visualService = (App.Current as App)?.Services?.GetService(typeof(IConnectionVisualService)) as IConnectionVisualService;

        LoadDatabaseTypes();

        if (connection is not null)
        {
            LoadConnection(connection);
        }
        else
        {
            // 新增：默认选中当前数据库类型
            if (_vm.DatabaseTypes.Count > 0)
            {
                ComboDatabaseType.SelectedItem = _vm.SelectedDatabaseType;
            }
            ComboColorTag.SelectedItem = "无";
            ComboKingbaseMode.SelectedItem = KingbaseCompatibilityModes.Auto;
        }

        UpdateAuthVisibility();
    }

    private void LoadDatabaseTypes()
    {
        ComboDatabaseType.ItemsSource = _vm.DatabaseTypes;
        ComboColorTag.ItemsSource = new List<string> { "无" }
            .Concat(_visualService?.PaletteColors ?? new List<string>())
            .ToList();
        ComboKingbaseMode.ItemsSource = KingbaseCompatibilityModes.All;
    }

    private void LoadConnection(ConnectionItem connection)
    {
        ComboDatabaseType.SelectedItem = connection.DatabaseType;
        TxtProfileName.Text = connection.Name;
        TxtServer.Text = connection.Server;
        TxtPort.Text = connection.Port;
        ComboAuthentication.SelectedItem = connection.IntegratedSecurity ? WindowsAuthentication : PasswordAuthentication;
        TxtUserId.Text = connection.UserId;
        TxtPassword.Text = connection.Password;
        ChkRememberPassword.IsChecked = connection.RememberPassword;
        ChkIsDba.IsChecked = connection.IsDba;
        ChkUseSsl.IsChecked = connection.UseSsl;
        ChkSsh.IsChecked = connection.Ssh?.Enabled ?? false;
        TxtSshHost.Text = connection.Ssh?.Host;
        TxtSshPort.Text = (connection.Ssh?.Port ?? 22).ToString();
        TxtSshUser.Text = connection.Ssh?.UserName;
        TxtSshKey.Text = connection.Ssh?.PrivateKeyPath;
        TxtSshSecret.Text = connection.Ssh?.Secret;
        TxtSshFingerprint.Text = connection.Ssh?.HostFingerprint;
        ComboDatabase.Text = connection.Database;
        TxtDatabasePath.Text = connection.Database;
        TxtGroup.Text = connection.Group ?? string.Empty;
        ComboColorTag.SelectedItem = string.IsNullOrEmpty(connection.ColorTag)
            ? "无"
            : (ComboColorTag.Items.Cast<object?>().FirstOrDefault(i => string.Equals(i as string, connection.ColorTag, StringComparison.OrdinalIgnoreCase)) ?? "无");
        ComboKingbaseMode.SelectedItem = KingbaseCompatibilityModes.Normalize(connection.KingbaseCompatibilityMode);
        ChkDuckDbMemory.IsChecked = GetDatabaseTypeOf(connection) == DatabaseType.DuckDB
            && string.Equals(connection.Database, ":memory:", StringComparison.OrdinalIgnoreCase);
        ChkDuckDbReadOnly.IsChecked = connection.DuckDbReadOnly;

        UpdateAuthVisibility();
    }

    private void ComboDatabaseType_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        var dbType = GetDatabaseType();
        TxtPort.Text = ConnectionHelper.UpdateDefaultPort(_previousDatabaseType, dbType, TxtPort.Text);
        _previousDatabaseType = dbType;
        ComboDatabase.ItemsSource = null;
        ComboDatabase.Text = string.Empty;
        if (ConnectionHelper.IsFileDatabase(dbType))
            TxtPassword.Text = null;
        UpdateAuthVisibility();
    }

    /// <summary>按数据库类型更新各字段的可见性与默认端口。</summary>
    private void UpdateAuthVisibility()
    {
        var dbType = GetDatabaseType();
        var supportsIntegratedSecurity = DatabaseAuthentication.SupportsIntegratedSecurity(dbType);

        if (!supportsIntegratedSecurity)
        {
            ComboAuthentication.SelectedItem = PasswordAuthentication;
        }
        ComboAuthentication.IsEnabled = supportsIntegratedSecurity;
        UpdateAuthenticationFields();

        // 仅 Oracle 显示 DBA
        ChkIsDba.IsVisible = dbType == DatabaseType.Oracle;

        // 仅显示当前驱动支持的 SSL 设置。
        ChkUseSsl.IsVisible = ConnectionHelper.SupportsSsl(dbType);
        PanelKingbaseMode.IsVisible = dbType == DatabaseType.KingbaseES;

        // 文件数据库无需服务器或用户认证。
        bool isDuckDb = dbType == DatabaseType.DuckDB;
        PanelDuckDbMode.IsVisible = isDuckDb;
        bool isFileDatabase = ConnectionHelper.IsFileDatabase(dbType);
        PanelServerPort.IsVisible = !isFileDatabase;
        PanelAuthentication.IsVisible = !isFileDatabase;
        PanelUserPassword.IsVisible = !isFileDatabase;
        ChkRememberPassword.IsVisible = !isFileDatabase;
        PanelSsh.IsVisible = !isFileDatabase;
        ComboDatabase.IsVisible = !isFileDatabase;
        ComboDatabase.IsEnabled = dbType is not (DatabaseType.Oracle or DatabaseType.DM);
        BtnLoadDatabases.Content = dbType is DatabaseType.Oracle or DatabaseType.DM ? "读取 Schema" : "加载数据库";
        TxtDatabasePath.IsVisible = isFileDatabase;
        BtnBrowseDatabase.IsVisible = isFileDatabase;
        BtnLoadDatabases.IsVisible = !isFileDatabase;
        LblDatabase.Text = isFileDatabase ? "数据库路径"
            : dbType is DatabaseType.Oracle or DatabaseType.DM ? "登录用户 Schema（自动读取）" : "数据库";
        TxtServer.PlaceholderText = dbType switch
        {
            DatabaseType.Oracle => "主机/服务名，例如 localhost/ORCL",
            DatabaseType.SqlServer => @"主机或主机\实例，例如 localhost\SQLEXPRESS",
            _ => "服务器地址（端口在右侧填写）",
        };
        TxtPort.PlaceholderText = dbType == DatabaseType.SqlServer ? "可留空，使用默认或实例端口" : "默认端口";
        TxtConnectionHint.Text = dbType switch
        {
            DatabaseType.Oracle => "服务器填写主机/服务名（未写服务名时使用 ORCL）；Schema 按登录用户自动读取，无需填写数据库名。",
            DatabaseType.DM => "填写主机、端口和账号；Schema 按登录用户自动读取，无需填写数据库名。",
            DatabaseType.Postgres or DatabaseType.KingbaseES => "数据库可直接填写；加载列表也需要先连接到一个数据库，留空时驱动默认使用用户名作为库名。",
            DatabaseType.SqlServer => @"支持主机、主机\实例或主机,端口；命名实例的端口可留空。",
            DatabaseType.DuckDB => "填写文件路径或选择内存模式；只读模式需要已有文件，不能与内存模式同时使用。",
            DatabaseType.Sqlite => "选择已有 SQLite 数据库文件，无需填写服务器或用户名。",
            _ => "填写主机、端口和账号；数据库可直接填写或加载后选择。",
        };

    }

    private bool UsesWindowsAuthentication => DatabaseAuthentication.SupportsIntegratedSecurity(GetDatabaseType())
        && ComboAuthentication.SelectedItem as string == WindowsAuthentication;

    private void UpdateAuthenticationFields()
    {
        var integratedSecurity = UsesWindowsAuthentication;
        TxtUserId.IsEnabled = !integratedSecurity || DatabaseAuthentication.AllowsIntegratedUserName(GetDatabaseType());
        TxtPassword.IsEnabled = !integratedSecurity;
        ChkRememberPassword.IsEnabled = !integratedSecurity;
        TxtWindowsAuthenticationHint.IsVisible = integratedSecurity;
        TxtWindowsAuthenticationHint.Text = GetDatabaseType() switch
        {
            DatabaseType.Postgres => "使用系统凭据进行 GSS/SSPI 认证；用户名可留空或填写映射后的数据库用户名。",
            DatabaseType.Oracle => "使用外部身份验证；Windows 认证需要 Oracle 服务端和客户端配置支持。",
            _ => "Windows 身份验证使用当前运行程序的 Windows 账号。",
        };
    }

    private DatabaseType GetDatabaseType()
    {
        var text = ComboDatabaseType.SelectedItem as string ?? string.Empty;
        return Enum.TryParse<DatabaseType>(text, true, out var type) ? type : DatabaseType.Unknown;
    }

    private static DatabaseType GetDatabaseTypeOf(ConnectionItem connection)
        => Enum.TryParse<DatabaseType>(connection.DatabaseType, true, out var type) ? type : DatabaseType.Unknown;

    private ConnectionItem BuildConnection()
    {
        var connection = _working;

        connection.DatabaseType = GetDatabaseType().ToString();
        connection.Name = TxtProfileName.Text?.Trim() ?? string.Empty;
        connection.Server = ConnectionHelper.IsFileDatabase(GetDatabaseType()) ? string.Empty : TxtServer.Text?.Trim() ?? string.Empty;
        connection.Port = ConnectionHelper.IsFileDatabase(GetDatabaseType()) ? null : TxtPort.Text?.Trim();
        connection.IntegratedSecurity = UsesWindowsAuthentication;
        connection.UserId = ConnectionHelper.IsFileDatabase(GetDatabaseType())
            || connection.IntegratedSecurity && !DatabaseAuthentication.AllowsIntegratedUserName(GetDatabaseType())
            ? null : TxtUserId.Text?.Trim();
        connection.Password = connection.IntegratedSecurity ? null : TxtPassword.Text;
        connection.IsDba = GetDatabaseType() == DatabaseType.Oracle && ChkIsDba.IsChecked == true;
        connection.UseSsl = ConnectionHelper.SupportsSsl(GetDatabaseType()) && ChkUseSsl.IsChecked == true;
        connection.Ssh = new SshTunnelOptions
        {
            Enabled = !ConnectionHelper.IsFileDatabase(GetDatabaseType()) && ChkSsh.IsChecked == true, Host = TxtSshHost.Text?.Trim() ?? "",
            Port = int.TryParse(TxtSshPort.Text, out var sshPort) ? sshPort : 0,
            UserName = TxtSshUser.Text?.Trim() ?? "", PrivateKeyPath = TxtSshKey.Text?.Trim() ?? "",
            Secret = TxtSshSecret.Text ?? "", HostFingerprint = TxtSshFingerprint.Text?.Trim() ?? "",
        };
        connection.Database = (ConnectionHelper.IsFileDatabase(GetDatabaseType())
            ? TxtDatabasePath.Text : ComboDatabase.Text)?.Trim() ?? string.Empty;
        connection.RememberPassword = !connection.IntegratedSecurity && ChkRememberPassword.IsChecked == true;
        connection.KingbaseCompatibilityMode = GetDatabaseType() == DatabaseType.KingbaseES
            ? KingbaseCompatibilityModes.Normalize(ComboKingbaseMode.SelectedItem as string)
            : null;

        var duckDbType = GetDatabaseType();
        connection.DuckDbReadOnly = duckDbType == DatabaseType.DuckDB && ChkDuckDbReadOnly.IsChecked == true;
        if (duckDbType == DatabaseType.DuckDB && ChkDuckDbMemory.IsChecked == true)
        {
            connection.Database = ":memory:";
        }

        return connection;
    }

    private async Task<bool> EnsureSupportedKingbaseModeAsync(ConnectionItem connection)
    {
        if (connection.DatabaseType != nameof(DatabaseType.KingbaseES))
            return true;

        var reason = KingbaseCompatibilityModes.GetConnectionBlockReason(connection.KingbaseCompatibilityMode);
        if (reason is null)
            return true;

        await ShowErrorAsync(reason);
        return false;
    }

    private async void BtnBrowseDatabase_Click(object? sender, RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "选择数据库文件",
            AllowMultiple = false,
        });
        if (files.Count > 0 && files[0].TryGetLocalPath() is { } path)
            TxtDatabasePath.Text = path;
    }

    private async void BtnLoadDatabases_Click(object? sender, RoutedEventArgs e)
    {
        var connection = BuildConnection();

        if (!await EnsureSupportedKingbaseModeAsync(connection))
            return;

        if (ConnectionHelper.ValidateEndpoint(connection) is { } endpointError)
        {
            await ShowErrorAsync(endpointError);
            return;
        }

        BtnLoadDatabases.IsEnabled = false;
        try
        {
            var databases = await _vm.TestConnectionAsync(connection);
            PopulateDatabases(databases, connection);
        }
        catch (Exception ex)
        {
            await ShowErrorAsync($"加载数据库失败：{ex.Message}");
        }
        finally
        {
            BtnLoadDatabases.IsEnabled = true;
        }
    }

    private async void BtnTestConnection_Click(object? sender, RoutedEventArgs e)
    {
        var connection = BuildConnection();

        if (!await EnsureSupportedKingbaseModeAsync(connection))
            return;

        if (ConnectionHelper.ValidateEndpoint(connection) is { } endpointError)
        {
            await ShowErrorAsync(endpointError);
            return;
        }

        BtnTestConnection.IsEnabled = false;
        ComboDatabase.ItemsSource = null;
        ComboDatabase.Items.Clear();

        try
        {
            var databases = await _vm.TestConnectionAsync(connection);
            PopulateDatabases(databases, connection);

            await ShowInfoAsync(ConnectionHelper.IsFileDatabase(GetDatabaseType())
                ? "连接成功。" : $"连接成功，共发现 {databases.Count} 个数据库。");
        }
        catch (Exception ex)
        {
            await ShowErrorAsync($"连接失败：{ex.Message}");
        }
        finally
        {
            BtnTestConnection.IsEnabled = true;
        }
    }

    /// <summary>将加载到的数据库列表填充到下拉框，供用户选择。</summary>
    private void PopulateDatabases(IReadOnlyList<string> databases, ConnectionItem connection)
    {
        if (ConnectionHelper.IsFileDatabase(GetDatabaseType()))
            return;

        ComboDatabase.ItemsSource = databases;
        if (databases.Count > 0 && string.IsNullOrEmpty(ComboDatabase.Text))
        {
            ComboDatabase.Text = databases.FirstOrDefault(d => string.Equals(d, connection.Database, StringComparison.OrdinalIgnoreCase))
                               ?? databases[0];
        }
    }

    private async void BtnConfirm_Click(object? sender, RoutedEventArgs e)
    {
        var connection = BuildConnection();

        if (!await EnsureSupportedKingbaseModeAsync(connection))
            return;

        // 基本校验
        if (ConnectionHelper.ValidateEndpoint(connection) is { } endpointError)
        {
            await ShowErrorAsync(endpointError);
            return;
        }

        if (ConnectionHelper.RequiresDatabase(GetDatabaseType()) && string.IsNullOrEmpty(connection.Database))
        {
            await ShowErrorAsync("请选择或填写数据库。");
            return;
        }

        if (string.IsNullOrEmpty(connection.Name))
        {
            await ShowErrorAsync("请填写连接名称（Profile Name）。");
            return;
        }

        // 名称唯一性校验
        var isNameExisted = await _vm.IsNameExistedAsync(_isAdd, connection.AccountId, connection.Name, connection.Id);
        if (isNameExisted)
        {
            await ShowErrorAsync($"连接名称“{connection.Name}”已存在。");
            return;
        }

        var success = await _vm.SaveAsync(connection);
        if (!success)
        {
            await ShowErrorAsync("保存连接失败，请检查配置。");
            return;
        }

        // 保存分组与颜色标签（侧车存储，随连接 Id 关联）。
        connection.Group = TxtGroup.Text?.Trim();
        connection.ColorTag = ResolveSelectedColorTag();
        _visualService?.Save(connection.Id ?? string.Empty, connection.Name, connection.Group, connection.ColorTag,
            connection.KingbaseCompatibilityMode, connection.DuckDbReadOnly);

        Result = connection;
        Close();
    }

    /// <summary>取当前选中的颜色标签（「无」或未选择时返回 null）。</summary>
    private string? ResolveSelectedColorTag()
    {
        var color = ComboColorTag.SelectedItem as string;
        return string.IsNullOrEmpty(color) || color == "无" ? null : color;
    }

    private void BtnCancel_Click(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    private async Task ShowInfoAsync(string message)
        => await MessageBoxManager.GetMessageBoxStandard("提示", message, ButtonEnum.Ok, MsBox.Avalonia.Enums.Icon.Info)
            .ShowWindowDialogAsync(this);

    private async Task ShowErrorAsync(string message)
        => await MessageBoxManager.GetMessageBoxStandard("错误", message, ButtonEnum.Ok, MsBox.Avalonia.Enums.Icon.Error)
            .ShowWindowDialogAsync(this);
}
