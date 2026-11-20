using DatabaseManager.FileUtility.Model;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace DatabaseManager.FileUtility
{
    /// <summary>
    /// JSON 数据读取器：约定文件为对象数组，或包含首个数组属性的对象（如 {"rows":[...]}）。
    /// 取首条记录的键作为表头列。
    /// </summary>
    public class JsonDataReader : BaseReader
    {
        public JsonDataReader(SourceFileInfo info) : base(info) { }

        public override DataReadResult Read(bool onlyReadHeader = false)
        {
            var result = new DataReadResult();

            var encoding = TextEncoding.Resolve(this.info.EncodingName);
            string text;
            using (var reader = encoding == null
                       ? new StreamReader(this.info.FilePath, detectEncodingFromByteOrderMarks: true)
                       : new StreamReader(this.info.FilePath, encoding))
            {
                text = reader.ReadToEnd();
            }

            using var document = ParseRows(text);
            var rows = document.RootElement;

            var header = new List<string>();

            foreach (var row in rows.EnumerateArray())
            {
                if (row.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                foreach (var prop in row.EnumerateObject())
                {
                    if (!header.Contains(prop.Name, StringComparer.OrdinalIgnoreCase))
                    {
                        header.Add(prop.Name);
                    }
                }

                if (header.Count > 0)
                {
                    break;
                }
            }

            result.HeaderColumns = this.info.FirstRowIsColumnName
                ? header.ToArray()
                : header.Select((_, i) => $"column{i + 1}").ToArray();

            if (!onlyReadHeader)
            {
                var dict = new Dictionary<int, Dictionary<int, object?>>();

                int index = 0;

                foreach (var row in rows.EnumerateArray())
                {
                    var dictRow = new Dictionary<int, object?>();

                    for (int i = 0; i < result.HeaderColumns.Length; i++)
                    {
                        var columnName = this.info.FirstRowIsColumnName ? result.HeaderColumns[i] : header[i];

                        object? value = null;

                        if (row.ValueKind == JsonValueKind.Object && row.TryGetProperty(columnName, out var element))
                        {
                            value = element.ValueKind switch
                            {
                                JsonValueKind.Null or JsonValueKind.Undefined => null,
                                JsonValueKind.String => element.GetString(),
                                JsonValueKind.True => true,
                                JsonValueKind.False => false,
                                JsonValueKind.Number when element.TryGetInt64(out long longValue) => longValue,
                                JsonValueKind.Number when element.TryGetDecimal(out decimal decimalValue) => decimalValue,
                                JsonValueKind.Number => element.GetRawText(),
                                _ => element.GetRawText(),
                            };
                        }

                        dictRow.Add(i, value);
                    }

                    dict.Add(index++, dictRow);
                }

                result.Data = dict;
            }

            return result;
        }

        internal static JsonDocument ParseRows(string text)
        {
            using var document = JsonDocument.Parse(string.IsNullOrEmpty(text) ? "[]" : text);
            var root = document.RootElement;

            if (root.ValueKind == JsonValueKind.Array)
            {
                return JsonDocument.Parse(root.GetRawText());
            }

            if (root.ValueKind == JsonValueKind.Object)
            {
                foreach (var prop in root.EnumerateObject())
                {
                    if (prop.Value.ValueKind == JsonValueKind.Array)
                    {
                        return JsonDocument.Parse(prop.Value.GetRawText());
                    }
                }
            }

            return JsonDocument.Parse("[]");
        }
    }
}
