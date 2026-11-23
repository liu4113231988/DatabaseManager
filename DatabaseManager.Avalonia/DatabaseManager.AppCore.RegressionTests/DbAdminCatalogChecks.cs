using DatabaseInterpreter.Model;
using DatabaseManager.AppCore.Models;
using DatabaseManager.AppCore.Services;

/// <summary>管理型只读目录（扩展/表空间/分区）的 SQL、文件夹和节点映射检查。</summary>
internal static class DbAdminCatalogChecks
{
    public static void Run()
    {
        VerifyCatalogSql();
        VerifyNodeMapping();
        VerifyFolders();
        Console.WriteLine("Admin catalog checks passed: extension, tablespace and partition directories.");
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
        Check(oracleFolders.Count == 1
              && oracleFolders[0].CatalogKind == DbCatalogKind.Tablespace
              && oracleFolders[0].DatabaseName == "ORCL",
            "Oracle must expose a database-level tablespace folder.");

        var sqlServerFolders = DbAdminCatalog.CreateFolders(Connection("SqlServer"), "app_db")
            .Select(f => f.CatalogKind).ToList();
        Check(sqlServerFolders.Contains(DbCatalogKind.PartitionScheme)
              && sqlServerFolders.Contains(DbCatalogKind.PartitionFunction),
            "SQL Server must expose partition scheme and function folders at database level.");
        Check(!DbAdminCatalog.CreateFolders(Connection("SqlServer"), null).Any(),
            "Partition folders must not be created at connection level.");
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
