using DatabaseInterpreter.Model;
using DatabaseManager.AppCore.Models;
using DatabaseManager.AppCore.Services;

/// <summary>管理型只读目录（扩展/表空间/分区）的 SQL、文件夹和节点映射检查。</summary>
internal static class DbAdminCatalogChecks
{
    public static void Run()
    {
        VerifyCatalogSql();
        VerifyAdvancedCatalogSql();
        VerifyNodeMapping();
        VerifyAdvancedNodeMapping();
        VerifyFolders();
        VerifyAdvancedFolders();
        Console.WriteLine("Admin catalog checks passed: extension, tablespace, partition, synonym, package, materialized view and job directories.");
    }

    private static void VerifyCatalogSql()
    {
        string pgExtension = DbAdminCatalog.GetSql(DatabaseType.Postgres, DbCatalogKind.Extension);
        Check(pgExtension.Contains("pg_catalog.pg_extension", StringComparison.Ordinal)
              && pgExtension.Contains("extversion", StringComparison.Ordinal)
              && pgExtension.Contains("nspname", StringComparison.Ordinal),
            "PostgreSQL extension catalog must read name, version and schema.");
        Check(DbAdminCatalog.GetSql(DatabaseType.KingbaseES, DbCatalogKind.Extension) == pgExtension,
            "KingbaseES extension catalog must follow the PG-compatible path.");

        string tablespace = DbAdminCatalog.GetSql(DatabaseType.Oracle, DbCatalogKind.Tablespace);
        Check(tablespace.Contains("USER_TABLESPACES", StringComparison.Ordinal)
              && tablespace.Contains("BIGFILE", StringComparison.Ordinal),
            "Oracle tablespace catalog must use USER_TABLESPACES by default.");
        Check(DbAdminCatalog.GetSql(DatabaseType.Oracle, DbCatalogKind.Tablespace, dbaScope: true)
                .Contains("DBA_TABLESPACES", StringComparison.Ordinal),
            "Oracle DBA fallback must read DBA_TABLESPACES.");
        Check(DbAdminCatalog.GetSql(DatabaseType.DM, DbCatalogKind.Tablespace)
                .Contains("USER_TABLESPACES", StringComparison.Ordinal),
            "DM tablespace catalog must use the inherited Oracle view.");

        string scheme = DbAdminCatalog.GetSql(DatabaseType.SqlServer, DbCatalogKind.PartitionScheme);
        Check(scheme.Contains("sys.partition_schemes", StringComparison.Ordinal)
              && scheme.Contains("sys.destination_data_spaces", StringComparison.Ordinal),
            "Partition scheme catalog must read schemes and their partition count.");
        string function = DbAdminCatalog.GetSql(DatabaseType.SqlServer, DbCatalogKind.PartitionFunction);
        Check(function.Contains("sys.partition_functions", StringComparison.Ordinal)
              && function.Contains("sys.partition_range_values", StringComparison.Ordinal),
            "Partition function catalog must read functions and boundary count.");

        bool rejected = false;
        try { DbAdminCatalog.GetSql(DatabaseType.Sqlite, DbCatalogKind.Extension); }
        catch (NotSupportedException) { rejected = true; }
        Check(rejected, "Unsupported catalog types must fail with an explicit error.");
    }

