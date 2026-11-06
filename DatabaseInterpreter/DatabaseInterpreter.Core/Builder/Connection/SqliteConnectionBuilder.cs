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
                Mode = connectionInfo.RequireExistingDatabase
                    ? Microsoft.Data.Sqlite.SqliteOpenMode.ReadWrite
                    : Microsoft.Data.Sqlite.SqliteOpenMode.ReadWriteCreate,
            };

            if (connectionInfo.RequireExistingDatabase && string.IsNullOrWhiteSpace(builder.DataSource))
                throw new System.ArgumentException("SQLite 数据库文件路径不能为空。", nameof(connectionInfo));

            if (!string.IsNullOrEmpty(connectionInfo.Password))
            {
                // SQLite3MC 默认使用 ChaCha20；密码连接显式保持 SQLCipher 4 格式。
                if (!string.IsNullOrEmpty(builder.DataSource) && builder.DataSource != ":memory:")
                    builder.DataSource = new System.Uri(System.IO.Path.GetFullPath(builder.DataSource)).AbsoluteUri
                        + "?cipher=sqlcipher&legacy=4";
                builder.Password = connectionInfo.Password;
            }

            return builder.ConnectionString;
        }
    }
}
