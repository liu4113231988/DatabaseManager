using DatabaseManager.FileUtility.Model;
using System;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace DatabaseManager.FileUtility
{
    /// <summary>XML 数据写出器：输出 &lt;tableName&gt;&lt;row&gt;&lt;column&gt;value&lt;/column&gt;...&lt;/row&gt;...&lt;/tableName&gt;。</summary>
    public class XmlDataWriter : BaseWriter
    {
        private readonly ExportDataOption option;

        public XmlDataWriter(ExportDataOption option)
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

                filePath = Path.Combine(base.AssemblyFolder, folder, $"{(tableName == null ? "" : $"{tableName}_")}{DateTime.Now.ToString("yyyyMMdd")}.xml");
            }

            var encoding = TextEncoding.Resolve(this.option.EncodingName) ?? new UTF8Encoding(false);

            var rootName = string.IsNullOrWhiteSpace(tableName) ? "data" : tableName;
            var root = new XElement(rootName);

            foreach (DataRow row in dataTable.Rows)
            {
                var record = new XElement("row");

                foreach (DataColumn column in dataTable.Columns)
                {
                    var value = row[column];

                    if (value == null || value == DBNull.Value)
                    {
                        record.Add(new XElement(SanitizeName(column.ColumnName), null));
                    }
                    else if (value is byte[] bytes)
                    {
                        record.Add(new XElement(SanitizeName(column.ColumnName), Convert.ToBase64String(bytes)));
                    }
                    else
                    {
                        record.Add(new XElement(SanitizeName(column.ColumnName), value.ToString()));
                    }
                }

                root.Add(record);
            }

            using (var writer = new StreamWriter(filePath, false, encoding))
            {
                root.Save(writer);
            }

            return filePath;
        }

        /// <summary>把列名清理为合法的 XML 元素名（非法字符替换为下划线；空名用 column{n}）。</summary>
        private static string SanitizeName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return "column";
            }

            var chars = name.Trim().Select(c => char.IsLetterOrDigit(c) || c == '_' ? c : '_').ToArray();
            var candidate = new string(chars);

            if (candidate.Length == 0 || char.IsDigit(candidate[0]))
            {
                candidate = "_" + candidate;
            }

            return candidate;
        }
    }
}
