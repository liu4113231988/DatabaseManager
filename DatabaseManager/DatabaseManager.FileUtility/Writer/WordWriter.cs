using DatabaseManager.FileUtility.Model;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using W = DocumentFormat.OpenXml.Wordprocessing;
using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace DatabaseManager.FileUtility
{
    public class WordWriter
    {
        private readonly WordGenerationOption _option;

        public WordWriter(WordGenerationOption option)
        {
            _option = option ?? throw new ArgumentNullException(nameof(option));
        }

        public string Write(DocumentBody body)
        {
            ArgumentNullException.ThrowIfNull(body);
            string? foreground = ColorValue(_option.GridColumnHeaderForegroundColor);
            string? background = ColorValue(_option.GridColumnHeaderBackgroundColor);
            string path = _option.FilePath;
            string temporary = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(path))!, $".{Guid.NewGuid():N}.docx");
            try
            {
                using (var document = WordprocessingDocument.Create(temporary, WordprocessingDocumentType.Document))
                {
                    var main = document.AddMainDocumentPart();
                    var content = new W.Body();
                    main.Document = new W.Document(content);
                    foreach (var part in body.Parts)
                    {
                        var title = TextRun(part.Title);
                        title.RunProperties = new W.RunProperties(new W.Bold());
                        var paragraph = new W.Paragraph(new W.ParagraphProperties(new W.SpacingBetweenLines { Before = "0", After = "0" }), title);
                        if (!string.IsNullOrEmpty(part.Comment)) paragraph.Append(TextRun($"({part.Comment})"));
                        content.Append(paragraph);

                        var grid = part.Data;
                        if (grid.Columns.Count == 0 || grid.Rows.Any(row => row.Cells is null || row.Cells.Count != grid.Columns.Count))
                            throw new InvalidDataException("Word 表格必须有列，且每行数据必须与列数一致。");
                        var table = new W.Table(new W.TableProperties(new W.TableWidth { Width = "0", Type = W.TableWidthUnitValues.Auto },
                            new W.TableBorders(new W.TopBorder { Val = W.BorderValues.Single, Size = 4U },
                                new W.LeftBorder { Val = W.BorderValues.Single, Size = 4U },
                                new W.BottomBorder { Val = W.BorderValues.Single, Size = 4U },
                                new W.RightBorder { Val = W.BorderValues.Single, Size = 4U },
                                new W.InsideHorizontalBorder { Val = W.BorderValues.Single, Size = 4U },
                                new W.InsideVerticalBorder { Val = W.BorderValues.Single, Size = 4U })));
                        var columns = new W.TableGrid();
                        foreach (var column in grid.Columns) columns.Append(new W.GridColumn());
                        table.Append(columns);
                        for (int index = -1; index < grid.Rows.Count; index++)
                        {
                            bool header = index < 0;
                            var row = new W.TableRow();
                            for (int column = 0; column < grid.Columns.Count; column++)
                            {
                                var properties = new W.TableCellProperties(new W.TableCellVerticalAlignment { Val = W.TableVerticalAlignmentValues.Center });
                                var run = TextRun(header ? grid.Columns[column].Name : grid.Rows[index].Cells[column].Content?.ToString());
                                var format = new W.RunProperties();
                                if (header)
                                {
                                    if (_option.GridColumnHeaderFontIsBold) format.Append(new W.Bold());
                                    if (foreground is not null) format.Append(new W.Color { Val = foreground });
                                    if (background is not null)
                                        properties.AddChild(new W.Shading { Val = W.ShadingPatternValues.Clear, Fill = background }, true);
                                }
                                run.RunProperties = format;
                                var p = new W.ParagraphProperties(new W.SpacingBetweenLines { Before = "0", After = "0" });
                                if (header) p.Append(new W.Justification { Val = W.JustificationValues.Center });
                                row.Append(new W.TableCell(properties, new W.Paragraph(p, run)));
                            }
                            table.Append(row);
                        }
                        content.Append(table, new W.Paragraph());
                    }
                    main.Document.Save();
                }
                File.Move(temporary, path, overwrite: true);
                return path;
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }

        private static W.Run TextRun(string? text)
        {
            var run = new W.Run();
            var lines = (text ?? string.Empty).Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            for (int index = 0; index < lines.Length; index++)
            {
                if (index > 0) run.Append(new W.Break());
                run.Append(new W.Text(lines[index]) { Space = SpaceProcessingModeValues.Preserve });
            }
            return run;
        }

        private static string? ColorValue(string? color)
        {
            if (string.IsNullOrWhiteSpace(color)) return null;
            string value = color.Trim().TrimStart('#');
            if (!Regex.IsMatch(value, "\\A[0-9a-fA-F]{6}\\z"))
                throw new ArgumentException("Word 表格颜色必须为六位十六进制 RGB。");
            return value.ToUpperInvariant();
        }
    }
}
