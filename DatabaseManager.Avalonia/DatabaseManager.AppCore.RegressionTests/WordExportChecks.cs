using DatabaseManager.FileUtility;
using DatabaseManager.FileUtility.Model;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;
using W = DocumentFormat.OpenXml.Wordprocessing;

internal static class WordExportChecks
{
    public static void Run()
    {
        string folder = Path.Combine(Path.GetTempPath(), "dbm-word-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            string path = Path.Combine(folder, "dictionary.docx");
            var grid = new GridData
            {
                Columns = new() { new() { Name = "名称" }, new() { Name = "说明" } },
                Rows = new() { new() { Cells = new() { new() { Content = " <&> 中文 " }, new() { Content = "line1\r\nline2" } } } },
            };
            var body = new DocumentBody
            {
                Parts = new()
                {
                    new() { Title = "表一<&>", Comment = "备注 & <tag>", Data = grid },
                    new() { Title = "空表", Data = new() { Columns = new() { new() { Name = "字段" } }, Rows = new() } },
                },
            };
            var option = new WordGenerationOption
            {
                FilePath = path, GridColumnHeaderFontIsBold = true,
                GridColumnHeaderForegroundColor = "#ffffff", GridColumnHeaderBackgroundColor = "336699",
            };
            var writer = new WordWriter(option);
            writer.Write(body);
            using (var document = WordprocessingDocument.Open(path, false))
            {
                var errors = new OpenXmlValidator().Validate(document).ToArray();
                Check(errors.Length == 0, "Word output must pass schema validation: " + string.Join("; ", errors.Select(e => e.Description)));
                var content = document.MainDocumentPart!.Document.Body!;
                Check(content.InnerText.Contains("表一<&>(备注 & <tag>)") && content.InnerText.Contains(" <&> 中文 "),
                    "Titles, comments, XML characters, Unicode and significant spaces must retain their text.");
                Check(content.Elements<W.Paragraph>().First().Elements<W.Run>().First().RunProperties!.Bold is not null,
                    "Document section titles must remain bold.");
                var tables = content.Elements<W.Table>().ToArray();
                Check(tables.Length == 2 && tables[0].Elements<W.TableRow>().Count() == 2 && tables[1].Elements<W.TableRow>().Count() == 1,
                    "Word export must preserve multiple sections and empty tables with headers.");
                var header = tables[0].Elements<W.TableRow>().First().Elements<W.TableCell>().First();
                Check(header.TableCellProperties!.Shading!.Fill == "336699"
                    && header.TableCellProperties.TableCellVerticalAlignment!.Val == W.TableVerticalAlignmentValues.Center,
                    "Header background and vertical alignment must survive migration.");
                var paragraph = header.GetFirstChild<W.Paragraph>()!;
                Check(paragraph.ParagraphProperties!.Justification!.Val == W.JustificationValues.Center
                    && paragraph.GetFirstChild<W.Run>()!.RunProperties!.Bold is not null
                    && paragraph.GetFirstChild<W.Run>()!.RunProperties!.Color!.Val == "FFFFFF",
                    "Header centering, bold and foreground colors must survive migration.");
                Check(content.Descendants<W.Break>().Count() == 1, "Multiline values must include explicit Word line breaks.");
            }

            byte[] original = File.ReadAllBytes(path);
            grid.Rows[0].Cells.Clear();
            bool rejected = false;
            try { writer.Write(body); }
            catch (InvalidDataException) { rejected = true; }
            Check(rejected && File.ReadAllBytes(path).SequenceEqual(original) && Directory.GetFiles(folder, ".*.docx").Length == 0,
                "Invalid row shapes must fail without destroying an existing document or leaving temporary files.");
            option.GridColumnHeaderForegroundColor = "bad color";
            rejected = false;
            try { writer.Write(body); }
            catch (ArgumentException) { rejected = true; }
            Check(rejected && File.ReadAllBytes(path).SequenceEqual(original), "Invalid colors must fail before changing an existing document.");
        }
        finally { Directory.Delete(folder, true); }
        Console.WriteLine("Word export checks passed: schema, sections, tables, colors, alignment, Unicode, line breaks and failure safety.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
