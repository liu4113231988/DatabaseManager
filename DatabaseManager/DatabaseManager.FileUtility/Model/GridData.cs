using System.Collections.Generic;

namespace DatabaseManager.FileUtility.Model
{
    public class GridData
    {
        public List<GridColumn> Columns { get; set; } = new List<GridColumn>();
        public List<GridRow> Rows { get; set; } = new List<GridRow>();
    }

    public class GridRow
    {
        public List<GridCell> Cells { get; set; } = new List<GridCell>();

        public List<GridRowComment> Comments { get; set; } = new List<GridRowComment>();
    }

    public class GridRowComment
    {
        public string? Comment { get; set; }
        public List<int> ColumnIndexes { get; set; } = new List<int>();
    }

    public class GridCell
    {
        public int ColumnIndex { get; set; }
        public string ColumnName { get; set; } = string.Empty;
        public object? Content { get; set; }
        public string? Comment { get; set; }
        public GridCellHighlightMode HighlightMode { get; set; } = GridCellHighlightMode.None;
    }

    public enum GridCellHighlightMode
    {
        None = 0,
        Background = 2,
        Foreground = 4
    }
}
