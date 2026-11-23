using DatabaseManager.AppCore.Services;

/// <summary>用户与角色管理 SQL 生成的回归检查（纯模板，不依赖真实实例）。</summary>
internal static class DbPrincipalChecks
{
    public static void Run()
    {
        var service = new DefaultDbUserService();

        VerifyCreateRole(service);
        VerifyDropRole(service);
        VerifyRevoke(service);
        VerifyRoleMembership(service);
        VerifyAlterPassword(service);
        VerifyCatalogColumns();

        Console.WriteLine("User/role management checks passed: role lifecycle, revoke, membership, password and principal catalog.");
    }

    private static void VerifyCreateRole(DefaultDbUserService service)
    {
        Check(service.BuildCreateRoleSql("Postgres", "app_reader", null, false, null)
              == "CREATE ROLE \"app_reader\";",
            "PostgreSQL role creation must use plain CREATE ROLE.");
        Check(service.BuildCreateRoleSql("Postgres", "app_login", "s3cret", true, null)
              == "CREATE ROLE \"app_login\" LOGIN PASSWORD 's3cret';",
            "PostgreSQL login role must include LOGIN and PASSWORD.");
        Check(service.BuildCreateRoleSql("KingbaseES", "app_reader", null, false, null)
              == "CREATE ROLE \"app_reader\";",
            "KingbaseES role creation must follow PG-compatible syntax.");
        Check(service.BuildCreateRoleSql("MySql", "app_reader", null, false, null)
              == "CREATE ROLE 'app_reader'@'%';",
            "MySQL role creation must include the host part.");
        Check(service.BuildCreateRoleSql("SqlServer", "app_reader", null, false, null)
              == "CREATE ROLE [app_reader];",
            "SQL Server role creation must quote the identifier.");
        Check(service.BuildCreateRoleSql("Oracle", "APP_READER", null, false, null)
              == "CREATE ROLE \"APP_READER\";",
            "Oracle role creation must quote the identifier.");
        Check(service.BuildCreateRoleSql("Sqlite", "app_reader", null, false, null) == string.Empty,
            "Unsupported database types must not generate role DDL.");
    }

    private static void VerifyDropRole(DefaultDbUserService service)
    {
        Check(service.BuildDropRoleSql("Postgres", "app_reader", null) == "DROP ROLE \"app_reader\";",
            "PostgreSQL role removal must use DROP ROLE.");
        Check(service.BuildDropRoleSql("KingbaseES", "app_reader", null) == "DROP ROLE \"app_reader\";",
            "KingbaseES role removal must use DROP ROLE.");
        Check(service.BuildDropRoleSql("MySql", "app_reader", "localhost") == "DROP ROLE 'app_reader'@'localhost';",
            "MySQL role removal must preserve the host part.");
        Check(service.BuildDropRoleSql("SqlServer", "app_reader", null) == "DROP ROLE [app_reader];",
            "SQL Server role removal must quote the identifier.");
    }

    private static void VerifyRevoke(DefaultDbUserService service)
    {
        Check(service.BuildRevokeSql("Postgres", "reporter", null, "SELECT", "public.*")
              == "REVOKE SELECT ON ALL TABLES IN SCHEMA \"public\" FROM \"reporter\";",
            "PostgreSQL schema-level REVOKE must use PG syntax.");
        Check(service.BuildRevokeSql("KingbaseES", "reporter", null, "SELECT", "public.orders")
              == "REVOKE SELECT ON TABLE \"public\".\"orders\" FROM \"reporter\";",
            "KingbaseES table-level REVOKE must quote qualified identifiers.");
        Check(service.BuildRevokeSql("MySql", "app", "%", "ALL PRIVILEGES", "shop.orders")
              == "REVOKE ALL PRIVILEGES ON `shop`.`orders` FROM 'app'@'%';",
            "MySQL REVOKE must include the host part.");
        Check(service.BuildRevokeSql("SqlServer", "app", null, "SELECT", "dbo.orders")
              == "REVOKE SELECT ON OBJECT::[dbo].[orders] FROM [app];",
            "SQL Server REVOKE must use OBJECT:: scope.");
        Check(service.BuildRevokeSql("Oracle", "app", null, "SELECT", "SHOP.ORDERS")
              .StartsWith("REVOKE SELECT ON ", StringComparison.Ordinal),
            "Oracle REVOKE must be generated for qualified objects.");
        Check(service.BuildRevokeSql("Postgres", "app", null, "DROP TABLE", "public.orders") == string.Empty,
            "Unsupported privileges must be rejected.");
        Check(service.BuildRevokeSql("Oracle", "app", null, "SELECT", "*.*") == string.Empty,
            "Invalid grant targets must be rejected.");
    }

