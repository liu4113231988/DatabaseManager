using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Interactivity;
using DatabaseManager.AppCore.Models;
using DatabaseManager.AppCore.ViewModels;

namespace DatabaseManager.Avalonia.Views;

/// <summary>
/// 数据库文档生成窗口（阶段 5）。对应原 WinForms <c>Documentation/frmGenerateColumnDocumentation</c>。
/// 选择连接与列属性，生成列结构文档（Word）。
/// </summary>
public partial class ColumnDocumentationWindow : Window
{
    private readonly ColumnDocumentationViewModel? _vm;

    public ColumnDocumentationWindow()
    {
        InitializeComponent();
    }

    public ColumnDocumentationWindow(ColumnDocumentationViewModel vm) : this()
    {
        DataContext = vm;
        _vm = vm;
    }

    protected override void OnOpened(EventArgs e)
    {
        if (_vm is null) return;
        base.OnOpened(e);
        ComboConnection.SelectionChanged += ComboConnection_SelectionChanged;
        Refresh();
    }

    protected override void OnClosed(EventArgs e)
    {
        if (_vm is null) return;
        base.OnClosed(e);
        ComboConnection.SelectionChanged -= ComboConnection_SelectionChanged;
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
    }

    private void ComboConnection_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        => _vm.SelectedConnection = ComboConnection.SelectedItem as ConnectionItem;

    private async void BtnBrowseFile_Click(object? sender, RoutedEventArgs e)
    {
        var file = await StorageProvider.SaveFilePickerAsync(new global::Avalonia.Platform.Storage.FilePickerSaveOptions
        {
            Title = "选择文档输出文件",
            SuggestedFileName = "ColumnDocumentation.docx",
            DefaultExtension = "docx",
            FileTypeChoices = new[]
            {
                new global::Avalonia.Platform.Storage.FilePickerFileType("Word 文档") { Patterns = new[] { "*.docx" } },
                new global::Avalonia.Platform.Storage.FilePickerFileType("所有文件") { Patterns = new[] { "*" } },
            },
        });

        if (file is not null)
        {
            _vm.FilePath = file.Path?.LocalPath ?? string.Empty;
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
