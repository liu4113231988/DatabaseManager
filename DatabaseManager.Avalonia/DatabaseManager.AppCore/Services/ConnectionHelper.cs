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

    public static bool IsFileDatabase(DatabaseType databaseType)
        => databaseType is DatabaseType.Sqlite or DatabaseType.DuckDB;

    public static string? ValidateEndpoint(ConnectionItem connection)
    {
        var dbType = ParseDatabaseType(connection.DatabaseType);
        if (dbType == DatabaseType.Sqlite)
        {
            if (string.IsNullOrWhiteSpace(connection.Database))
                return "请选择或填写 SQLite 数据库文件路径。";
            if (!File.Exists(connection.Database))
                return "SQLite 数据库文件不存在，请选择已有数据库文件。";
        }
        else if (!IsFileDatabase(dbType) && string.IsNullOrWhiteSpace(connection.Server))
            return "请填写服务器地址（Server）。";
        return null;
    }

    /// <summary>将 AppCore 的 <see cref="ConnectionItem"/> 转换为核心库的 <see cref="ConnectionInfo"/>。</summary>
    public static ConnectionInfo ToConnectionInfo(ConnectionItem connection)
    {
        var endpoint = SshTunnelManager.Resolve(connection);
        return new()
    {
        Server = endpoint.Host,
        Port = endpoint.Port,
        ServerVersion = connection.ServerVersion,
        Database = connection.Database,
        IntegratedSecurity = connection.IntegratedSecurity,
        UserId = connection.UserId,
        Password = connection.Password,
        IsDba = connection.IsDba,
        UseSsl = connection.UseSsl,
        UseReadOnly = connection.DuckDbReadOnly,
    };
    }
}
