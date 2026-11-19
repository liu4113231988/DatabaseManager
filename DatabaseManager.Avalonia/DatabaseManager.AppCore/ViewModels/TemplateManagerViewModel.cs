using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DatabaseInterpreter.Model;
using DatabaseManager.AppCore.Common;
using DatabaseManager.AppCore.Templating;

namespace DatabaseManager.AppCore.ViewModels;

public partial class TemplateManagerViewModel : ViewModelBase
{
    private const string AllKinds = "全部";
    private static readonly Regex VariableNameRegex = new("^[a-z]+$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly ITemplateStore _store;
    private readonly ITemplateEngine _engine;

    public ObservableCollection<TemplateManifest> Templates { get; } = new();
    public ObservableCollection<string> Kinds { get; } = new();

    [ObservableProperty] private TemplateManifest? _selectedTemplate;
    [ObservableProperty] private string _editingContent = string.Empty;
    [ObservableProperty] private string _editingName = string.Empty;
    [ObservableProperty] private string _variablesText = string.Empty;
    [ObservableProperty] private string _dialectsText = string.Empty;
    [ObservableProperty] private string _previewText = string.Empty;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private string _kindFilter = AllKinds;

    /// <summary>由窗口注入文件选择，ViewModel 保持 UI 无关。</summary>
    public Func<Task<string?>>? SavePathProvider { get; set; }
    public Func<Task<string?>>? OpenPathProvider { get; set; }

    public TemplateManagerViewModel(ITemplateStore store, ITemplateEngine engine)
    {
        _store = store;
        _engine = engine;
        RefreshKinds();
        Reload(null);
    }

    partial void OnSelectedTemplateChanged(TemplateManifest? value)
    {
        EditingContent = value?.Content ?? string.Empty;
        EditingName = value?.Name ?? string.Empty;
        VariablesText = FormatVariables(value?.Variables);
        DialectsText = string.Join(", ", value?.AppliesToDialects ?? new List<string>());
        PreviewText = string.Empty;
        StatusMessage = value?.Source == "user" ? "当前使用用户模板。" : "编辑并保存后覆盖内置模板。";
    }

    partial void OnKindFilterChanged(string value) => Reload(SelectedTemplate?.Id);

    private static string FormatVariables(IReadOnlyList<TemplateVariable>? variables)
        => string.Join(Environment.NewLine, (variables ?? new List<TemplateVariable>())
            .Select(v => $"{v.Name}|{v.Description}|{v.Sample ?? string.Empty}|{(v.Required ? "1" : "0")}"));

    private TemplateManifest Edited()
    {
        var source = SelectedTemplate!;
        return new TemplateManifest
        {
            Id = source.Id,
            Kind = source.Kind,
            Name = string.IsNullOrWhiteSpace(EditingName) ? source.Name : EditingName.Trim(),
            Version = source.Version,
            Engine = source.Engine,
            Language = source.Language,
            Variables = ParseVariables(),
            AppliesToDialects = ParseDialects(),
            Content = EditingContent,
        };
    }

    private List<TemplateVariable> ParseVariables()
    {
        var result = new List<TemplateVariable>();
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (string line in VariablesText.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            string[] parts = line.Split('|');
            if (parts.Length > 4) throw new ArgumentException("变量定义每行最多 4 段：name|说明|示例|必填。");
            string name = parts[0].Trim();
            if (!VariableNameRegex.IsMatch(name) || !names.Add(name))
                throw new ArgumentException($"变量名 '{name}' 非法或重复（只允许小写字母）。");
            result.Add(new TemplateVariable
            {
                Name = name,
                Description = parts.Length > 1 ? parts[1].Trim() : string.Empty,
                // 示例值可能包含缩进（如代码模板的 \t），不能 Trim 掉。
                Sample = parts.Length > 2 && parts[2].Length > 0 ? parts[2] : null,
                Required = parts.Length < 4 || parts[3].Trim() != "0",
            });
        }
        return result;
    }

    private List<string> ParseDialects()
    {
        if (string.IsNullOrWhiteSpace(DialectsText)) return new List<string>();
        var result = new List<string>();
        foreach (string dialect in DialectsText.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!Enum.TryParse<DatabaseType>(dialect, true, out var type))
                throw new ArgumentException($"未知数据库方言：{dialect}。");
            result.Add(type.ToString());
        }
        return result;
    }

    private string Render(TemplateManifest template)
    {
        var samples = template.Variables
            .ToDictionary(v => v.Name, v => v.Sample ?? string.Empty, StringComparer.Ordinal);
        return _engine.Expand(template, TemplateContext.CreateDefault(), samples);
    }

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
            string id = SelectedTemplate.Id;
            string kind = SelectedTemplate.Kind;
            if (!_store.GetBuiltin(kind).Any(t => t.Id == id))
            { StatusMessage = "该模板没有内置默认值。"; return; }
            if (SelectedTemplate.Source == "user" && !_store.Delete(kind, id))
            { StatusMessage = "恢复失败：无法删除用户模板。"; return; }
            Reload(id);
            StatusMessage = "已恢复内置模板。";
        }
        catch (Exception ex) { StatusMessage = $"恢复失败：{ex.Message}"; }
    }

    [RelayCommand]
    private async Task ExportAsync()
    {
        if (SavePathProvider is null) { StatusMessage = "当前窗口未配置文件导出入口。"; return; }
        try
        {
            string? path = await SavePathProvider();
            if (path is null) return;
            var selected = Templates.Where(t => KindFilter == AllKinds || t.Kind == KindFilter).ToList();
            await File.WriteAllTextAsync(path, TemplateTransferService.Export(selected));
            StatusMessage = $"已导出 {selected.Count} 个模板。";
        }
        catch (Exception ex) { StatusMessage = $"导出失败：{ex.Message}"; }
    }

    [RelayCommand]
    private async Task ImportAsync()
    {
        if (OpenPathProvider is null) { StatusMessage = "当前窗口未配置文件导入入口。"; return; }
        try
        {
            string? path = await OpenPathProvider();
            if (path is null) return;
            if (new FileInfo(path).Length > TemplateTransferService.MaxBytes)
                throw new ArgumentException("模板文件过大。");
            var templates = TemplateTransferService.Preview(await File.ReadAllTextAsync(path));
            int count = TemplateTransferService.Import(_store, templates);
            RefreshKinds();
            Reload(null);
            StatusMessage = $"已导入 {count} 个模板（作为用户覆盖）。";
        }
        catch (Exception ex) { StatusMessage = $"导入失败：{ex.Message}"; }
    }

    private void RefreshKinds()
    {
        Kinds.Clear();
        Kinds.Add(AllKinds);
        foreach (string kind in _store.GetAll().Select(t => t.Kind).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(k => k, StringComparer.OrdinalIgnoreCase))
            Kinds.Add(kind);
        if (!Kinds.Contains(KindFilter)) KindFilter = AllKinds;
    }

    private void Reload(string? id)
    {
        string? kind = KindFilter == AllKinds || string.IsNullOrWhiteSpace(KindFilter) ? null : KindFilter;
        Templates.Clear();
        foreach (var template in _store.GetAll(kind)) Templates.Add(template);
        SelectedTemplate = Templates.FirstOrDefault(t => t.Id == id) ?? Templates.FirstOrDefault();
    }
}
