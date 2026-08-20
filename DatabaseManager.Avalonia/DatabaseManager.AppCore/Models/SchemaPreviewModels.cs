using System.Collections.Generic;
using System.Linq;
using DatabaseInterpreter.Model;

namespace DatabaseManager.AppCore.Models;

/// <summary>
/// Schema 预览结果（阶段 4 剩余项）。
/// 对应原 WinForms <c>frmSchemaPreviewer</c>：转换前预览目标 Schema 结构，允许编辑列定义后执行转换。
/// </summary>
public class ConvertPreviewResult
{
    /// <summary>是否成功。</summary>
    public bool IsSuccess { get; set; }

    /// <summary>结果消息。</summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>是否已取消。</summary>
    public bool IsCanceled { get; set; }

    /// <summary>结果类型。</summary>
    public ConvertResultType ResultType { get; set; } = ConvertResultType.Information;

    /// <summary>转换翻译后的目标 Schema 结构（供预览/编辑）。</summary>
    public SchemaInfo? TranslatedSchemaInfo { get; set; }

    /// <summary>预览过程日志。</summary>
    public List<string> Logs { get; } = new();
}

/// <summary>
/// Schema 映射加载结果（对应原 WinForms <c>frmSchemaMapping</c>）。
/// </summary>
public class SchemaMappingLoadResult
{
    /// <summary>是否成功。</summary>
    public bool IsSuccess { get; set; }

    /// <summary>消息。</summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>源库 Schema 列表。</summary>
    public List<string> SourceSchemas { get; set; } = new();

    /// <summary>目标库 Schema 列表。</summary>
    public List<string> TargetSchemas { get; set; } = new();

    /// <summary>当前映射（含自动映射结果）。</summary>
    public List<SchemaMappingInfo> Mappings { get; set; } = new();
}

/// <summary>
/// Schema 预览列定义（UI 可编辑的列信息）。
/// 对应 <c>frmSchemaPreviewer</c> 的列网格行：列名 / 目标数据类型 / 长度 / 精度 / 小数位 / 默认值 / 当前内容最大长度。
/// </summary>
public class SchemaPreviewColumn
{
    /// <summary>列名（只读）。</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>目标数据类型（可编辑）。</summary>
    public string DataType { get; set; } = string.Empty;

    /// <summary>长度（可编辑）。</summary>
    public long? MaxLength { get; set; }

    /// <summary>精度（可编辑）。</summary>
    public long? Precision { get; set; }

    /// <summary>小数位（可编辑）。</summary>
    public long? Scale { get; set; }

    /// <summary>默认值（可编辑）。</summary>
    public string DefaultValue { get; set; } = string.Empty;

    /// <summary>当前列内容最大长度（可选，用于长度校验提示）。</summary>
    public long? CurrentContentMaxLength { get; set; }

    /// <summary>长度是否过短（MaxLength 小于当前内容最大长度时标红）。</summary>
    public bool IsLengthTooShort =>
        CurrentContentMaxLength.HasValue
        && MaxLength.HasValue
        && MaxLength != -1
        && MaxLength < CurrentContentMaxLength;

    /// <summary>原始列引用（用于回写编辑结果到 SchemaInfo）。</summary>
    public TableColumn SourceColumn { get; set; } = null!;
}

/// <summary>
/// Schema 预览表节点（UI 友好）。
/// </summary>
public class SchemaPreviewTable
{
    /// <summary>表 Schema。</summary>
    public string Schema { get; set; } = string.Empty;

    /// <summary>表名。</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>展示名。</summary>
    public string DisplayName => string.IsNullOrEmpty(Schema) ? Name : $"{Schema}.{Name}";

    /// <summary>该表的列集合（可编辑）。</summary>
    public List<SchemaPreviewColumn> Columns { get; set; } = new();

    /// <summary>列数。</summary>
    public int ColumnCount => Columns.Count;

    /// <summary>列名列表（用于下拉选择/展示）。</summary>
    public List<string> ColumnNames => Columns.Select(c => c.Name).ToList();
}

/// <summary>
/// 列映射项（对应 <c>DataImportColumnMapping</c> / <c>ForeignKeyColumn</c> 的 UI 友好封装）。
/// </summary>
public class ColumnMappingItem
{
    /// <summary>本表列名。</summary>
    public string SourceColumn { get; set; } = string.Empty;

    /// <summary>目标/引用表列名。</summary>
    public string TargetColumn { get; set; } = string.Empty;
}
