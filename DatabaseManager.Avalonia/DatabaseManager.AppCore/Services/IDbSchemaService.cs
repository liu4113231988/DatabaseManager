using DatabaseManager.AppCore.Models;

namespace DatabaseManager.AppCore.Services;

/// <summary>
/// 数据库 Schema 解析服务。封装对象浏览、表结构读取等能力。
/// 实现复用 <c>DatabaseInterpreter</c>。
/// </summary>
public interface IDbSchemaService
{
    /// <summary>返回当前支持的数据库类型列表。</summary>
    IReadOnlyList<string> GetSupportedDatabaseTypes();

    /// <summary>
    /// 获取指定连接下所有数据库的根节点列表。
    /// 每个数据库节点包含其 Schema → 类型文件夹（表/视图/存储过程/函数/序列/触发器）的树形结构。
    /// </summary>
    /// <param name="connectionName">连接名称。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    Task<IReadOnlyList<DbObjectTreeNode>> GetObjectTreeAsync(string connectionName, CancellationToken cancellationToken = default);

    /// <summary>
    /// 加载某类型文件夹下的具体对象子节点。
    /// 用于按需展开时获取某数据库下某类对象（如表、视图、存储过程等）。
    /// </summary>
    /// <param name="connectionName">连接名称。</param>
    /// <param name="databaseName">数据库名。</param>
    /// <param name="objectType">数据库对象类型。</param>
    /// <param name="schema">Schema 名（可为空）。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    Task<IReadOnlyList<DbObjectTreeNode>> GetDbObjectNodesAsync(
        string connectionName,
        string databaseName,
        DatabaseInterpreter.Model.DatabaseObjectType objectType,
        string? schema = null,
        CancellationToken cancellationToken = default);
}
