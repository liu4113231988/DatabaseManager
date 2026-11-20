using DatabaseManager.FileUtility.Model;
using System;
using System.Data;
using System.IO;
using System.Text;
using System.Text.Json;

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

        public string Write(DataTable dataTable, string? tableName = null)
        {
            string filePath = this.option.FilePath;

            if (string.IsNullOrEmpty(filePath))
            {
                string folder = this.option.IsTemporary ? base.TemporaryFolder : base.DefaultSaveFolder;

                base.CheckFolder(folder);

                filePath = Path.Combine(base.AssemblyFolder, folder, $"{(tableName == null ? "" : $"{tableName}_")}{DateTime.Now.ToString("yyyyMMdd")}.json");
            }

            var encoding = TextEncoding.Resolve(this.option.EncodingName) ?? new UTF8Encoding(false);

            using (var stream = new MemoryStream())
            {
                using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
                {
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
                                writer.WriteNullValue();
                            }
                            else if (value is byte[] bytes)
                            {
                                writer.WriteStringValue(Convert.ToBase64String(bytes));
                            }
                            else if (value is bool boolValue)
                            {
                                writer.WriteBooleanValue(boolValue);
                            }
                            else if (value is sbyte or byte or short or ushort or int or uint or long or ulong)
                            {
                                writer.WriteNumberValue(Convert.ToInt64(value));
                            }
                            else if (value is float or double or decimal)
                            {
                                writer.WriteNumberValue(Convert.ToDecimal(value));
                            }
                            else if (value is DateTime dateTime)
                            {
                                writer.WriteStringValue(dateTime.ToString("O"));
                            }
                            else
                            {
                                writer.WriteStringValue(Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture));
                            }
                        }

                        writer.WriteEndObject();
                    }

                    writer.WriteEndArray();
                }

                File.WriteAllText(filePath, encoding.GetString(stream.ToArray()), encoding);
            }

            return filePath;
        }
    }
}
