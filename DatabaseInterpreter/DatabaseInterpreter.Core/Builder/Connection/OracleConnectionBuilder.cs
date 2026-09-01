using DatabaseInterpreter.Model;

namespace DatabaseInterpreter.Core
{
    public class OracleConnectionBuilder : IConnectionBuilder
    {
        public string BuildConntionString(ConnectionInfo connectionInfo)
        {
            string server = connectionInfo.Server?.Trim() ?? string.Empty;
            string serviceName = OracleInterpreter.DEFAULT_SERVICE_NAME;
            string portStr = connectionInfo.Port?.Trim() ?? string.Empty;
            int port = OracleInterpreter.DEFAULT_PORT;
            if (int.TryParse(portStr, out int p) && p > 0) port = p;

            if (!string.IsNullOrEmpty(server) && server.Contains("/"))
            {
                string[] parts = server.Split('/', 2);
                server = parts[0].Trim();
                if (parts.Length > 1 && !string.IsNullOrWhiteSpace(parts[1]))
                    serviceName = parts[1].Trim();
            }

            // 使用 Oracle 托管驱动的连接字符串构造器，自动转义特殊字符
            var builder = new Oracle.ManagedDataAccess.Client.OracleConnectionStringBuilder
            {
                DataSource = $"(DESCRIPTION=(ADDRESS_LIST=(ADDRESS=(PROTOCOL=TCP)(HOST={server})(PORT={port})))(CONNECT_DATA=(SERVICE_NAME={serviceName})))"
            };

            if (connectionInfo.IntegratedSecurity)
            {
                builder.UserID = "/";
                // 口令由外部集成认证提供，无需设置
            }
            else
            {
                if (!string.IsNullOrEmpty(connectionInfo.UserId))
                    builder.UserID = connectionInfo.UserId;
                if (connectionInfo.Password != null)
                    builder.Password = connectionInfo.Password;
            }

            if (connectionInfo.IsDba)
            {
                builder["DBA Privilege"] = "SYSDBA";
            }

            return builder.ConnectionString;
        }
    }
}
