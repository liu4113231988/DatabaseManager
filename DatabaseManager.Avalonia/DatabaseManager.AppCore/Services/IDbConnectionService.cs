using DatabaseManager.AppCore.Models;

namespace DatabaseManager.AppCore.Services;

/// <summary>
/// 数据库连接服务。封装连接配置（Account / Connection Profile）的增删改查与连接测试。
/// 实现复用 <c>DatabaseManager.Profile</c> 与 <c>DatabaseInterpreter</c>。
/// </summary>
public interface IDbConnectionService
{
    /// <summary>获取当前已保存的全部连接配置。</summary>
    IReadOnlyList<ConnectionItem> GetConnections();

    /// <summary>按数据库类型获取已保存的全部连接配置。</summary>
    IReadOnlyList<ConnectionItem> GetConnections(string databaseType);

    /// <summary>根据 Id 获取单个连接配置。</summary>
    ConnectionItem? GetConnectionById(string id);

    /// <summary>测试连接是否可用，并返回可用的数据库列表。</summary>
    Task<IReadOnlyList<string>> TestConnectionAsync(ConnectionItem connection, CancellationToken cancellationToken = default);

    /// <summary>新增或更新连接配置，返回保存后的连接 Id。</summary>
    Task<string?> SaveAsync(ConnectionItem connection, CancellationToken cancellationToken = default);

    /// <summary>批量删除连接配置。</summary>
    Task<bool> DeleteAsync(IEnumerable<string> ids, CancellationToken cancellationToken = default);

    /// <summary>判断连接名称是否已存在。</summary>
    Task<bool> IsNameExistedAsync(bool isAdd, string? accountId, string name, string? id, CancellationToken cancellationToken = default);
}
