using DatabaseInterpreter.Model;

namespace DatabaseInterpreter.Core
{
    public class PostgresConnectionBuilder : IConnectionBuilder
    {
        public string BuildConntionString(ConnectionInfo connectionInfo)
        {
            var builder = new Npgsql.NpgsqlConnectionStringBuilder
            {
                Host = connectionInfo.Server?.Trim() ?? string.Empty,
                CommandTimeout = DbInterpreter.Setting.CommandTimeout,
                Pooling = true,
            };

            string port = connectionInfo.Port?.Trim() ?? string.Empty;
            if (int.TryParse(port, out int p) && p > 0)
                builder.Port = p;
            else
                builder.Port = PostgresInterpreter.DEFAULT_PORT;

            if (!string.IsNullOrWhiteSpace(connectionInfo.Database))
                builder.Database = connectionInfo.Database.Trim();

            // Npgsql 8+ 自动协商 GSS/SSPI；集成认证不传旧配置中残留的密码，保留可选用户名映射。
            if (!string.IsNullOrEmpty(connectionInfo.UserId))
                builder.Username = connectionInfo.UserId;
            if (!connectionInfo.IntegratedSecurity && connectionInfo.Password != null)
                builder.Password = connectionInfo.Password;

            // SSL 选项：若 UseSsl 为 true 则要求 SSL，Npgsql 默认会协商
            if (connectionInfo.UseSsl)
                builder.SslMode = Npgsql.SslMode.Require;

            return builder.ConnectionString;
        }
    }
}
