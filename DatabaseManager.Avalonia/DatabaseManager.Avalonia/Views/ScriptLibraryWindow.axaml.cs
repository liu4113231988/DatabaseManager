using Avalonia.Controls;
using Avalonia.Interactivity;
using DatabaseManager.AppCore.ViewModels;

namespace DatabaseManager.Avalonia.Views;

/// <summary>脚本库窗口：管理用户脚本与内置片段，插入到查询编辑器。</summary>
public partial class ScriptLibraryWindow : Window
{
    private readonly ScriptLibraryViewModel? _vm;

    public ScriptLibraryWindow()
    {
        InitializeComponent();
    }

    public ScriptLibraryWindow(ScriptLibraryViewModel vm) : this()
    {
        DataContext = vm;
        _vm = vm;
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        _vm?.RefreshCommand.Execute(null);
    }

    private void LibraryTabs_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_vm is null || sender is not TabControl tabs)
        {
            return;
        }

        // 切换到内置片段页时展示内置片段，切回时展示用户脚本。
        if (tabs.SelectedIndex == 1)
        {
            _vm.ShowBuiltIn();
        }
        else
        {
            _vm.RefreshCommand.Execute(null);
        }
    }

    private void BtnNew_Click(object? sender, RoutedEventArgs e)
    {
        _vm?.BeginNewWithSql(string.Empty);
    }

    private void BtnClose_Click(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
