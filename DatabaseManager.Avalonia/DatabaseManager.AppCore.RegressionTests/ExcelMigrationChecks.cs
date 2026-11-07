using System.Data;
using DatabaseManager.FileUtility;
using DatabaseManager.FileUtility.Model;
using MiniExcelLibs;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;
using S = DocumentFormat.OpenXml.Spreadsheet;

internal static class ExcelMigrationChecks
{
    public static void Run()
    {
        string folder = Path.Combine(Path.GetTempPath(), "dbm-miniexcel-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            string path = Path.Combine(folder, "export.xlsx");
            var table = new DataTable();
            table.Columns.Add(" id ", typeof(long));
            table.Columns.Add("text", typeof(string));
            table.Rows.Add(9007199254740993L, "=1+2");
            table.Rows.Add(2L, "中文 ' \" & < >\nsecond line");
            var writer = new ExcelWriter(new ExportDataOption { FilePath = path });
            writer.Write(table);
            var data = Read(path);
            Check(data.HeaderColumns.SequenceEqual(new[] { " id ", "text" }), "Excel headers must retain surrounding spaces.");
            Check((string)data.Data[1][0] == "9007199254740993" && (string)data.Data[1][1] == "=1+2",
                "Long identifiers and formula-looking text must not lose precision or become formulas.");
            Check((string)data.Data[2][1] == (string)table.Rows[1][1], "Unicode, quotes, XML characters and newlines must survive export.");
            var preview = new DatabaseManager.AppCore.Services.DefaultExportImportService(null!).PreviewFileAsync(path).GetAwaiter().GetResult();
            Check(preview.IsSuccess && preview.TotalRows == 2 && preview.SampleRows[0][0] == "9007199254740993",
                "The application's import preview must use the new XLSX reader without changing long identifiers.");
            using (var book = SpreadsheetDocument.Open(path, false))
            {
                var cell = book.WorkbookPart!.WorksheetParts.First().Worksheet.Descendants<S.Cell>().Single(c => c.CellReference == "B2");
                Check(cell.DataType == S.CellValues.String || cell.DataType == S.CellValues.InlineString,
                    "Text beginning with '=' must be stored as text.");
            }

            table.Rows.Clear();
            writer.Write(table);
            Check(Read(path).Data.Count == 0 && Read(path, true).HeaderColumns.SequenceEqual(new[] { " id ", "text" }),
                "Empty result exports must produce a valid workbook with headers and replace existing content.");
            table.Rows.Add(3L, "first data row");
            table.Rows.Add(DBNull.Value, DBNull.Value);
            new ExcelWriter(new ExportDataOption { FilePath = path, ShowColumnNames = false }).Write(table);
            var noHeader = Read(path, header: false);
            Check(noHeader.HeaderColumns is null && noHeader.Data.Count == 2 && Convert.ToInt64(noHeader.Data[0][0]) == 3
                && noHeader.Data[1].Values.All(value => value is null),
                "Exports without headers must retain their first row and all-null records.");

            var grid = new GridData
            {
                Columns = new() { new() { Name = "duplicate" }, new() { Name = "duplicate" }, new() { Name = "" } },
                Rows = new() { new()
                {
                    Cells = new()
                    {
                        new() { Content = "a", Comment = "cell note", HighlightMode = GridCellHighlightMode.Background },
                        new() { Content = "b", HighlightMode = GridCellHighlightMode.Foreground },
                        new() { Content = null },
                    },
                    Comments = new() { new() { Comment = "row note", ColumnIndexes = new() { 1, 2 } } },
                } },
            };
            writer.Write(grid);
            Check(Read(path).HeaderColumns.SequenceEqual(new[] { "duplicate", "duplicate", "" }),
                "Duplicate and blank header labels must not collapse or rename columns.");
            using (var book = SpreadsheetDocument.Open(path, false))
            {
                var part = book.WorkbookPart!.WorksheetParts.First();
                var notes = part.WorksheetCommentsPart!.Comments.CommentList!.Elements<S.Comment>().ToDictionary(c => c.Reference!.Value!);
                Check(notes["A2"].InnerText == "cell note" && notes["B2"].InnerText == "row note" && notes["C2"].InnerText == "row note",
                    "Cell and row notes must remain available, including on null cells.");
                var row = part.Worksheet.Descendants<S.Row>().Single(r => r.RowIndex == 2U);
                var styles = book.WorkbookPart.WorkbookStylesPart!.Stylesheet;
                var red = styles.CellFormats!.Elements<S.CellFormat>().ElementAt((int)row.Elements<S.Cell>().First().StyleIndex!.Value);
                var bold = styles.CellFormats.Elements<S.CellFormat>().ElementAt((int)row.Elements<S.Cell>().ElementAt(1).StyleIndex!.Value);
                Check(styles.Fills!.Elements<S.Fill>().ElementAt((int)red.FillId!.Value).PatternFill!.ForegroundColor!.Rgb == "FFFF0000"
                    && styles.Fonts!.Elements<S.Font>().ElementAt((int)bold.FontId!.Value).Bold is not null,
                    "Background and foreground highlights must remain available.");
                Check(part.VmlDrawingParts.Count() == 1 && part.Worksheet.GetFirstChild<S.LegacyDrawing>() is not null,
                    "Excel notes must include a VML drawing relationship for visible note boxes.");
                Check(!new OpenXmlValidator().Validate(book).Any(), "Annotated spreadsheets must pass Open XML schema validation.");
            }
            byte[] original = File.ReadAllBytes(path);
            grid.Columns.RemoveAt(2);
            bool rejected = false;
            try { writer.Write(grid); }
            catch (InvalidDataException) { rejected = true; }
            Check(rejected && File.ReadAllBytes(path).SequenceEqual(original) && Directory.GetFiles(folder, ".*.xlsx").Length == 0,
                "A failed export must leave the existing file intact and clean up its temporary workbook.");

            var wide = new GridData
            {
                Columns = Enumerable.Range(0, 16384).Select(i => new GridColumn { Name = "c" + i }).ToList(),
                Rows = new() { new() { Cells = Enumerable.Range(0, 16384).Select(_ => new GridCell()).ToList() } },
            };
            wide.Rows[0].Cells[^1].Content = "last column";
            wide.Rows[0].Cells[^1].Comment = "edge note";
            writer.Write(wide);
            using (var book = SpreadsheetDocument.Open(path, false))
            {
                var part = book.WorkbookPart!.WorksheetParts.First();
                Check(part.WorksheetCommentsPart!.Comments.CommentList!.Elements<S.Comment>().Single().Reference == "XFD2",
                    "Annotation column references must handle the final Excel column.");
                using var drawing = part.VmlDrawingParts.Single().GetStream();
                var xml = System.Xml.Linq.XDocument.Load(drawing);
                System.Xml.Linq.XNamespace ns = "urn:schemas-microsoft-com:office:excel";
                int[] anchor = xml.Descendants(ns + "Anchor").Single().Value.Split(',').Select(int.Parse).ToArray();
                Check(xml.Descendants(ns + "Column").Single().Value == "16383" && anchor[0] < anchor[4] && anchor[4] <= 16383,
                    "Final-column notes must have an in-range anchor with a nonzero width.");
                Check(!new OpenXmlValidator().Validate(book).Any(), "Wide annotated output must remain structurally valid.");
            }

            using (var book = SpreadsheetDocument.Create(path, SpreadsheetDocumentType.Workbook))
            {
                var workbook = book.AddWorkbookPart();
                workbook.Workbook = new S.Workbook(new S.Sheets());
                for (int sheet = 0; sheet < 2; sheet++)
                {
                    var page = workbook.AddNewPart<WorksheetPart>();
                    page.Worksheet = new S.Worksheet(new S.SheetData(
                        new S.Row(TextCell("A1", "id"), TextCell("C1", "name"), TextCell("AA1", "wide")) { RowIndex = 1U },
                        new S.Row(new S.Cell { CellReference = "A3", CellValue = new S.CellValue(sheet + 1) },
                            TextCell("C3", "sheet " + sheet), TextCell("AA3", "column AA")) { RowIndex = 3U }));
                    workbook.Workbook.Sheets!.Append(new S.Sheet { Name = "Sheet" + sheet, SheetId = (uint)sheet + 1, Id = workbook.GetIdOfPart(page) });
                    page.Worksheet.Save();
                }
                workbook.Workbook.Save();
            }
            data = Read(path);
            var nonempty = data.Data.Values.Where(row => row[0] is not null).ToArray();
            Check(data.Data.Count == 4 && nonempty.Select(row => (string)row[2]).SequenceEqual(new[] { "sheet 0", "sheet 1" })
                && data.HeaderColumns.Length == 27 && data.HeaderColumns[1] == "" && data.HeaderColumns[2] == "name"
                && data.HeaderColumns[26] == "wide" && nonempty.All(row => (string)row[26] == "column AA"),
                "Sheets must merge without row-key collisions and preserve sparse columns and null records.");
            Check(Read(path, true).Data.Count == 0, "Header preview must not load data rows.");
            string mismatch = Path.Combine(folder, "mismatched.xlsx");
            File.Copy(path, mismatch);
            using (var book = SpreadsheetDocument.Open(mismatch, true))
            {
                var page = book.WorkbookPart!.WorksheetParts.Last();
                page.Worksheet.Descendants<S.Cell>().First().InlineString = new S.InlineString(new S.Text("other_schema"));
                page.Worksheet.Save();
            }
            rejected = false;
            try { Read(mismatch); }
            catch (InvalidDataException) { rejected = true; }
            Check(rejected, "Different sheet schemas must fail explicitly rather than mix columns silently.");

            string legacy = Path.Combine(folder, "legacy.xls");
            File.WriteAllBytes(legacy, new byte[] { 0xD0, 0xCF, 0x11, 0xE0 });
            rejected = false;
            try { Read(legacy); }
            catch (NotSupportedException) { rejected = true; }
            var unsupported = new DatabaseManager.AppCore.Services.DefaultExportImportService(null!).PreviewFileAsync(legacy).GetAwaiter().GetResult();
            Check(rejected && !unsupported.IsSuccess, "Legacy XLS must be rejected explicitly by both reader and import preview.");

            if (Environment.GetEnvironmentVariable("DBM_TEST_LARGE_EXCEL") == "1")
            {
                var row = new GridRow { Cells = new() { new() { Content = "row" } } };
                var large = new GridData { Columns = new() { new() { Name = "value" } }, Rows = Enumerable.Repeat(row, 1048576).ToList() };
                writer.Write(large);
                var names = MiniExcel.GetSheetNames(path).ToArray();
                Check(names.Length == 2 && MiniExcel.Query(path, true, names[0]).Count() == 1048575
                    && MiniExcel.Query(path, true, names[1]).Count() == 1,
                    "Sheet splitting must reserve the header row and must not duplicate records.");
                Console.WriteLine("Large Excel sheet-boundary check passed.");
            }
        }
        finally { Directory.Delete(folder, true); }
        Console.WriteLine("MiniExcel migration checks passed: data fidelity, headers, sheets, Open XML notes/styles, atomic export and XLS rejection.");
    }

    private static S.Cell TextCell(string reference, string value)
        => new() { CellReference = reference, DataType = S.CellValues.InlineString,
            InlineString = new S.InlineString(new S.Text(value) { Space = SpaceProcessingModeValues.Preserve }) };

    private static DataReadResult Read(string path, bool onlyHeader = false, bool header = true)
        => new ExcelReader(new SourceFileInfo { FilePath = path, FirstRowIsColumnName = header }).Read(onlyHeader);

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
