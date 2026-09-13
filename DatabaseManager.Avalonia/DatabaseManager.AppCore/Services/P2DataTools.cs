using System.Globalization;
using System.Text.RegularExpressions;
using DatabaseInterpreter.Core;
using DatabaseInterpreter.Model;
using DatabaseManager.AppCore.Models;

namespace DatabaseManager.AppCore.Services;

public sealed record BuilderTable(string Name, string? Schema, string Alias);
public sealed record BuilderField(string TableAlias, string Column, string Aggregate = "", string Alias = "");
public sealed record BuilderJoin(string Kind, string LeftAlias, string LeftColumn, string RightAlias, string RightColumn);
public sealed class BuilderCondition
{
    public string Logic { get; set; } = "AND";
    public List<BuilderCondition> Children { get; set; } = new();
    public string TableAlias { get; set; } = "";
    public string Column { get; set; } = "";
    public string Operator { get; set; } = "=";
    public string Value { get; set; } = "";
}
public sealed class QueryDesign
{
    public List<BuilderTable> Tables { get; set; } = new();
    public List<BuilderField> Fields { get; set; } = new();
    public List<BuilderJoin> Joins { get; set; } = new();
    public BuilderCondition? Where { get; set; }
    public List<BuilderField> OrderBy { get; set; } = new();
    public bool Descending { get; set; }
}

public static class VisualQueryBuilder
{
    public static string Build(QueryDesign design, DatabaseType type, string? viewName = null, string? viewSchema = null)
    {
        string Q(string value) => string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("名称不能为空。") : SqlDialectHelper.QuoteIdentifier(type, value);
        string Qualified(string? schema, string name) => string.IsNullOrEmpty(schema) ? Q(name) : Q(schema) + "." + Q(name);
        if (design.Tables.Count == 0 || design.Fields.Count == 0) throw new ArgumentException("请选择表和输出字段。");
        var aliases = design.Tables.Select(t => t.Alias).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (aliases.Count != design.Tables.Count) throw new ArgumentException("表别名不能重复。");
        string Col(string alias, string column) => aliases.Contains(alias) ? Q(alias) + "." + Q(column) : throw new ArgumentException("字段引用了不存在的表别名。");
        string Field(BuilderField f)
        {
            var value = Col(f.TableAlias, f.Column);
            if (f.Aggregate.Length > 0)
            {
                if (!new[] { "COUNT", "SUM", "AVG", "MIN", "MAX" }.Contains(f.Aggregate)) throw new ArgumentException("聚合方式无效。");
                value = f.Aggregate + "(" + value + ")";
            }
            return value;
        }
        string Condition(BuilderCondition c, int depth)
        {
            if (depth > 12) throw new ArgumentException("条件分组最多 12 层。");
            if (c.Children.Count > 0)
            {
                if (c.Logic is not ("AND" or "OR")) throw new ArgumentException("条件逻辑无效。");
                return "(" + string.Join(" " + c.Logic + " ", c.Children.Select(x => Condition(x, depth + 1))) + ")";
            }
            var column = Col(c.TableAlias, c.Column);
            if (c.Operator is "IS NULL" or "IS NOT NULL") return column + " " + c.Operator;
            if (!new[] { "=", "<>", ">", ">=", "<", "<=", "LIKE", "NOT LIKE" }.Contains(c.Operator)) throw new ArgumentException("条件运算符无效。");
            string literal = type == DatabaseType.MySql ? "CONVERT(X'" + Convert.ToHexString(System.Text.Encoding.UTF8.GetBytes(c.Value)) + "' USING utf8mb4)"
                : (type == DatabaseType.SqlServer ? "N" : type is DatabaseType.Postgres or DatabaseType.KingbaseES ? "E" : "") + "'" + (type is DatabaseType.Postgres or DatabaseType.KingbaseES ? c.Value.Replace("\\", "\\\\") : c.Value).Replace("'", "''") + "'";
            return column + " " + c.Operator + " " + literal;
        }
        string Table(BuilderTable t) => Qualified(t.Schema, t.Name) + " " + Q(t.Alias);
        var sql = "SELECT " + string.Join(", ", design.Fields.Select(f => Field(f) + (f.Alias.Length > 0 ? " AS " + Q(f.Alias) : ""))) + "\nFROM " + Table(design.Tables[0]);
        var joined = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { design.Tables[0].Alias };
        foreach (var table in design.Tables.Skip(1))
        {
            var joins = design.Joins.Where(j => j.RightAlias == table.Alias).ToList();
            if (joins.Count == 0 || joins.Any(j => !joined.Contains(j.LeftAlias))) throw new ArgumentException("每个新增表需要与已加入的表建立关联。");
            if (joins.Select(j => j.Kind).Distinct().Count() != 1 || joins[0].Kind is not ("INNER" or "LEFT")) throw new ArgumentException("关联类型需统一为 INNER 或 LEFT。");
            sql += "\n" + joins[0].Kind + " JOIN " + Table(table) + " ON " + string.Join(" AND ", joins.Select(j => Col(j.LeftAlias, j.LeftColumn) + " = " + Col(j.RightAlias, j.RightColumn)));
            joined.Add(table.Alias);
        }
        if (design.Where is not null) sql += "\nWHERE " + Condition(design.Where, 0);
        if (design.Fields.Any(f => f.Aggregate.Length > 0) && design.Fields.Any(f => f.Aggregate.Length == 0))
            sql += "\nGROUP BY " + string.Join(", ", design.Fields.Where(f => f.Aggregate.Length == 0).Select(Field));
        if (design.OrderBy.Count > 0) sql += "\nORDER BY " + string.Join(", ", design.OrderBy.Select(Field)) + (design.Descending ? " DESC" : " ASC");
        return (string.IsNullOrWhiteSpace(viewName) ? "" : "CREATE VIEW " + Qualified(viewSchema, viewName) + " AS\n") + sql + ";";
    }
}

