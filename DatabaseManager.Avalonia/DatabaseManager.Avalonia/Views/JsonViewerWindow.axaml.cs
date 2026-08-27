using Avalonia.Controls;
using Avalonia.Interactivity;
using DatabaseManager.AppCore.ViewModels;
using System.IO;

namespace DatabaseManager.Avalonia.Views;

/// <summary>
/// JSON 查看器窗口。对应原 WinForms frmJsonViewer。
/// </summary>
public partial class JsonViewerWindow : Window
{
    private readonly JsonViewerViewModel? _vm;

    public JsonViewerWindow()
    {
        InitializeComponent();
    }

    public JsonViewerWindow(JsonViewerViewModel vm) : this()
    {
        DataContext = vm;
        _vm = vm;
    }

    private async void BtnOpenFile_Click(object? sender, RoutedEventArgs e)
    {
        if (_vm is null) return;
        var files = await StorageProvider.OpenFilePickerAsync(new global::Avalonia.Platform.Storage.FilePickerOpenOptions
        {
            Title = "打开 JSON 文件",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new global::Avalonia.Platform.Storage.FilePickerFileType("JSON 文件") { Patterns = new[] { "*.json" } },
                new global::Avalonia.Platform.Storage.FilePickerFileType("所有文件") { Patterns = new[] { "*" } },
            },
        });
        if (files.Count > 0)
        {
            var path = files[0].Path?.LocalPath;
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
            {
                _vm.JsonText = await File.ReadAllTextAsync(path);
                _vm.BuildTreeCommand.Execute(null);
            }
        }
    }

    private void BtnClose_Click(object? sender, RoutedEventArgs e) => Close();
}
