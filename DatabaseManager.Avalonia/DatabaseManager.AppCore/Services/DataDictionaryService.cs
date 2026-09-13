using System.Net;
using System.Text;
using DatabaseInterpreter.Core;
using DatabaseInterpreter.Model;
using DatabaseManager.AppCore.Models;

namespace DatabaseManager.AppCore.Services;

public sealed class DictionaryOptions
{
    public string Title { get; set; } = "数据字典";
    public string Introduction { get; set; } = "";
    public string ObjectTemplate { get; set; } = "{schema}.{name} — {comment}";
    public string ColumnTemplate { get; set; } = "{name} | {type} | 可空:{nullable} | 默认:{default} | {comment}";
    public List<string> Objects { get; set; } = new();
}
public sealed record DictionaryDocument(string Title, IReadOnlyList<string> Lines);
public static class DataDictionaryService
{
    public static async Task<DictionaryDocument> ReadAsync(ConnectionItem connection, DictionaryOptions options, CancellationToken ct)
    {
        var db = DbInterpreterHelper.GetDbInterpreter(ConnectionHelper.ParseDatabaseType(connection.DatabaseType), ConnectionHelper.ToConnectionInfo(connection), new DbInterpreterOption { ThrowExceptionWhenErrorOccurs = true });
        ct.ThrowIfCancellationRequested();
        var schema = await db.GetSchemaInfoAsync(new SchemaInfoFilter { DatabaseObjectType = DatabaseObjectType.Table | DatabaseObjectType.View | DatabaseObjectType.Column | DatabaseObjectType.ForeignKey, ColumnType = ColumnType.TableColumn | ColumnType.ViewColumn });
        ct.ThrowIfCancellationRequested();
        return Build(connection.Database, schema, options);
    }
    public static DictionaryDocument Build(string database, SchemaInfo schema, DictionaryOptions options)
    {
        var lines = new List<string> { options.Title, "数据库：" + database, options.Introduction, "目录" };
        string Name(DatabaseObject obj) => string.IsNullOrEmpty(obj.Schema) ? obj.Name : obj.Schema + "." + obj.Name;
        var objects = schema.Tables.Cast<DatabaseObject>().Concat(schema.Views).Where(t => options.Objects.Count == 0 || options.Objects.Contains(Name(t))).OrderBy(Name).ToArray();
        if (options.Objects.Any(n => !objects.Any(o => Name(o) == n))) throw new ArgumentException("选择的文档对象已不存在。");
        lines.AddRange(objects.Select((t, i) => $"{i + 1}. {Name(t)}"));
        foreach (var obj in objects)
        {
            string comment = obj is Table t ? t.Comment : "视图";
            lines.Add(Expand(options.ObjectTemplate, new() { ["schema"] = obj.Schema ?? "", ["name"] = obj.Name, ["comment"] = comment ?? "" }));
            foreach (var c in schema.TableColumns.Where(c => c.TableName == obj.Name && c.Schema == obj.Schema).OrderBy(c => c.Order))
                lines.Add(Expand(options.ColumnTemplate, new() { ["name"] = c.Name, ["type"] = c.DataType, ["nullable"] = c.IsNullable ? "是" : "否", ["default"] = c.DefaultValue ?? "", ["comment"] = c.Comment ?? "" }));
            foreach (var fk in schema.TableForeignKeys.Where(k => k.TableName == obj.Name && k.Schema == obj.Schema))
                lines.Add($"外键 {fk.Name}：({string.Join(", ", fk.Columns.Select(c => c.ColumnName))}) → {fk.ReferencedSchema}.{fk.ReferencedTableName} ({string.Join(", ", fk.Columns.Select(c => c.ReferencedColumnName))})");
            lines.Add("");
        }
        return new(options.Title, lines);
    }
    private static string Expand(string template, Dictionary<string, string> values)
    {
        if (template.Length > 2000) throw new ArgumentException("模板过长。");
        return System.Text.RegularExpressions.Regex.Replace(template, @"\{([a-z]+)\}", m => values.TryGetValue(m.Groups[1].Value, out var value) ? value : throw new ArgumentException("未知模板变量：" + m.Value));
    }
    public static async Task SaveAsync(DictionaryDocument doc, string path, CancellationToken ct)
    {
        if (!Path.IsPathFullyQualified(path)) throw new ArgumentException("请选择完整输出路径。");
        byte[] content = Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".pdf" => SimpleDocumentPdf.Create(doc.Lines),
            ".html" => Encoding.UTF8.GetBytes("<!doctype html><html lang=\"zh\"><meta charset=\"utf-8\"><title>" + WebUtility.HtmlEncode(doc.Title) + "</title><style>body{font-family:sans-serif;max-width:1100px;margin:40px auto}p{white-space:pre-wrap;overflow-wrap:anywhere;border-bottom:1px solid #ddd;padding:8px}</style><body>" + string.Join("", doc.Lines.Select(l => "<p>" + WebUtility.HtmlEncode(l) + "</p>")) + "</body></html>"),
            _ => throw new ArgumentException("输出格式请选择 .pdf 或 .html。"),
        };
        var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try { await File.WriteAllBytesAsync(temp, content, ct); ct.ThrowIfCancellationRequested(); File.Move(temp, path, true); }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
}

