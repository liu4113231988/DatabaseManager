using DatabaseManager.AppCore.Models;

namespace DatabaseManager.AppCore.Services;

/// <summary>
/// 导入 / 导出服务。封装数据与结构的文件导入导出。
/// 实现复用 <c>DatabaseManager.Core.DataExporter</c> / <c>DataImporter</c> 与 <c>DatabaseManager.FileUtility</c>。
/// </summary>
public interface IExportImportService
{
    /// <summary>返回支持的导出格式列表。</summary>
    IReadOnlyList<string> GetExportFormats();

    /// <summary>
    /// 获取指定连接/数据库中的表列表（用于导入导出选择目标表）。
    /// </summary>
    Task<IReadOnlyList<TableItem>> GetTablesAsync(
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
    /// 导出表/视图数据到文件（CSV / Excel）。
    /// </summary>
    /// <param name="connection">连接。</param>
    /// <param name="tableName">表/视图名。</param>
    /// <param name="schema">Schema（可为空）。</param>
    /// <param name="isView">是否为视图。</param>
    /// <param name="format">导出格式（CSV / Excel）。</param>
    /// <param name="filePath">导出文件路径。</param>
    /// <param name="showColumnNames">是否包含列名。</param>
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
        Action<string>? onFeedback = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 导入文件数据（CSV / Excel）到指定表。
    /// </summary>
    /// <param name="connection">连接。</param>
    /// <param name="tableName">目标表名。</param>
    /// <param name="schema">Schema（可为空）。</param>
    /// <param name="filePath">导入文件路径。</param>
    /// <param name="firstRowIsColumnName">文件首行是否为列名。</param>
    /// <param name="columnMappings">列映射（可选，<see cref="DataImportColumnMapping"/> 的 UI 友好封装）。</param>
    /// <param name="onFeedback">进度/反馈回调。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    Task<ImportResult> ImportDataAsync(
        ConnectionItem connection,
        string tableName,
        string? schema,
        string filePath,
        bool firstRowIsColumnName = true,
        IReadOnlyList<ColumnMappingItem>? columnMappings = null,
        Action<string>? onFeedback = null,
        CancellationToken cancellationToken = default);
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
}
