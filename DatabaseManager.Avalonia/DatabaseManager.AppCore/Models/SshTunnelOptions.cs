namespace DatabaseManager.AppCore.Models;

public sealed class SshTunnelOptions
{
    public bool Enabled { get; set; }
    public string Host { get; set; } = "";
    public int Port { get; set; } = 22;
    public string UserName { get; set; } = "";
    public string PrivateKeyPath { get; set; } = "";
    public string HostFingerprint { get; set; } = "";
    public string EncryptedSecret { get; set; } = "";
    [Newtonsoft.Json.JsonIgnore]
    public string Secret { get; set; } = "";
}
