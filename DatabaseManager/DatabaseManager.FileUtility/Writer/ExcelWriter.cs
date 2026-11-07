using DatabaseManager.FileUtility.Model;
using MiniExcelLibs;
using MiniExcelLibs.Attributes;
using MiniExcelLibs.OpenXml;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using S = DocumentFormat.OpenXml.Spreadsheet;
using System.Xml.Linq;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;

namespace DatabaseManager.FileUtility
{
    public class ExcelWriter : BaseWriter
    {
        private readonly ExportDataOption _option;
        private int RowsPerSheet => 1048576 - (_option.ShowColumnNames ? 1 : 0);

        public ExcelWriter(ExportDataOption option)
        {
            _option = option ?? new ExportDataOption();
        }

        public string Write(DataTable dataTable, string tableName = null)
            => WriteFile(dataTable.Columns.Cast<DataColumn>().Select(c => c.ColumnName).ToArray(),
                dataTable.Rows.Count, index => dataTable.Rows[index].ItemArray, tableName);

        public string Write(GridData gridData, string tableName = null)
        {
            if (gridData.Rows is null) return null;
            bool annotated = gridData.Rows.Any(row => row.Cells?.Any(cell => cell.Comment is not null
                || cell.HighlightMode != GridCellHighlightMode.None) == true || row.Comments?.Count > 0);
            return WriteFile(gridData.Columns.Select(c => c.Name).ToArray(), gridData.Rows.Count,
                index => gridData.Rows[index].Cells?.Select(c => c.Content).ToArray() ?? Array.Empty<object>(),
                tableName, annotated ? path => ApplyAnnotations(path, gridData) : null);
        }

        private string WriteFile(string[] names, int rowCount, Func<int, object[]> readRow, string tableName,
            Action<string> annotate = null)
        {
            if (names.Length == 0 || names.Length > 16384)
                throw new ArgumentException("Excel 导出需要 1 到 16384 列。");
            string path = _option.FilePath;
            if (string.IsNullOrEmpty(path))
            {
                string folder = Path.Combine(AssemblyFolder, _option.IsTemporary ? TemporaryFolder : DefaultSaveFolder);
                Directory.CreateDirectory(folder);
                path = Path.Combine(folder, $"{(tableName is null ? "" : tableName + "_")}{DateTime.Now:yyyyMMdd}.xlsx");
            }
            if (!string.Equals(Path.GetExtension(path), ".xlsx", StringComparison.OrdinalIgnoreCase))
                throw new NotSupportedException("Excel 导出仅支持 .xlsx 文件。");

            string temporary = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(path)), $".{Guid.NewGuid():N}.xlsx");
            try
            {
                var columns = names.Select((name, index) => new DynamicExcelColumn("C" + index)
                    { Index = index, Name = name ?? string.Empty }).ToArray();
                var sheets = new Dictionary<string, object>();
                int count = Math.Max(1, (int)Math.Ceiling((double)rowCount / RowsPerSheet));
                for (int sheet = 0; sheet < count; sheet++)
                {
                    int start = sheet * RowsPerSheet;
                    if (rowCount == 0)
                    {
                        var empty = new DataTable();
                        for (int column = 0; column < names.Length; column++)
                            empty.Columns.Add(new DataColumn("C" + column) { Caption = names[column] });
                        sheets.Add("Sheet" + (sheet + 1), empty);
                    }
                    else sheets.Add("Sheet" + (sheet + 1), ReadRows(start, Math.Min(RowsPerSheet, rowCount - start)));
                }
                MiniExcel.SaveAs(temporary, sheets, printHeader: _option.ShowColumnNames, excelType: ExcelType.XLSX,
                    configuration: new OpenXmlConfiguration
                    {
                        DynamicColumns = columns,
                        AutoFilter = false,
                        FreezeRowCount = 0,
                        EnableConvertByteArray = false,
                        EnableWriteFilePath = false,
                    });
                annotate?.Invoke(temporary);
                File.Move(temporary, path, overwrite: true);
                return path;
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }

