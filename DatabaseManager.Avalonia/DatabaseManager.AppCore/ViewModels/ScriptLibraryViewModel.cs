using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DatabaseManager.AppCore.Common;
using DatabaseManager.AppCore.Services;

namespace DatabaseManager.AppCore.ViewModels;

/// <summary>
/// 脚本库 ViewModel：管理用户脚本（新建/编辑/删除）与内置代码片段，插入到当前编辑器。
/// </summary>
public partial class ScriptLibraryViewModel : ViewModelBase
{
    private readonly IScriptLibraryService _libraryService;

    /// <summary>列表显示的脚本（我的脚本或内置片段）。</summary>
    public ObservableCollection<ScriptLibraryItem> Scripts { get; } = new();

    /// <summary>内置片段独立集合，避免切换选项卡时修改“我的脚本”列表导致 ListBox 选择更新重入。</summary>
    public ObservableCollection<ScriptLibraryItem> BuiltInScripts { get; } = new();

    [ObservableProperty]
    private ScriptLibraryItem? _selectedScript;

    [ObservableProperty]
    private string _searchText = string.Empty;

    /// <summary>编辑区：名称 / 分类 / SQL。</summary>
    [ObservableProperty]
    private string _editingName = string.Empty;

    [ObservableProperty]
    private string _editingCategory = "默认";

    [ObservableProperty]
    private string _editingSqlText = string.Empty;

    /// <summary>是否为内置片段（只读，仅可插入）。</summary>
    [ObservableProperty]
    private bool _isBuiltInSelected;

    /// <summary>由窗口注入：把 SQL 插入到当前编辑器。</summary>
    public Action<string>? InsertToEditorRequested { get; set; }

    public ScriptLibraryViewModel(IScriptLibraryService libraryService)
    {
        _libraryService = libraryService;

        foreach (var snippet in SqlSnippets.BuiltIn)
        {
            BuiltInScripts.Add(snippet);
        }
    }

    partial void OnSelectedScriptChanged(ScriptLibraryItem? value)
    {
        if (value is null)
        {
            EditingName = string.Empty;
            EditingCategory = string.Empty;
            EditingSqlText = string.Empty;
            IsBuiltInSelected = false;
            return;
        }

        IsBuiltInSelected = string.Equals(value.Category, "内置片段", StringComparison.Ordinal);
        EditingName = value.Name;
        EditingCategory = value.Category;
        EditingSqlText = value.SqlText;
        DeleteCommand.NotifyCanExecuteChanged();
        SaveCommand.NotifyCanExecuteChanged();
    }

    /// <summary>刷新脚本列表（我的脚本页）。</summary>
    [RelayCommand]
    private void Refresh()
    {
        Scripts.Clear();

        var filter = SearchText?.Trim() ?? string.Empty;
        foreach (var script in _libraryService.GetAll())
        {
            if (filter.Length > 0
                && !script.Name.Contains(filter, StringComparison.OrdinalIgnoreCase)
                && !script.SqlText.Contains(filter, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            Scripts.Add(script);
        }
    }

    /// <summary>以给定 SQL 新建脚本（供「保存当前 SQL 到脚本库」使用）。</summary>
    public void BeginNewWithSql(string sql)
    {
        SelectedScript = null;
        IsBuiltInSelected = false;
        EditingName = $"脚本 {DateTime.Now:yyyyMMdd-HHmmss}";
        EditingCategory = "默认";
        EditingSqlText = sql;
    }

    [RelayCommand(CanExecute = nameof(CanSave))]
    private void Save()
    {
        var item = SelectedScript is not null && !IsBuiltInSelected
            ? SelectedScript
            : new ScriptLibraryItem();

        item.Name = string.IsNullOrWhiteSpace(EditingName) ? "未命名脚本" : EditingName.Trim();
        item.Category = string.IsNullOrWhiteSpace(EditingCategory) ? "默认" : EditingCategory.Trim();
        item.SqlText = EditingSqlText;

        _libraryService.Save(item);
        SelectedScript = item;
        Refresh();
        SelectedScript = Scripts.FirstOrDefault(s => s.Id == item.Id);
    }

    private bool CanSave() => !IsBuiltInSelected && EditingSqlText.Length > 0;

    partial void OnEditingSqlTextChanged(string value) => SaveCommand.NotifyCanExecuteChanged();
    partial void OnIsBuiltInSelectedChanged(bool value) => SaveCommand.NotifyCanExecuteChanged();

    [RelayCommand(CanExecute = nameof(CanDelete))]
    private void Delete()
    {
        if (SelectedScript is null || IsBuiltInSelected)
        {
            return;
        }

        _libraryService.Delete(SelectedScript.Id);
        SelectedScript = null;
        Refresh();
    }

    private bool CanDelete() => !IsBuiltInSelected && SelectedScript is not null;

    [RelayCommand]
    private void InsertToEditor()
    {
        if (EditingSqlText.Length > 0 && InsertToEditorRequested is not null)
        {
            InsertToEditorRequested(EditingSqlText);
        }
    }
}
