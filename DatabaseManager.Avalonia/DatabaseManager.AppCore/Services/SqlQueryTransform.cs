using System.Text.RegularExpressions;

namespace DatabaseManager.AppCore.Services;

/// <summary>只读查询的受控转换工具。</summary>
public static class SqlQueryTransform
{
    /// <summary>为不带排序的单条 SELECT 添加按结果列序号排序的子句。</summary>
    public static string AppendOrdinalOrderBy(string sql, int ordinal, bool descending)
    {
        if (string.IsNullOrWhiteSpace(sql))
            throw new ArgumentException("SQL 为空。", nameof(sql));
        if (ordinal < 1)
            throw new ArgumentOutOfRangeException(nameof(ordinal));

        var statement = sql.Trim().TrimEnd(';').Trim();
        if (!Regex.IsMatch(statement, @"^SELECT\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)
            || statement.Contains(';', StringComparison.Ordinal)
            || Regex.IsMatch(statement, @"\bORDER\s+BY\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
        {
            throw new ArgumentException("服务端排序仅支持不含已有 ORDER BY 的单条 SELECT；可在 SQL 编辑器中自行调整复杂排序。", nameof(sql));
        }

        return $"{statement}{Environment.NewLine}ORDER BY {ordinal} {(descending ? "DESC" : "ASC")};";
    }
}
