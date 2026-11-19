using System.Collections.Generic;
using System.Linq;

namespace DatabaseManager.AppCore.Models;

/// <summary>表设计器列选择器与内联编辑共用的文本解析规则（UI 无关）。</summary>
public static class TableDesignColumnParser
{
    /// <summary>逗号分隔列名；支持中文逗号，去除空白、空项与重复项，保留首次出现顺序。</summary>
    public static List<string> ParseColumns(string? text)
    {
        var result = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string part in Split(text))
        {
            if (seen.Add(part))
            {
                result.Add(part);
            }
        }
        return result;
    }

    /// <summary>逗号分隔的「本表列=引用列」映射；省略 = 时两列同名。</summary>
    public static List<ForeignKeyMapping> ParseMappings(string? text)
    {
        var result = new List<ForeignKeyMapping>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string part in Split(text))
        {
            string[] mapping = part.Split('=', 2, System.StringSplitOptions.TrimEntries);
            string local = mapping[0];
            string referenced = mapping.Length > 1 && mapping[1].Length > 0 ? mapping[1] : local;
            if (string.IsNullOrWhiteSpace(local) || string.IsNullOrWhiteSpace(referenced) || !seen.Add(local))
            {
                continue;
            }
            result.Add(new ForeignKeyMapping { ColumnName = local, ReferencedColumnName = referenced });
        }
        return result;
    }

    public static string ToDisplay(IEnumerable<string> columns) => string.Join(", ", columns);

    public static string ToDisplay(IEnumerable<ForeignKeyMapping> mappings)
        => string.Join(", ", mappings.Select(m => $"{m.ColumnName}={m.ReferencedColumnName}"));

    private static IEnumerable<string> Split(string? text)
        => (text ?? string.Empty)
            .Replace('，', ',')
            .Split(',', System.StringSplitOptions.TrimEntries | System.StringSplitOptions.RemoveEmptyEntries);
}
