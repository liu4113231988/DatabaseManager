using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Interactivity;
using DatabaseManager.AppCore.Models;
using DatabaseManager.AppCore.Services;
using DatabaseManager.AppCore.ViewModels;

namespace DatabaseManager.Avalonia.Views;

/// <summary>
/// 数据导入窗口（阶段 6 / M6）。对应原 WinForms <c>frmImportData</c>。
/// 从 CSV / Excel 文件导入数据到指定表，支持列映射。
/// </summary>
public partial class ImportWindow : Window
{
    private readonly ImportViewModel? _vm;

    public ImportWindow()
    {
        InitializeComponent();
    }

    public ImportWindow(ImportViewModel vm) : this()
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
        UseColumnMappingCheckBox.IsCheckedChanged += UseColumnMapping_CheckedChanged;

        Refresh();
    }

    protected override void OnClosed(EventArgs e)
    {
        if (_vm is null) return;
        base.OnClosed(e);

        ComboConnection.SelectionChanged -= ComboConnection_SelectionChanged;
        ComboTable.SelectionChanged -= ComboTable_SelectionChanged;
        UseColumnMappingCheckBox.IsCheckedChanged -= UseColumnMapping_CheckedChanged;
    }

    private void Refresh()
    {
        if (_vm is null) return;
        _vm.RefreshConnections();
        LoadConnections();
    }

    private void LoadConnections()
    {
        if (_vm is null) return;
        ComboConnection.ItemsSource = _vm.Connections;
        ComboConnection.ItemTemplate = new FuncDataTemplate<ConnectionItem>((item, _) =>
            new TextBlock { Text = item.Description });

        ComboConnection.SelectedItem = _vm.SelectedConnection;

        // 目标表下拉模板。
        ComboTable.ItemTemplate = new FuncDataTemplate<TableItem>((item, _) =>
            new TextBlock { Text = item.DisplayName });
        ComboTable.SelectedItem = _vm.SelectedTable;
    }

    private void ComboConnection_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        => _vm.SelectedConnection = ComboConnection.SelectedItem as ConnectionItem;

    private void ComboTable_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        => _vm.SelectedTable = ComboTable.SelectedItem as TableItem;

    private void UseColumnMapping_CheckedChanged(object? sender, RoutedEventArgs e)
    {
        if (_vm.UseColumnMapping)
        {
            _vm.RefreshColumnMappings();
        }
        else
        {
            _vm.ColumnMappings.Clear();
        }
    }

    private async void BtnBrowseFile_Click(object? sender, RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new global::Avalonia.Platform.Storage.FilePickerOpenOptions
        {
            Title = "选择要导入的数据文件",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new global::Avalonia.Platform.Storage.FilePickerFileType("CSV 文件") { Patterns = new[] { "*.csv" } },
                new global::Avalonia.Platform.Storage.FilePickerFileType("Excel 文件") { Patterns = new[] { "*.xlsx", "*.xls" } },
                new global::Avalonia.Platform.Storage.FilePickerFileType("所有文件") { Patterns = new[] { "*" } },
            },
        });

        if (files.Count > 0)
        {
            _vm.SetFilePath(files[0].Path?.LocalPath ?? string.Empty);
        }
    }

    private void BtnRemoveMapping_Click(object? sender, RoutedEventArgs e)
    {
        if (MappingGrid.SelectedItem is ColumnMappingItem item)
        {
            _vm.RemoveColumnMappingCommand.Execute(item);
        }
        else
        {
            _vm.StatusMessage = "请先选中要删除的映射行。";
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
