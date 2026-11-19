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

    /// <summary>阶段 E 运维能力仅按 PG 兼容模式的代码级路径提供。</summary>
    public static string? GetAdminFeatureBlockReason(string? mode) => Normalize(mode) switch
    {
        Oracle or SqlServer => "KingbaseES 运维功能当前仅提供 PG 兼容模式路径；该兼容模式尚未验证，功能不可用。",
        _ => null,
    };

    /// <summary>发布标注：当前仅完成代码级接入，真实实例验收通过后才能改为已验证。</summary>
    public static string GetReleaseSupportLabel(string? mode)
    {
        string normalized = Normalize(mode);
        return normalized is Postgres or Auto
            ? "PG 兼容模式：代码级已接入，真实 V8 实例验收待补"
            : $"{normalized} 兼容模式：实验性 / 不可用，待单独验证";
    }
}
