using DatabaseManager.Core.Model;

namespace DatabaseManager.AppCore.Models;

/// <summary>
/// 优化结果（UI 友好）。对应底层 <see cref="OptimizeResultDetail"/>。
/// 描述单个对象（数据库 / 表）优化前后的数据长度。
/// </summary>
public class OptimizeResultItem
{
    /// <summary>对象类型（Database / Table）。</summary>
    public string ObjectType { get; }

    /// <summary>对象名。</summary>
    public string ObjectName { get; }

    /// <summary>是否优化成功。</summary>
    public bool IsOK { get; }

    /// <summary>优化前数据长度（MB）。</summary>
    public decimal DataLengthBeforeOptimization { get; }

    /// <summary>优化后数据长度（MB）。</summary>
    public decimal DataLengthAfterOptimization { get; }

    /// <summary>失败时的错误信息。</summary>
    public string Message { get; }

    /// <summary>结果文本（OK / Failed）。</summary>
    public string ResultText => IsOK ? "OK" : "Failed";

    /// <summary>展示用数据长度（前 → 后）。</summary>
    public string DataLengthText =>
        $"{DataLengthBeforeOptimization:N2} MB → {DataLengthAfterOptimization:N2} MB";

    public OptimizeResultItem(OptimizeResultDetail detail)
    {
        ObjectType = detail.ObjectType ?? string.Empty;
        ObjectName = detail.ObjectName ?? string.Empty;
        IsOK = detail.IsOK;
        DataLengthBeforeOptimization = detail.DataLengthBeforeOptimization;
        DataLengthAfterOptimization = detail.DataLengthAfterOptimization;
        Message = detail.Message ?? string.Empty;
    }
}
