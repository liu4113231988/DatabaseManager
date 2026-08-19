using DatabaseInterpreter.Model;

namespace DatabaseManager.AppCore.Services;

/// <summary>
/// 转换服务实现。阶段 0 建立骨架，阶段 4 接入完整 <c>DatabaseConverter</c> 转换链路。
/// </summary>
public class DefaultConvertService : IConvertService
{
    public IReadOnlyList<string> GetSupportedConverters()
    {
        // 所有非 Unknown 的数据库类型均可作为源/目标进行转换。
        var types = Enum.GetValues<DatabaseType>()
                        .Where(t => t != DatabaseType.Unknown)
                        .Select(t => t.ToString())
                        .ToList();

        return types;
    }
}
