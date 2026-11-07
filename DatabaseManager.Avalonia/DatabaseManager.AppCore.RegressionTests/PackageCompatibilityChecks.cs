using System.Data;
using System.IO.Compression;
using DatabaseManager.FileUtility;
using DatabaseManager.FileUtility.Model;

internal static class PackageCompatibilityChecks
{
    public static void Run()
    {
        var folder = Path.Combine(Path.GetTempPath(), "dbm-packages-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            var table = new DataTable();
            table.Columns.Add("id", typeof(int));
            table.Columns.Add("text", typeof(string));
            table.Rows.Add(1, "中文, \"quoted\"\nsecond line");
            table.Rows.Add(2, "ordinary");
            foreach (var extension in new[] { "csv", "xlsx" })
            {
                string path = Path.Combine(folder, "sample." + extension);
                var option = new ExportDataOption { FilePath = path, ShowColumnNames = true, EncodingName = "UTF-8" };
                if (extension == "csv") new CsvWriter(option).Write(table);
                else new ExcelWriter(option).Write(table);
                var source = new SourceFileInfo { FilePath = path, FirstRowIsColumnName = true, EncodingName = "UTF-8" };
                var result = extension == "csv" ? new CsvReader(source).Read() : new ExcelReader(source).Read();
                Check(result.HeaderColumns.SequenceEqual(new[] { "id", "text" }) && result.Data.Count == 2,
                    "Upgraded file readers must preserve headers and row counts: " + extension);
                Check(result.Data.Values.Select(row => Convert.ToString(row[1])).SequenceEqual(table.Rows.Cast<DataRow>().Select(row => (string)row[1])),
                    "CSV and Excel round trips must preserve Unicode, quotes, commas and line breaks.");
                var header = extension == "csv" ? new CsvReader(source).Read(true) : new ExcelReader(source).Read(true);
                Check(header.HeaderColumns.SequenceEqual(new[] { "id", "text" }), "Header-only reads must remain supported.");
            }

            string noHeader = Path.Combine(folder, "no-header.csv");
            File.WriteAllText(noHeader, "1,alpha\n2,beta\n");
            var withoutHeader = new CsvReader(new SourceFileInfo { FilePath = noHeader, FirstRowIsColumnName = false }).Read();
            Check(withoutHeader.HeaderColumns is null && withoutHeader.Data.Count == 2 && (string)withoutHeader.Data[0][0] == "1",
                "CSV without a header must not lose its first record.");
            File.WriteAllText(noHeader, "id,text\n1,\"unterminated");
            bool rejected = false;
            try { new CsvReader(new SourceFileInfo { FilePath = noHeader, FirstRowIsColumnName = true }).Read(); }
            catch (CsvHelper.CsvFormatException) { rejected = true; }
            Check(rejected, "Malformed CSV must still fail instead of silently recovering altered field values.");

            var grid = new GridData
            {
                Columns = new() { new GridColumn { Name = "字段" } },
                Rows = new() { new GridRow { Cells = new() { new GridCell { ColumnIndex = 0, Content = "测试值", Comment = "字段备注" } } } },
            };
            string commented = Path.Combine(folder, "commented.xlsx");
            new ExcelWriter(new ExportDataOption { FilePath = commented }).Write(grid);
            using (var workbook = DocumentFormat.OpenXml.Packaging.SpreadsheetDocument.Open(commented, false))
                Check(workbook.WorkbookPart!.WorksheetParts.First().WorksheetCommentsPart!.Comments.CommentList!.InnerText == "字段备注",
                    "Excel cell comments must survive the Open XML annotation pass.");

            string document = Path.Combine(folder, "dictionary.docx");
            new WordWriter(new WordGenerationOption { FilePath = document }).Write(new DocumentBody
            {
                Parts = new() { new DocumentPart { Title = "测试表", Data = grid } },
            });
            using (var archive = ZipFile.OpenRead(document))
            using (var reader = new StreamReader(archive.GetEntry("word/document.xml")!.Open()))
            {
                string xml = reader.ReadToEnd();
                Check(xml.Contains("测试表") && xml.Contains("测试值"), "Word exports must still contain the title and grid data.");
            }
            string zipped = Path.Combine(folder, "archive.zip");
            DatabaseManager.Core.FileHelper.Zip(document, zipped);
            using (var archive = ZipFile.OpenRead(zipped))
                Check(archive.Entries.Single().Name == "dictionary.docx" && archive.Entries.Single().Length == new FileInfo(document).Length,
                    "The maintained SharpZipLib package must produce a readable ZIP without changing the payload.");

            Check(Convert.ToInt32(new NCalc.Expression("(12+3)*2").Evaluate()) == 30,
                "Upgraded NCalc must retain the arithmetic used by data type mappings.");
            using var duck = new DuckDB.NET.Data.DuckDBConnection("DataSource=:memory:");
            duck.Open();
            using var command = duck.CreateCommand();
            command.CommandText = "SELECT 1 + 2";
            Check(Convert.ToInt32(command.ExecuteScalar()) == 3, "The upgraded native DuckDB runtime must load and execute SQL.");
        }
        finally { Directory.Delete(folder, true); }
        Console.WriteLine("Package compatibility checks passed: CSV/Excel round trips, Word, ZIP, NCalc and native DuckDB.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
