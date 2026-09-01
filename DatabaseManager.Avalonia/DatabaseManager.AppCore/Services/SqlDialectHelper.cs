using System.Text.RegularExpressions;
using DatabaseInterpreter.Model;

namespace DatabaseManager.AppCore.Services;

/// <summary>集中处理本轮工具窗口所需的方言标识符与只读 SQL 规则。</summary>
public static class SqlDialectHelper
{
    public static string QuoteQualifiedIdentifier(DatabaseType databaseType, string identifier)
    {
        var parts = identifier.Split('.', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        return string.Join('.', parts.Select(part => QuoteIdentifier(databaseType, part)));
    }

    public static string QuoteIdentifier(DatabaseType databaseType, string identifier) => databaseType switch
    {
        DatabaseType.SqlServer => "[" + identifier.Replace("]", "]]", StringComparison.Ordinal) + "]",
        DatabaseType.MySql => "`" + identifier.Replace("`", "``", StringComparison.Ordinal) + "`",
        _ => "\"" + identifier.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"",
    };

    public static string EscapeLiteral(string value)
        => (value ?? string.Empty).Replace("'", "''", StringComparison.Ordinal);
}

/// <summary>性能剖析仅允许执行单条只读 SELECT，避免把写入操作重复执行。</summary>
public static class SqlSafety
{
    private static readonly Regex SelectStatement = new(@"^SELECT\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex SelectInto = new(@"\bINTO\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    /// <returns>安全时返回 null；否则返回面向用户的拒绝原因。</returns>
    public static string? ValidateProfilerStatement(string sql)
    {
        var statement = (sql ?? string.Empty).Trim();
        if (statement.EndsWith(';'))
        {
            statement = statement[..^1].TrimEnd();
        }

        if (statement.Length == 0 || statement.Contains(';', StringComparison.Ordinal))
        {
            return "性能剖析仅支持一条只读 SELECT 语句。";
        }

        if (!SelectStatement.IsMatch(statement) || SelectInto.IsMatch(statement))
        {
            return "性能剖析仅支持不含 SELECT INTO 的只读 SELECT 语句，不能执行写入、DDL 或多语句脚本。";
        }

        return null;
    }
}