    private static void VerifyRoleMembership(DefaultDbUserService service)
    {
        Check(service.BuildGrantRoleSql("Postgres", "app_reader", "app_writer", null)
              == "GRANT \"app_reader\" TO \"app_writer\";",
            "PostgreSQL role membership must use GRANT role TO member.");
        Check(service.BuildRevokeRoleSql("KingbaseES", "app_reader", "app_writer", null)
              == "REVOKE \"app_reader\" FROM \"app_writer\";",
            "KingbaseES membership revocation must use PG syntax.");
        Check(service.BuildGrantRoleSql("MySql", "reader_role", "app", "%")
              == "GRANT 'reader_role'@'%' TO 'app'@'%';",
            "MySQL role grant must use account syntax.");
        Check(service.BuildRevokeRoleSql("MySql", "reader_role", "app", "localhost")
              == "REVOKE 'reader_role'@'%' FROM 'app'@'localhost';",
            "MySQL role revocation must use account syntax.");
        Check(service.BuildGrantRoleSql("SqlServer", "db_datareader", "app", null)
              == "ALTER ROLE [db_datareader] ADD MEMBER [app];",
            "SQL Server membership must use ALTER ROLE.");
        Check(service.BuildRevokeRoleSql("SqlServer", "db_datareader", "app", null)
              == "ALTER ROLE [db_datareader] DROP MEMBER [app];",
            "SQL Server membership revocation must use ALTER ROLE.");
        Check(service.BuildGrantRoleSql("Oracle", "READER_ROLE", "APP", null)
              == "GRANT \"READER_ROLE\" TO \"APP\";",
            "Oracle role grant must quote identifiers.");
        Check(service.BuildGrantRoleSql("Sqlite", "reader", "app", null) == string.Empty,
            "Unsupported database types must not generate membership SQL.");
    }

    private static void VerifyAlterPassword(DefaultDbUserService service)
    {
        Check(service.BuildAlterPasswordSql("Postgres", "app", null, "newPwd")
              == "ALTER USER \"app\" WITH PASSWORD 'newPwd';",
            "PostgreSQL password change must use ALTER USER.");
        Check(service.BuildAlterPasswordSql("KingbaseES", "app", null, "newPwd")
              == "ALTER USER \"app\" WITH PASSWORD 'newPwd';",
            "KingbaseES password change must follow PG syntax.");
        Check(service.BuildAlterPasswordSql("MySql", "app", "%", "newPwd")
              == "ALTER USER 'app'@'%' IDENTIFIED BY 'newPwd';",
            "MySQL password change must include the host part.");
        Check(service.BuildAlterPasswordSql("SqlServer", "app", null, "newPwd")
              == "ALTER LOGIN [app] WITH PASSWORD = 'newPwd';",
            "SQL Server password change must target the login.");
        Check(service.BuildAlterPasswordSql("Oracle", "APP", null, "newPwd")
              == "ALTER USER \"APP\" IDENTIFIED BY \"newPwd\";",
            "Oracle password change must quote identifiers.");
        Check(service.BuildAlterPasswordSql("Postgres", "app", null, "p'ass").Contains("'p''ass'", StringComparison.Ordinal),
            "Password literals must be escaped.");
        Check(service.BuildAlterPasswordSql("Sqlite", "app", null, "newPwd") == string.Empty,
            "Unsupported database types must not generate password SQL.");
    }

    private static void VerifyCatalogColumns()
    {
        string kingbaseUsers = KingbaseUserSql.BuildUsersSql(true);
        Check(kingbaseUsers.Contains("CASE WHEN rolcanlogin THEN 'user' ELSE 'role' END", StringComparison.Ordinal),
            "Principal catalog must expose a user/role discriminator column.");
        Check(KingbaseUserSql.BuildFallbackUsersSql().Contains("CASE WHEN rolcanlogin THEN 'user' ELSE 'role' END", StringComparison.Ordinal),
            "Fallback principal catalog must expose the same discriminator column.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
