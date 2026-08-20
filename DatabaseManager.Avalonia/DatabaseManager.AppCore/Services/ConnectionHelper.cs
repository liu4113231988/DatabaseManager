using DatabaseInterpreter.Core;
using DatabaseInterpreter.Model;
using DatabaseManager.AppCore.Models;

namespace DatabaseManager.AppCore.Services;

/// <summary>
/// 连接相关的通用辅助方法（数据库类型解析、连接信息转换）。
/// </summary>
public static class ConnectionHelper
{
    /// <summary>将数据库类型字符串解析为 <see cref="DatabaseType"/>。</summary>
    public static DatabaseType ParseDatabaseType(string databaseType)
        => Enum.TryParse<DatabaseType>(databaseType, true, out var type) ? type : DatabaseType.Unknown;

    /// <summary>将 AppCore 的 <see cref="ConnectionItem"/> 转换为核心库的 <see cref="ConnectionInfo"/>。</summary>
    public static ConnectionInfo ToConnectionInfo(ConnectionItem connection) => new()
    {
        Server = connection.Server,
        Port = connection.Port,
        ServerVersion = connection.ServerVersion,
        Database = connection.Database,
        IntegratedSecurity = connection.IntegratedSecurity,
        UserId = connection.UserId,
        Password = connection.Password,
        IsDba = connection.IsDba,
        UseSsl = connection.UseSsl,
    };
}
