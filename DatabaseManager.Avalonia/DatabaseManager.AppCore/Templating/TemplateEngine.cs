using System.Text.RegularExpressions;

namespace DatabaseManager.AppCore.Templating;

/// <summary>模板渲染引擎。</summary>
public interface ITemplateEngine
{
    /// <summary>按变量契约展开模板：{name} 白名单展开，$TOKEN$ 作为兼容别名。</summary>
    string Expand(TemplateManifest manifest, IReadOnlyDictionary<string, string> variables);
}

/// <summary>simple 引擎实现：统一占位符语法 + $TOKEN$ 兼容别名（一期后移除）。</summary>
public partial class TemplateEngine : ITemplateEngine
{
    // $TOKEN$ → {token}（兼容旧 SQL 对象脚本模板语法）。
    [GeneratedRegex(@"\$([A-Za-z_][A-Za-z0-9_]*)\$")]
    private static partial Regex LegacyTokenRegex();

    // {name} 白名单：仅小写字母（与 DataDictionaryService.Expand 一致）。
    [GeneratedRegex(@"\{([a-z]+)\}")]
    private static partial Regex PlaceholderRegex();

    public string Expand(TemplateManifest manifest, IReadOnlyDictionary<string, string> variables)
    {
        if (manifest is null)
        {
            throw new ArgumentNullException(nameof(manifest));
        }

        if (manifest.Version != TemplateContract.CurrentVersion)
        {
            throw new ArgumentException($"模板契约版本不匹配（期望 {TemplateContract.CurrentVersion}，实际 {manifest.Version}），请回退内置模板或升级模板文件。");
        }

        var values = variables ?? new Dictionary<string, string>();
        TemplateContract.Validate(manifest, values);

        // 兼容别名：$TOKEN$ 视为 {token}。
        var normalized = LegacyTokenRegex().Replace(manifest.Content, m => "{" + m.Groups[1].Value.ToLowerInvariant() + "}");

        return PlaceholderRegex().Replace(normalized, m =>
            values.TryGetValue(m.Groups[1].Value, out var value)
                ? value
                : throw new ArgumentException("未知模板变量：" + m.Value));
    }
}
