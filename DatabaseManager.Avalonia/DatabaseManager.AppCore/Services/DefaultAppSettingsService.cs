using System.IO;
using Newtonsoft.Json;

namespace DatabaseManager.AppCore.Services;

/// <summary>
/// 应用设置服务实现：JSON 文件存储（随程序目录 Profiles\），锁 + 静默容错（与 query-history 同范式）。
/// </summary>
public class DefaultAppSettingsService : IAppSettingsService
{
    private static readonly object FileLock = new();

    private readonly string _filePath;

    public AppSettings Settings { get; private set; }

    public DefaultAppSettingsService()
    {
        var dir = Path.Combine(AppContext.BaseDirectory, "Profiles");
        Directory.CreateDirectory(dir);
        _filePath = Path.Combine(dir, "app-settings.json");

        Settings = Load();
    }

    public void Save()
    {
        lock (FileLock)
        {
            try
            {
                File.WriteAllText(_filePath, JsonConvert.SerializeObject(Settings, Formatting.Indented));
            }
            catch
            {
                // 写入失败（磁盘只读等）静默忽略，设置仍在内存中生效。
            }
        }
    }

    private AppSettings Load()
    {
        lock (FileLock)
        {
            try
            {
                if (!File.Exists(_filePath))
                {
                    return new AppSettings();
                }

                return JsonConvert.DeserializeObject<AppSettings>(File.ReadAllText(_filePath)) ?? new AppSettings();
            }
            catch
            {
                return new AppSettings();
            }
        }
    }
}
