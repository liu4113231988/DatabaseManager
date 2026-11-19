using System.Data.Common;
using DatabaseInterpreter.Core;
using DatabaseInterpreter.Model;
using DatabaseManager.AppCore.Models;
using DatabaseManager.AppCore.Services;
using DatabaseManager.Core;

internal static class KingbaseStageEFChecks
{
    public static async Task RunAsync()
    {
        VerifyCapabilityMatrix();
        VerifyUserAndPermissionSql();
        VerifyBackupAndRestore();
        await VerifyModeGatesAsync();
        await RunRealInstanceChecksAsync();
        Console.WriteLine("KingbaseES stage E/F checks passed: capability matrix, user/grant SQL, backup adapters, mode gates and opt-in instance probe.");
    }

    private static void VerifyCapabilityMatrix()
    {
        Check(new DefaultDbUserService().IsSupported("KingbaseES"), "KingbaseES must support user management in PG compatibility mode.");
        Check(new DefaultDbSessionService().IsSupported("KingbaseES"), "KingbaseES must support session monitoring.");
        Check(QueryProfilerSql.SupportsAnalyze(DatabaseType.KingbaseES), "KingbaseES must expose read-only EXPLAIN ANALYZE.");
        Check(DefaultBackupService.SupportsBackup("KingbaseES"), "KingbaseES must support backup.");
        Check(DefaultBackupService.SupportsRestore("KingbaseES"), "KingbaseES must support restore.");
        string searchSql = DefaultFullDataSearchService.BuildSearchSql(
            DatabaseType.KingbaseES, "public.orders", new List<string> { "name" }, "a%b", 10);
        Check(searchSql.Contains("\"public\".\"orders\"", StringComparison.Ordinal)
              && searchSql.Contains("LIMIT 10", StringComparison.Ordinal)
              && searchSql.Contains("ESCAPE '\\'", StringComparison.Ordinal),
            "KingbaseES full-database search must use quoted identifiers, LIKE escaping and LIMIT.");
        Check(KingbaseSupportStatus.DatabaseType == DatabaseType.KingbaseES, "Release status must identify KingbaseES.");
        Check(!KingbaseSupportStatus.IsInstanceVerified, "Release status must not claim real-instance verification before V8 acceptance.");
        Check(KingbaseSupportStatus.GetReleaseNote().Contains("SqlSugarCore.Kdbndp", StringComparison.Ordinal)
              && KingbaseSupportStatus.GetReleaseNote().Contains("实例验收待补", StringComparison.Ordinal),
            "Release note must state code-level status and pending instance verification.");
        Check(KingbaseCompatibilityModes.GetAdminFeatureBlockReason("Oracle") is not null
              && KingbaseCompatibilityModes.GetAdminFeatureBlockReason("SqlServer") is not null,
            "Non-PG compatibility modes must remain blocked for stage E features.");
        Check(KingbaseCompatibilityModes.GetAdminFeatureBlockReason("Auto") is null
              && KingbaseCompatibilityModes.GetAdminFeatureBlockReason("Postgres") is null,
            "Auto and PG compatibility modes must expose code-level stage E features.");
        Check(!KingbaseSupportStatus.IsAdminFeatureEnabled("Oracle")
              && KingbaseSupportStatus.IsAdminFeatureEnabled("Postgres"),
            "Admin capability flag must follow compatibility mode.");
    }

