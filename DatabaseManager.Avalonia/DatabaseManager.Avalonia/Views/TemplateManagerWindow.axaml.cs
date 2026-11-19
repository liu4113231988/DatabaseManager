using Avalonia.Controls;
using Avalonia.Platform.Storage;
using DatabaseManager.AppCore.ViewModels;

namespace DatabaseManager.Avalonia.Views;

public partial class TemplateManagerWindow : Window
{
    public TemplateManagerWindow() => InitializeComponent();

    public TemplateManagerWindow(TemplateManagerViewModel vm) : this()
    {
        DataContext = vm;
        vm.SavePathProvider = async () =>
        {
            var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "导出模板",
                SuggestedFileName = "templates.dbm.json",
                DefaultExtension = "json",
                ShowOverwritePrompt = true,
                FileTypeChoices = new[]
                {
                    new FilePickerFileType("模板文件") { Patterns = new[] { "*.dbm.json", "*.json" } },
                    new FilePickerFileType("所有文件") { Patterns = new[] { "*" } },
                },
            });
            return file?.Path?.LocalPath;
        };
        vm.OpenPathProvider = async () =>
        {
            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "选择模板文件",
                AllowMultiple = false,
                FileTypeFilter = new[]
                {
                    new FilePickerFileType("模板文件") { Patterns = new[] { "*.dbm.json", "*.json" } },
                    new FilePickerFileType("所有文件") { Patterns = new[] { "*" } },
                },
            });
            return files.FirstOrDefault()?.Path?.LocalPath;
        };
    }
}
