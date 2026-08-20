namespace DatabaseManager.AppCore.Models;

/// <summary>
/// 统计类型选项（UI 友好）：表记录数 / 列内容最大长度。
/// </summary>
public sealed record StatisticTypeOption(string DisplayName, string Value)
{
    /// <summary>表记录数统计。</summary>
    public static readonly StatisticTypeOption RecordCount = new("表记录数 (Table Record Count)", "RecordCount");

    /// <summary>列内容最大长度统计。</summary>
    public static readonly StatisticTypeOption ColumnLength = new("列内容最大长度 (Column Max Length)", "ColumnLength");
}

/// <summary>
/// 表记录数（UI 友好）。
/// </summary>
public class RecordCountItem
{
    /// <summary>完整表名（含 Schema）。</summary>
    public string TableName { get; }

    /// <summary>记录数。</summary>
    public int RecordCount { get; }

    public RecordCountItem(string tableName, int recordCount)
    {
        TableName = tableName;
        RecordCount = recordCount;
    }
}

/// <summary>
/// 列内容最大长度（UI 友好）。
/// </summary>
public class ColumnLengthItem
{
    /// <summary>完整表名（含 Schema）。</summary>
    public string TableName { get; }

    /// <summary>列名。</summary>
    public string ColumnName { get; }

    /// <summary>内容最大长度。</summary>
    public int ContentMaxLength { get; }

    public ColumnLengthItem(string tableName, string columnName, int contentMaxLength)
    {
        TableName = tableName;
        ColumnName = columnName;
        ContentMaxLength = contentMaxLength;
    }
}
