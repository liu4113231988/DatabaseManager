using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Interactivity;
using DatabaseManager.AppCore.Models;
using DatabaseManager.AppCore.Services;
using DatabaseManager.AppCore.ViewModels;

namespace DatabaseManager.Avalonia.Views;

/// <summary>
/// 数据导出窗口（阶段 6 / M6）。对应原 WinForms <c>frmExportData</c>。
/// 选择连接/表/视图与导出格式，导出数据到 CSV / Excel。
/// </summary>
public partial class ExportWindow : Window
{
    private readonly ExportViewModel? _vm;

    public ExportWindow()
    {
        InitializeComponent();
    }

    public ExportWindow(ExportViewModel vm) : this()
    {
        DataContext = vm;
        _vm = vm;
    }

    protected override void OnOpened(EventArgs e)
    {
        if (_vm is null) return;
        base.OnOpened(e);

        ComboConnection.SelectionChanged += ComboConnection_SelectionChanged;
        ComboTable.SelectionChanged += ComboTable_SelectionChanged;
        ComboFormat.SelectionChanged += ComboFormat_SelectionChanged;

        Refresh();
    }

    protected override void OnClosed(EventArgs e)
    {
        if (_vm is null) return;
        base.OnClosed(e);

        ComboConnection.SelectionChanged -= ComboConnection_SelectionChanged;
        ComboTable.SelectionChanged -= ComboTable_SelectionChanged;
        ComboFormat.SelectionChanged -= ComboFormat_SelectionChanged;
    }

    private void Refresh()
    {
        if (_vm is null) return;
        _vm.RefreshConnections();
        LoadConnections();
        LoadFormat();
    }

    private void LoadConnections()
    {
        if (_vm is null) return;
        ComboConnection.ItemsSource = _vm.Connections;
        ComboConnection.ItemTemplate = new FuncDataTemplate<ConnectionItem>((item, _) =>
            new TextBlock { Text = item.Description });

        ComboConnection.SelectedItem = _vm.SelectedConnection;

        // 表/视图下拉模板。
        ComboTable.ItemTemplate = new FuncDataTemplate<TableItem>((item, _) =>
            new TextBlock { Text = item.DisplayName });
        ComboTable.SelectedItem = _vm.SelectedTable;
    }

    private void LoadFormat()
    {
        if (_vm is null) return;
        ComboFormat.ItemsSource = _vm.Formats;
        ComboFormat.SelectedItem = _vm.SelectedFormat;
    }

    private void ComboConnection_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        => _vm.SelectedConnection = ComboConnection.SelectedItem as ConnectionItem;

    private void ComboTable_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        => _vm.SelectedTable = ComboTable.SelectedItem as TableItem;

    private void ComboFormat_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        => _vm.SelectedFormat = ComboFormat.SelectedItem as string ?? "Excel";

    private async void BtnBrowseFile_Click(object? sender, RoutedEventArgs e)
    {
        var extension = _vm.SelectedFormat?.Equals("CSV", StringComparison.OrdinalIgnoreCase) == true ? "csv" : "xlsx";

        var file = await StorageProvider.SaveFilePickerAsync(new global::Avalonia.Platform.Storage.FilePickerSaveOptions
        {
            Title = "选择导出文件路径",
            SuggestedFileName = $"{_vm.SelectedTable?.Name ?? "export"}.{extension}",
            DefaultExtension = extension,
            FileTypeChoices = new[]
            {
                new global::Avalonia.Platform.Storage.FilePickerFileType(extension.ToUpperInvariant() + " 文件") { Patterns = new[] { $"*.{extension}" } },
                new global::Avalonia.Platform.Storage.FilePickerFileType("所有文件") { Patterns = new[] { "*" } },
            },
        });

        if (file is not null)
        {
            _vm.SetFilePath(file.Path?.LocalPath ?? string.Empty);
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
