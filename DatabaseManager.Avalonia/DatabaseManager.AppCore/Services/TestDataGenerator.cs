using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using DatabaseManager.AppCore.Models;

namespace DatabaseManager.AppCore.Services;

public sealed class GenerationRule
{
    public string Column { get; set; } = "";
    public string Kind { get; set; } = "自动";
    public long Minimum { get; set; } = 1;
    public long Maximum { get; set; } = 1000000;
    public string Values { get; set; } = "";
    public string Pattern { get; set; } = "[A-Z]{3}[0-9]{5}";
    public bool Unique { get; set; }
    public double NullRatio { get; set; }
}

public static class TestDataGenerator
{
    public static IReadOnlyList<DataEditRow> Generate(DataTableInfo table, IReadOnlyList<GenerationRule> rules, int count, int seed,
        IReadOnlyList<ForeignKeyChoices>? foreignKeys = null, CancellationToken ct = default)
    {
        if (count is < 1 or > 10000) throw new ArgumentException("每批生成 1–10000 行。");
        if (rules.GroupBy(r => r.Column).Any(g => g.Count() > 1)) throw new ArgumentException("列规则重复。");
        foreach (var rule in rules)
        {
            var col = table.Columns.SingleOrDefault(c => c.Name == rule.Column) ?? throw new ArgumentException("规则列不存在。");
            if (!double.IsFinite(rule.NullRatio) || rule.NullRatio is < 0 or > 1 || ((!col.IsNullable || col.IsPrimaryKey) && rule.NullRatio > 0)) throw new ArgumentException($"{col.Name} 的空值比例无效。");
            if (rule.Maximum < rule.Minimum || rule.Maximum == long.MaxValue) throw new ArgumentException("数值范围无效。");
        }
        var random = new Random(seed); var rows = new List<DataEditRow>();
        var unique = rules.Where(r => r.Unique).Select(r => r.Column).Concat(table.PrimaryKeyColumns.Count == 1 ? table.PrimaryKeyColumns : Array.Empty<string>()).Distinct().ToDictionary(c => c, _ => new HashSet<string>());
        var keys = foreignKeys ?? Array.Empty<ForeignKeyChoices>();
        foreach (var key in keys) if (key.Values.Rows.Count == 0) throw new InvalidOperationException($"外键 {key.Key.Name} 的引用表没有候选值，请先生成父表。");
        for (int index = 0; index < count; index++)
        {
            ct.ThrowIfCancellationRequested();
            DataEditRow? accepted = null;
            for (int attempt = 0; attempt < 1000 && accepted is null; attempt++)
            {
                var row = new DataEditRow(table.Columns) { State = DataRowState.Added };
                foreach (var column in table.Columns.Where(c => !c.IsReadOnly))
                {
                    var rule = rules.FirstOrDefault(r => r.Column == column.Name) ?? new GenerationRule { Column = column.Name };
                    if (random.NextDouble() < rule.NullRatio) { row[column.Name] = null; continue; }
                    string kind = rule.Kind;
                    if (kind == "自动")
                    {
                        string type = column.DataType.ToLowerInvariant();
                        kind = type.Contains("int") || type.Contains("numeric") || type.Contains("decimal") || type.Contains("real") || type.Contains("float") || type.Contains("double") ? "数值" : type.Contains("date") || type.Contains("time") ? "日期" : type.Contains("bool") || type == "bit" ? "布尔" : type.Contains("uuid") || type.Contains("uniqueidentifier") ? "UUID" : DatabaseInterpreter.Utility.DataTypeHelper.IsBinaryType(type) ? "二进制" : "文本";
                    }
                    row[column.Name] = kind switch
                    {
                        "数值" => random.NextInt64(rule.Minimum, rule.Maximum + 1),
                        "枚举" => Pick(rule.Values.Split('|', StringSplitOptions.RemoveEmptyEntries), random),
                        "正则" => GeneratePattern(rule.Pattern, random),
                        "日期" => new DateTime(2020, 1, 1).AddDays(random.Next(3650)).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                        "布尔" => random.Next(2) == 1,
                        "UUID" => SeededGuid(random),
                        "二进制" => new byte[] { (byte)random.Next(256), (byte)random.Next(256) },
                        "文本" => "v" + random.NextInt64(100000000).ToString(CultureInfo.InvariantCulture),
                        _ => throw new ArgumentException("未知生成类型。"),
                    };
                }
                // A composite foreign key must come from one parent row, never independent column picks.
                foreach (var key in keys)
                {
                    var choice = key.Values.Rows[random.Next(key.Values.Rows.Count)];
                    for (int k = 0; k < key.Key.Columns.Count; k++) row[key.Key.Columns[k].ColumnName] = choice[k];
                }
                if (unique.Any(kv => row[kv.Key] is not null && kv.Value.Contains(Convert.ToString(row[kv.Key], CultureInfo.InvariantCulture)!))) continue;
                foreach (var kv in unique) if (row[kv.Key] is not null) kv.Value.Add(Convert.ToString(row[kv.Key], CultureInfo.InvariantCulture)!);
                accepted = row;
            }
            rows.Add(accepted ?? throw new InvalidOperationException("规则的唯一值空间不足；本批未写入数据库。请扩大范围或降低行数。"));
        }
        return rows;
    }
    private static string SeededGuid(Random random) { var bytes = new byte[16]; random.NextBytes(bytes); return new Guid(bytes).ToString(); }
    private static string Pick(string[] values, Random random) => values.Length > 0 ? values[random.Next(values.Length)] : throw new ArgumentException("枚举值不能为空，用 | 分隔。");

