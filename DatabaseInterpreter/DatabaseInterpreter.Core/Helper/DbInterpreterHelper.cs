using DatabaseInterpreter.Model;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DatabaseInterpreter.Core
{
    public class DbInterpreterHelper
    {
        public static DbInterpreter GetDbInterpreter(DatabaseType dbType, ConnectionInfo connectionInfo, DbInterpreterOption option = null)
        {
            if (option == null)
            {
                option = new DbInterpreterOption();
            }

            return dbType switch
            {
                DatabaseType.SqlServer => new SqlServerInterpreter(connectionInfo, option),
                DatabaseType.MySql => new MySqlInterpreter(connectionInfo, option),
                DatabaseType.Oracle => new OracleInterpreter(connectionInfo, option),
                DatabaseType.Postgres => new PostgresInterpreter(connectionInfo, option),
                DatabaseType.Sqlite => new SqliteInterpreter(connectionInfo, option),
                DatabaseType.KingbaseES => new KingbaseInterpreter(connectionInfo, option),
                DatabaseType.DuckDB => new DuckDbInterpreter(connectionInfo, option),
                DatabaseType.DM => new DmInterpreter(connectionInfo, option),
                _ => throw new NotSupportedException($"Database type '{dbType}' is not supported.")
            };
        }

        public static IEnumerable<DatabaseType> GetDisplayDatabaseTypes()
        {
            return Enum.GetValues(typeof(DatabaseType)).Cast<DatabaseType>().Where(item => item != DatabaseType.Unknown);
        }
    }
}
