namespace DatabaseManager.AppCore.Services;

/// <summary>
/// 数据库 Schema 解析服务。封装对象浏览、表结构读取等能力。
/// 实现复用 <c>DatabaseInterpreter</c>。
/// </summary>
public interface IDbSchemaService
{
    /// <summary>返回当前支持的数据库类型列表。</summary>
    IReadOnlyList<string> GetSupportedDatabaseTypes();
}
