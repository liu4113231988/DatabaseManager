namespace DatabaseManager.FileUtility.Model
{
    public class SourceFileInfo
    {
        public string FilePath { get; set; }
        public bool FirstRowIsColumnName { get; set; }

        /// <summary>读取文件使用的文本编码名称（如 UTF-8、GBK）；为空或 "auto" 时由 StreamReader 自动探测（BOM）。</summary>
        public string EncodingName { get; set; }
    }
}
