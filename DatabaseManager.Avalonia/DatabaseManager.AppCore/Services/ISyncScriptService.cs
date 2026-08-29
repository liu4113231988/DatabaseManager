using DatabaseManager.AppCore.Models;

namespace DatabaseManager.AppCore.Services;

/// <summary>
/// 差异到变更发布服务：从结构/数据对比结果生成可审阅的同步脚本（含可选回滚脚本），
/// 并支持在目标连接上按勾选执行。
/// </summary>
public interface ISyncScriptService
{
    /// <summary>
    /// 依据结构对比上下文与勾选的差异节点生成变更脚本（应用到目标库）。
    /// </summary>
    Task<IReadOnlyList<ScriptItem>> GenerateStructuralScriptsAsync(
        SchemaCompareContext context,
        IReadOnlyList<SchemaCompareItem> roots,
        Action<string>? onFeedback = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 依据结构对比上下文与勾选的差异节点生成回滚脚本（把目标库恢复为对比前状态）。
    /// </summary>
    Task<IReadOnlyList<ScriptItem>> GenerateStructuralRollbackScriptsAsync(
        SchemaCompareContext context,
        IReadOnlyList<SchemaCompareItem> roots,
        Action<string>? onFeedback = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 依据数据对比结果（按勾选的表）生成同步脚本（把目标库数据同步为源库）。
    /// </summary>
    Task<IReadOnlyList<ScriptItem>> GenerateDataSyncScriptsAsync(
        ConnectionItem source,
        ConnectionItem target,
        IReadOnlyList<DataCompareResultItem> results,
        Action<string>? onFeedback = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 依据数据对比结果（按勾选的表）生成回滚脚本（把目标库数据恢复为对比前状态）。
    /// </summary>
    Task<IReadOnlyList<ScriptItem>> GenerateDataRollbackScriptsAsync(
        ConnectionItem source,
        ConnectionItem target,
        IReadOnlyList<DataCompareResultItem> results,
        Action<string>? onFeedback = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 在目标连接上执行勾选的脚本：结构脚本在单事务内执行，数据脚本按条目顺序执行。
    /// </summary>
    Task<ScriptExecutionResult> ExecuteScriptsAsync(
        ConnectionItem target,
        IReadOnlyList<ScriptItem> scripts,
        Action<string>? onFeedback = null,
        CancellationToken cancellationToken = default);
}