            IEnumerable<IDictionary<string, object>> ReadRows(int start, int count)
            {
                for (int row = start; row < start + count; row++)
                {
                    var values = readRow(row);
                    if (values.Length > names.Length) throw new InvalidDataException("数据行的列数超过导出列数。");
                    var output = new Dictionary<string, object>();
                    for (int column = 0; column < names.Length; column++)
                    {
                        object value = column < values.Length ? values[column] : null;
                        output.Add("C" + column, value switch
                        {
                            null or DBNull => null,
                            DateTime date => date.ToUniversalTime().ToString(),
                            long number when number > 999999999999999L || number < -999999999999999L
                                => number.ToString(System.Globalization.CultureInfo.InvariantCulture),
                            int or long or float or double or decimal => value,
                            _ => value.ToString(),
                        });
                    }
                    yield return output;
                }
            }
        }

        private void ApplyAnnotations(string path, GridData data)
        {
            // shortcut: 批注后处理使用 DOM；大型带批注导出遇到内存瓶颈时改为流式处理。
            using var document = SpreadsheetDocument.Open(path, true);
            var book = document.WorkbookPart ?? throw new InvalidDataException("工作簿缺少内容。");
            var styles = book.WorkbookStylesPart?.Stylesheet ?? throw new InvalidDataException("工作簿缺少样式表。");
            var fonts = styles.Fonts!;
            var fills = styles.Fills!;
            var formats = styles.CellFormats!;
            uint boldFont = (uint)fonts.ChildElements.Count;
            fonts.Append(new S.Font(new S.Bold()));
            fonts.Count = (uint)fonts.ChildElements.Count;
            uint redFill = (uint)fills.ChildElements.Count;
            fills.Append(new S.Fill(new S.PatternFill(new S.ForegroundColor { Rgb = "FFFF0000" },
                new S.BackgroundColor { Indexed = 64U }) { PatternType = S.PatternValues.Solid }));
            fills.Count = (uint)fills.ChildElements.Count;
            uint backgroundStyle = (uint)formats.ChildElements.Count;
            formats.Append(new S.CellFormat { FontId = 0U, FillId = redFill, BorderId = 0U, ApplyFill = true });
            uint foregroundStyle = (uint)formats.ChildElements.Count;
            formats.Append(new S.CellFormat { FontId = boldFont, FillId = 0U, BorderId = 0U, ApplyFont = true });
            formats.Count = (uint)formats.ChildElements.Count;

            var sheets = book.Workbook.Sheets!.Elements<S.Sheet>().ToArray();
            for (int sheetIndex = 0; sheetIndex < sheets.Length; sheetIndex++)
            {
                var part = (WorksheetPart)book.GetPartById(sheets[sheetIndex].Id!);
                var sheetData = part.Worksheet.GetFirstChild<S.SheetData>()!;
                var rows = sheetData.Elements<S.Row>().ToDictionary(row => row.RowIndex!.Value);
                var comments = new S.CommentList();
                var anchors = new List<(int Row, int Column)>();
                int start = sheetIndex * RowsPerSheet;
                int end = start + Math.Min(RowsPerSheet, data.Rows.Count - start);
                for (int index = start; index < end; index++)
                {
                    var source = data.Rows[index];
                    if (source.Cells is null) continue;
                    uint rowIndex = (uint)(index - start + (_option.ShowColumnNames ? 2 : 1));
                    if (!rows.TryGetValue(rowIndex, out var row))
                    {
                        row = new S.Row { RowIndex = rowIndex };
                        sheetData.InsertBefore(row, sheetData.Elements<S.Row>().FirstOrDefault(r => r.RowIndex!.Value > rowIndex));
                        rows.Add(rowIndex, row);
                    }
                    var cells = row.Elements<S.Cell>().ToDictionary(cell => cell.CellReference!.Value!);
                    for (int column = 0; column < source.Cells.Count; column++)
                    {
                        var original = source.Cells[column];
                        string? comment = original.Comment ?? source.Comments?.LastOrDefault(c => c.ColumnIndexes.Contains(column))?.Comment;
                        if (comment is null && original.HighlightMode == GridCellHighlightMode.None) continue;
                        string reference = ColumnName(column) + rowIndex;
                        if (!cells.TryGetValue(reference, out var cell))
                        {
                            cell = new S.Cell { CellReference = reference };
                            row.InsertBefore(cell, row.Elements<S.Cell>().FirstOrDefault(c => ColumnIndex(c.CellReference!) > column));
                            cells.Add(reference, cell);
                        }
                        if (original.HighlightMode == GridCellHighlightMode.Background) cell.StyleIndex = backgroundStyle;
                        if (original.HighlightMode == GridCellHighlightMode.Foreground) cell.StyleIndex = foregroundStyle;
                        if (comment is not null)
                        {
                            comments.Append(new S.Comment(new S.CommentText(new S.Run(new S.Text(comment)
                            { Space = SpaceProcessingModeValues.Preserve }))) { Reference = reference, AuthorId = 0U });
                            anchors.Add(((int)rowIndex - 1, column));
                        }
                    }
                }
                if (anchors.Count > 0)
                {
                    var notes = part.AddNewPart<WorksheetCommentsPart>();
                    notes.Comments = new S.Comments(new S.Authors(new S.Author("DatabaseManager")), comments);
                    notes.Comments.Save();
                    var drawing = part.AddNewPart<VmlDrawingPart>();
                    WriteNoteDrawing(drawing, anchors);
                    part.Worksheet.AddChild(new S.LegacyDrawing { Id = part.GetIdOfPart(drawing) }, true);
                }
                part.Worksheet.Save();
            }
            styles.Save();
        }

        private static string ColumnName(int column)
        {
            string name = string.Empty;
            for (column++; column > 0; column = (column - 1) / 26)
                name = (char)('A' + (column - 1) % 26) + name;
            return name;
        }

        private static int ColumnIndex(string reference)
        {
            int column = 0;
            foreach (char letter in reference)
            {
                if (letter < 'A' || letter > 'Z') break;
                column = checked(column * 26 + letter - 'A' + 1);
            }
            return column - 1;
        }

        private static void WriteNoteDrawing(VmlDrawingPart part, List<(int Row, int Column)> anchors)
        {
            // 传统 Excel 批注还需要 VML 锚点，只有 comments.xml 无法完整显示批注框。
            XNamespace v = "urn:schemas-microsoft-com:vml", o = "urn:schemas-microsoft-com:office:office",
                x = "urn:schemas-microsoft-com:office:excel";
            var root = new XElement("xml", new XAttribute(XNamespace.Xmlns + "v", v),
                new XAttribute(XNamespace.Xmlns + "o", o), new XAttribute(XNamespace.Xmlns + "x", x),
                new XElement(o + "shapelayout", new XAttribute(v + "ext", "edit"),
                    new XElement(o + "idmap", new XAttribute(v + "ext", "edit"), new XAttribute("data", "1"))),
                new XElement(v + "shapetype", new XAttribute("id", "_x0000_t202"), new XAttribute("coordsize", "21600,21600"),
                    new XAttribute(o + "spt", "202"), new XAttribute("path", "m,l,21600r21600,l21600,xe"),
                    new XElement(v + "stroke", new XAttribute("joinstyle", "miter")),
                    new XElement(v + "path", new XAttribute("gradientshapeok", "t"), new XAttribute(o + "connecttype", "rect"))));
            for (int index = 0; index < anchors.Count; index++)
            {
                var (row, column) = anchors[index];
                int anchorColumn = Math.Min(column, 16380), anchorRow = Math.Min(row, 1048572);
                root.Add(new XElement(v + "shape", new XAttribute("id", "_x0000_s" + (1025 + index)),
                    new XAttribute("type", "#_x0000_t202"),
                    new XAttribute("style", "position:absolute;width:144pt;height:79pt;visibility:hidden"),
                    new XAttribute("fillcolor", "#ffffe1"), new XAttribute(o + "insetmode", "auto"),
                    new XElement(v + "fill", new XAttribute("color2", "#ffffe1")),
                    new XElement(v + "shadow", new XAttribute("on", "t"), new XAttribute("color", "black"), new XAttribute("obscured", "t")),
                    new XElement(v + "path", new XAttribute(o + "connecttype", "none")),
                    new XElement(v + "textbox", new XAttribute("style", "mso-direction-alt:auto"),
                        new XElement("div", new XAttribute("style", "text-align:left"))),
                    new XElement(x + "ClientData", new XAttribute("ObjectType", "Note"), new XElement(x + "MoveWithCells"),
                        new XElement(x + "SizeWithCells"),
                        new XElement(x + "Anchor", $"{anchorColumn}, 15, {anchorRow}, 2, {anchorColumn + 3}, 15, {anchorRow + 3}, 2"),
                        new XElement(x + "AutoFill", "False"), new XElement(x + "Row", row), new XElement(x + "Column", column))));
            }
            using var output = part.GetStream(FileMode.Create, FileAccess.Write);
            root.Save(output);
        }
    }
}
