using DatabaseManager.AppCore.Models;

namespace DatabaseManager.AppCore.Services;

/// <summary>
/// 代码生成服务（阶段 5）。
/// 复用 <c>DatabaseManager.Core.CodeGenerator</c> 根据表/视图结构生成实体类代码。
/// </summary>
public interface ICodeGenerateService
{
    /// <summary>加载指定连接的数据库中的表与视图（供选择）。</summary>
    Task<IReadOnlyList<CodeGenerateTarget>> GetTargetsAsync(
        ConnectionItem connection,
        CancellationToken cancellationToken = default);

    /// <summary>根据所选对象生成代码文件。</summary>
    Task<CodeGenerateResultItem> GenerateAsync(
        ConnectionItem connection,
        IReadOnlyList<CodeGenerateTarget> targets,
        string language,
        string? namespaceName,
        bool generateComments,
        string outputFolder,
        Action<string>? onFeedback = null,
        CancellationToken cancellationToken = default);
}
