using DatabaseManager.FileUtility.Model;
using System.Collections.Generic;

namespace DatabaseManager.Core.Model
{
    public class ExportSpecificDataOption : ExportDataOption
    {
        public bool ExportAllThatMeetCondition { get; set; }
        public List<long> PageNumbers { get; set; } = new List<long>();
        public long PageCount { get; set; }
        public int PageSize { get; set; }
        public string OrderColumns { get; set; }
        public string ConditionClause { get; set; }

        /// <summary>起始页码（从 1 开始；大于 1 时跳过前面的页，用于失败后从指定页续传导出）。</summary>
        public long StartPageNumber { get; set; } = 1;
    }
}
