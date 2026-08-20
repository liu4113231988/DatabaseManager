using DatabaseInterpreter.Model;
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
        DatabaseObjectType objectType,
        string? schema = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 判断当前连接下的数据库是否为多 Schema 结构（Oracle/Postgres 等）。
    /// </summary>
    /// <param name="databaseName">数据库名。</param>
    /// <param name="connectionName">连接名称。</param>
    Task<bool> HasMultipleSchemasAsync(string connectionName, string databaseName, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取指定数据库下的 Schema 列表（Oracle/Postgres 等）。
    /// </summary>
    Task<IReadOnlyList<DbObjectTreeNode>> GetSchemasAsync(string connectionName, string databaseName, CancellationToken cancellationToken = default);

    /// <summary>
    /// 加载表/视图的子类型文件夹下的具体子对象（列/索引/键/约束/触发器）。
    /// </summary>
    /// <param name="connectionName">连接名称。</param>
    /// <param name="databaseName">数据库名。</param>
    /// <param name="childFolderType">子类型文件夹类型（列/索引/键/约束/触发器）。</param>
    /// <param name="tableOrView">所属表或视图。</param>
    /// <param name="isForView">是否为视图（列类型不同）。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    Task<IReadOnlyList<DbObjectTreeNode>> GetTableChildNodesAsync(
        string connectionName,
        string databaseName,
        DbObjectChildType childFolderType,
        DatabaseObject tableOrView,
        bool isForView = false,
        CancellationToken cancellationToken = default);
}