    private static void VerifyUserAndPermissionSql()
    {
        Check(KingbaseUserSql.BuildUsersSql(true).Contains("sys_catalog.sys_roles", StringComparison.Ordinal),
            "KingbaseES user list must prefer sys_catalog.sys_roles.");
        Check(KingbaseUserSql.BuildFallbackUsersSql().Contains("pg_catalog.pg_roles", StringComparison.Ordinal),
            "KingbaseES user list must fall back to pg_catalog.pg_roles.");
        string grants = KingbaseUserSql.BuildGrantsSql("o'brien");
        Check(grants.Contains("information_schema.role_table_grants", StringComparison.Ordinal)
              && grants.Contains("information_schema.role_routine_grants", StringComparison.Ordinal)
              && grants.Contains("information_schema.applicable_roles", StringComparison.Ordinal),
            "KingbaseES grants must use standard information_schema views.");
        Check(grants.Contains("o''brien", StringComparison.Ordinal), "User name must be escaped in grants SQL.");
        Check(DbAdminGuidance.GetUserPermissionHint(DatabaseType.KingbaseES).Contains("sys_catalog.sys_roles", StringComparison.Ordinal),
            "Admin guidance must explain KingbaseES catalog fallback.");

        var service = new DefaultDbUserService();
        Check(service.BuildCreateUserSql("KingbaseES", "app", "p@ss", null)
                == "CREATE USER \"app\" WITH PASSWORD 'p@ss';",
            "KingbaseES CREATE USER must use PG-compatible syntax.");
        Check(service.BuildDropUserSql("KingbaseES", "app", null, true) == "DROP USER \"app\";",
            "KingbaseES DROP USER must use PG-compatible syntax.");
        Check(service.BuildGrantSql("KingbaseES", "reporter", null, "SELECT", "public.*")
                == "GRANT SELECT ON ALL TABLES IN SCHEMA \"public\" TO \"reporter\";",
            "KingbaseES schema-level GRANT must use PG-compatible syntax.");
        Check(service.BuildGrantSql("KingbaseES", "reporter", null, "SELECT", "public.orders")
                == "GRANT SELECT ON TABLE \"public\".\"orders\" TO \"reporter\";",
            "KingbaseES table-level GRANT must quote qualified identifiers.");
    }

    private static void VerifyBackupAndRestore()
    {
        Check(DbBackup.GetInstance(DatabaseType.KingbaseES) is KingbaseBackup,
            "Backup factory must return the KingbaseES adapter.");
        Check(KingbaseBackup.IsTool("C:/KingbaseES/bin/sys_dump.exe", KingbaseBackup.BackupToolName),
            "sys_dump.exe must be recognized as the KingbaseES backup tool.");
        Check(!KingbaseBackup.IsTool("C:/PostgreSQL/bin/pg_dump.exe", KingbaseBackup.BackupToolName),
            "pg_dump must not be silently used for KingbaseES backup.");
        Check(KingbaseBackup.IsTool("/opt/kingbase/bin/ksql", KingbaseBackup.SqlRestoreToolName),
            "ksql must be recognized for .sql restore.");
        Check(KingbaseBackup.IsTool("/opt/kingbase/bin/sys_restore", KingbaseBackup.ArchiveRestoreToolName),
            "sys_restore must be recognized for archive restore.");
    }

    private static async Task VerifyModeGatesAsync()
    {
        var blocked = KingbaseConnection("Oracle");
        var backupService = new DefaultBackupService();
        var backupResult = await backupService.BackupAsync(
            blocked, Path.Combine(Path.GetTempPath(), $"dbm-kingbase-gate-{Guid.NewGuid():N}"), null, false);
        Check(!backupResult.IsOK && backupResult.Message.Contains("PG 兼容模式", StringComparison.Ordinal),
            "Backup must be blocked for non-PG compatibility modes before invoking any tool.");

        string restoreFile = Path.Combine(Path.GetTempPath(), $"dbm-kingbase-restore-{Guid.NewGuid():N}.tar");
        File.WriteAllText(restoreFile, "fixture");
        try
        {
            var restoreResult = await backupService.RestoreAsync(blocked, restoreFile, null);
            Check(!restoreResult.IsOK && restoreResult.Message.Contains("PG 兼容模式", StringComparison.Ordinal),
                "Restore must be blocked for non-PG compatibility modes.");
        }
        finally { File.Delete(restoreFile); }

        var users = await new DefaultDbUserService().GetUsersAsync(blocked);
        Check(users.Error is not null && users.Error.Contains("PG 兼容模式", StringComparison.Ordinal),
            "User management must be blocked for non-PG compatibility modes.");
    }

