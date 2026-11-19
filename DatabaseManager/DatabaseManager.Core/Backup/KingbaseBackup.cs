using DatabaseInterpreter.Core;
using DatabaseInterpreter.Model;
using DatabaseManager.Core.Model;
using System;
using System.Diagnostics;
using System.IO;

namespace DatabaseManager.Core
{
    /// <summary>
    /// KingbaseES PG 兼容模式备份适配器（阶段 E 代码级接入）。
    /// 只调用用户显式选择的金仓客户端工具 sys_dump，不假设 pg_dump 可用；
    /// 密码通过 KBPASSWORD / PGPASSWORD 环境变量传递，不写入命令行参数。
    /// 客户端工具名、参数与权限仍须在目标 V8 实例上验收。
    /// </summary>
    public class KingbaseBackup : DbBackup
    {
        public const string BackupToolName = "sys_dump";
        public const string SqlRestoreToolName = "ksql";
        public const string ArchiveRestoreToolName = "sys_restore";

        public KingbaseBackup() : base() { }

        public KingbaseBackup(BackupSetting setting, ConnectionInfo connectionInfo) : base(setting, connectionInfo) { }

        public static bool IsTool(string toolPath, string expectedName)
            => !string.IsNullOrWhiteSpace(toolPath)
               && Path.GetFileNameWithoutExtension(toolPath).Equals(expectedName, StringComparison.OrdinalIgnoreCase);

        public override string Backup()
        {
            if (this.Setting == null)
            {
                throw new ArgumentException("KingbaseES 备份配置缺失。");
            }

            string toolPath = this.Setting.ClientToolFilePath;
            if (string.IsNullOrWhiteSpace(toolPath) || !File.Exists(toolPath))
            {
                throw new ArgumentException("KingbaseES 备份需要选择 sys_dump 客户端工具（绝对路径）。");
            }
            if (!IsTool(toolPath, BackupToolName))
            {
                throw new ArgumentException("KingbaseES 备份客户端工具必须是 sys_dump / sys_dump.exe。");
            }

            string port = string.IsNullOrEmpty(this.ConnectionInfo.Port)
                ? KingbaseInterpreter.DEFAULT_PORT.ToString()
                : this.ConnectionInfo.Port;
            string fileNameWithoutExt = this.ConnectionInfo.Database + "_" + DateTime.Now.ToString("yyyyMMddHHmmss");
            string saveFolder = this.CheckSaveFolder();
            string saveFilePath = Path.Combine(saveFolder, fileNameWithoutExt + ".tar");

            var startInfo = new ProcessStartInfo(toolPath)
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
            startInfo.ArgumentList.Add("--host");
            startInfo.ArgumentList.Add(this.ConnectionInfo.Server ?? string.Empty);
            startInfo.ArgumentList.Add("--port");
            startInfo.ArgumentList.Add(port);
            startInfo.ArgumentList.Add("--username");
            startInfo.ArgumentList.Add(this.ConnectionInfo.UserId ?? string.Empty);
            startInfo.ArgumentList.Add("--dbname");
            startInfo.ArgumentList.Add(this.ConnectionInfo.Database ?? string.Empty);
            startInfo.ArgumentList.Add("--no-password");
            startInfo.ArgumentList.Add("--format=t");
            startInfo.ArgumentList.Add("--file");
            startInfo.ArgumentList.Add(saveFilePath);
            SetPasswordEnvironment(startInfo, this.ConnectionInfo);

            using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("无法启动 sys_dump。");
            string output = process.StandardOutput.ReadToEnd();
            string error = process.StandardError.ReadToEnd();
            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException($"sys_dump 失败（退出码 {process.ExitCode}）：{error.Trim()} {output.Trim()}".Trim());
            }

            if (this.Setting.ZipFile)
            {
                saveFilePath = this.ZipFile(saveFilePath, Path.Combine(saveFolder, fileNameWithoutExt + ".zip"));
            }
            return saveFilePath;
        }

        /// <summary>恢复也走同一套密码环境变量，避免命令行泄露。</summary>
        public static void SetPasswordEnvironment(ProcessStartInfo startInfo, ConnectionInfo connectionInfo)
        {
            if (startInfo is null || connectionInfo is null || connectionInfo.IntegratedSecurity
                || string.IsNullOrEmpty(connectionInfo.Password))
            {
                return;
            }
            startInfo.Environment["KBPASSWORD"] = connectionInfo.Password;
            startInfo.Environment["PGPASSWORD"] = connectionInfo.Password;
        }
    }
}
