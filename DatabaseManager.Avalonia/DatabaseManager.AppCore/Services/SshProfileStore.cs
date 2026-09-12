using DatabaseManager.AppCore.Models;
using DatabaseInterpreter.Utility;
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
            if (id is null || !Load().TryGetValue(id, out var options)) return null;
            options.Secret = SessionSecrets.TryGetValue(id, out var secret) ? secret : string.IsNullOrEmpty(options.EncryptedSecret) ? "" : AesHelper.Decrypt(options.EncryptedSecret);
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
                copy.EncryptedSecret = remember && options.Secret.Length > 0 ? AesHelper.Encrypt(options.Secret) : "";
                items[id] = copy;
            }
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            var temp = FilePath + ".tmp";
            File.WriteAllText(temp, JsonConvert.SerializeObject(items, Formatting.Indented));
            File.Move(temp, FilePath, true);
        }
    }
}
