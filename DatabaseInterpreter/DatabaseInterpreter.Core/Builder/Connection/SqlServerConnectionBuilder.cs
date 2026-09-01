using DatabaseInterpreter.Model;
using System;

namespace DatabaseInterpreter.Core
{
    public class SqlServerConnectionBuilder : IConnectionBuilder
    {
        public string BuildConntionString(ConnectionInfo connectionInfo)
        {
            var builder = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder
            {
                TrustServerCertificate = true,
                PersistSecurityInfo = false,
            };

            // DataSource 支持 Server,Port 与 Server\Instance 形式
            string server = connectionInfo.Server?.Trim() ?? string.Empty;
            string port = connectionInfo.Port?.Trim() ?? string.Empty;
            if (!string.IsNullOrEmpty(port) && !server.Contains(","))
            {
                // 若 Server 已含逗号端口则不再追加
                builder.DataSource = $"{server},{port}";
            }
            else
            {
                builder.DataSource = server;
            }

            if (!string.IsNullOrWhiteSpace(connectionInfo.Database))
            {
                builder.InitialCatalog = connectionInfo.Database.Trim();
            }

            if (connectionInfo.IntegratedSecurity)
            {
                builder.IntegratedSecurity = true;
            }
            else
            {
                builder.IntegratedSecurity = false;
                if (!string.IsNullOrEmpty(connectionInfo.UserId))
                    builder.UserID = connectionInfo.UserId;
                if (connectionInfo.Password != null)
                    builder.Password = connectionInfo.Password;
            }

            return builder.ConnectionString;
        }
    }
}
