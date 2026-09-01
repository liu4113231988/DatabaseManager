namespace DatabaseManager.AppCore.Services;

/// <summary>
/// KingbaseES 实例的兼容模式标记及当前客户端的安全边界。
/// 不会尝试在客户端切换服务端兼容模式；该值仅用于保存用户选择和决定可用能力。
/// </summary>
public static class KingbaseCompatibilityModes
{
    public const string Auto = "Auto";
    public const string Postgres = "Postgres";
    public const string Oracle = "Oracle";
    public const string SqlServer = "SqlServer";

    public static IReadOnlyList<string> All { get; } = new[] { Auto, Postgres, Oracle, SqlServer };

    public static string Normalize(string? value)
    {
        return All.FirstOrDefault(mode => string.Equals(mode, value, StringComparison.OrdinalIgnoreCase))
               ?? Auto;
    }

    /// <summary>
    /// 当前版本只验证 Kdbndp + PG catalog 路径。Auto 也按该路径尝试连接，
    /// 实际环境不兼容时由驱动/服务端返回可见错误。
    /// </summary>
    public static string? GetConnectionBlockReason(string? mode)
    {
        return Normalize(mode) switch
        {
            Oracle => "KingbaseES 的 Oracle 兼容模式尚未完成元数据与 SQL 方言验证，当前版本不能建立此模式的连接。",
            SqlServer => "KingbaseES 的 SQL Server 兼容模式尚未完成元数据与 SQL 方言验证，当前版本不能建立此模式的连接。",
            _ => null,
        };
    }
}