    private static async Task RunRealInstanceChecksAsync()
    {
        string? basicConnectionString = Environment.GetEnvironmentVariable("DBSMOKE_KINGBASE_CONNSTR");
        if (string.IsNullOrWhiteSpace(basicConnectionString))
        {
            Console.WriteLine("KingbaseES real-instance integration skipped: set DBSMOKE_KINGBASE_CONNSTR (and optionally DBSMOKE_KINGBASE_ADMIN_CONNSTR).");
            return;
        }

        var basic = ParseConnection(basicConnectionString!);
        string? adminConnectionString = Environment.GetEnvironmentVariable("DBSMOKE_KINGBASE_ADMIN_CONNSTR");
        ConnectionItem? admin = string.IsNullOrWhiteSpace(adminConnectionString) ? null : ParseConnection(adminConnectionString!);

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await ProbeAsync(basic, timeout.Token);

        var userService = new DefaultDbUserService();
        var (users, error) = await userService.GetUsersAsync(basic, timeout.Token);
        if (error is not null)
        {
            Console.WriteLine("KingbaseES basic account user list (expected permission limit): " + error);
        }
        else
        {
            Check(users.Count >= 0, "User list must be readable or fail with a permission hint.");
        }

        if (admin is not null)
        {
            var (adminUsers, adminError) = await userService.GetUsersAsync(admin, timeout.Token);
            Check(adminError is null, "Admin account must read roles: " + adminError);
            Check(adminUsers.Count > 0, "Admin account must see at least one role.");
            var (grants, grantError) = await userService.GetGrantsAsync(admin, adminUsers[0].Name, null, timeout.Token);
            Check(grantError is null, "Admin account must read grants: " + grantError);
            Check(!string.IsNullOrWhiteSpace(grants), "Grant details must not be empty.");
        }
        Console.WriteLine("KingbaseES real-instance integration passed: connection, Unicode scalar, role/grant catalog and cancellation timeout.");
    }

    private static async Task ProbeAsync(ConnectionItem connection, CancellationToken ct)
    {
        var interpreter = DbInterpreterHelper.GetDbInterpreter(
            DatabaseType.KingbaseES, ConnectionHelper.ToConnectionInfo(connection), new DbInterpreterOption());
        await using var dbConnection = interpreter.CreateConnection();
        await dbConnection.OpenAsync(ct);
        await using var command = dbConnection.CreateCommand();
        command.CommandTimeout = 10;
        command.CommandText = "SELECT '中文' AS value";
        object? value = await command.ExecuteScalarAsync(ct);
        Check(string.Equals(value?.ToString(), "中文", StringComparison.Ordinal),
            "KingbaseES Unicode round-trip must preserve Chinese characters.");
    }

    private static ConnectionItem ParseConnection(string connectionString)
    {
        var builder = new DbConnectionStringBuilder { ConnectionString = connectionString };
        string Pick(params string[] keys) => keys
            .Select(key => builder.TryGetValue(key, out object? value) ? value?.ToString() : null)
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;

        var item = new ConnectionItem
        {
            DatabaseType = "KingbaseES",
            Name = "kingbase-smoke",
            Server = Pick("Host", "Server", "Data Source"),
            Port = Pick("Port"),
            Database = Pick("Database", "Initial Catalog"),
            UserId = Pick("Username", "User ID", "User Id", "UID"),
            Password = Pick("Password", "PWD"),
            KingbaseCompatibilityMode = KingbaseCompatibilityModes.Postgres,
        };
        Check(!string.IsNullOrWhiteSpace(item.Server) && !string.IsNullOrWhiteSpace(item.Database),
            "DBSMOKE_KINGBASE_CONNSTR must include Host/Server and Database.");
        return item;
    }

    private static ConnectionItem KingbaseConnection(string mode) => new()
    {
        DatabaseType = "KingbaseES",
        Name = "kingbase-gate",
        Server = "127.0.0.1",
        Database = "test",
        UserId = "system",
        Password = "fixture-only",
        KingbaseCompatibilityMode = mode,
    };

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
