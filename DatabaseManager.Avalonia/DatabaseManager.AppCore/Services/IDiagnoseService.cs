using DatabaseManager.AppCore.Models;
using DatabaseManager.Core.Model;

namespace DatabaseManager.AppCore.Services;

/// <summary>
/// 诊断服务（阶段 4）。封装 <c>DbDiagnosis</c> 能力，对单库执行表 / 脚本诊断。
/// </summary>
public interface IDiagnoseService
{
    /// <summary>
    /// 对指定连接的表执行诊断。
    /// </summary>
    /// <param name="connection">连接。</param>
    /// <param name="diagnoseType">表诊断类型。</param>
    /// <param name="schema">Schema（可空，表示全部）。</param>
    /// <param name="onFeedback">进度/反馈回调。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>表诊断结果列表。</returns>
    Task<IReadOnlyList<TableDiagnoseResultItem>> DiagnoseTableAsync(
        ConnectionItem connection,
        TableDiagnoseType diagnoseType,
        string? schema = null,
        Action<string>? onFeedback = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 对指定连接的脚本对象（视图 / 函数 / 存储过程）执行诊断。
    /// </summary>
    /// <param name="connection">连接。</param>
    /// <param name="diagnoseType">脚本诊断类型。</param>
    /// <param name="schema">Schema（可空，表示全部）。</param>
    /// <param name="onFeedback">进度/反馈回调。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>脚本诊断结果列表。</returns>
    Task<IReadOnlyList<ScriptDiagnoseResultItem>> DiagnoseScriptAsync(
        ConnectionItem connection,
        ScriptDiagnoseType diagnoseType,
        string? schema = null,
        Action<string>? onFeedback = null,
        CancellationToken cancellationToken = default);
}
