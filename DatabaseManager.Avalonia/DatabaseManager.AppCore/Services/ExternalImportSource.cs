using System.Data.Odbc;
using System.Globalization;
using System.Text;

namespace DatabaseManager.AppCore.Services;

public static class ExternalImportSource
{
    public static string AccessConnectionString(string path, string driver)
    {
        if (!File.Exists(path) || !new[] { ".mdb", ".accdb" }.Contains(Path.GetExtension(path).ToLowerInvariant())) throw new ArgumentException("请选择有效的 Access 文件。");
        if (string.IsNullOrWhiteSpace(driver)) throw new ArgumentException("请填写已安装的 Access ODBC 驱动名。");
        var builder = new OdbcConnectionStringBuilder { Driver = driver };
        builder["DBQ"] = Path.GetFullPath(path); builder["ReadOnly"] = "1";
        return builder.ConnectionString;
    }
    public static async Task<int> SnapshotOdbcAsync(string connectionString, string select, string path, CancellationToken ct)
    {
        var reason = SqlSafety.ValidateProfilerStatement(select);
        if (reason is not null) throw new ArgumentException(reason);
        return await Task.Run(async () =>
        {
            using var db = new OdbcConnection(connectionString);
            await db.OpenAsync(ct);
            using var command = db.CreateCommand(); command.CommandTimeout = 60; command.CommandText = select;
            using var cancel = ct.Register(() => { try { command.Cancel(); } catch { } });
            using var reader = await command.ExecuteReaderAsync(ct);
            var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                await using var writer = new StreamWriter(temp, false, new UTF8Encoding(true));
                var names = Enumerable.Range(0, reader.FieldCount).Select(reader.GetName).ToArray();
                if (names.Distinct(StringComparer.OrdinalIgnoreCase).Count() != names.Length) throw new ArgumentException("源查询存在同名列，请设置唯一别名。");
                await writer.WriteLineAsync(Csv(names)); int rows = 0;
                while (await reader.ReadAsync(ct))
                {
                    if (++rows > 1000000) throw new InvalidOperationException("单个快照最多 100 万行，请按主键范围拆分源查询。");
                    await writer.WriteLineAsync(Csv(Enumerable.Range(0, reader.FieldCount).Select(i => reader.IsDBNull(i) ? "" : reader.GetValue(i) is byte[] bytes ? "0x" + Convert.ToHexString(bytes) : Convert.ToString(reader.GetValue(i), CultureInfo.InvariantCulture) ?? "")));
                }
                await writer.FlushAsync(ct); await writer.DisposeAsync(); ct.ThrowIfCancellationRequested(); File.Move(temp, path, true); return rows;
            }
            finally { if (File.Exists(temp)) File.Delete(temp); }
        }, ct);
    }
    public static async Task<int> SnapshotDbfAsync(string input, string output, string encodingName, CancellationToken ct)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var encoding = Encoding.GetEncoding(encodingName, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);
        await using var stream = File.OpenRead(input); using var reader = new BinaryReader(stream, encoding, true);
        var header = reader.ReadBytes(32);
        if (header.Length != 32 || header[0] is not (0x03 or 0x83)) throw new ArgumentException("仅支持 dBASE III DBF；FoxPro/其他版本请通过 ODBC 导入。");
        int count = BitConverter.ToInt32(header, 4), headerLength = BitConverter.ToUInt16(header, 8), recordLength = BitConverter.ToUInt16(header, 10);
        if (count < 0 || count > 1000000 || headerLength < 33 || recordLength < 2 || (long)headerLength + (long)count * recordLength > stream.Length) throw new ArgumentException("DBF 头或记录长度无效。");
        var fields = new List<(string Name, char Type, int Length)>();
        while (stream.Position < headerLength - 1)
        {
            ct.ThrowIfCancellationRequested(); var field = reader.ReadBytes(32);
            if (field.Length != 32) throw new ArgumentException("DBF 字段描述不完整。");
            var name = encoding.GetString(field, 0, 11).Split('\0')[0].Trim(); char type = (char)field[11];
            if (type is not ('C' or 'N' or 'F' or 'L' or 'D')) throw new ArgumentException($"DBF 字段 {name} 的类型 {type} 暂不支持；Memo/二进制请使用 ODBC 驱动。");
            if (string.IsNullOrEmpty(name) || field[16] == 0) throw new ArgumentException("DBF 字段无效。");
            fields.Add((name, type, field[16]));
        }
        if (fields.Count == 0 || fields.Sum(f => f.Length) + 1 != recordLength || fields.Select(f => f.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count() != fields.Count) throw new ArgumentException("DBF 列布局无效。");
        stream.Position = headerLength;
        var temp = output + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await using var writer = new StreamWriter(temp, false, new UTF8Encoding(true));
            await writer.WriteLineAsync(Csv(fields.Select(f => f.Name))); int written = 0;
            for (int r = 0; r < count; r++)
            {
                ct.ThrowIfCancellationRequested(); var record = reader.ReadBytes(recordLength);
                if (record[0] == '*') continue;
                if (record[0] != ' ') throw new ArgumentException("DBF 记录标记无效。");
                int pos = 1; var values = new List<string>();
                foreach (var f in fields)
                {
                    var value = encoding.GetString(record, pos, f.Length).Trim(); pos += f.Length;
                    if (f.Type == 'D' && value.Length > 0) value = DateTime.ParseExact(value, "yyyyMMdd", CultureInfo.InvariantCulture).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                    if (f.Type == 'L') value = value.ToUpperInvariant() switch { "Y" or "T" => "true", "N" or "F" => "false", "?" or "" => "", _ => throw new ArgumentException("DBF 布尔值无效。") };
                    values.Add(value);
                }
                await writer.WriteLineAsync(Csv(values)); written++;
            }
            await writer.FlushAsync(ct); await writer.DisposeAsync(); ct.ThrowIfCancellationRequested(); File.Move(temp, output, true); return written;
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
    public static string Csv(IEnumerable<string?> values) => string.Join(",", values.Select(v => "\"" + (v ?? "").Replace("\"", "\"\"") + "\""));
}
