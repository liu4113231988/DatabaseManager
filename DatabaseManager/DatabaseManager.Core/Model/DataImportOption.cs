namespace DatabaseManager.Core.Model
{
    /// <summary>导入选项：断点续传（跳过行）、校验行为与错误行处理。</summary>
    public class DataImportOption
    {
        /// <summary>跳过文件开头的 N 行数据（用于失败后从指定位置续导）。</summary>
        public int SkipRows { get; set; }

        /// <summary>是否校验值与列类型的兼容性（默认开启）。</summary>
        public bool ValidateTypes { get; set; } = true;

        /// <summary>校验未通过时是否跳过错误行继续导入（默认整体失败）。</summary>
        public bool ContinueOnInvalidRows { get; set; }
    }
}
