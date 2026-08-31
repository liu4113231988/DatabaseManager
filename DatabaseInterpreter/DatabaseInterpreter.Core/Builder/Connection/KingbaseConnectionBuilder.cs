using DatabaseInterpreter.Model;
using Kdbndp;

namespace DatabaseInterpreter.Core
{
    /// <summary>KingbaseES 连接串构建器（首期 PG 兼容模式）。</summary>
    public class KingbaseConnectionBuilder : IConnectionBuilder
    {
        public const int DefaultPort = 54321;

        public string BuildConntionString(ConnectionInfo connectionInfo)
        {
            var builder = new KdbndpConnectionStringBuilder
            {
                Host = connectionInfo.Server?.Trim() ?? string.Empty,
                CommandTimeout = DbInterpreter.Setting.CommandTimeout,
                Pooling = true,
            };

            builder.Port = int.TryParse(connectionInfo.Port?.Trim(), out int port) && port > 0
                ? port : DefaultPort;
            if (!string.IsNullOrWhiteSpace(connectionInfo.Database)) builder.Database = connectionInfo.Database.Trim();
            if (!string.IsNullOrWhiteSpace(connectionInfo.UserId)) builder.Username = connectionInfo.UserId;
            if (connectionInfo.Password is not null) builder.Password = connectionInfo.Password;
            if (connectionInfo.UseSsl) builder.SslMode = SslMode.Require;
            return builder.ConnectionString;
        }
    }
}
