using DatabaseInterpreter.Model;

namespace DatabaseInterpreter.Core
{
    public class SqliteConnectionStringBuilder : IConnectionBuilder
    {
        public string BuildConntionString(ConnectionInfo connectionInfo)
        {
            var builder = new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder
            {
                DataSource = connectionInfo.Database?.Trim() ?? string.Empty,
                Mode = Microsoft.Data.Sqlite.SqliteOpenMode.ReadWriteCreate,
            };

            if (!string.IsNullOrEmpty(connectionInfo.Password))
                builder.Password = connectionInfo.Password;

            return builder.ConnectionString;
        }
    }
}
