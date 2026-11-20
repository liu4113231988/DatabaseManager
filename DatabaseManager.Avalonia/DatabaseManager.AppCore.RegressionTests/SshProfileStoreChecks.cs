using System.Text.Json;
using DatabaseManager.AppCore.Models;
using DatabaseManager.AppCore.Services;

internal static class SshProfileStoreChecks
{
    public static void Run()
    {
        var options = new SshTunnelOptions
        {
            Enabled = true,
            Host = "ssh.example.com",
            UserName = "tester",
            Secret = "plain-secret",
            EncryptedSecret = "encrypted-secret",
        };

        byte[] json = JsonSerializer.SerializeToUtf8Bytes(options, SshProfileJsonContext.Default.SshTunnelOptions);
        string text = System.Text.Encoding.UTF8.GetString(json);
        if (text.Contains("plain-secret", StringComparison.Ordinal))
            throw new InvalidOperationException("SSH source-generated JSON must not persist the plain secret.");
        if (!text.Contains("encrypted-secret", StringComparison.Ordinal))
            throw new InvalidOperationException("SSH source-generated JSON must persist the encrypted secret.");

        var restored = JsonSerializer.Deserialize(json, SshProfileJsonContext.Default.SshTunnelOptions);
        if (restored is null || restored.Host != options.Host || restored.UserName != options.UserName
            || restored.Secret.Length != 0 || restored.EncryptedSecret != options.EncryptedSecret)
            throw new InvalidOperationException("SSH source-generated JSON round trip must preserve fields and omit the secret.");

        Console.WriteLine("SSH profile store checks passed: source-generated JSON, secret isolation and field round trip.");
    }
}
