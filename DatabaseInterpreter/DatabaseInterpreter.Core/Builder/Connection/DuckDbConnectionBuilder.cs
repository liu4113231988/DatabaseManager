using DatabaseInterpreter.Model;

namespace DatabaseInterpreter.Core
{
    /// <summary>
    /// DuckDB 连接串构建。DuckDB 为嵌入式数据库，没有服务器/端口/账号概念：
    /// Database 即数据源（文件路径或 :memory:），未指定时使用内存库。
    /// </summary>
    public class DuckDbConnectionBuilder : IConnectionBuilder
    {
        public const string MemoryDataSource = ":memory:";

        public string BuildConntionString(ConnectionInfo connectionInfo)
        {
            string dataSource = string.IsNullOrWhiteSpace(connectionInfo.Database) ? MemoryDataSource : connectionInfo.Database.Trim();

            // DuckDB.NET.Data 允许在连接串中附加任意 DuckDB 配置项。
            string connectionString = $"DataSource={dataSource}";

            if (connectionInfo.UseReadOnly)
            {
                connectionString += ";ACCESS_MODE=READ_ONLY";
            }

            return connectionString;
        }
    }
}
