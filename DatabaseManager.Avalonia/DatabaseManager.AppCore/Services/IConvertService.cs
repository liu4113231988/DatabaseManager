using DatabaseManager.AppCore.Models;

namespace DatabaseManager.AppCore.Services;

/// <summary>
/// 数据库结构/数据转换服务。封装跨库迁移能力。
/// 实现复用 <c>DatabaseConverter</c>。
/// </summary>
public interface IConvertService
{
    /// <summary>返回可用的转换源/目标数据库类型组合描述。</summary>
    IReadOnlyList<string> GetSupportedConverters();

    /// <summary>
    /// 执行数据库转换（结构 / 数据 / 结构+数据）。
    /// </summary>
    /// <param name="source">源连接。</param>
    /// <param name="target">目标连接。</param>
    /// <param name="mode">转换模式（<see cref="ConvertMode"/>）。</param>
    /// <param name="options">转换选项。</param>
    /// <param name="onFeedback">进度/反馈回调（UI 日志）。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    Task<ConvertResult> ConvertAsync(
        ConnectionItem source,
        ConnectionItem target,
        string mode,
        ConvertOptions? options = null,
        Action<string>? onFeedback = null,
        CancellationToken cancellationToken = default);
}
