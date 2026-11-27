using DatabaseInterpreter.Core;
using DatabaseInterpreter.Model;
using DatabaseManager.Core.Model;
using System;
using System.IO;

namespace DatabaseManager.Core
{
    /// <summary>
    /// 达梦 DM 备份：调用官方 dexp 工具执行全库导出（Oracle 兼容生态，参数风格接近 exp）。
    /// 需在备份设置中指定 dexp.exe 路径；未配置时抛出明确提示，由调用方捕获展示。
    /// </summary>
    public class DmBackup : DbBackup
    {
        public DmBackup() : base() { }

        public DmBackup(BackupSetting setting, ConnectionInfo connectionInfo) : base(setting, connectionInfo) { }

        public override string Backup()
        {
            if (this.Setting == null)
            {
                throw new ArgumentException("There is no backup setting for DM.");
            }

            string exeFilePath = this.Setting.ClientToolFilePath;

            if (string.IsNullOrEmpty(exeFilePath))
            {
                throw new ArgumentNullException("client backup file path is empty.");
            }

            if (!File.Exists(exeFilePath))
            {
                throw new ArgumentException($"The backup file path is not existed:{this.Setting.ClientToolFilePath}.");
            }
            else if (Path.GetFileName(exeFilePath).ToLower() != "dexp.exe")
            {
                throw new ArgumentException("The backup file should be dexp.exe");
            }

            string server = this.ConnectionInfo.Server;
            string port = string.IsNullOrEmpty(this.ConnectionInfo.Port) ? DmInterpreter.DEFAULT_PORT.ToString() : this.ConnectionInfo.Port;

            string connectStr = this.ConnectionInfo.IntegratedSecurity
                ? "/"
                : $"{this.ConnectionInfo.UserId}/{this.ConnectionInfo.Password}@{server}:{port}";

            string fileNameWithoutExt = (this.ConnectionInfo.Database ?? "dm") + "_" + DateTime.Now.ToString("yyyyMMddHHmmss");
            string fileName = fileNameWithoutExt + ".dmp";

            string saveFolder = this.CheckSaveFolder();
            string saveFilePath = Path.Combine(saveFolder, fileName);

            string cmdArgs = $"USERID={connectStr} FILE={fileName} DIRECTORY={saveFolder} FULL=Y";

            string output = ProcessHelper.RunExe(this.Setting.ClientToolFilePath, cmdArgs);

            if (!string.IsNullOrEmpty(output) && output.IndexOf("失败", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                throw new Exception(output);
            }

            if (this.Setting.ZipFile)
            {
                saveFilePath = this.ZipFile(saveFilePath, Path.Combine(saveFolder, fileNameWithoutExt + ".zip"));
            }

            return saveFilePath;
        }
    }
}