    private static void VerifyAdvancedCatalogSql()
    {
        Check(DbAdminCatalog.GetSql(DatabaseType.Oracle, DbCatalogKind.Synonym)
                .Contains("USER_SYNONYMS", StringComparison.Ordinal),
            "Oracle synonym catalog must use USER_SYNONYMS for unprivileged accounts.");
        Check(DbAdminCatalog.GetSql(DatabaseType.SqlServer, DbCatalogKind.Synonym)
                .Contains("sys.synonyms", StringComparison.Ordinal),
            "SQL Server synonym catalog must read sys.synonyms.");

        string package = DbAdminCatalog.GetSql(DatabaseType.Oracle, DbCatalogKind.Package);
        Check(package.Contains("USER_SOURCE", StringComparison.Ordinal)
              && package.Contains("PACKAGE BODY", StringComparison.Ordinal),
            "Oracle package catalog must read package specs and bodies from USER_SOURCE.");

        Check(DbAdminCatalog.GetSql(DatabaseType.Oracle, DbCatalogKind.MaterializedView)
                .Contains("USER_MVIEWS", StringComparison.Ordinal),
            "Oracle materialized view catalog must use USER_MVIEWS.");
        string pgMv = DbAdminCatalog.GetSql(DatabaseType.Postgres, DbCatalogKind.MaterializedView);
        Check(pgMv.Contains("pg_catalog.pg_matviews", StringComparison.Ordinal),
            "PostgreSQL materialized view catalog must use pg_matviews.");

        Check(DbAdminCatalog.GetSql(DatabaseType.Oracle, DbCatalogKind.Job)
                .Contains("USER_SCHEDULER_JOBS", StringComparison.Ordinal),
            "Oracle job catalog must use USER_SCHEDULER_JOBS.");
        Check(DbAdminCatalog.GetSql(DatabaseType.SqlServer, DbCatalogKind.Job)
                .Contains("msdb.dbo.sysjobs", StringComparison.Ordinal),
            "SQL Server job catalog must read msdb sysjobs.");

        Check(DbAdminGuidance.GetCatalogPermissionHint(DatabaseType.SqlServer, DbCatalogKind.Job)
                .Contains("SQLAgentReaderRole", StringComparison.Ordinal),
            "SQL Server job errors must explain the agent role requirement.");
        Check(DbAdminGuidance.GetCatalogPermissionHint(DatabaseType.Oracle, DbCatalogKind.Package)
                .Contains("SELECT_CATALOG_ROLE", StringComparison.Ordinal),
            "Oracle package errors must explain catalog role requirements.");
    }

    private static void VerifyNodeMapping()
    {
        var extension = DbAdminCatalog.CreateNode(["uuid-ossp", "1.1", "public"], DbCatalogKind.Extension);
        Check(extension.Name == "uuid-ossp"
              && extension.NodeType == DbObjectTreeNodeType.CatalogObject
              && extension.CatalogDetails.Contains("1.1", StringComparison.Ordinal)
              && extension.CatalogDetails.Contains("public", StringComparison.Ordinal),
            "Extension nodes must keep version and schema details.");

        var tablespace = DbAdminCatalog.CreateNode(["USERS", "ONLINE", "PERMANENT", "NO"], DbCatalogKind.Tablespace);
        Check(tablespace.CatalogDetails.Contains("ONLINE", StringComparison.Ordinal)
              && tablespace.CatalogDetails.Contains("PERMANENT", StringComparison.Ordinal),
            "Tablespace nodes must expose status and contents.");

        var legacyTablespace = DbAdminCatalog.CreateNode(["USERS"], DbCatalogKind.Tablespace);
        Check(legacyTablespace.Name == "USERS" && legacyTablespace.CatalogDetails.Length == 0,
            "DM single-column tablespace rows must not break node creation.");

        var function = DbAdminCatalog.CreateNode(["pf_range", "R", "4"], DbCatalogKind.PartitionFunction);
        Check(function.CatalogDetails.Contains("RANGE", StringComparison.Ordinal)
              && function.CatalogDetails.Contains("4", StringComparison.Ordinal),
            "Partition function nodes must translate range type and show boundary count.");

        var scheme = DbAdminCatalog.CreateNode(["ps_order", "pf_range", "12"], DbCatalogKind.PartitionScheme);
        Check(scheme.CatalogDetails.Contains("pf_range", StringComparison.Ordinal)
              && scheme.CatalogDetails.Contains("12", StringComparison.Ordinal),
            "Partition scheme nodes must show function and partition count.");
    }

    private static void VerifyFolders()
    {
        Check(DbAdminCatalog.CreateFolders(Connection("Postgres"), null)
                .Any(f => f.CatalogKind == DbCatalogKind.Extension && f.DatabaseName is null),
            "PostgreSQL must expose the connection-level extension folder.");
        Check(!DbAdminCatalog.CreateFolders(Connection("Sqlite"), null).Any(),
            "SQLite must not expose admin catalog folders.");

        var oracleFolders = DbAdminCatalog.CreateFolders(Connection("Oracle"), "ORCL").ToList();
        Check(oracleFolders.Count == 5
              && oracleFolders.Any(f => f.CatalogKind == DbCatalogKind.Tablespace)
              && oracleFolders.Any(f => f.CatalogKind == DbCatalogKind.Synonym)
              && oracleFolders.Any(f => f.CatalogKind == DbCatalogKind.Package)
              && oracleFolders.Any(f => f.CatalogKind == DbCatalogKind.MaterializedView)
              && oracleFolders.Any(f => f.CatalogKind == DbCatalogKind.Job)
              && oracleFolders.All(f => f.DatabaseName == "ORCL"),
            "Oracle must expose tablespace, synonym, package, materialized view and job folders at database level.");

        var sqlServerFolders = DbAdminCatalog.CreateFolders(Connection("SqlServer"), "app_db")
            .Select(f => f.CatalogKind).ToList();
        Check(sqlServerFolders.Contains(DbCatalogKind.PartitionScheme)
              && sqlServerFolders.Contains(DbCatalogKind.PartitionFunction)
              && sqlServerFolders.Contains(DbCatalogKind.Synonym),
            "SQL Server must expose partition and synonym folders at database level.");
        Check(DbAdminCatalog.CreateFolders(Connection("SqlServer"), null)
                .Any(f => f.CatalogKind == DbCatalogKind.Job && f.DatabaseName is null),
            "SQL Server jobs must be exposed at connection level via msdb.");
    }

