using System.Globalization;
using System.Text;
using DatabaseManager.AppCore.Models;
using Newtonsoft.Json;

namespace DatabaseManager.AppCore.Common;

/// <summary>查询结果网格"复制为"的目标格式。</summary>
public enum ResultCopyFormat
{
    Csv,
    Tsv,
    Json,
    Markdown,
    Insert,
}

/// <summary>
/// 查询结果"复制为格式"的纯文本格式化器（UI 无关）。
/// 输入为结果列名与行集合（QueryResultRow），输出可直接写入剪贴板的文本。
/// </summary>
public static class ResultCopyFormatter
{
    /// <summary>按指定格式把行集合格式化为文本。</summary>
    /// <param name="columns">列名（显示名）。</param>
    /// <param name="rows">行集合（取列索引位置的字符串值）。</param>
    /// <param name="tableName">INSERT 格式的目标表名（含 Schema 时传 Schema.Name）。</param>
    public static string Format(
        ResultCopyFormat format,
        IReadOnlyList<string> columns,
        IReadOnlyList<QueryResultRow> rows,
        string tableName = "TABLE_NAME")
    {
        ArgumentNullException.ThrowIfNull(columns);
        ArgumentNullException.ThrowIfNull(rows);

        return format switch
        {
            ResultCopyFormat.Csv => FormatCsv(columns, rows),
            ResultCopyFormat.Tsv => FormatTsv(columns, rows),
            ResultCopyFormat.Json => FormatJson(columns, rows),
            ResultCopyFormat.Markdown => FormatMarkdown(columns, rows),
            ResultCopyFormat.Insert => FormatInsert(columns, rows, tableName),
            _ => throw new ArgumentOutOfRangeException(nameof(format)),
        };
    }

    /// <summary>提取每行的列值（字符串形式；缺列补空）。</summary>
    private static IEnumerable<string?[]> GetValues(IReadOnlyList<string> columns, IReadOnlyList<QueryResultRow> rows)
    {
        foreach (var row in rows)
        {
            var values = new string?[columns.Count];
            for (int i = 0; i < columns.Count; i++)
            {
                values[i] = row[i];
            }

            yield return values;
        }
    }

    private static string FormatCsv(IReadOnlyList<string> columns, IReadOnlyList<QueryResultRow> rows)
    {
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(",", columns.Select(EscapeCsv)));
        foreach (var values in GetValues(columns, rows))
        {
            sb.AppendLine(string.Join(",", values.Select(EscapeCsv)));
        }

        return sb.ToString();
    }

    private static string EscapeCsv(string? value)
    {
        var text = value ?? string.Empty;
        if (text.Contains('"') || text.Contains(',') || text.Contains('\r') || text.Contains('\n'))
        {
            return $"\"{text.Replace("\"", "\"\"")}\"";
        }

        return text;
    }

    private static string FormatTsv(IReadOnlyList<string> columns, IReadOnlyList<QueryResultRow> rows)
    {
        // 制表符分隔：便于直接粘贴到 Excel；值内的制表符/换行替换为空格。
        var sb = new StringBuilder();
        sb.AppendLine(string.Join("\t", columns));
        foreach (var values in GetValues(columns, rows))
        {
            sb.AppendLine(string.Join("\t", values.Select(v => (v ?? string.Empty)
                .Replace("\t", " ")
                .Replace("\r", " ")
                .Replace("\n", " "))));
        }

        return sb.ToString();
    }

    private static string FormatJson(IReadOnlyList<string> columns, IReadOnlyList<QueryResultRow> rows)
    {
        // 列名与值统一走 JSON 字符串转义（值统一按字符串输出，避免类型猜测失真）。
        var lines = new List<string>();

        foreach (var values in GetValues(columns, rows))
        {
            var row = new StringBuilder("  {");
            for (int i = 0; i < columns.Count; i++)
            {
                if (i > 0)
                {
                    row.Append(", ");
                }

                row.Append($"{JsonConvert.SerializeObject(columns[i])}: {JsonConvert.SerializeObject(values[i] ?? string.Empty)}");
            }

            row.Append(" }");
            lines.Add(row.ToString());
        }

        return "[" + Environment.NewLine
            + string.Join("," + Environment.NewLine, lines) + Environment.NewLine
            + "]" + Environment.NewLine;
    }

    private static string FormatMarkdown(IReadOnlyList<string> columns, IReadOnlyList<QueryResultRow> rows)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"| {string.Join(" | ", columns.Select(EscapeMarkdown))} |");
        sb.AppendLine($"| {string.Join(" | ", columns.Select(_ => "---"))} |");
        foreach (var values in GetValues(columns, rows))
        {
            sb.AppendLine($"| {string.Join(" | ", values.Select(EscapeMarkdown))} |");
        }

        return sb.ToString();
    }

    private static string EscapeMarkdown(string? value)
        => (value ?? string.Empty)
            .Replace("|", "\\|")
            .Replace("\r\n", " ")
            .Replace("\n", " ");

    private static string FormatInsert(IReadOnlyList<string> columns, IReadOnlyList<QueryResultRow> rows, string tableName)
    {
        var sb = new StringBuilder();
        string columnList = string.Join(", ", columns);

        foreach (var values in GetValues(columns, rows))
        {
            sb.Append($"INSERT INTO {tableName} ({columnList}) VALUES (");
            for (int i = 0; i < values.Length; i++)
            {
                if (i > 0)
                {
                    sb.Append(", ");
                }

                sb.Append(ToSqlLiteral(values[i]));
            }

            sb.AppendLine(");");
        }

        return sb.ToString();
    }

    /// <summary>SQL 字面量：空值→NULL；可解析为数字→原样；否则单引号转义。</summary>
    private static string ToSqlLiteral(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return "NULL";
        }

        if (double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out _))
        {
            return value;
        }

        return $"'{value.Replace("'", "''")}'";
    }
}
