namespace DatabaseManager.AppCore.Models;

/// <summary>图表可控取样策略。</summary>
public static class ChartSampling
{
    public const int DefaultLimit = 100;
    public const int MaxLimit = 1000;

    public static int NormalizeLimit(int value) => Math.Clamp(value, 1, MaxLimit);
}
