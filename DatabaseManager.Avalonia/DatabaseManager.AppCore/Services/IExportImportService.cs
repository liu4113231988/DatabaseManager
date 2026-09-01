using CommunityToolkit.Mvvm.ComponentModel;
using DatabaseManager.AppCore.Models;
using DatabaseManager.Core.Model;

namespace DatabaseManager.AppCore.Services;

/// <summary>
/// 导入 / 导出表项（含视图区分 + 多选勾选状态，用于导入导出工具列表）。
/// </summary>
public partial class ExportTableItem : ObservableObject
{
    public string Name { get; }
    public string? Schema { get; }
    public string DisplayName { get; }
    public bool IsView { get; }

    [ObservableProperty]
    private bool _isSelected;

    public ExportTableItem(string name, string? schema, string displayName, bool isView = false)
    {
        Name = name;
        Schema = schema;
        DisplayName = displayName;
        IsView = isView;
    }
}

/// <summary>
/// 导入 / 导出服务。封装数据与结构的文件导入导出。
/// 实现复用 <c>DatabaseManager.Core.DataExporter</c> / <c>DataImporter</c> 与 <c>DatabaseManager.FileUtility</c>。
/// </summary>
public interface IExportImportService
{
    /// <summary>返回支持的导出格式列表。</summary>
    IReadOnlyList<string> GetExportFormats();

    /// <summary>
    /// 获取指定连接/数据库中的表和视图列表（用于导入导出选择目标表）。
    /// </summary>
    Task<IReadOnlyList<ExportTableItem>> GetTablesAsync(
        ConnectionItem connection,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取指定表的列名列表（用于导入列映射）。
    /// </summary>
    Task<IReadOnlyList<string>> GetTableColumnsAsync(
        ConnectionItem connection,
        string tableName,
        string? schema,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 导出表/视图数据到文件（CSV / Excel / SQL / JSON / XML）。
    /// </summary>
    /// <param name="connection">连接。</param>
    /// <param name="tableName">表/视图名。</param>
    /// <param name="schema">Schema（可为空）。</param>
    /// <param name="isView">是否为视图。</param>
    /// <param name="format">导出格式（CSV / Excel / SQL / JSON / XML）。</param>
    /// <param name="filePath">导出文件路径。</param>
    /// <param name="showColumnNames">是否包含列名。</param>
    /// <param name="encodingName">文本编码名称（CSV/SQL/JSON/XML 生效；空或 auto 表示自动）。</param>
    /// <param name="startPageNumber">起始页码（大于 1 时跳过前面的页，用于续传；CSV/Excel 全量导出生效）。</param>
    /// <param name="onFeedback">进度/反馈回调。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    Task<ExportResult> ExportDataAsync(
        ConnectionItem connection,
        string tableName,
        string? schema,
        bool isView,
        string format,
        string filePath,
        bool showColumnNames = true,
        string? encodingName = null,
        long startPageNumber = 1,
        Action<string>? onFeedback = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 导入文件数据（CSV / Excel / JSON / XML）到指定表。
    /// </summary>
    /// <param name="connection">连接。</param>
    /// <param name="tableName">目标表名。</param>
    /// <param name="schema">Schema（可为空）。</param>
    /// <param name="filePath">导入文件路径。</param>
    /// <param name="firstRowIsColumnName">文件首行是否为列名。</param>
    /// <param name="columnMappings">列映射：SourceColumn = 文件列，TargetColumn = 表列。</param>
    /// <param name="encodingName">文本编码名称（CSV 生效；空或 auto 表示自动探测）。</param>
    /// <param name="skipRows">跳过文件开头的 N 行数据（用于续导）。</param>
    /// <param name="continueOnInvalidRows">校验未通过时跳过错误行继续导入。</param>
    /// <param name="onFeedback">进度/反馈回调。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    Task<ImportResult> ImportDataAsync(
        ConnectionItem connection,
        string tableName,
        string? schema,
        string filePath,
        bool firstRowIsColumnName = true,
        IReadOnlyList<ColumnMappingItem>? columnMappings = null,
        string? encodingName = null,
        int skipRows = 0,
        bool continueOnInvalidRows = false,
        Action<string>? onFeedback = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 预览导入文件的列与样例行（最多 5 行），用于列映射前确认。
    /// </summary>
    Task<FilePreviewResult> PreviewFileAsync(
        string filePath,
        bool firstRowIsColumnName = true,
        string? encodingName = null,
        CancellationToken cancellationToken = default);
}

/// <summary>文件预览结果。</summary>
public class FilePreviewResult
{
    public bool IsSuccess { get; set; }

    public string Message { get; set; } = string.Empty;

    /// <summary>文件列名（首行为列名时）或生成的列名。</summary>
    public List<string> Columns { get; } = new();

    /// <summary>样例行（最多 5 行，与 Columns 对应）。</summary>
    public List<List<string>> SampleRows { get; } = new();

    /// <summary>数据总行数（大文件只读表头时为 0）。</summary>
    public long TotalRows { get; set; }
}

/// <summary>导出结果。</summary>
public class ExportResult
{
    public bool IsSuccess { get; set; }

    public string Message { get; set; } = string.Empty;

    public string FilePath { get; set; } = string.Empty;
}

/// <summary>导入结果。</summary>
public class ImportResult
{
    public bool IsSuccess { get; set; }

    public string Message { get; set; } = string.Empty;

    /// <summary>校验结果明细（含错误行；校验失败或跳过错误行导入时有值）。</summary>
    public DataValidateResult? ValidateResultDetail { get; set; }

    /// <summary>导入成功但被跳过的错误行数。</summary>
    public int SkippedRowCount { get; set; }
}
