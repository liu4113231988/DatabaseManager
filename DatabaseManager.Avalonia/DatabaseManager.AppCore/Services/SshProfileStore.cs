using DatabaseManager.AppCore.Models;
using DatabaseManager.Profile.Security;
using Newtonsoft.Json;

namespace DatabaseManager.AppCore.Services;

public static class SshProfileStore
{
    private static readonly object Gate = new();
    private static readonly Dictionary<string, string> SessionSecrets = new();
    private static string FilePath => Path.Combine(AppContext.BaseDirectory, "Profiles", "ssh-tunnels.json");
    private static Dictionary<string, SshTunnelOptions> Load() => File.Exists(FilePath)
        ? JsonConvert.DeserializeObject<Dictionary<string, SshTunnelOptions>>(File.ReadAllText(FilePath)) ?? new()
        : new();
    public static SshTunnelOptions? Find(string? id)
    {
        lock (Gate)
        {
            var items = Load();
            if (id is null || !items.TryGetValue(id, out var options)) return null;
            if (SessionSecrets.TryGetValue(id, out var secret))
            {
                options.Secret = secret;
            }
            else if (string.IsNullOrEmpty(options.EncryptedSecret))
            {
                options.Secret = "";
            }
            else
            {
                options.Secret = CredentialProtector.Default.Unprotect(options.EncryptedSecret, out bool needsMigration);
                if (needsMigration)
                {
                    options.EncryptedSecret = CredentialProtector.Default.Protect(options.Secret);
                    Write(items);
                }
            }
            return options;
        }
    }
    public static void Save(string id, SshTunnelOptions? options, bool remember)
    {
        lock (Gate)
        {
            var items = Load();
            if (options is null) { items.Remove(id); SessionSecrets.Remove(id); }
            else
            {
                SessionSecrets[id] = options.Secret;
                var copy = JsonConvert.DeserializeObject<SshTunnelOptions>(JsonConvert.SerializeObject(options))!;
                copy.EncryptedSecret = remember && options.Secret.Length > 0 ? CredentialProtector.Default.Protect(options.Secret) : "";
                items[id] = copy;
            }
            Write(items);
        }
    }

    private static void Write(Dictionary<string, SshTunnelOptions> items)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        var temp = FilePath + ".tmp";
        File.WriteAllText(temp, JsonConvert.SerializeObject(items, Formatting.Indented));
        File.Move(temp, FilePath, true);
    }
}
