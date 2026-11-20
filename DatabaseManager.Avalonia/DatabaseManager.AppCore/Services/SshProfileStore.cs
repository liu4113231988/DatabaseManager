using System.Text.Json;
using System.Text.Json.Serialization;
using DatabaseManager.AppCore.Models;
using DatabaseManager.Profile.Security;

namespace DatabaseManager.AppCore.Services;

[JsonSourceGenerationOptions(WriteIndented = true, PropertyNameCaseInsensitive = true,
    AllowTrailingCommas = true, ReadCommentHandling = JsonCommentHandling.Skip)]
[JsonSerializable(typeof(Dictionary<string, SshTunnelOptions>))]
[JsonSerializable(typeof(SshTunnelOptions))]
internal partial class SshProfileJsonContext : JsonSerializerContext;

public static class SshProfileStore
{
    private static readonly object Gate = new();
    private static readonly Dictionary<string, string> SessionSecrets = new();
    private static Dictionary<string, SshTunnelOptions>? _cachedItems;
    private static string FilePath => Path.Combine(AppContext.BaseDirectory, "Profiles", "ssh-tunnels.json");

    private static Dictionary<string, SshTunnelOptions> Load()
    {
        if (_cachedItems is not null)
        {
            return _cachedItems;
        }

        try
        {
            _cachedItems = File.Exists(FilePath)
                ? JsonSerializer.Deserialize(File.ReadAllText(FilePath), SshProfileJsonContext.Default.DictionaryStringSshTunnelOptions)
                  ?? new Dictionary<string, SshTunnelOptions>()
                : new Dictionary<string, SshTunnelOptions>();
        }
        catch (JsonException)
        {
            _cachedItems = new Dictionary<string, SshTunnelOptions>();
        }
        return _cachedItems;
    }

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
                var copy = JsonSerializer.Deserialize(
                    JsonSerializer.SerializeToUtf8Bytes(options, SshProfileJsonContext.Default.SshTunnelOptions),
                    SshProfileJsonContext.Default.SshTunnelOptions)!;
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
        File.WriteAllText(temp, JsonSerializer.Serialize(items, SshProfileJsonContext.Default.DictionaryStringSshTunnelOptions));
        File.Move(temp, FilePath, true);
        _cachedItems = items;
    }
}