// A text PDF with a standard CJK CID font. Lines wrap and paginate without truncating metadata.
public static class SimpleDocumentPdf
{
    public static byte[] Create(IEnumerable<string> source)
    {
        var lines = new List<string>();
        foreach (var raw in source)
        {
            var line = new StringBuilder(); int width = 0;
            foreach (var rune in (raw ?? "").EnumerateRunes())
            {
                int size = rune.Value > 255 ? 2 : 1;
                if (width + size > 94 || rune.Value == '\n') { lines.Add(line.ToString()); line.Clear(); width = 0; }
                if (rune.Value is '\n' or '\r') continue;
                line.Append(rune.ToString()); width += size;
            }
            lines.Add(line.ToString());
        }
        if (lines.Count == 0) lines.Add("");
        var pages = lines.Chunk(45).ToArray();
        var objects = new List<string> { "<< /Type /Catalog /Pages 2 0 R >>", "", "<< /Type /Font /Subtype /Type0 /BaseFont /STSong-Light /Encoding /UniGB-UCS2-H /DescendantFonts [4 0 R] >>", "<< /Type /Font /Subtype /CIDFontType0 /BaseFont /STSong-Light /CIDSystemInfo << /Registry (Adobe) /Ordering (GB1) /Supplement 4 >> /DW 1000 /W [1 95 500] >>" };
        var pageIds = new List<int>();
        foreach (var page in pages)
        {
            int pageId = objects.Count + 1; pageIds.Add(pageId);
            objects.Add($"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Resources << /Font << /F1 3 0 R >> >> /Contents {pageId + 1} 0 R >>");
            var stream = "BT /F1 10 Tf 16 TL 40 802 Td\n" + string.Join("\n", page.Select(l => "<" + Convert.ToHexString(Encoding.BigEndianUnicode.GetBytes(l)) + "> Tj T*")) + "\nET";
            objects.Add($"<< /Length {Encoding.ASCII.GetByteCount(stream)} >>\nstream\n{stream}\nendstream");
        }
        objects[1] = $"<< /Type /Pages /Count {pageIds.Count} /Kids [{string.Join(" ", pageIds.Select(i => i + " 0 R"))}] >>";
        using var result = new MemoryStream();
        void Write(string value) { var bytes = Encoding.ASCII.GetBytes(value); result.Write(bytes); }
        Write("%PDF-1.4\n"); var offsets = new List<long> { 0 };
        for (int i = 0; i < objects.Count; i++) { offsets.Add(result.Position); Write($"{i + 1} 0 obj\n{objects[i]}\nendobj\n"); }
        long xref = result.Position; Write($"xref\n0 {offsets.Count}\n0000000000 65535 f \n");
        foreach (long offset in offsets.Skip(1)) Write(offset.ToString("D10", System.Globalization.CultureInfo.InvariantCulture) + " 00000 n \n");
        Write($"trailer\n<< /Size {offsets.Count} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n");
        return result.ToArray();
    }
}
