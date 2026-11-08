using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DatabaseManager.AppCore.Templating;
using DatabaseManager.AppCore.Common;

namespace DatabaseManager.AppCore.ViewModels;

public partial class TemplateManagerViewModel : ViewModelBase
{
    private readonly ITemplateStore _store;
    private readonly ITemplateEngine _engine;
    public ObservableCollection<TemplateManifest> Templates { get; } = new();
    [ObservableProperty] private TemplateManifest? _selectedTemplate;
    [ObservableProperty] private string _editingContent = string.Empty;
    [ObservableProperty] private string _previewText = string.Empty;
    [ObservableProperty] private string _statusMessage = string.Empty;

    public TemplateManagerViewModel(ITemplateStore store, ITemplateEngine engine)
    {
        _store = store;
        _engine = engine;
        Reload(null);
    }

    partial void OnSelectedTemplateChanged(TemplateManifest? value)
    {
        EditingContent = value?.Content ?? string.Empty;
        PreviewText = string.Empty;
        StatusMessage = value?.Source == "user" ? "当前使用用户模板。" : "编辑并保存后覆盖内置模板。";
    }

    private TemplateManifest Edited() => new()
    {
        Id = SelectedTemplate!.Id, Kind = SelectedTemplate.Kind, Name = SelectedTemplate.Name,
        Version = SelectedTemplate.Version, Engine = SelectedTemplate.Engine, Language = SelectedTemplate.Language,
        Variables = SelectedTemplate.Variables, AppliesToDialects = SelectedTemplate.AppliesToDialects,
        Content = EditingContent,
    };

    private string Render(TemplateManifest template) => _engine.Expand(template,
        template.Variables.Where(v => v.Sample is not null).ToDictionary(v => v.Name, v => v.Sample!));

    [RelayCommand]
    private void Preview()
    {
        if (SelectedTemplate is null) return;
        try { PreviewText = Render(Edited()); StatusMessage = "示例预览，不执行 SQL。"; }
        catch (Exception ex) { PreviewText = string.Empty; StatusMessage = ex.Message; }
    }

    [RelayCommand]
    private void Save()
    {
        if (SelectedTemplate is null) return;
        try
        {
            var template = Edited();
            Render(template);
            _store.Save(template);
            Reload(template.Id);
            StatusMessage = "已保存；脚本库重新打开后生效。";
        }
        catch (Exception ex) { StatusMessage = $"保存失败：{ex.Message}"; }
    }

    [RelayCommand]
    private void RestoreDefault()
    {
        if (SelectedTemplate is null) return;
        try
        {
            var id = SelectedTemplate.Id;
            if (!_store.GetBuiltin("sql-snippet").Any(t => t.Id == id))
            { StatusMessage = "该模板没有内置默认值。"; return; }
            if (SelectedTemplate.Source == "user" && !_store.Delete("sql-snippet", id))
            { StatusMessage = "恢复失败：无法删除用户模板。"; return; }
            Reload(id);
            StatusMessage = "已恢复内置模板。";
        }
        catch (Exception ex) { StatusMessage = $"恢复失败：{ex.Message}"; }
    }

    private void Reload(string? id)
    {
        Templates.Clear();
        foreach (var template in _store.GetAll("sql-snippet")) Templates.Add(template);
        SelectedTemplate = Templates.FirstOrDefault(t => t.Id == id) ?? Templates.FirstOrDefault();
    }
}
