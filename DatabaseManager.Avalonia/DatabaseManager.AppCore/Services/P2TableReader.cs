using System.Globalization;
using DatabaseInterpreter.Core;
using DatabaseInterpreter.Model;
using DatabaseManager.AppCore.Models;

namespace DatabaseManager.AppCore.Services;

public sealed record TableSample(IReadOnlyList<string> Columns, IReadOnlyList<IReadOnlyList<string?>> Rows, bool IsSample)
{
    public QueryResult ToResult() => new() { Columns = Columns, Rows = Rows.Select(r => (IReadOnlyList<string>)r.Select(v => v!).ToArray()).ToArray(), RowCount = Rows.Count, IsTruncated = IsSample, WarningMessage = IsSample ? "仅包含限定行数的样本。" : null };
}
public static class P2TableReader
{
    public static async Task<TableSample> ReadAsync(ConnectionItem connection, string table, string? schema, int limit, CancellationToken ct)
    {
        if (limit is < 1 or > 100000) throw new ArgumentException("采样上限为 1–100000 行。");
        var interpreter = DbInterpreterHelper.GetDbInterpreter(ConnectionHelper.ParseDatabaseType(connection.DatabaseType), ConnectionHelper.ToConnectionInfo(connection), new DbInterpreterOption { ThrowExceptionWhenErrorOccurs = true });
        using var db = interpreter.CreateConnection(); await db.OpenAsync(ct);
        using var cmd = db.CreateCommand(); cmd.CommandTimeout = 60;
        var type = ConnectionHelper.ParseDatabaseType(connection.DatabaseType);
        cmd.CommandText = "SELECT * FROM " + (string.IsNullOrEmpty(schema) ? "" : SqlDialectHelper.QuoteIdentifier(type, schema) + ".") + SqlDialectHelper.QuoteIdentifier(type, table);
        using var reader = await cmd.ExecuteReaderAsync(ct);
        var columns = Enumerable.Range(0, reader.FieldCount).Select(reader.GetName).ToArray();
        var rows = new List<IReadOnlyList<string?>>(); long characters = 0;
        while (await reader.ReadAsync(ct))
        {
            if (rows.Count == limit) return new(columns, rows, true);
            if ((long)(rows.Count + 1) * reader.FieldCount > 2000000) throw new InvalidOperationException("样本超过 200 万单元格，请降低采样行数。");
            var values = new string?[reader.FieldCount];
            for (int i = 0; i < values.Length; i++)
            {
                values[i] = reader.IsDBNull(i) ? null : reader.GetValue(i) is byte[] bytes ? "0x" + Convert.ToHexString(bytes) : Convert.ToString(reader.GetValue(i), CultureInfo.InvariantCulture);
                characters += values[i]?.Length ?? 0;
                if (characters > 16 * 1024 * 1024) throw new InvalidOperationException("样本超过约 32 MiB 文本预算，请降低采样行数。");
            }
            rows.Add(values);
        }
        return new(columns, rows, false);
    }
}
