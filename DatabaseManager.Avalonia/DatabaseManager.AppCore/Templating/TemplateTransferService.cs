using System.Text;
using System.Text.RegularExpressions;
using DatabaseInterpreter.Model;
using Newtonsoft.Json;

namespace DatabaseManager.AppCore.Templating;

/// <summary>T2 模板导入导出信封：{ Version = 1, Payload = Base64(JSON) }。</summary>
public sealed class TemplateTransferEnvelope
{
    public int Version { get; set; } = 1;
    public string Payload { get; set; } = string.Empty;
}

/// <summary>
/// 按连接配置迁移的信封模式导入 / 导出模板。导入只写用户覆盖文件，不改写内置模板。
/// </summary>
public static class TemplateTransferService
{
    public const int MaxBytes = 8 * 1024 * 1024;
    private const int MaxTemplateCount = 200;
    private static readonly Regex VariableNameRegex = new("^[a-z]+$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex SegmentRegex = new("^[A-Za-z0-9_.-]+$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static string Export(IReadOnlyList<TemplateManifest> templates)
    {
        if (templates is null || templates.Count == 0)
        {
            throw new ArgumentException("没有可导出的模板。");
        }
        if (templates.Count > MaxTemplateCount)
        {
            throw new ArgumentException($"单次最多导出 {MaxTemplateCount} 个模板。");
        }

        string payload = JsonConvert.SerializeObject(templates.Select(CloneForTransfer).ToList(), Formatting.Indented);
        string json = JsonConvert.SerializeObject(new TemplateTransferEnvelope
        {
            Version = TemplateContract.CurrentVersion,
            Payload = Convert.ToBase64String(Encoding.UTF8.GetBytes(payload)),
        }, Formatting.Indented);

        if (Encoding.UTF8.GetByteCount(json) > MaxBytes)
        {
            throw new ArgumentException("模板集合过大，无法导出。");
        }
        return json;
    }

    public static IReadOnlyList<TemplateManifest> Preview(string json)
    {
        if (string.IsNullOrWhiteSpace(json) || Encoding.UTF8.GetByteCount(json) > MaxBytes)
        {
            throw new ArgumentException("模板文件为空或过大。");
        }

        var envelope = JsonConvert.DeserializeObject<TemplateTransferEnvelope>(json)
            ?? throw new ArgumentException("模板文件格式无效。");
        if (envelope.Version != TemplateContract.CurrentVersion)
        {
            throw new ArgumentException($"模板契约版本不匹配（期望 {TemplateContract.CurrentVersion}）。");
        }
        if (string.IsNullOrWhiteSpace(envelope.Payload))
        {
            throw new ArgumentException("模板文件缺少 Payload。");
        }

        byte[] bytes;
        try { bytes = Convert.FromBase64String(envelope.Payload); }
        catch (FormatException) { throw new ArgumentException("模板 Payload 不是有效的 Base64 内容。"); }

        var templates = JsonConvert.DeserializeObject<List<TemplateManifest>>(Encoding.UTF8.GetString(bytes))
            ?? throw new ArgumentException("模板 Payload 解析失败。");
        if (templates.Count == 0 || templates.Count > MaxTemplateCount)
        {
            throw new ArgumentException($"模板数量必须在 1 到 {MaxTemplateCount} 之间。");
        }

        foreach (var template in templates)
        {
            Validate(template);
        }
        return templates;
    }

    public static int Import(ITemplateStore store, IReadOnlyList<TemplateManifest> templates)
    {
        if (store is null) throw new ArgumentNullException(nameof(store));
        foreach (var template in templates)
        {
            var copy = CloneForTransfer(template);
            copy.Source = "user";
            store.Save(copy);
        }
        return templates.Count;
    }

    public static TemplateManifest CloneForTransfer(TemplateManifest source)
    {
        return new TemplateManifest
        {
            Id = source.Id,
            Name = source.Name,
            Kind = source.Kind,
            Version = source.Version,
            Engine = source.Engine,
            Language = source.Language,
            AppliesToDialects = source.AppliesToDialects?.ToList() ?? new List<string>(),
            Variables = source.Variables?.Select(v => new TemplateVariable
            {
                Name = v.Name, Description = v.Description, Required = v.Required, Sample = v.Sample,
            }).ToList() ?? new List<TemplateVariable>(),
            Content = source.Content ?? string.Empty,
            Source = "user",
        };
    }

    private static void Validate(TemplateManifest template)
    {
        if (template is null
            || string.IsNullOrWhiteSpace(template.Kind) || string.IsNullOrWhiteSpace(template.Id)
            || !SegmentRegex.IsMatch(template.Kind) || !SegmentRegex.IsMatch(template.Id)
            || template.Kind is "." or ".." || template.Id is "." or "..")
        {
            throw new ArgumentException("模板 Kind / Id 不合法。");
        }
        if (template.Version != TemplateContract.CurrentVersion)
        {
            throw new ArgumentException($"模板「{template.Name}」的契约版本不匹配。");
        }
        if (!string.Equals(template.Engine, "simple", StringComparison.Ordinal))
        {
            throw new ArgumentException($"模板「{template.Name}」使用了未知渲染引擎。");
        }

        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var variable in template.Variables ?? new List<TemplateVariable>())
        {
            if (string.IsNullOrWhiteSpace(variable.Name) || !VariableNameRegex.IsMatch(variable.Name) || !names.Add(variable.Name))
            {
                throw new ArgumentException($"模板「{template.Name}」包含非法或重复的变量名。");
            }
        }
        foreach (string dialect in template.AppliesToDialects ?? new List<string>())
        {
            if (!Enum.TryParse<DatabaseType>(dialect, true, out _))
            {
                throw new ArgumentException($"模板「{template.Name}」包含未知数据库方言：{dialect}。");
            }
        }

        var values = new Dictionary<string, string>(TemplateContext.CreateDefault().ToVariables(), StringComparer.Ordinal);
        foreach (var variable in template.Variables ?? new List<TemplateVariable>())
        {
            values[variable.Name] = variable.Sample ?? string.Empty;
        }
        TemplateContract.Validate(template, values);
    }
}
