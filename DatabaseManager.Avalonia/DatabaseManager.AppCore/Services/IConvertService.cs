using DatabaseInterpreter.Model;
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
    /// <param name="targetSchemaInfo">可选：预览后编辑过的目标 Schema（若提供，则跳过翻译直接用该结构执行转换）。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    Task<ConvertResult> ConvertAsync(
        ConnectionItem source,
        ConnectionItem target,
        string mode,
        ConvertOptions? options = null,
        Action<string>? onFeedback = null,
        SchemaInfo? targetSchemaInfo = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 生成转换预览（对应原 WinForms <c>frmSchemaPreviewer</c> 的翻译阶段）：
    /// 读取源库完整 Schema，调用 <see cref="DbConverter"/> 生成目标 Schema 结构（不执行转换），
    /// 供 UI 预览 / 编辑列定义后再次执行。
    /// </summary>
    /// <param name="source">源连接。</param>
    /// <param name="target">目标连接。</param>
    /// <param name="options">转换选项（含 SchemaMappings 与 NeedPreview）。</param>
    /// <param name="onFeedback">进度/反馈回调。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    Task<ConvertPreviewResult> PreviewAsync(
        ConnectionItem source,
        ConnectionItem target,
        ConvertOptions? options = null,
        Action<string>? onFeedback = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 加载源/目标数据库的 Schema 列表与自动映射（对应原 WinForms <c>frmSchemaMapping</c>）。
    /// </summary>
    Task<SchemaMappingLoadResult> LoadSchemaMappingsAsync(
        ConnectionItem source,
        ConnectionItem target,
        CancellationToken cancellationToken = default);
}
