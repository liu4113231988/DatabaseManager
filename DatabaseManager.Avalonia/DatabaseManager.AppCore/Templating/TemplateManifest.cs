namespace DatabaseManager.AppCore.Templating;

/// <summary>
/// 统一模板变量契约（P3 模板外部化 T1）。
/// 占位符语法统一为 <c>{name}</c>（小写白名单）；旧 <c>$TOKEN$</c> 语法由引擎作为兼容别名处理，一期后移除。
/// </summary>
public class TemplateVariable
{
    /// <summary>变量名（小写，展开时按名查找）。</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>变量说明（模板管理界面展示）。</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>是否必填（缺失时渲染直接报错，不静默留空）。</summary>
    public bool Required { get; set; } = true;

    /// <summary>示例值（预览用）。</summary>
    public string? Sample { get; set; }
}

/// <summary>
/// 模板清单：一个可外部化、可导入导出的模板（内容内嵌于 Content 字段）。
/// </summary>
public class TemplateManifest
{
    /// <summary>模板唯一标识（同 Kind 内唯一，如 snippet.select-all）。</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>展示名称。</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>模板类别（目录名）：sql-snippet / ddl / dictionary / export 等。</summary>
    public string Kind { get; set; } = "sql-snippet";

    /// <summary>契约版本；版本不匹配时回退内置模板并提示，不静默改写用户文件。</summary>
    public int Version { get; set; } = 1;

    /// <summary>渲染引擎：simple（{name} 展开；后续可扩展条件/循环引擎）。</summary>
    public string Engine { get; set; } = "simple";

    /// <summary>语言（sql / csharp / java / html 等）。</summary>
    public string Language { get; set; } = "sql";

    /// <summary>适用的数据库方言（空表示不限；按 DatabaseType.ToString() 填写）。</summary>
    public List<string> AppliesToDialects { get; set; } = new();

    /// <summary>变量契约。</summary>
    public List<TemplateVariable> Variables { get; set; } = new();

    /// <summary>模板内容（占位符使用 {name} 语法；$TOKEN$ 作为兼容别名）。</summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>来源：builtin（内置，只读）/ user（用户目录，可编辑）。</summary>
    public string Source { get; set; } = "builtin";
}

/// <summary>契约版本常量：模板文件 Version 与其不一致时回退内置并提示。</summary>
public static class TemplateContract
{
    public const int CurrentVersion = 1;

    /// <summary>渲染前校验：内容长度与变量完整性（沿用 DataDictionaryService.Expand 的严格校验思路）。</summary>
    public static void Validate(TemplateManifest manifest, IReadOnlyDictionary<string, string> variables)
    {
        if (manifest.Content.Length > 2000)
        {
            throw new ArgumentException("模板过长。");
        }

        var missing = manifest.Variables
            .Where(v => v.Required && !variables.ContainsKey(v.Name))
            .Select(v => v.Name)
            .ToList();

        if (missing.Count > 0)
        {
            throw new ArgumentException("缺少模板变量：" + string.Join(", ", missing));
        }
    }
}
