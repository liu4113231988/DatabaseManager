using System.Globalization;
using DatabaseManager.AppCore.Models;

namespace DatabaseManager.AppCore.Services;

public sealed class CalculatedField
{
    public string Name { get; set; } = "";
    public string Expression { get; set; } = "";
}
public static class DashboardTransform
{
    public static QueryResult Apply(QueryResult source, IReadOnlyList<CalculatedField> fields, string? filterColumn = null, string? filterValue = null)
    {
        var columns = source.Columns.ToList();
        if (fields.Count > 20) throw new ArgumentException("每张图表最多 20 个计算字段。");
        foreach (var field in fields)
        {
            if (string.IsNullOrWhiteSpace(field.Name) || columns.Contains(field.Name, StringComparer.OrdinalIgnoreCase)) throw new ArgumentException("计算字段名为空或重复。");
            columns.Add(field.Name);
        }
        var rows = new List<IReadOnlyList<string>>();
        foreach (var sourceRow in source.Rows)
        {
            var values = sourceRow.ToList();
            foreach (var field in fields)
            {
                decimal Read(string name)
                {
                    int index = columns.FindIndex(c => c.Equals(name, StringComparison.OrdinalIgnoreCase));
                    if (index < 0 || index >= values.Count || !decimal.TryParse(values[index], NumberStyles.Float, CultureInfo.InvariantCulture, out var number)) throw new ArgumentException("计算字段引用了不存在、尚未计算或非数值的列：" + name);
                    return number;
                }
                values.Add(Evaluate(field.Expression, Read).ToString(CultureInfo.InvariantCulture));
            }
            int filter = string.IsNullOrEmpty(filterColumn) ? -1 : columns.FindIndex(c => c.Equals(filterColumn, StringComparison.OrdinalIgnoreCase));
            if (filter >= 0 && !string.IsNullOrEmpty(filterValue) && !string.Equals(values[filter], filterValue, StringComparison.Ordinal)) continue;
            rows.Add(values);
        }
        return new QueryResult { Columns = columns, Rows = rows, RowCount = rows.Count, IsTruncated = source.IsTruncated, WarningMessage = source.WarningMessage };
    }
    public static decimal Evaluate(string expression, Func<string, decimal> read)
    {
        if (expression.Length > 1000) throw new ArgumentException("计算表达式过长。");
        int i = 0; int depth = 0;
        void Space() { while (i < expression.Length && char.IsWhiteSpace(expression[i])) i++; }
        decimal Atom()
        {
            Space(); if (++depth > 32) throw new ArgumentException("计算表达式嵌套过深。");
            try
            {
                if (i >= expression.Length) throw new ArgumentException("计算表达式不完整。");
                if (expression[i] is '+' or '-') { bool negative = expression[i++] == '-'; return (negative ? -1 : 1) * Atom(); }
                if (expression[i] == '(') { i++; var n = Sum(); Space(); if (i >= expression.Length || expression[i++] != ')') throw new ArgumentException("括号不匹配。"); return n; }
                if (expression[i] == '[') { int end = expression.IndexOf(']', ++i); if (end < 0) throw new ArgumentException("字段引用不完整。"); var name = expression[i..end]; i = end + 1; return read(name); }
                int start = i; while (i < expression.Length && (char.IsAsciiDigit(expression[i]) || expression[i] == '.')) i++;
                return decimal.TryParse(expression[start..i], NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var value) ? value : throw new ArgumentException("只支持数字、[列名]、四则运算和括号。");
            }
            finally { depth--; }
        }
        decimal Product() { decimal value = Atom(); while (true) { Space(); if (i >= expression.Length || expression[i] is not ('*' or '/')) return value; char op = expression[i++]; decimal rhs = Atom(); value = op == '*' ? value * rhs : rhs != 0 ? value / rhs : throw new ArgumentException("计算字段发生除零。"); } }
        decimal Sum() { decimal value = Product(); while (true) { Space(); if (i >= expression.Length || expression[i] is not ('+' or '-')) return value; char op = expression[i++]; decimal rhs = Product(); value = op == '+' ? value + rhs : value - rhs; } }
        decimal result = Sum(); Space(); if (i != expression.Length) throw new ArgumentException("计算表达式包含不支持的内容。"); return result;
    }
}
