using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace DatabaseManager.AppCore.Models;

/// <summary>
/// 查询结果（AppCore 领域模型，UI 无关）。
/// 封装查询返回的数据表、受影响行数、执行耗时等信息。
/// </summary>
public class QueryResult
{
    /// <summary>列名列表。</summary>
    public IReadOnlyList<string> Columns { get; init; } = System.Array.Empty<string>();

    /// <summary>行数据（每行为列值的字符串化集合）。</summary>
    public IReadOnlyList<IReadOnlyList<string>> Rows { get; init; } = System.Array.Empty<IReadOnlyList<string>>();

    /// <summary>受影响/返回的行数。</summary>
    public int RowCount { get; init; }

    /// <summary>执行耗时（毫秒）。</summary>
    public long ElapsedMilliseconds { get; init; }

    /// <summary>是否为非查询语句（仅受影响行数，无结果集）。</summary>
    public bool IsNonQuery { get; init; }

    /// <summary>错误信息（若执行失败）。</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>是否执行成功。</summary>
    public bool IsSuccess => string.IsNullOrEmpty(ErrorMessage);

    /// <summary>从 DataTable 转换为 UI 无关的查询结果。</summary>
    public static QueryResult FromDataTable(DataTable table, long elapsedMilliseconds)
    {
        var columns = table.Columns.Cast<DataColumn>().Select(c => c.ColumnName).ToList();

        var rows = new List<IReadOnlyList<string>>();
        foreach (DataRow row in table.Rows)
        {
            var values = new List<string>(table.Columns.Count);
            foreach (DataColumn col in table.Columns)
            {
                var value = row[col];
                values.Add(value is null || value == System.DBNull.Value ? string.Empty : value.ToString() ?? string.Empty);
            }
            rows.Add(values);
        }

        return new QueryResult
        {
            Columns = columns,
            Rows = rows,
            RowCount = rows.Count,
            ElapsedMilliseconds = elapsedMilliseconds,
        };
    }
}
