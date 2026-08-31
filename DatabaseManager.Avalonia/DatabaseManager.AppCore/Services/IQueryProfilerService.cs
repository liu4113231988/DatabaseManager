using DatabaseManager.AppCore.Models;

namespace DatabaseManager.AppCore.Services;

/// <summary>单次剖析运行统计。</summary>
public class QueryProfileRunStat
{
    public int Index { get; set; }

    /// <summary>建立连接耗时（仅首次连接记录，其余为 0）。</summary>
    public long OpenMs { get; set; }

    /// <summary>执行耗时（提交到服务端并取回第一行/完成）。</summary>
    public long ExecuteMs { get; set; }

    /// <summary>取数耗时（读取全部行）。</summary>
    public long FetchMs { get; set; }

    public long TotalMs => OpenMs + ExecuteMs + FetchMs;

    public int Rows { get; set; }

    public string? Error { get; set; }
}

/// <summary>查询剖析结果。</summary>
public class QueryProfileResult
{
    public long OpenMs { get; set; }

    public List<QueryProfileRunStat> Runs { get; } = new();

    /// <summary>服务端 EXPLAIN ANALYZE 输出（MySQL/PostgreSQL 支持时）。</summary>
    public string? AnalyzeText { get; set; }

    public string? Error { get; set; }

    public long AverageMs => Runs.Count == 0 ? 0 : (long)Runs.Average(r => r.TotalMs);

    public long MinMs => Runs.Count == 0 ? 0 : Runs.Min(r => r.TotalMs);

    public long MaxMs => Runs.Count == 0 ? 0 : Runs.Max(r => r.TotalMs);

    public int TotalRows => Runs.Sum(r => r.Rows);
}

/// <summary>
/// 查询性能剖析服务：对一条 SQL 重复运行 N 次，分阶段（执行/取数）计时并统计；
/// 可选输出服务端 EXPLAIN ANALYZE 文本（MySQL 8+/PostgreSQL）。
/// </summary>
public interface IQueryProfilerService
{
    /// <summary>指定数据库类型是否支持 EXPLAIN ANALYZE。</summary>
    bool SupportsAnalyze(string databaseType);

    Task<QueryProfileResult> ProfileAsync(
        ConnectionItem connection,
        string sql,
        int runs,
        bool includeAnalyze,
        CancellationToken cancellationToken = default);
}
