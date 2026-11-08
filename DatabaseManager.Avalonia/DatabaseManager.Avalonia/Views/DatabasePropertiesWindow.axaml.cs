using Avalonia.Controls;
using Avalonia.Interactivity;
using DatabaseManager.AppCore.Models;
using DatabaseManager.AppCore.Services;

namespace DatabaseManager.Avalonia.Views;

public partial class DatabasePropertiesWindow : Window
{
    private readonly ConnectionItem _connection;
    private readonly string _databaseName;
    private readonly DatabasePropertiesService _service;
    private readonly CancellationTokenSource _cancellation = new();

    public DatabasePropertiesWindow(ConnectionItem connection, string databaseName, DatabasePropertiesService service)
    {
        InitializeComponent();
        _connection = connection.Clone();
        _databaseName = databaseName;
        _service = service;
        TargetText.Text = $"连接：{connection.Name}    对象：{databaseName}";
    }

    protected override async void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        await RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        RefreshButton.IsEnabled = false;
        StatusText.Text = "正在读取属性...";
        try
        {
            PropertyGrid.ItemsSource = DatabasePropertiesService.GetBasicProperties(_connection, _databaseName);
            var properties = await _service.GetAsync(_connection, _databaseName, _cancellation.Token);
            if (_cancellation.IsCancellationRequested) return;
            PropertyGrid.ItemsSource = properties;
            StatusText.Text = properties.Any(p => p.Name == "信息范围") ? "当前类型仅提供连接或文件信息。" : "属性已更新。";
        }
        catch (OperationCanceledException) when (_cancellation.IsCancellationRequested) { }
        catch (Exception ex) { StatusText.Text = $"读取属性失败：{ex.Message}"; }
        finally { RefreshButton.IsEnabled = true; }
    }

    protected override void OnClosed(EventArgs e)
    {
        _cancellation.Cancel();
        _cancellation.Dispose();
        base.OnClosed(e);
    }

    private async void Refresh_Click(object? sender, RoutedEventArgs e) => await RefreshAsync();
    private void Close_Click(object? sender, RoutedEventArgs e) => Close();
}