    // Deliberately bounded regular-expression subset. Unsupported syntax fails before any data is written.
    public static string GeneratePattern(string pattern, Random random)
    {
        if (pattern.Length > 512) throw new ArgumentException("正则长度最多 512。");
        var text = new StringBuilder(); int i = 0;
        if (pattern.StartsWith('^')) i++;
        while (i < pattern.Length)
        {
            if (pattern[i] == '$' && i == pattern.Length - 1) break;
            string chars;
            char token = pattern[i++];
            if (token == '[')
            {
                var set = new StringBuilder();
                while (i < pattern.Length && pattern[i] != ']')
                {
                    char start = pattern[i++];
                    if (start is '^' or '\\') throw new ArgumentException("字符集暂不支持取反或转义。");
                    if (i + 1 < pattern.Length && pattern[i] == '-' && pattern[i + 1] != ']')
                    {
                        i++; char end = pattern[i++]; if (end < start || end - start > 128) throw new ArgumentException("字符范围无效。");
                        for (char c = start; c <= end; c++) set.Append(c);
                    }
                    else set.Append(start);
                }
                if (i >= pattern.Length || set.Length == 0) throw new ArgumentException("字符集无效。");
                i++; chars = set.ToString();
            }
            else if (token == '\\')
            {
                if (i >= pattern.Length) throw new ArgumentException("转义不完整。");
                char escaped = pattern[i++];
                chars = escaped switch { 'd' => "0123456789", 'w' => "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789_", 's' => " ", _ when !char.IsLetterOrDigit(escaped) => escaped.ToString(), _ => throw new ArgumentException("不支持此转义。") };
            }
            else if (".*+?()|{}".Contains(token)) throw new ArgumentException("支持字符集、字面量、\\d/\\w 和 {n}/{min,max}；不支持分组、通配或无限重复。");
            else chars = token.ToString();
            int repetitions = 1;
            if (i < pattern.Length && pattern[i] == '{')
            {
                int end = pattern.IndexOf('}', ++i); if (end < 0) throw new ArgumentException("重复次数不完整。");
                var parts = pattern[i..end].Split(',');
                if (parts.Length > 2 || !int.TryParse(parts[0], out int min) || !int.TryParse(parts[^1], out int max) || min < 0 || max < min || max > 128) throw new ArgumentException("重复次数需为 0–128。");
                repetitions = random.Next(min, max + 1); i = end + 1;
            }
            for (int n = 0; n < repetitions; n++) text.Append(chars[random.Next(chars.Length)]);
            if (text.Length > 4096) throw new ArgumentException("生成文本过长。");
        }
        var value = text.ToString();
        if (!Regex.IsMatch(value, "\\A(?:" + pattern + ")\\z", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100))) throw new ArgumentException("无法满足该正则。");
        return value;
    }
}
