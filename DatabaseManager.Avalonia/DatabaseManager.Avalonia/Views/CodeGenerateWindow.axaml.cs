using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Interactivity;
using DatabaseManager.AppCore.Models;
using DatabaseManager.AppCore.ViewModels;

namespace DatabaseManager.Avalonia.Views;

/// <summary>
/// 代码生成窗口（阶段 5）。对应原 WinForms <c>frmCodeGenerator</c>。
/// 选择表/视图与语言，生成实体类代码。
/// </summary>
public partial class CodeGenerateWindow : Window
{
    private readonly CodeGenerateViewModel? _vm;

    public CodeGenerateWindow()
    {
        InitializeComponent();
    }

    public CodeGenerateWindow(CodeGenerateViewModel vm) : this()
    {
        DataContext = vm;
        _vm = vm;
    }

    protected override void OnOpened(EventArgs e)
    {
        if (_vm is null) return;
        base.OnOpened(e);

        ComboConnection.SelectionChanged += ComboConnection_SelectionChanged;
        ComboLanguage.SelectionChanged += ComboLanguage_SelectionChanged;

        Refresh();
    }

    protected override void OnClosed(EventArgs e)
    {
        if (_vm is null) return;
        base.OnClosed(e);

        ComboConnection.SelectionChanged -= ComboConnection_SelectionChanged;
        ComboLanguage.SelectionChanged -= ComboLanguage_SelectionChanged;
    }

    private void Refresh()
    {
        if (_vm is null) return;
        _vm.RefreshConnections();
        LoadConnections();
        LoadLanguages();
    }

    private void LoadConnections()
    {
        if (_vm is null) return;
        ComboConnection.ItemsSource = _vm.Connections;
        ComboConnection.ItemTemplate = new FuncDataTemplate<ConnectionItem>((item, _) =>
            new TextBlock { Text = item?.Description ?? string.Empty });

        ComboConnection.SelectedItem = _vm.SelectedConnection;
    }

    private void LoadLanguages()
    {
        if (_vm is null) return;
        ComboLanguage.ItemsSource = _vm.Languages;
        ComboLanguage.ItemTemplate = new FuncDataTemplate<CodeGenerateLanguageOption>((item, _) =>
            new TextBlock { Text = item?.DisplayName ?? string.Empty });

        ComboLanguage.SelectedItem = _vm.SelectedLanguage;
    }

    private void ComboConnection_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        => _vm.SelectedConnection = ComboConnection.SelectedItem as ConnectionItem;

    private void ComboLanguage_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        => _vm.SelectedLanguage = ComboLanguage.SelectedItem as CodeGenerateLanguageOption;

    private async void BtnBrowseOutput_Click(object? sender, RoutedEventArgs e)
    {
        var dialog = await StorageProvider.OpenFolderPickerAsync(new global::Avalonia.Platform.Storage.FolderPickerOpenOptions
        {
            Title = "选择代码输出文件夹",
            AllowMultiple = false,
        });

        if (dialog.Count > 0)
        {
            _vm.OutputFolder = dialog[0].Path?.LocalPath ?? string.Empty;
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