public sealed record QualityFinding(int Row, string Column, string Kind, string? Value);
public sealed record QualityColumn(string Column, int Rows, int Nulls, double NullRate, int Duplicates, string? Minimum, string? Maximum, string Format, string Distribution);
public sealed record QualityReport(IReadOnlyList<QualityColumn> Columns, IReadOnlyList<QualityFinding> Findings, bool IsSample);
public static class DataQualityProfiler
{
    public static QualityReport Analyze(IReadOnlyList<string> columns, IReadOnlyList<IReadOnlyList<string?>> rows, bool sample, CancellationToken ct = default)
    {
        var summaries = new List<QualityColumn>(); var findings = new List<QualityFinding>();
        for (int col = 0; col < columns.Count; col++)
        {
            ct.ThrowIfCancellationRequested();
            var values = rows.Select((r, i) => (Value: r[col], Row: i + 1)).ToArray();
            var nonNull = values.Where(v => v.Value is not null).ToArray();
            var groups = nonNull.GroupBy(v => v.Value!, StringComparer.Ordinal).OrderByDescending(g => g.Count()).ToArray();
            foreach (var value in values.Where(v => v.Value is null)) findings.Add(new(value.Row, columns[col], "空值", null));
            foreach (var group in groups.Where(g => g.Count() > 1)) foreach (var value in group) findings.Add(new(value.Row, columns[col], "重复值", value.Value));
            var numeric = nonNull.Select(v => (v, Parsed: double.TryParse(v.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) && double.IsFinite(d), Number: double.TryParse(v.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var n) ? n : 0)).ToArray();
            string format = "文本"; string? min = groups.Select(g => g.Key).Order(StringComparer.Ordinal).FirstOrDefault(), max = groups.Select(g => g.Key).Order(StringComparer.Ordinal).LastOrDefault();
            if (nonNull.Length > 0 && numeric.All(v => v.Parsed))
            {
                format = "数值"; var sorted = numeric.Select(v => v.Number).Order().ToArray();
                min = sorted[0].ToString(CultureInfo.InvariantCulture); max = sorted[^1].ToString(CultureInfo.InvariantCulture);
                double Quantile(double percentile) { double position = (sorted.Length - 1) * percentile; int low = (int)position, high = (int)Math.Ceiling(position); return sorted[low] + (sorted[high] - sorted[low]) * (position - low); }
                double q1 = Quantile(.25), q3 = Quantile(.75), iqr = q3 - q1;
                foreach (var v in numeric.Where(v => v.Number < q1 - 1.5 * iqr || v.Number > q3 + 1.5 * iqr)) findings.Add(new(v.v.Row, columns[col], "异常值（IQR）", v.v.Value));
            }
            if (nonNull.Length > 0)
            {
                var patterns = new[] { ("邮箱", @"^[^\s@]+@[^\s@]+\.[^\s@]+$"), ("手机号", @"^1[3-9]\d{9}$"), ("日期", @"^\d{4}-\d{2}-\d{2}(?:[ T].*)?$") };
                foreach (var (name, pattern) in patterns)
                {
                    var regex = new Regex(pattern, RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
                    if (nonNull.Count(v => regex.IsMatch(v.Value!)) < nonNull.Length * .8) continue;
                    format = name;
                    findings.RemoveAll(f => f.Column == columns[col] && f.Kind == "异常值（IQR）");
                    foreach (var v in nonNull.Where(v => !regex.IsMatch(v.Value!))) findings.Add(new(v.Row, columns[col], "格式不一致", v.Value));
                    break;
                }
            }
            int nulls = values.Length - nonNull.Length;
            summaries.Add(new(columns[col], rows.Count, nulls, rows.Count == 0 ? 0 : (double)nulls / rows.Count, nonNull.Length - groups.Length, min, max, format,
                string.Join("; ", groups.Take(10).Select(g => $"{g.Key}: {g.Count()}"))));
        }
        return new(summaries, findings, sample);
    }
}

public sealed class MaskRule
{
    public string Column { get; set; } = "";
    public string Kind { get; set; } = "手机号";
    public string Pattern { get; set; } = "";
    public string Replacement { get; set; } = "***";
}
public static class DataMasker
{
    public static string? Mask(string? value, MaskRule rule)
    {
        if (value is null || value.Length == 0) return value;
        return rule.Kind switch
        {
            "手机号" => Keep(value, 3, 4),
            "证件" => Keep(value, 3, 2),
            "银行卡" => Keep(value, 0, 4),
            "全部" => "***",
            "自定义" when !string.IsNullOrEmpty(rule.Pattern) => Regex.Replace(value, rule.Pattern, rule.Replacement, RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100)),
            _ => throw new ArgumentException("脱敏规则无效。"),
        };
    }
    private static string Keep(string value, int left, int right) => value.Length <= left + right ? new string('*', value.Length) : value[..left] + new string('*', value.Length - left - right) + (right == 0 ? "" : value[^right..]);
    public static QueryResult Apply(QueryResult result, IReadOnlyList<MaskRule> rules)
    {
        if (rules.GroupBy(r => r.Column, StringComparer.OrdinalIgnoreCase).Any(g => g.Count() > 1)) throw new ArgumentException("同一列不能配置多条脱敏规则。");
        foreach (var rule in rules) if (!result.Columns.Contains(rule.Column, StringComparer.OrdinalIgnoreCase)) throw new ArgumentException($"结果中没有列：{rule.Column}");
        var mapped = result.Columns.Select(c => rules.FirstOrDefault(r => r.Column.Equals(c, StringComparison.OrdinalIgnoreCase))).ToArray();
        return new QueryResult { Columns = result.Columns.ToArray(), Rows = result.Rows.Select(row => (IReadOnlyList<string>)row.Select((v, i) => mapped[i] is null ? v : Mask(v, mapped[i]!)!).ToArray()).ToArray(), RowCount = result.RowCount, IsTruncated = result.IsTruncated, WarningMessage = result.WarningMessage };
    }
}
