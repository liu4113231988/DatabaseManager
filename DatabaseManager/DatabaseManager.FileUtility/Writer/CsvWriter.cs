using DatabaseManager.FileUtility.Model;
using System;
using System.Data;
using System.Globalization;
using System.IO;
using System.Text;

namespace DatabaseManager.FileUtility
{
    public class CsvWriter : BaseWriter
    {
        private ExportDataOption option;

        public CsvWriter(ExportDataOption option)
        {
            if (option == null)
            {
                option = new ExportDataOption();
            }

            this.option = option;
        }

        public string Write(DataTable dataTable, string? tableName = null)
        {
            string filePath = this.option.FilePath;

            if (string.IsNullOrEmpty(filePath))
            {
                string folder = this.option.IsTemporary ? base.TemporaryFolder : base.DefaultSaveFolder;

                base.CheckFolder(folder);

                filePath = Path.Combine(base.AssemblyFolder, folder, $"{(tableName == null ? "" : $"{tableName}_")}{DateTime.Now.ToString("yyyyMMdd")}.csv");
            }

            var encoding = TextEncoding.Resolve(this.option.EncodingName) ?? Encoding.UTF8;

            using (StreamWriter sw = new StreamWriter(filePath, false, encoding))
            {
                using var writer = CsvHelper.CsvWriter.Create(sw, new CsvHelper.CsvWriterOptions
                {
                    Separator = ',',
                    FormatProvider = CultureInfo.CurrentCulture,
                }, leaveOpen: true);

                bool showColumnNames = this.option.ShowColumnNames;

                if (showColumnNames)
                {
                    foreach (DataColumn column in dataTable.Columns)
                    {
                        writer.WriteField(column.ColumnName);
                    }

                    writer.EndRecord();
                }

                int count = 0;

                foreach (DataRow row in dataTable.Rows)
                {
                    for (var i = 0; i < dataTable.Columns.Count; i++)
                    {
                        writer.WriteField(row[i]);
                    }

                    if(count< dataTable.Rows.Count)
                    {
                        writer.EndRecord();
                    }                  

                    count++;
                }
            }

            return filePath;
        }
    }
}
