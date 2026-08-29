using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace DatabaseManager.AppCore.Common;

/// <summary>单表简单 SELECT 解析结果。</summary>
public class SimpleSelectParseResult
{
    public bool IsSimpleSelect { get; init; }

    /// <summary>目标表名（不可编辑时的原因说明）。</summary>
    public string? TableName { get; init; }

    /// <summary>Schema（可为空）。</summary>
    public string? Schema { get; init; }

    /// <summary>不可编辑的原因（IsSimpleSelect 为 false 时有值）。</summary>
    public string? NotEditableReason { get; init; }

    /// <summary>
    /// 输出列名 → 源表列名 的映射（列别名场景，如 SELECT a.col AS X → X → col）。
    /// 未出现在映射中的输出列按同名处理。
    /// </summary>
    public IReadOnlyDictionary<string, string> ColumnAliases { get; init; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// 简单单表 SELECT 语句解析器（用于查询结果内联编辑的可编辑性检测）。
/// 允许 WHERE / ORDER BY / TOP / LIMIT / OFFSET、表别名与列别名；
/// 仍然禁止 JOIN / GROUP BY / UNION / 子查询 / 聚合等导致结果与表行不再一一对应的写法。
/// </summary>
public static class SimpleSelectParser
{
    /// <summary>出现即不可编辑的关键字（按词匹配）。</summary>
    private static readonly string[] ForbiddenKeywords =
    {
        "JOIN", "INNER", "LEFT", "RIGHT", "FULL", "CROSS", "OUTER",
        "GROUP", "HAVING", "UNION", "INTERSECT", "EXCEPT", "MINUS",
        "DISTINCT", "INTO", "OVER", "PARTITION", "WITHIN",
        "EXISTS", "PROCEDURE", "FUNCTION",
    };

    /// <summary>聚合/分析函数名：含此类调用时结果不再与表行一一对应，置为只读。</summary>
    private static readonly string[] ForbiddenAggregates =
    {
        "COUNT", "SUM", "AVG", "MAX", "MIN", "STDDEV", "STDDEV_SAMP", "STDDEV_POP",
        "VARIANCE", "VARIANCE_SAMP", "VAR_SAMP", "VAR_POP", "GROUPING", "STRING_AGG",
        "LISTAGG", "ARRAY_AGG", "MEDIAN", "PERCENTILE_CONT", "PERCENTILE_DISC",
    };

    /// <summary>标识符模式（含 [ ]、双引号、反引号包裹形式）。</summary>
    private const string IdentPattern = @"\[[^\]]+\]|`[^`]+|""[^""]+""|[A-Za-z_][\w$#]*";

    public static SimpleSelectParseResult Parse(string? sql)
    {
        if (string.IsNullOrWhiteSpace(sql))
            return Fail("SQL 为空。");

        // 去除注释与多余空白。
        var text = StripComments(sql).Trim();

        // 多语句不支持（尾部分号允许）。
        var body = text.TrimEnd(';').Trim();
        if (body.Contains(';'))
            return Fail("包含多条语句，无法定位唯一目标表。");

        // 仅支持以 SELECT 开头（CTE/WITH 不支持）。
        if (!Regex.IsMatch(body, @"^\s*SELECT\s", RegexOptions.IgnoreCase))
            return Fail("仅支持 SELECT 语句的结果编辑。");

        // 按词检查禁止关键字（含聚合函数与子查询特征）。
        var tokens = Regex.Matches(body, @"[A-Za-z_][A-Za-z0-9_]*")
            .Select(m => m.Value.ToUpperInvariant())
            .ToList();

        foreach (var keyword in ForbiddenKeywords)
        {
            if (tokens.Contains(keyword))
                return Fail($"包含 {keyword}，结果与表行不再一一对应。");
        }

        foreach (var agg in ForbiddenAggregates)
        {
            if (tokens.Contains(agg) && Regex.IsMatch(body, $@"\b{agg}\s*\(", RegexOptions.IgnoreCase))
                return Fail($"包含聚合函数 {agg}，结果与表行不再一一对应。");
        }
        // 通用表达式/函数列检测：SELECT 列表含 '(' 且非简单列引用时通常为表达式
        // 具体由 ParseColumnAliases 逐项判定，此处仅对明显的“* 聚合”做快速拦截

        if (Regex.IsMatch(body, @"\(\s*SELECT\s", RegexOptions.IgnoreCase))
            return Fail("包含子查询，无法定位唯一目标表。");

        // 提取 FROM 子句的表引用：支持 [db.][schema.]table 三段式（后随空白/结尾；别名不影响单表判定）。
        var fromMatch = Regex.Match(
            body,
            $@"\bFROM\s+(?<ref1>{IdentPattern})((\s*\.\s*)(?<ref2>{IdentPattern}))?((\s*\.\s*)(?<ref3>{IdentPattern}))?(?=\s|$)",
            RegexOptions.IgnoreCase);

        if (!fromMatch.Success)
            return Fail("未找到 FROM 表引用。");

        var tableRefParts = new List<string>();
        if (fromMatch.Groups["ref1"].Success) tableRefParts.Add(fromMatch.Groups["ref1"].Value);
        if (fromMatch.Groups["ref2"].Success) tableRefParts.Add(fromMatch.Groups["ref2"].Value);
        if (fromMatch.Groups["ref3"].Success) tableRefParts.Add(fromMatch.Groups["ref3"].Value);

        var parts = tableRefParts.Select(Unquote).ToList();
        string? schema;
        string tableName;
        if (parts.Count == 3)
        {
            // db.schema.table：忽略数据库名前缀，取中间为 schema
            schema = parts[1];
            tableName = parts[2];
        }
        else if (parts.Count == 2)
        {
            schema = parts[0];
            tableName = parts[1];
        }
        else
        {
            schema = null;
            tableName = parts[0];
        }

        if (string.IsNullOrEmpty(tableName))
            return Fail("表名为空。");

        // 解析输出列别名（SELECT 列表 → 列名映射）。
        var aliases = ParseColumnAliases(body, fromMatch.Index);

        return new SimpleSelectParseResult
        {
            IsSimpleSelect = true,
            Schema = schema,
            TableName = tableName,
            ColumnAliases = aliases,
        };
    }

    /// <summary>
    /// 解析 SELECT 列表，返回「输出列名 → 源列名」映射。
    /// 仅当输出项是简单列引用（可带表别名前缀）时建立映射；表达式/函数列不可编辑故不映射。
    /// </summary>
    private static IReadOnlyDictionary<string, string> ParseColumnAliases(string body, int fromIndex)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (fromIndex <= 7)
        {
            return map;
        }

        var selectList = body[7..fromIndex].Trim();

        // 去掉前导 ALL / TOP n（DISTINCT 已被禁止）。
        selectList = Regex.Replace(selectList, @"^\s*ALL\s+", string.Empty, RegexOptions.IgnoreCase);
        selectList = Regex.Replace(selectList, @"^\s*TOP\s*\(?\s*\d+\s*\)?(\s+PERCENT)?\s+", string.Empty, RegexOptions.IgnoreCase);

        if (string.IsNullOrWhiteSpace(selectList) || selectList.Trim() == "*")
        {
            return map;
        }

        foreach (var rawPart in SplitTopLevel(selectList))
        {
            var part = rawPart.Trim();
            if (part.Length == 0)
            {
                continue;
            }

            // 显式 AS 别名。
            string? alias = null;
            var expr = part;

            var asMatch = Regex.Match(part, $@"\s+AS\s+(?<alias>{IdentPattern})\s*$", RegexOptions.IgnoreCase);
            if (asMatch.Success)
            {
                expr = part[..asMatch.Index].Trim();
                alias = Unquote(asMatch.Groups["alias"].Value);
            }
            else
            {
                // 隐式别名："t.col c" / "col c"（末位裸标识符 + 其余是简单列引用）。
                var implicitMatch = Regex.Match(
                    part,
                    $@"^(?<ref>{IdentPattern})(\s*\.\s*(?<ref2>{IdentPattern}))?\s+(?<alias>{IdentPattern})\s*$",
                    RegexOptions.IgnoreCase);
                if (implicitMatch.Success)
                {
                    var column = implicitMatch.Groups["ref2"].Success
                        ? implicitMatch.Groups["ref2"].Value
                        : implicitMatch.Groups["ref"].Value;
                    map[Unquote(implicitMatch.Groups["alias"].Value)] = Unquote(column);
                    continue;
                }
            }

            // 解析列引用本身（可能有表别名前缀）。
            var colRefMatch = Regex.Match(
                expr,
                $@"^(?<ref>{IdentPattern})(\s*\.\s*(?<ref2>{IdentPattern}))?$",
                RegexOptions.IgnoreCase);

            if (!colRefMatch.Success)
            {
                // 表达式列不可编辑，跳过映射。
                continue;
            }

            var sourceColumn = colRefMatch.Groups["ref2"].Success
                ? colRefMatch.Groups["ref2"].Value
                : colRefMatch.Groups["ref"].Value;

            var outputName = alias ?? Unquote(sourceColumn);
            map[outputName] = Unquote(sourceColumn);
        }

        return map;
    }

    /// <summary>按顶层逗号切分（括号内逗号不切分）。</summary>
    private static List<string> SplitTopLevel(string text)
    {
        var result = new List<string>();
        var current = new System.Text.StringBuilder();
        int depth = 0;

        foreach (var ch in text)
        {
            if (ch == '(')
            {
                depth++;
            }
            else if (ch == ')')
            {
                depth = Math.Max(0, depth - 1);
            }

            if (ch == ',' && depth == 0)
            {
                result.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(ch);
            }
        }

        if (current.Length > 0)
        {
            result.Add(current.ToString());
        }

        return result;
    }

    private static SimpleSelectParseResult Fail(string reason)
        => new() { IsSimpleSelect = false, NotEditableReason = reason };

    /// <summary>去除 SQL 注释（-- 行注释与块注释）。</summary>
    private static string StripComments(string sql)
    {
        var linePattern = new Regex(@"--.*?$", RegexOptions.Multiline);
        var blockPattern = new Regex(@"/\*.*?\*/", RegexOptions.Singleline);
        return linePattern.Replace(blockPattern.Replace(sql, string.Empty), string.Empty);
    }

    private static string Unquote(string identifier)
    {
        identifier = identifier.Trim();
        if (identifier.Length >= 2)
        {
            if (identifier.StartsWith("[") && identifier.EndsWith("]"))
                return identifier[1..^1];
            if (identifier.StartsWith("`") && identifier.EndsWith("`"))
                return identifier[1..^1];
            if (identifier.StartsWith("\"") && identifier.EndsWith("\""))
                return identifier[1..^1];
        }
        return identifier;
    }
}
