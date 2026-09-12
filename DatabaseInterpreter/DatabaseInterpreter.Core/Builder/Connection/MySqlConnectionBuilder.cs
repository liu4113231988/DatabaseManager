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
                throw new System.NotSupportedException("当前 MySqlConnector 驱动不支持 Windows 集成认证，请使用用户名和密码认证。");
            }
            else
            {
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
