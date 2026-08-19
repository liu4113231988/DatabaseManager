using Avalonia.Controls;
using Avalonia.Interactivity;
using DatabaseInterpreter.Core;
using DatabaseInterpreter.Model;
using DatabaseManager.AppCore.Models;
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
    private readonly ConnectionManagerViewModel _vm;
    private readonly bool _isAdd;
    private readonly ConnectionItem _working;

    /// <summary>保存成功后返回的连接项。</summary>
    public ConnectionItem? Result { get; private set; }

    public ConnectWindow(ConnectionManagerViewModel vm, ConnectionItem? connection = null)
    {
        InitializeComponent();

        _vm = vm;
        _isAdd = connection is null;
        _working = connection ?? ConnectionItem.New(string.Empty);

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
        }

        UpdateAuthVisibility();
    }

    private void LoadDatabaseTypes()
    {
        ComboDatabaseType.ItemsSource = _vm.DatabaseTypes;
    }

    private void LoadConnection(ConnectionItem connection)
    {
        ComboDatabaseType.SelectedItem = connection.DatabaseType;
        TxtProfileName.Text = connection.Name;
        TxtServer.Text = connection.Server;
        TxtPort.Text = connection.Port;
        ComboAuthentication.SelectedItem = connection.IntegratedSecurity ? "Integrated Security" : "Password";
        TxtUserId.Text = connection.UserId;
        TxtPassword.Text = connection.Password;
        ChkRememberPassword.IsChecked = connection.RememberPassword;
        ChkIsDba.IsChecked = connection.IsDba;
        ChkUseSsl.IsChecked = connection.UseSsl;
        ComboDatabase.Text = connection.Database;

        UpdateAuthVisibility();
    }

    private void ComboDatabaseType_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        UpdateAuthVisibility();
    }

    /// <summary>按数据库类型更新各字段的可见性与默认端口。</summary>
    private void UpdateAuthVisibility()
    {
        var dbType = GetDatabaseType();
        var isSqlServer = dbType == DatabaseType.SqlServer;

        // 非 SqlServer 使用密码认证
        if (!isSqlServer)
        {
            ComboAuthentication.SelectedItem = "Password";
        }

        // 仅 Oracle 显示 DBA
        ChkIsDba.IsVisible = dbType == DatabaseType.Oracle;

        // 仅 MySql 显示 SSL
        ChkUseSsl.IsVisible = dbType == DatabaseType.MySql;

        // 默认端口
        if (string.IsNullOrEmpty(TxtPort.Text))
        {
            TxtPort.Text = dbType switch
            {
                DatabaseType.MySql => MySqlInterpreter.DEFAULT_PORT.ToString(),
                DatabaseType.Oracle => OracleInterpreter.DEFAULT_PORT.ToString(),
                DatabaseType.Postgres => PostgresInterpreter.DEFAULT_PORT.ToString(),
                _ => string.Empty,
            };
        }
    }

    private DatabaseType GetDatabaseType()
    {
        var text = ComboDatabaseType.SelectedItem as string ?? string.Empty;
        return Enum.TryParse<DatabaseType>(text, true, out var type) ? type : DatabaseType.Unknown;
    }

    private ConnectionItem BuildConnection()
    {
        var connection = _working;

        connection.DatabaseType = GetDatabaseType().ToString();
        connection.Name = TxtProfileName.Text?.Trim() ?? string.Empty;
        connection.Server = TxtServer.Text?.Trim() ?? string.Empty;
        connection.Port = TxtPort.Text?.Trim();
        connection.IntegratedSecurity = (ComboAuthentication.SelectedItem as string) == "Integrated Security";
        connection.UserId = TxtUserId.Text?.Trim();
        connection.Password = TxtPassword.Text;
        connection.IsDba = ChkIsDba.IsChecked == true;
        connection.UseSsl = ChkUseSsl.IsChecked == true;
        connection.Database = ComboDatabase.Text?.Trim() ?? string.Empty;
        connection.RememberPassword = ChkRememberPassword.IsChecked == true;

        return connection;
    }

    private async void BtnTestConnection_Click(object? sender, RoutedEventArgs e)
    {
        var connection = BuildConnection();

        if (string.IsNullOrEmpty(connection.Server))
        {
            await ShowErrorAsync("请填写服务器地址（Server）。");
            return;
        }

        BtnTestConnection.IsEnabled = false;
        ComboDatabase.ItemsSource = null;
        ComboDatabase.Items.Clear();

        try
        {
            var databases = await _vm.TestConnectionAsync(connection);
            ComboDatabase.ItemsSource = databases;
            if (databases.Count > 0 && string.IsNullOrEmpty(ComboDatabase.Text))
            {
                ComboDatabase.Text = databases.FirstOrDefault(d => string.Equals(d, connection.Database, StringComparison.OrdinalIgnoreCase))
                                   ?? databases[0];
            }

            await ShowInfoAsync($"连接成功，共发现 {databases.Count} 个数据库。");
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

    private async void BtnConfirm_Click(object? sender, RoutedEventArgs e)
    {
        var connection = BuildConnection();

        // 基本校验
        if (string.IsNullOrEmpty(connection.Server))
        {
            await ShowErrorAsync("请填写服务器地址（Server）。");
            return;
        }

        if (!connection.IntegratedSecurity && string.IsNullOrEmpty(connection.UserId))
        {
            await ShowErrorAsync("请填写用户名（User ID）。");
            return;
        }

        if (string.IsNullOrEmpty(connection.Database))
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

        Result = connection;
        Close();
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
