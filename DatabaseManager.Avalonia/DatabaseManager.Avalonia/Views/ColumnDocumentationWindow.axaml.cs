using Avalonia.Controls;
using Avalonia.Interactivity;
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

    protected override async void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        if (_vm is not null)
            await _vm.RefreshConnectionsCommand.ExecuteAsync(null);
    }

    protected override void OnClosed(EventArgs e)
    {
        _vm?.RefreshConnectionsCommand.Cancel();
        base.OnClosed(e);
    }

    private async void BtnBrowseFile_Click(object? sender, RoutedEventArgs e)
    {
        if (_vm is null) return;
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

    private void BtnClose_Click(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
