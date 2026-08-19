using DatabaseInterpreter.Model;

namespace DatabaseManager.AppCore.Services;

/// <summary>
/// 基于 <see cref="DatabaseType"/> 枚举的 Schema 服务实现。
/// 阶段 0 骨架：先验证 AppCore 能复用核心引擎并枚举支持的数据库类型。
/// </summary>
public class DefaultDbSchemaService : IDbSchemaService
{
    public IReadOnlyList<string> GetSupportedDatabaseTypes()
        => Enum.GetValues<DatabaseType>()
               .Where(t => t != DatabaseType.Unknown)
               .Select(t => t.ToString())
               .ToList();
}
