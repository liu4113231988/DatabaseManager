using DatabaseManager.Core.Model;

namespace DatabaseManager.AppCore.Models;

/// <summary>
/// 诊断类型选项（UI 友好）。描述一种表诊断或脚本诊断类型。
/// Value 为稳定的枚举键名（不随本地化改变），用于避免按显示文本解析脆弱。
/// </summary>
public sealed record DiagnoseTypeOption(string DisplayName, string Category, string Value);

/// <summary>
/// 表诊断结果（UI 友好）。对应底层 <see cref="TableDiagnoseResultDetail"/>。
/// 描述一个检出异常的数据库对象（列 / 外键）及异常记录数 / 定位 SQL。
/// </summary>
public class TableDiagnoseResultItem
{
    /// <summary>对象类型（列 / 外键等）。</summary>
    public string ObjectType { get; }

    /// <summary>所属 Schema。</summary>
    public string Schema { get; }

    /// <summary>所属表名。</summary>
    public string TableName { get; }

    /// <summary>对象名（列名 / 外键名）。</summary>
    public string ObjectName { get; }

    /// <summary>异常记录数。</summary>
    public int RecordCount { get; }

    /// <summary>定位异常的 SQL。</summary>
    public string? Sql { get; }

    /// <summary>用于展示的完整对象名。</summary>
    public string DisplayName =>
        string.IsNullOrEmpty(Schema) ? $"{TableName}.{ObjectName}" : $"{Schema}.{TableName}.{ObjectName}";

    public TableDiagnoseResultItem(string objectType, string schema, string tableName, string objectName, int recordCount, string? sql)
    {
        ObjectType = objectType;
        Schema = schema ?? string.Empty;
        TableName = tableName ?? string.Empty;
        ObjectName = objectName ?? string.Empty;
        RecordCount = recordCount;
        Sql = sql;
    }
}

/// <summary>
/// 脚本诊断结果（UI 友好）。对应底层 <see cref="ScriptDiagnoseResult"/>。
/// 描述一个脚本对象（视图 / 函数 / 存储过程）及其诊断明细。
/// </summary>
public class ScriptDiagnoseResultItem
{
    /// <summary>对象类型（视图 / 函数 / 存储过程）。</summary>
    public string ObjectType { get; }

    /// <summary>所属 Schema。</summary>
    public string Schema { get; }

    /// <summary>对象名。</summary>
    public string ObjectName { get; }

    /// <summary>诊断明细文本（名称不匹配等）。</summary>
    public IReadOnlyList<string> Details { get; }

    /// <summary>用于展示的完整对象名。</summary>
    public string DisplayName =>
        string.IsNullOrEmpty(Schema) ? ObjectName : $"{Schema}.{ObjectName}";

    public ScriptDiagnoseResultItem(string objectType, string schema, string objectName, IReadOnlyList<string> details)
    {
        ObjectType = objectType ?? string.Empty;
        Schema = schema ?? string.Empty;
        ObjectName = objectName ?? string.Empty;
        Details = details;
    }
}
