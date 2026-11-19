using DatabaseInterpreter.Model;

namespace DatabaseManager.AppCore.Services;

/// <summary>
/// 阶段 F 发布标注数据源：区分「代码级已接入」与「真实实例已验收」，避免发布说明误报。
/// 真实 V8 实例验收完成前，IsInstanceVerified 必须保持 false。
/// </summary>
public static class KingbaseSupportStatus
{
    public const string DriverPackage = "SqlSugarCore.Kdbndp 9.3.8.413";
    public const string DefaultPort = "54321";

    public static DatabaseType DatabaseType => DatabaseType.KingbaseES;
    public static string VerifiedMode => KingbaseCompatibilityModes.Postgres;
    public static bool IsInstanceVerified => false;

    public static IReadOnlyList<string> CodeLevelModes { get; } =
        new[] { KingbaseCompatibilityModes.Auto, KingbaseCompatibilityModes.Postgres };
    public static IReadOnlyList<string> ExperimentalModes { get; } =
        new[] { KingbaseCompatibilityModes.Oracle, KingbaseCompatibilityModes.SqlServer };

    public static string GetReleaseNote() =>
        $"KingbaseES：{DriverPackage}，默认端口 {DefaultPort}；{KingbaseCompatibilityModes.GetReleaseSupportLabel(VerifiedMode)}；"
        + "Oracle / SQL Server 兼容模式实验性或不可用。阶段 E 运维能力已完成代码级接入，真实实例、权限与 UI 冒烟验收待补。";

    public static bool IsAdminFeatureEnabled(string? mode)
        => KingbaseCompatibilityModes.GetAdminFeatureBlockReason(mode) is null;
}
