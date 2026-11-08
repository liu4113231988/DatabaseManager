using System;
using DatabaseInterpreter.Model;

namespace DatabaseInterpreter.Core
{
    public class DbScriptGeneratorHelper
    {
        public static DbScriptGenerator GetDbScriptGenerator(DbInterpreter dbInterpreter)
        {
            return dbInterpreter.DatabaseType switch
            {
                DatabaseType.SqlServer => new SqlServerScriptGenerator(dbInterpreter),
                DatabaseType.MySql => new MySqlScriptGenerator(dbInterpreter),
                DatabaseType.Oracle or DatabaseType.DM => new OracleScriptGenerator(dbInterpreter),
                DatabaseType.Postgres or DatabaseType.KingbaseES or DatabaseType.DuckDB => new PostgresScriptGenerator(dbInterpreter),
                DatabaseType.Sqlite => new SqliteScriptGenerator(dbInterpreter),
                _ => throw new NotSupportedException($"Script generation for database type '{dbInterpreter.DatabaseType}' is not supported.")
            };
        }
    }
}
