using DatabaseManager.FileUtility.Model;
using System;
using System.Data;
using System.IO;
using System.Text;
using Newtonsoft.Json;

namespace DatabaseManager.FileUtility
{
    /// <summary>JSON 数据写出器：DataTable 序列化为对象数组（列名为键；DBNull 输出 null）。</summary>
    public class JsonDataWriter : BaseWriter
    {
        private readonly ExportDataOption option;

        public JsonDataWriter(ExportDataOption option)
        {
            this.option = option ?? new ExportDataOption();
        }

        public string Write(DataTable dataTable, string tableName = null)
        {
            string filePath = this.option.FilePath;

            if (string.IsNullOrEmpty(filePath))
            {
                string folder = this.option.IsTemporary ? base.TemporaryFolder : base.DefaultSaveFolder;

                base.CheckFolder(folder);

                filePath = Path.Combine(base.AssemblyFolder, folder, $"{(tableName == null ? "" : $"{tableName}_")}{DateTime.Now.ToString("yyyyMMdd")}.json");
            }

            var encoding = TextEncoding.Resolve(this.option.EncodingName) ?? new UTF8Encoding(false);

            using (var writer = new JsonTextWriter(new StreamWriter(filePath, false, encoding)))
            {
                var serializer = new JsonSerializer() { Formatting = Formatting.Indented };

                writer.WriteStartArray();

                foreach (DataRow row in dataTable.Rows)
                {
                    writer.WriteStartObject();

                    foreach (DataColumn column in dataTable.Columns)
                    {
                        writer.WritePropertyName(column.ColumnName);

                        var value = row[column];

                        if (value == null || value == DBNull.Value)
                        {
                            writer.WriteNull();
                        }
                        else if (value is byte[] bytes)
                        {
                            writer.WriteValue(Convert.ToBase64String(bytes));
                        }
                        else
                        {
                            writer.WriteValue(value);
                        }
                    }

                    writer.WriteEndObject();
                }

                writer.WriteEndArray();
            }

            return filePath;
        }
    }
}
