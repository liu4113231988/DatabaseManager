using DatabaseInterpreter.Model;
using System.Text;

namespace DatabaseInterpreter.Core
{
    /// <summary>
    /// 达梦 DM8 连接串构建（按官方文档格式：Server=ip:port; UserId=xxx; PWD=xxx）。
    /// 达梦的 Schema 即登录用户（与 Oracle 一致），无需单独指定库。
    /// </summary>
    public class DmConnectionBuilder : IConnectionBuilder
    {
        public const int DefaultPort = 5236;

        public string BuildConntionString(ConnectionInfo connectionInfo)
        {
            string server = connectionInfo.Server?.Trim() ?? string.Empty;
            int port = DefaultPort;
            if (int.TryParse(connectionInfo.Port?.Trim(), out int p) && p > 0)
            {
                port = p;
            }

            var sb = new StringBuilder();
            sb.Append("Server=").Append(server.Contains(':') ? server : $"{server}:{port}");

            if (!string.IsNullOrEmpty(connectionInfo.UserId))
            {
                sb.Append("; UserId=").Append(connectionInfo.UserId);
            }

            if (connectionInfo.Password is not null)
            {
                sb.Append("; PWD=").Append(connectionInfo.Password);
            }

            return sb.ToString();
        }
    }
}
