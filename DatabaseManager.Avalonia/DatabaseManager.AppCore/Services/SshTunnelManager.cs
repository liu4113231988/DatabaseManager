using DatabaseManager.AppCore.Models;
using Renci.SshNet;
using System.Security.Cryptography;
using System.Text;

namespace DatabaseManager.AppCore.Services;

/// <summary>Reuses verified tunnels; reconnects on the next operation without replaying SQL.</summary>
public static class SshTunnelManager
{
    private sealed record Tunnel(SshClient Client, ForwardedPortLocal Port, PrivateKeyFile? Key) : IDisposable
    {
        public void Dispose() { Port.Dispose(); Client.Dispose(); Key?.Dispose(); }
    }
    private static readonly object Gate = new();
    private static readonly Dictionary<string, Tunnel> Tunnels = new();
    private static readonly Dictionary<string, string> Owners = new(StringComparer.OrdinalIgnoreCase);
    static SshTunnelManager() => AppDomain.CurrentDomain.ProcessExit += (_, _) => CloseAll();

    public static (string Host, string? Port) Resolve(ConnectionItem connection)
    {
        var options = connection.Ssh;
        if (options?.Enabled != true) return (connection.Server, connection.Port);
        if (string.IsNullOrWhiteSpace(options.Host) || string.IsNullOrWhiteSpace(options.UserName) ||
            string.IsNullOrWhiteSpace(options.HostFingerprint) || !options.HostFingerprint.StartsWith("SHA256:", StringComparison.Ordinal) || options.Port is < 1 or > 65535)
            throw new InvalidOperationException("SSH 需要主机、用户名、有效端口和经可信渠道核对的 SHA256: 主机指纹。");
        if (!uint.TryParse(connection.Port, out var remotePort) || remotePort is < 1 or > 65535)
            throw new InvalidOperationException("SSH 隧道需要显式填写数据库端口。");
        var key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("\0", options.Host, options.Port,
            options.UserName, options.PrivateKeyPath, options.HostFingerprint, options.Secret, connection.Server, remotePort))));
        lock (Gate)
        {
            if (Owners.TryGetValue(connection.Name, out var previous) && previous != key)
            {
                Owners.Remove(connection.Name);
                if (!Owners.Values.Contains(previous) && Tunnels.Remove(previous, out var obsolete)) obsolete.Dispose();
            }
            Owners[connection.Name] = key;
            if (Tunnels.TryGetValue(key, out var current))
            {
                if (current.Client.IsConnected && current.Port.IsStarted) return ("127.0.0.1", current.Port.BoundPort.ToString());
                current.Dispose();
                Tunnels.Remove(key);
            }
            PrivateKeyFile? privateKey = null;
            SshClient? client = null;
            ForwardedPortLocal? port = null;
            try
            {
                AuthenticationMethod auth;
                if (string.IsNullOrWhiteSpace(options.PrivateKeyPath)) auth = new PasswordAuthenticationMethod(options.UserName, options.Secret);
                else
                {
                    privateKey = new PrivateKeyFile(options.PrivateKeyPath, options.Secret);
                    auth = new PrivateKeyAuthenticationMethod(options.UserName, privateKey);
                }
                client = new SshClient(new Renci.SshNet.ConnectionInfo(options.Host, options.Port, options.UserName, auth) { Timeout = TimeSpan.FromSeconds(15) });
                client.KeepAliveInterval = TimeSpan.FromSeconds(30);
                client.HostKeyReceived += (_, e) => e.CanTrust = string.Equals("SHA256:" + e.FingerPrintSHA256, options.HostFingerprint.Trim(), StringComparison.Ordinal);
                client.Connect();
                port = new ForwardedPortLocal("127.0.0.1", 0, connection.Server, remotePort);
                client.AddForwardedPort(port);
                port.Start();
                Tunnels.Add(key, new Tunnel(client, port, privateKey));
                return ("127.0.0.1", port.BoundPort.ToString());
            }
            catch { port?.Dispose(); client?.Dispose(); privateKey?.Dispose(); throw; }
        }
    }
    public static void Close(string name)
    {
        lock (Gate)
            if (Owners.Remove(name, out var key) && !Owners.Values.Contains(key) && Tunnels.Remove(key, out var tunnel)) tunnel.Dispose();
    }
    public static void CloseAll()
    {
        lock (Gate) { foreach (var tunnel in Tunnels.Values) tunnel.Dispose(); Tunnels.Clear(); Owners.Clear(); }
    }
}