    private static void VerifyAdvancedNodeMapping()
    {
        var oracleSynonym = DbAdminCatalog.CreateNode(["EMP_SYN", "HR", "EMPLOYEES", "REMOTE_DB"], DbCatalogKind.Synonym);
        Check(oracleSynonym.CatalogDetails.Contains("HR.EMPLOYEES", StringComparison.Ordinal)
              && oracleSynonym.CatalogDetails.Contains("REMOTE_DB", StringComparison.Ordinal),
            "Oracle synonym nodes must show target object and database link.");
        var sqlServerSynonym = DbAdminCatalog.CreateNode(["syn_orders", "dbo", "Sales.Orders"], DbCatalogKind.Synonym);
        Check(sqlServerSynonym.CatalogDetails.Contains("Sales.Orders", StringComparison.Ordinal),
            "SQL Server synonym nodes must show the base object name.");

        var spec = DbAdminCatalog.CreateNode(["PKG_UTIL", "PACKAGE"], DbCatalogKind.Package);
        var body = DbAdminCatalog.CreateNode(["PKG_UTIL", "PACKAGE BODY"], DbCatalogKind.Package);
        Check(spec.CatalogDetails == "包说明" && body.CatalogDetails == "包体",
            "Package nodes must distinguish spec from body.");

        var oracleMv = DbAdminCatalog.CreateNode(["MV_SALES", "COMPLETE", "FAST", "2026-11-23 09:30"], DbCatalogKind.MaterializedView);
        Check(oracleMv.CatalogDetails.Contains("COMPLETE", StringComparison.Ordinal)
              && oracleMv.CatalogDetails.Contains("2026-11-23", StringComparison.Ordinal),
            "Oracle materialized view nodes must show refresh mode and last refresh.");
        var pgMv = DbAdminCatalog.CreateNode(["mv_sales", "true", "SELECT * FROM sales"], DbCatalogKind.MaterializedView);
        Check(pgMv.CatalogDetails.Contains("已填充", StringComparison.Ordinal)
              && pgMv.CatalogDetails.Contains("SELECT", StringComparison.Ordinal),
            "PostgreSQL materialized view nodes must show population state and definition.");

        var oracleJob = DbAdminCatalog.CreateNode(["JOB_ETL", "SCHEDULED", "TRUE", "FREQ=DAILY"], DbCatalogKind.Job);
        Check(oracleJob.CatalogDetails.Contains("启用", StringComparison.Ordinal)
              && oracleJob.CatalogDetails.Contains("FREQ=DAILY", StringComparison.Ordinal),
            "Oracle job nodes must show state, enabled flag and schedule.");
        var sqlServerJob = DbAdminCatalog.CreateNode(["Nightly ETL", "False", "Legacy job"], DbCatalogKind.Job);
        Check(sqlServerJob.CatalogDetails.Contains("禁用", StringComparison.Ordinal),
            "SQL Server job nodes must translate the enabled flag.");
    }

    private static void VerifyAdvancedFolders()
    {
        Check(!DbAdminCatalog.CreateFolders(Connection("Postgres"), "app_db")
                .Any(f => f.CatalogKind == DbCatalogKind.Job),
            "PostgreSQL jobs must not be exposed because pg_cron is not built in.");
        Check(!DbAdminCatalog.CreateFolders(Connection("Sqlite"), null)
                .Any(f => f.CatalogKind is DbCatalogKind.Synonym or DbCatalogKind.Package
                    or DbCatalogKind.MaterializedView or DbCatalogKind.Job),
            "SQLite must not expose any advanced catalog folder.");
    }

    private static ConnectionItem Connection(string databaseType) => new()
    {
        Name = $"catalog-{databaseType}",
        DatabaseType = databaseType,
        Database = "fixture",
    };

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
