using DatabaseManager.FileUtility.Model;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;

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

            JArray rows = ParseRows(text);

            var header = new List<string>();

            foreach (var row in rows)
            {
                if (row is JObject obj)
                {
                    foreach (var prop in obj.Properties())
                    {
                        if (!header.Contains(prop.Name, System.StringComparer.OrdinalIgnoreCase))
                        {
                            header.Add(prop.Name);
                        }
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
                var dict = new Dictionary<int, Dictionary<int, object>>();

                int index = 0;

                foreach (var row in rows)
                {
                    var dictRow = new Dictionary<int, object>();

                    for (int i = 0; i < result.HeaderColumns.Length; i++)
                    {
                        var columnName = this.info.FirstRowIsColumnName ? result.HeaderColumns[i] : header[i];

                        object value = null;

                        if (row is JObject obj && obj.TryGetValue(columnName, out var token))
                        {
                            if (token is JValue jValue)
                            {
                                value = jValue.Type == JTokenType.Null ? null : jValue.Value;
                            }
                            else
                            {
                                value = token.ToString();
                            }
                        }

                        dictRow.Add(i, value);
                    }

                    dict.Add(index++, dictRow);
                }

                result.Data = dict;
            }

            return result;
        }

        internal static JArray ParseRows(string text)
        {
            var token = JToken.Parse(text ?? "[]");

            if (token is JArray array)
            {
                return array;
            }

            // 对象包装：取第一个数组属性（如 {"rows":[...]}）。
            if (token is JObject obj)
            {
                foreach (var prop in obj.Properties())
                {
                    if (prop.Value is JArray propArray)
                    {
                        return propArray;
                    }
                }
            }

            return new JArray();
        }
    }
}
