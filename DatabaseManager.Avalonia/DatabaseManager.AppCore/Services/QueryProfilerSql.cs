using DatabaseInterpreter.Model;

namespace DatabaseManager.AppCore.Services;

/// <summary>各方言服务端查询剖析 SQL。</summary>
public static class QueryProfilerSql
{
    public static bool SupportsAnalyze(DatabaseType databaseType)
        => databaseType is DatabaseType.MySql or DatabaseType.Postgres or DatabaseType.SqlServer or DatabaseType.Oracle;

    /// <summary>生成服务端阶段/执行计划输出语句。Oracle 需在目标 SQL 执行后调用。</summary>
    public static string BuildAnalyzeSql(DatabaseType databaseType, string statement) => databaseType switch
    {
        DatabaseType.MySql or DatabaseType.Postgres => $"EXPLAIN ANALYZE {statement}",
        DatabaseType.SqlServer => $"SET STATISTICS XML ON;{Environment.NewLine}{statement};{Environment.NewLine}SET STATISTICS XML OFF;",
        DatabaseType.Oracle => "SELECT plan_table_output FROM TABLE(DBMS_XPLAN.DISPLAY_CURSOR(NULL, NULL, 'ALLSTATS LAST'))",
        _ => string.Empty,
    };
}
