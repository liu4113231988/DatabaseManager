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
}

/// <summary>
/// 简单单表 SELECT 语句解析器（保守判定，用于查询结果内联编辑的可编辑性检测）。
/// 仅当 SELECT 来自单一表（无 JOIN/子查询/聚合等）时返回表名；任何不确定的情况都判为不可编辑（安全侧）。
/// </summary>
public static class SimpleSelectParser
{
    /// <summary>出现即不可编辑的关键字（按词匹配）。</summary>
    private static readonly string[] ForbiddenKeywords =
    {
        "JOIN", "INNER", "LEFT", "RIGHT", "FULL", "CROSS", "OUTER",
        "GROUP", "HAVING", "UNION", "INTERSECT", "EXCEPT", "MINUS",
        "DISTINCT", "ALL", "INTO", "OVER", "PARTITION", "WITHIN",
        "CASE", "EXISTS", "PROCEDURE", "FUNCTION",
    };

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

        if (Regex.IsMatch(body, @"\(\s*SELECT\s", RegexOptions.IgnoreCase))
            return Fail("包含子查询，无法定位唯一目标表。");

        // 提取 FROM 子句的表引用：FROM [schema.]table（后随空白/结尾；别名不影响单表判定）。
        var fromMatch = Regex.Match(
            body,
            @"\bFROM\s+(?<ref>\[[^\]]+\]|`[^`]+|""[^""]+""|[A-Za-z_][\w$#]*)((\s*\.\s*)(?<ref2>\[[^\]]+\]|`[^`]+|""[^""]+""|[A-Za-z_][\w$#]*))?(?=\s|$)",
            RegexOptions.IgnoreCase);

        if (!fromMatch.Success)
            return Fail("未找到 FROM 表引用。");

        // 有 schema 时 ref 为 schema、ref2 为表名；否则 ref 即表名。
        var tableRefParts = new List<string>();
        if (fromMatch.Groups["ref"].Success) tableRefParts.Add(fromMatch.Groups["ref"].Value);
        if (fromMatch.Groups["ref2"].Success) tableRefParts.Add(fromMatch.Groups["ref2"].Value);

        var parts = tableRefParts.Select(Unquote).ToList();
        string? schema = parts.Count == 2 ? parts[0] : null;
        string tableName = parts[^1];

        if (string.IsNullOrEmpty(tableName))
            return Fail("表名为空。");

        return new SimpleSelectParseResult
        {
            IsSimpleSelect = true,
            Schema = schema,
            TableName = tableName,
        };
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
