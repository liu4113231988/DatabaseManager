using DatabaseManager.FileUtility.Model;
using MiniExcelLibs;
using MiniExcelLibs.OpenXml;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DatabaseManager.FileUtility
{
    public class ExcelReader : BaseReader
    {
        public ExcelReader(SourceFileInfo info) : base(info) { }

        public override DataReadResult Read(bool onlyReadHeader = false)
        {
            if (!string.Equals(Path.GetExtension(info.FilePath), ".xlsx", StringComparison.OrdinalIgnoreCase))
                throw new NotSupportedException("Excel 导入仅支持 .xlsx 文件。");
            var result = new DataReadResult { Data = new Dictionary<int, Dictionary<int, object>>() };
            var configuration = new OpenXmlConfiguration { TrimColumnNames = false };
            foreach (string sheet in MiniExcel.GetSheetNames(info.FilePath))
            {
                bool first = true;
                foreach (IDictionary<string, object> row in MiniExcel.Query(info.FilePath, useHeaderRow: false,
                    sheetName: sheet, configuration: configuration))
                {
                    if (first && info.FirstRowIsColumnName && row.Values.All(value => value is null)) continue;
                    var values = new Dictionary<int, object>();
                    foreach (var entry in row)
                    {
                        int column = 0;
                        foreach (char letter in entry.Key) column = checked(column * 26 + letter - 'A' + 1);
                        values.Add(column - 1, entry.Value);
                    }
                    if (first && info.FirstRowIsColumnName)
                    {
                        var headers = Enumerable.Range(0, values.Keys.Max() + 1)
                            .Select(index => values.TryGetValue(index, out var value) ? Convert.ToString(value) : string.Empty).ToArray();
                        if (result.HeaderColumns is not null && !result.HeaderColumns.SequenceEqual(headers))
                            throw new InvalidDataException($"工作表 {sheet} 的表头与前一工作表不一致，无法合并导入。");
                        result.HeaderColumns = headers;
                    }
                    else if (!onlyReadHeader)
                    {
                        result.Data.Add(result.Data.Count + (info.FirstRowIsColumnName ? 1 : 0), values);
                    }
                    first = false;
                    if (onlyReadHeader) return result;
                }
            }
            return result;
        }

    }
}
