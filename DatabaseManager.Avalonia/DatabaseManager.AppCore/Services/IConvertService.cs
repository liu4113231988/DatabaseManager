namespace DatabaseManager.AppCore.Services;

/// <summary>
/// 数据库结构/数据转换服务。封装跨库迁移能力。
/// 实现复用 <c>DatabaseConverter</c>。
/// </summary>
public interface IConvertService
{
    /// <summary>返回可用的转换源/目标数据库类型组合描述。</summary>
    IReadOnlyList<string> GetSupportedConverters();
}
