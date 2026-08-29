using DatabaseInterpreter.Model;

namespace DatabaseInterpreter.Core
{
    public class MySqlConnectionBuilder : IConnectionBuilder
    {
        public string BuildConntionString(ConnectionInfo connectionInfo)
        {
            var builder = new MySqlConnector.MySqlConnectionStringBuilder
            {
                Server = connectionInfo.Server?.Trim() ?? string.Empty,
                CharacterSet = "utf8",
                AllowLoadLocalInfile = true,
                AllowZeroDateTime = true,
                AllowPublicKeyRetrieval = true,
                AllowUserVariables = true,
            };

            if (uint.TryParse(connectionInfo.Port?.Trim(), out uint p) && p > 0)
                builder.Port = p;

            if (!string.IsNullOrWhiteSpace(connectionInfo.Database))
                builder.Database = connectionInfo.Database.Trim();

            if (connectionInfo.IntegratedSecurity)
            {
                // 保持原有行为：Windows 认证时指定 auth_windows；MySqlConnector.Builder 不直接暴露 IntegratedSecurity 属性
                builder.UserID = "auth_windows";
                // 通过索引器追加 IntegratedSecurity（若驱动支持则生效）
                try { builder["Integrated Security"] = true; } catch { }
            }
            else
            {
                try { builder["Integrated Security"] = false; } catch { }
                if (!string.IsNullOrEmpty(connectionInfo.UserId))
                    builder.UserID = connectionInfo.UserId;
                if (connectionInfo.Password != null)
                    builder.Password = connectionInfo.Password;
                builder.SslMode = connectionInfo.UseSsl
                    ? MySqlConnector.MySqlSslMode.Preferred
                    : MySqlConnector.MySqlSslMode.None;
            }

            return builder.ConnectionString;
        }
    }
}
