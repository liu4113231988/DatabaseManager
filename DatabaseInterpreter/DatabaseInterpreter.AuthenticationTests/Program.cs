using DatabaseInterpreter.Core;
using DatabaseInterpreter.Model;
using System.Data.Common;

static void Check(bool condition, string message)
{
    if (!condition) throw new Exception(message);
}

var info = new ConnectionInfo
{
    Server = "localhost", Database = "sample", UserId = "mapped_user",
    Password = " leftover;password ", IntegratedSecurity = true,
};

var sql = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(new SqlServerConnectionBuilder().BuildConntionString(info));
Check(sql.IntegratedSecurity && sql.UserID == "" && sql.Password == "", "SQL Server must use system credentials.");
var oracle = new Oracle.ManagedDataAccess.Client.OracleConnectionStringBuilder(new OracleConnectionBuilder().BuildConntionString(info));
Check(oracle.UserID == "/" && string.IsNullOrEmpty(oracle.Password), "Oracle must use external authentication.");
var pg = new Npgsql.NpgsqlConnectionStringBuilder(new PostgresConnectionBuilder().BuildConntionString(info));
Check(pg.Username == info.UserId && string.IsNullOrEmpty(pg.Password), "Postgres must keep mapped username but omit stale password.");
info.UserId = null;
pg = new Npgsql.NpgsqlConnectionStringBuilder(new PostgresConnectionBuilder().BuildConntionString(info));
Check(string.IsNullOrEmpty(pg.Username), "Postgres must allow system username detection.");
try
{
    new MySqlConnectionBuilder().BuildConntionString(info);
    throw new Exception("MySQL must reject unsupported integrated authentication.");
}
catch (NotSupportedException) { }

info.IntegratedSecurity = false;
info.UserId = "database_user";
foreach (var builder in new IConnectionBuilder[] { new SqlServerConnectionBuilder(), new OracleConnectionBuilder(), new PostgresConnectionBuilder(), new MySqlConnectionBuilder() })
{
    var parsed = new DbConnectionStringBuilder { ConnectionString = builder.BuildConntionString(info) };
    Check(parsed.Values.Cast<object>().Any(value => Equals(value, info.Password)), $"{builder.GetType().Name} must preserve password characters.");
}
foreach (var type in new[] { DatabaseType.SqlServer, DatabaseType.Oracle, DatabaseType.Postgres })
    Check(DatabaseAuthentication.SupportsIntegratedSecurity(type), $"{type} must expose integrated authentication.");
foreach (var type in new[] { DatabaseType.MySql, DatabaseType.Sqlite, DatabaseType.KingbaseES, DatabaseType.Unknown })
    Check(!DatabaseAuthentication.SupportsIntegratedSecurity(type), $"{type} must not expose unsupported integrated authentication.");
Console.WriteLine("Authentication regression checks passed.");
