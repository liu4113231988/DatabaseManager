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

    public static bool SupportsSsl(DatabaseType databaseType)
        => databaseType is DatabaseType.MySql or DatabaseType.Postgres or DatabaseType.KingbaseES;

    public static string GetDefaultPort(DatabaseType databaseType) => databaseType switch
    {
        DatabaseType.MySql => MySqlInterpreter.DEFAULT_PORT.ToString(),
        DatabaseType.Oracle => OracleInterpreter.DEFAULT_PORT.ToString(),
        DatabaseType.Postgres => PostgresInterpreter.DEFAULT_PORT.ToString(),
        DatabaseType.KingbaseES => KingbaseInterpreter.DEFAULT_PORT.ToString(),
        DatabaseType.DM => DmInterpreter.DEFAULT_PORT.ToString(),
        _ => string.Empty, // SQL Server 的命名实例可使用动态端口。
    };

    public static string? UpdateDefaultPort(DatabaseType previousType, DatabaseType nextType, string? port)
        => string.IsNullOrWhiteSpace(port) || port == GetDefaultPort(previousType) ? GetDefaultPort(nextType) : port;

    public static bool RequiresDatabase(DatabaseType databaseType)
        => databaseType is not (DatabaseType.Oracle or DatabaseType.DM);

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
        else if (dbType == DatabaseType.DuckDB)
        {
            if (string.IsNullOrWhiteSpace(connection.Database))
                return "请填写 DuckDB 数据库文件路径，或勾选内存模式（:memory:）。";
            if (connection.DuckDbReadOnly && connection.Database == ":memory:")
                return "DuckDB 内存模式不能使用只读模式。";
            if (connection.DuckDbReadOnly && !File.Exists(connection.Database))
                return "DuckDB 只读模式需要选择已有数据库文件。";
        }
        else
        {
            if (string.IsNullOrWhiteSpace(connection.Server))
                return "请填写服务器地址（Server）。";
            if (!string.IsNullOrWhiteSpace(connection.Port)
                && (!int.TryParse(connection.Port, out var port) || port is < 1 or > 65535))
                return "数据库端口必须是 1 到 65535 之间的整数。";
            if (!connection.IntegratedSecurity && string.IsNullOrWhiteSpace(connection.UserId))
                return "请填写用户名（User ID）。";
            if (connection.IntegratedSecurity && !DatabaseAuthentication.SupportsIntegratedSecurity(dbType))
                return "该数据库类型不支持 Windows 身份验证。";
        }
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
