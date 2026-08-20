using DatabaseManager.AppCore.Models;

namespace DatabaseManager.AppCore.Services;

/// <summary>
/// 数据库文档生成服务（阶段 5）。
/// 复用 <c>DatabaseManager.Core.DocumentationGenerator</c> 生成列结构文档（Word）。
/// </summary>
public interface IColumnDocumentationService
{
    /// <summary>获取可用列属性选项（名称/类型/可空/主键/自增/默认值/注释）。</summary>
    IReadOnlyList<ColumnDocumentationProperty> GetDefaultProperties();

    /// <summary>生成列结构文档。</summary>
    Task<ColumnDocumentationResultItem> GenerateAsync(
        ConnectionItem connection,
        IReadOnlyList<ColumnDocumentationProperty> properties,
        bool showTableComment,
        string filePath,
        Action<string>? onFeedback = null,
        CancellationToken cancellationToken = default);
}
