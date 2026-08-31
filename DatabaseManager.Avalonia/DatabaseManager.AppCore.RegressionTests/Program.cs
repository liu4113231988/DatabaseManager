using DatabaseInterpreter.Model;
using DatabaseInterpreter.Core;
using DatabaseManager.AppCore.Common;
using DatabaseManager.AppCore.Models;
using DatabaseManager.AppCore.Services;

static class Program
{
    private static int Main()
    {
        AssertEqual("`sales`.`orders`", SqlDialectHelper.QuoteQualifiedIdentifier(DatabaseType.MySql, "sales.orders"));
        AssertEqual("[sales].[orders]", SqlDialectHelper.QuoteQualifiedIdentifier(DatabaseType.SqlServer, "sales.orders"));
        AssertEqual("\"sales\".\"orders\"", SqlDialectHelper.QuoteQualifiedIdentifier(DatabaseType.Postgres, "sales.orders"));
        AssertEqual(null, SqlSafety.ValidateProfilerStatement(" SELECT * FROM orders; "));
        AssertContains("仅支持", SqlSafety.ValidateProfilerStatement("DELETE FROM orders")!);
        AssertContains("仅支持", SqlSafety.ValidateProfilerStatement("SELECT * INTO archive FROM orders")!);
        AssertContains("仅支持", SqlSafety.ValidateProfilerStatement("SELECT * FROM orders; DELETE FROM orders")!);
        AssertContains("NVL(", DbSessionSql.BuildSessionsSql(DatabaseType.Oracle));

        var userService = new DefaultDbUserService();
        AssertEqual("GRANT SELECT ON ALL TABLES IN SCHEMA \"public\" TO \"reporter\";",
            userService.BuildGrantSql("Postgres", "reporter", null, "SELECT", "public.*"));
        AssertEqual("GRANT SELECT ON SCHEMA::[dbo] TO [reporter];",
            userService.BuildGrantSql("SqlServer", "reporter", null, "SELECT", "dbo.*"));
        AssertEqual(string.Empty,
            userService.BuildGrantSql("Oracle", "reporter", null, "SELECT", "*.*"));
        AssertEqual("SELECT id, name FROM orders\r\nORDER BY 2 DESC;",
            SqlQueryTransform.AppendOrdinalOrderBy("SELECT id, name FROM orders", 2, true));
        AssertEqualDate(new DateTime(2026, 9, 1, 9, 30, 0),
            CronSchedule.GetNextOccurrence("30 9 * * 1-5", new DateTime(2026, 8, 31, 10, 0, 0)));
        AssertEqualDate(new DateTime(2026, 8, 31, 10, 15, 0),
            CronSchedule.GetNextOccurrence("*/15 * * * *", new DateTime(2026, 8, 31, 10, 7, 0)));
        var literalFromParse = SimpleSelectParser.Parse("SELECT 'FROM audit' AS note, name FROM users");
        AssertTrue(literalFromParse.IsSimpleSelect && literalFromParse.TableName == "users", "字符串字面量中的 FROM 不应被当作表引用。");
        var literalCommentParse = SimpleSelectParser.Parse("SELECT '-- FROM audit' AS note, name FROM users");
        AssertTrue(literalCommentParse.IsSimpleSelect && literalCommentParse.TableName == "users", "字符串字面量中的 -- 不应被当作注释。");
        AssertContains("VIEW SERVER STATE", DbAdminGuidance.GetSessionPermissionHint(DatabaseType.SqlServer));
        AssertContains("pg_read_all_stats", DbAdminGuidance.GetUserPermissionHint(DatabaseType.Postgres));
        AssertContains("不存在", DbAdminGuidance.ValidateClientToolPath("C:\\missing-tool.exe")!);
        AssertTrue(QueryProfilerSql.SupportsAnalyze(DatabaseType.SqlServer), "SQL Server 应支持服务端剖析输出。");
        AssertContains("STATISTICS XML", QueryProfilerSql.BuildAnalyzeSql(DatabaseType.SqlServer, "SELECT 1"));
        AssertContains("DISPLAY_CURSOR", QueryProfilerSql.BuildAnalyzeSql(DatabaseType.Oracle, "SELECT 1"));
        AssertEqual("1000", ChartSampling.NormalizeLimit(9999).ToString());
        var kingbaseConnection = new ConnectionInfo { Server = "127.0.0.1", Database = "test", UserId = "system", Password = "secret" };
        AssertContains("Port=54321", new KingbaseConnectionBuilder().BuildConntionString(kingbaseConnection));
        AssertTrue(DbInterpreterHelper.GetDbInterpreter(DatabaseType.KingbaseES, kingbaseConnection) is KingbaseInterpreter,
            "KingbaseES 应注册独立解释器，而不是伪装为 Postgres。");
        AssertEqual(KingbaseCompatibilityModes.Postgres,
            KingbaseCompatibilityModes.Normalize("postgres"));
        AssertContains("尚未完成", KingbaseCompatibilityModes.GetConnectionBlockReason(KingbaseCompatibilityModes.SqlServer)!);
        AssertTrue(QueryProfilerSql.SupportsAnalyze(DatabaseType.KingbaseES), "已验证的 KingbaseES PG 路径应提供 EXPLAIN ANALYZE。 ");
        AssertContains("EXPLAIN ANALYZE", QueryProfilerSql.BuildAnalyzeSql(DatabaseType.KingbaseES, "SELECT 1"));
        Console.WriteLine("All regression checks passed.");
        return 0;
    }

    private static void AssertEqual(string? expected, string? actual)
    {
        if (!string.Equals(expected, actual, StringComparison.Ordinal))
            throw new InvalidOperationException($"Expected '{expected}', got '{actual}'.");
    }

    private static void AssertEqualDate(DateTime expected, DateTime actual)
    {
        if (expected != actual)
            throw new InvalidOperationException($"Expected '{expected:O}', got '{actual:O}'.");
    }

    private static void AssertTrue(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private static void AssertContains(string expected, string actual)
    {
        if (!actual.Contains(expected, StringComparison.Ordinal))
            throw new InvalidOperationException($"Expected '{actual}' to contain '{expected}'.");
    }
}
