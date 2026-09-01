namespace DatabaseManager.FileUtility.Model
{
    public class ExportDataOption
    {
        public ExportFileType FileType { get; set; }
        public bool ShowColumnNames { get; set; } = true;
        public string FilePath { get; set; }
        public bool IsTemporary { get; set; }

        /// <summary>写出文件的文本编码名称（如 UTF-8、GBK）；为空时使用 UTF-8。</summary>
        public string EncodingName { get; set; }
    }

    public enum ExportFileType
    {
        None = 0,
        CSV = 1,
        EXCEL = 2,
        SQL = 4,
        JSON = 8,
        XML = 16
    }
}
