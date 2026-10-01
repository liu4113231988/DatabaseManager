using DatabaseInterpreter.Model;
using DatabaseInterpreter.Core;
using DatabaseManager.AppCore.Common;
using DatabaseManager.AppCore.Models;
using DatabaseManager.AppCore.Services;
using DatabaseManager.Core;
using DatabaseManager.Core.Model;
using DatabaseManager.Profile.Manager;
using DatabaseManager.Profile.Security;
using System.Security.Cryptography;
using System.Text;

static class Program
{
    private static async Task VerifySqliteConnectionAsync()
    {
        var connection = new ConnectionItem { DatabaseType = "Sqlite", Database = "" };
        AssertContains("路径", ConnectionHelper.ValidateEndpoint(connection)!);
        AssertTrue(ConnectionHelper.IsFileDatabase(DatabaseType.Sqlite), "SQLite 应使用文件连接表单。");
        AssertTrue(ConnectionHelper.IsFileDatabase(DatabaseType.DuckDB), "DuckDB 应使用文件连接表单。");
        AssertTrue(!ConnectionHelper.IsFileDatabase(DatabaseType.MySql), "MySQL 应使用服务器连接表单。");
        AssertContains("服务器", ConnectionHelper.ValidateEndpoint(new ConnectionItem { DatabaseType = "MySql" })!);
        var path = Path.Combine(Path.GetTempPath(), $"dbm-sqlite-{Guid.NewGuid():N}.db");
        connection.Database = path;
        var service = new ProfileDbConnectionService(null!);
        try
        {
            AssertContains("不存在", ConnectionHelper.ValidateEndpoint(connection)!);
            try
            {
                await service.TestConnectionAsync(connection);
                throw new InvalidOperationException("缺失的 SQLite 文件不能通过连接测试。");
            }
            catch (ArgumentException) { }
            AssertTrue(!File.Exists(path), "测试连接不能创建缺失的数据库文件。");
            using (var sqlite = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={path};Pooling=False"))
            {
                await sqlite.OpenAsync();
                using var command = sqlite.CreateCommand();
                command.CommandText = "CREATE TABLE sample (id INTEGER)";
                await command.ExecuteNonQueryAsync();
            }
            AssertEqual(null, ConnectionHelper.ValidateEndpoint(connection));
            AssertEqual(path, (await service.TestConnectionAsync(connection)).Single());
            await File.WriteAllTextAsync(path, "invalid sqlite database");
            try
            {
                await service.TestConnectionAsync(connection);
                throw new InvalidOperationException("无效的 SQLite 文件不能通过连接测试。");
            }
            catch (Microsoft.Data.Sqlite.SqliteException) { }
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static void VerifyConnectionInputs()
    {
        var ports = new Dictionary<DatabaseType, string>
        {
            [DatabaseType.MySql] = "3306", [DatabaseType.Oracle] = "1521",
            [DatabaseType.Postgres] = "5432", [DatabaseType.KingbaseES] = "54321",
            [DatabaseType.DM] = "5236", [DatabaseType.SqlServer] = "",
            [DatabaseType.Sqlite] = "", [DatabaseType.DuckDB] = "",
        };
        foreach (var (type, expected) in ports)
        {
            AssertEqual(expected, ConnectionHelper.GetDefaultPort(type));
            AssertEqual(expected, ConnectionHelper.UpdateDefaultPort(DatabaseType.MySql, type, "3306"));
            AssertEqual("13306", ConnectionHelper.UpdateDefaultPort(DatabaseType.MySql, type, "13306"));
            if (ConnectionHelper.IsFileDatabase(type)) continue;
            var connection = new ConnectionItem { DatabaseType = type.ToString(), Server = "localhost", UserId = "tester" };
            AssertEqual(null, ConnectionHelper.ValidateEndpoint(connection));
            foreach (var port in new[] { "0", "-1", "65536", "abc" })
            {
                connection.Port = port;
                AssertContains("端口", ConnectionHelper.ValidateEndpoint(connection)!);
            }
            connection.Port = "65535";
            AssertEqual(null, ConnectionHelper.ValidateEndpoint(connection));
            connection.UserId = null;
            AssertContains("用户名", ConnectionHelper.ValidateEndpoint(connection)!);
            connection.IntegratedSecurity = true;
            AssertEqual(DatabaseAuthentication.SupportsIntegratedSecurity(type) ? null : "该数据库类型不支持 Windows 身份验证。",
                ConnectionHelper.ValidateEndpoint(connection));
        }
        AssertTrue(!ConnectionHelper.RequiresDatabase(DatabaseType.Oracle) && !ConnectionHelper.RequiresDatabase(DatabaseType.DM),
            "Oracle/DM 不应强制填写数据库名。");
        AssertTrue(ConnectionHelper.RequiresDatabase(DatabaseType.Postgres), "PostgreSQL 保存时需要目标数据库。");
        AssertTrue(ConnectionHelper.SupportsSsl(DatabaseType.Postgres), "PostgreSQL 应显示 SSL 设置。");
        AssertTrue(!ConnectionHelper.SupportsSsl(DatabaseType.DM), "DM 不应沿用隐藏的 SSL 设置。");
        var duck = new ConnectionItem { DatabaseType = "DuckDB", Database = ":memory:" };
        AssertEqual(null, ConnectionHelper.ValidateEndpoint(duck));
        duck.DuckDbReadOnly = true;
        AssertContains("内存", ConnectionHelper.ValidateEndpoint(duck)!);
        duck.Database = Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}.duckdb");
        AssertContains("已有", ConnectionHelper.ValidateEndpoint(duck)!);

        var id = $"regression-{Guid.NewGuid():N}";
        var visuals = new DefaultConnectionVisualService();
        try
        {
            visuals.Save(id, "readonly-test", null, null, duckDbReadOnly: true);
            AssertTrue(new DefaultConnectionVisualService().Find(id)?.DuckDbReadOnly == true, "DuckDB 只读设置应在重新读取后保留。");
            visuals.Save(id, "readonly-test", "group", null);
            AssertTrue(new DefaultConnectionVisualService().Find(id)?.DuckDbReadOnly == true, "修改分组不能清除只读设置。");
            visuals.Save(id, "readonly-test", "group", null, duckDbReadOnly: false);
            AssertTrue(new DefaultConnectionVisualService().Find(id)?.DuckDbReadOnly == false, "只读设置应允许关闭。");
        }
        finally { visuals.Remove(id); }
    }

    private static int Main()
    {
        VerifyConnectionInputs();
        VerifySqliteConnectionAsync().GetAwaiter().GetResult();
        P2FeatureChecks.RunAsync().GetAwaiter().GetResult();
        PostgresP2Checks.RunAsync().GetAwaiter().GetResult();
        QueryExecutionChecks.RunAsync().GetAwaiter().GetResult();
        P0FeatureChecks.RunAsync().GetAwaiter().GetResult();
        PostgresP0Checks.RunAsync().GetAwaiter().GetResult();
        if (Environment.GetEnvironmentVariable("DBM_TEST_TEMP_DATABASES") == "1") PostgresJobChecks.RunAsync().GetAwaiter().GetResult();
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
        AssertEqual($"SELECT id, name FROM orders{Environment.NewLine}ORDER BY 2 DESC;",
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
        AssertContains("不存在", DbAdminGuidance.ValidateClientToolPath(Path.Combine(Path.GetTempPath(), $"missing-tool-{Guid.NewGuid():N}.exe"))!);
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
        AssertContains("sys_stat_activity", DbSessionSql.BuildSessionsSql(DatabaseType.KingbaseES));
        AssertContains("sys_blocking_pids", DbSessionSql.BuildLocksSql(DatabaseType.KingbaseES));
        AssertEqual("SELECT sys_terminate_backend(12345)", DbSessionSql.BuildTerminateSessionSql(DatabaseType.KingbaseES, "12345"));
        AssertEqual(null, DbSessionSql.BuildTerminateSessionSql(DatabaseType.KingbaseES, "12345; SELECT 1"));
        AssertContains("pg_stat_activity", DbSessionSql.BuildKingbaseFallbackSessionsSql());
        AssertContains("pg_blocking_pids", DbSessionSql.BuildKingbaseFallbackLocksSql());
        AssertEqual("SELECT pg_terminate_backend(12345)", DbSessionSql.BuildKingbaseFallbackTerminateSessionSql("12345"));
        AssertEqual(null, DbSessionSql.BuildKingbaseFallbackTerminateSessionSql("12345; SELECT 1"));
        AssertContains("sys_catalog", DbSessionSql.BuildKingbaseProbeSql());
        var kingbaseInterpreter = DbInterpreterHelper.GetDbInterpreter(DatabaseType.KingbaseES, kingbaseConnection);
        AssertTrue(DbScriptGeneratorHelper.GetDbScriptGenerator(kingbaseInterpreter) is PostgresScriptGenerator,
            "KingbaseES PG 兼容路径应使用 PostgreSQL 脚本生成器。 ");
        AssertTrue(!kingbaseInterpreter.SupportBulkCopy,
            "Kdbndp 二进制批量导入尚未验证前，KingbaseES 必须退回可回放的参数化批量插入。 ");

        VerifyCredentialProtection();
        VerifyOracleRestoreDoesNotExposePassword();
        VerifyProfileInitializationIsCatchable();
        var kingbaseCondition = new QueryConditionBuilder
        {
            DatabaseType = DatabaseType.KingbaseES,
            QuotationLeftChar = '"',
            QuotationRightChar = '"',
        };
        kingbaseCondition.Add(new QueryConditionItem
        {
            ColumnName = "created_at",
            DataType = typeof(DateTime),
            Mode = QueryConditionMode.Single,
            Operator = "=",
            Value = "2026-08-31",
        });
        AssertContains("::CHARACTER VARYING", kingbaseCondition.ToString());

        // 阶段 D 任务 5：跨库转换能力标记。KingbaseES 未用真实实例验证前，必须
        // 禁用并返回明确提示，而不是静默套用 PostgreSQL 翻译规则。
        AssertContains("未验证", DefaultConvertService.GetConversionBlockReason(DatabaseType.KingbaseES)!);
        AssertEqual(null, DefaultConvertService.GetConversionBlockReason(DatabaseType.Postgres));
        AssertTrue(DefaultConvertService.UnverifiedConversionTypes.Contains(DatabaseType.KingbaseES),
            "KingbaseES 应列入未验证转换能力集合，避免静默执行。 ");

        Console.WriteLine("All regression checks passed.");
        return 0;
    }

    private static void VerifyCredentialProtection()
    {
        var folder = Path.Combine(Path.GetTempPath(), $"dbm-credential-{Guid.NewGuid():N}");
        try
        {
            var protector = new CredentialProtector(folder);
            const string secret = "P@ssw0rd-安全";
            var first = protector.Protect(secret);
            var second = protector.Protect(secret);

            AssertTrue(CredentialProtector.IsCurrentFormat(first), "保存的凭据必须使用带版本的安全格式。");
            AssertTrue(!string.Equals(first, second, StringComparison.Ordinal), "相同凭据每次加密必须产生不同密文。");
            AssertEqual(secret, protector.Unprotect(first));

            var legacy = CreateLegacyCiphertext(secret);
            AssertEqual(secret, protector.Unprotect(legacy, out bool needsMigration));
            AssertTrue(needsMigration, "旧版凭据读取后必须标记为待迁移。");

            var tampered = first.ToCharArray();
            var index = first.LastIndexOf(':') + 2;
            tampered[index] = tampered[index] == 'A' ? 'B' : 'A';
            bool rejected = false;
            try
            {
                protector.Unprotect(new string(tampered));
            }
            catch (CryptographicException)
            {
                rejected = true;
            }
            AssertTrue(rejected, "被篡改的凭据密文必须被拒绝。");
        }
        finally
        {
            if (Directory.Exists(folder)) Directory.Delete(folder, recursive: true);
        }
    }

    private static string CreateLegacyCiphertext(string plainText)
    {
        using var aes = Aes.Create();
        aes.Key = Encoding.UTF8.GetBytes(string.Concat("FA5DEAAB-5171-", "405A-9CED-E2C6DED6"));
        aes.IV = Encoding.UTF8.GetBytes(string.Concat("12345678", "12345678"));
        using var encryptor = aes.CreateEncryptor();
        var plain = Encoding.UTF8.GetBytes(plainText);
        try
        {
            return Convert.ToBase64String(encryptor.TransformFinalBlock(plain, 0, plain.Length));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plain);
        }
    }

    private static void VerifyOracleRestoreDoesNotExposePassword()
    {
        var connection = new ConnectionItem
        {
            Server = "db.example.test",
            Port = "1521",
            Database = "ORCL",
            UserId = "restore_user",
            Password = "command-line-secret",
        };
        var invocation = DefaultBackupService.BuildOracleRestoreInvocation(connection, "backup.dmp");
        AssertTrue(invocation.Arguments.All(argument => !argument.Contains(connection.Password, StringComparison.Ordinal)),
            "Oracle 恢复命令行不得包含密码。");
        AssertContains("restore_user@db.example.test:1521/ORCL", invocation.StandardInput);
        AssertContains(connection.Password, invocation.StandardInput);
    }

    private static void VerifyProfileInitializationIsCatchable()
    {
        var syncInit = typeof(ProfileBaseManager).GetMethod(nameof(ProfileBaseManager.Init));
        var asyncInit = typeof(ProfileBaseManager).GetMethod(nameof(ProfileBaseManager.InitAsync));
        AssertTrue(syncInit?.ReturnType == typeof(void), "Profile 同步初始化入口必须保留兼容性。");
        AssertTrue(asyncInit?.ReturnType == typeof(Task), "Profile 异步初始化必须返回 Task，禁止 async void 丢失异常。");
        AssertTrue(syncInit?.GetCustomAttributes(typeof(System.Runtime.CompilerServices.AsyncStateMachineAttribute), false).Length == 0,
            "Profile 同步初始化入口不得是 async void。");
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
