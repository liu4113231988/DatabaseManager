using DatabaseInterpreter.Core;
using DatabaseInterpreter.Model;
using DatabaseManager.Core.Model;
using System.IO;
using System.Text.Json;

namespace DatabaseManager.Core
{
    public class SettingManager : ConfigManager
    {
        public static Setting Setting { get; private set; } = new Setting();

        static SettingManager()
        {
            LoadConfig();
        }       

        public static string ConfigFilePath
        {
            get
            {
                return Path.Combine(ConfigRootFolder, "Setting.json");
            }
        }

        public static void LoadConfig()
        {
            if (File.Exists(ConfigFilePath))
            {
                Setting = JsonSerializer.Deserialize(File.ReadAllText(ConfigFilePath), CoreJsonContext.Default.Setting) ?? Setting;
            }
        }

        public static void SaveConfig(Setting setting)
        {
            Setting = setting;
            string content = JsonSerializer.Serialize(setting, CoreJsonContext.Default.Setting);

            File.WriteAllText(ConfigFilePath, content);
        }

        public static DbInterpreterSetting GetInterpreterSetting()
        {
            return CoreModelCopy.Copy<DbInterpreterSetting>(Setting);
        }
    }
}
