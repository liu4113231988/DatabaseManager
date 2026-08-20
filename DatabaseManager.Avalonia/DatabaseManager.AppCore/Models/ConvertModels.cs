using DatabaseInterpreter.Model;

namespace DatabaseManager.AppCore.Models;

/// <summary>
/// 数据库转换选项。对应 <c>DatabaseConverter</c> 的 <c>DbConverterOption</c> 关键配置。
/// </summary>
public class ConvertOptions
{
    /// <summary>是否在目标服务器上执行生成的脚本。</summary>
    public bool ExecuteScriptOnTargetServer { get; set; } = true;

    /// <summary>是否使用事务包裹目标端脚本执行。</summary>
    public bool UseTransaction { get; set; }

    /// <summary>是否使用 BulkCopy 批量迁移数据（MySql/SqlServer/Postgres 支持）。</summary>
    public bool BulkCopy { get; set; }

    /// <summary>脚本对象出错时是否继续。</summary>
    public bool ContinueWhenErrorOccurs { get; set; }

    /// <summary>目标端不存在 Schema 时是否创建。</summary>
    public bool CreateSchemaIfNotExists { get; set; }

    /// <summary>是否将 Nchar/Nvarchar 转为双字符宽度的 Char/Varchar。</summary>
    public bool NcharToDoubleChar { get; set; } = true;

    /// <summary>是否在预览后执行转换。</summary>
    public bool NeedPreview { get; set; }

    /// <summary>Schema 映射（源 Schema → 目标 Schema）。</summary>
    public List<SchemaMappingInfo> SchemaMappings { get; set; } = new();
}

/// <summary>
/// 数据库转换结果（AppCore 层 UI 无关模型）。
/// </summary>
public class ConvertResult
{
    /// <summary>结果类型：信息 / 警告 / 错误。</summary>
    public ConvertResultType ResultType { get; set; } = ConvertResultType.Information;

    /// <summary>结果消息（成功/失败描述）。</summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>是否已取消。</summary>
    public bool IsCanceled { get; set; }

    /// <summary>转换过程中产生的反馈日志（供 UI 展示）。</summary>
    public List<string> Logs { get; } = new();

    /// <summary>转换模式。</summary>
    public string Mode { get; set; } = string.Empty;
}

/// <summary>转换结果类型。</summary>
public enum ConvertResultType
{
    Information = 0,
    Warning = 1,
    Error = 2,
}

/// <summary>转换模式。</summary>
public static class ConvertMode
{
    public const string Schema = "Schema";
    public const string Data = "Data";
    public const string SchemaAndData = "SchemaAndData";
}
