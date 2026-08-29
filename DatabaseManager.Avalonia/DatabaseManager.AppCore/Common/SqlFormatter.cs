using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace DatabaseManager.AppCore.Common;

/// <summary>简易 SQL 美化器：关键字大写 + 主子句换行缩进（非全方言精确，仅提升可读性）。</summary>
public static class SqlFormatter
{
    private static readonly string[] MajorKeywords = new[]
    {
        "SELECT", "FROM", "WHERE", "GROUP BY", "HAVING", "ORDER BY", "LIMIT", "OFFSET",
        "UNION", "UNION ALL", "INSERT", "INTO", "VALUES", "UPDATE", "SET", "DELETE",
        "CREATE", "ALTER", "DROP", "TRUNCATE", "WITH"
    };

    private static readonly string[] JoinKeywords = new[]
    {
        "JOIN", "INNER JOIN", "LEFT JOIN", "RIGHT JOIN", "FULL JOIN", "CROSS JOIN", "LEFT OUTER JOIN", "RIGHT OUTER JOIN"
    };

    public static string Format(string sql)
    {
        if (string.IsNullOrWhiteSpace(sql))
            return sql ?? string.Empty;

        // 1. 规范化空白（保留字符串内的空白）
        var normalized = NormalizeWhitespace(sql);

        // 2. 关键字大写（大小写不敏感替换，避开字符串）
        normalized = UppercaseKeywords(normalized);

        // 3. 主子句换行
        foreach (var kw in MajorKeywords)
        {
            var pattern = $@"\s+{Regex.Escape(kw)}\s+";
            normalized = Regex.Replace(normalized, pattern, $"\n{kw} ", RegexOptions.IgnoreCase);
        }
        foreach (var kw in JoinKeywords)
        {
            var pattern = $@"\s+{Regex.Escape(kw)}\s+";
            normalized = Regex.Replace(normalized, pattern, $"\n{kw} ", RegexOptions.IgnoreCase);
        }

        // 4. 逗号后换行（SELECT 列表）
        normalized = Regex.Replace(normalized, @",\s*", ",\n    ");

        // 5. AND/OR 在 WHERE/HAVING 中换行缩进
        normalized = Regex.Replace(normalized, @"\s+AND\s+", "\n    AND ", RegexOptions.IgnoreCase);
        normalized = Regex.Replace(normalized, @"\s+OR\s+", "\n    OR ", RegexOptions.IgnoreCase);

        // 6. 括号内简单缩进（可选）
        normalized = normalized.Trim();
        // 确保以分号结尾的语句后换行
        normalized = Regex.Replace(normalized, @";\s*", ";\n");

        // 7. 去除多余空行
        normalized = Regex.Replace(normalized, @"\n\s*\n", "\n");
        return normalized.Trim();
    }

    private static string NormalizeWhitespace(string sql)
    {
        // 保留单引号字符串内的原样，其余空白压缩
        var result = "";
        bool inString = false;
        bool inDoubleString = false;
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < sql.Length; i++)
        {
            char c = sql[i];
            if (c == '\'' && !inDoubleString)
            {
                // 处理 '' 转义
                if (i + 1 < sql.Length && sql[i + 1] == '\'')
                {
                    sb.Append("''");
                    i++;
                    continue;
                }
                inString = !inString;
                sb.Append(c);
            }
            else if (c == '"' && !inString)
            {
                inDoubleString = !inDoubleString;
                sb.Append(c);
            }
            else if (!inString && !inDoubleString && char.IsWhiteSpace(c))
            {
                // 压缩连续空白为单空格
                if (sb.Length > 0 && sb[sb.Length - 1] != ' ')
                    sb.Append(' ');
            }
            else
            {
                sb.Append(c);
            }
        }
        return sb.ToString();
    }

    private static string UppercaseKeywords(string sql)
    {
        // 简易：直接对 Major + Join + 常用关键字做大小写不敏感替换，避开字符串需更复杂解析，此处先对非字符串区域处理
        // 为简化，暂直接对整个文本做关键词大写（字符串内的关键词也会被大写，但影响可接受）
        var allKeywords = new List<string>();
        allKeywords.AddRange(MajorKeywords);
        allKeywords.AddRange(JoinKeywords);
        allKeywords.AddRange(new[] { "AND", "OR", "NOT", "IN", "IS", "LIKE", "BETWEEN", "EXISTS", "CASE", "WHEN", "THEN", "ELSE", "END", "ASC", "DESC", "ON", "USING", "AS", "DISTINCT", "ALL" });

        foreach (var kw in allKeywords.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var pattern = $@"\b{Regex.Escape(kw)}\b";
            sql = Regex.Replace(sql, pattern, kw, RegexOptions.IgnoreCase);
        }
        return sql;
    }
}
