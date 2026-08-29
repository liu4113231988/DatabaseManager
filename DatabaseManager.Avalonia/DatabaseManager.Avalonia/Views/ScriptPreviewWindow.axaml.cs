using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using DatabaseManager.AppCore.ViewModels;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;

namespace DatabaseManager.Avalonia.Views;

/// <summary>
/// 脚本预览与执行窗口：审阅由结构/数据对比生成的同步脚本，勾选后在目标库上执行。
/// </summary>
public partial class ScriptPreviewWindow : Window
{
    private readonly ScriptPreviewViewModel? _vm;

    public ScriptPreviewWindow()
    {
        InitializeComponent();
    }

    public ScriptPreviewWindow(ScriptPreviewViewModel vm) : this()
    {
        DataContext = vm;
        _vm = vm;
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        if (_vm is null) return;

        // 执行前确认（列出脚本数与目标库）。
        _vm.RequestExecuteConfirm = async () =>
        {
            var box = MessageBoxManager.GetMessageBoxStandard(
                title: "确认执行脚本",
                text: $"将在目标库「{_vm.TargetConnection?.Description}」上执行 {_vm.Scripts.Count(s => s.IsSelected)} 项脚本。\n结构脚本在单事务内执行（失败整体回滚），数据脚本按条目提交。是否继续？",
                ButtonEnum.YesNo,
                MsBox.Avalonia.Enums.Icon.Warning);
            return await box.ShowWindowDialogAsync(this) == ButtonResult.Yes;
        };
    }

    private async void BtnSaveScripts_Click(object? sender, RoutedEventArgs e)
    {
        if (_vm is null || _vm.Scripts.Count == 0)
            return;

        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "保存同步脚本",
            SuggestedFileName = $"sync-scripts-{DateTime.Now:yyyyMMdd-HHmmss}.sql",
            DefaultExtension = "sql",
            FileTypeChoices = new[]
            {
                new FilePickerFileType("SQL 脚本") { Patterns = new[] { "*.sql" } },
                new FilePickerFileType("所有文件") { Patterns = new[] { "*" } },
            },
        });

        if (file is not null)
        {
            await _vm.SaveScriptsToFileAsync(file.Path?.LocalPath ?? string.Empty);
        }
    }

    private void BtnClose_Click(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
