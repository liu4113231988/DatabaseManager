using DatabaseInterpreter.Core;
using DatabaseInterpreter.Model;
using DatabaseManager.Core.Model;
using System;
using System.IO;
using System.Threading.Tasks;

namespace DatabaseManager.Core
{
    /// <summary>
    /// DuckDB 备份：通过 EXPORT DATABASE 导出到独立目录（逻辑一致备份），可选打包为 zip。
    /// 调用方在 Task.Run 内执行同步 Backup()，故此处用阻塞等待不会引发 UI 线程死锁。
    /// </summary>
    public class DuckDbBackup : DbBackup
    {
        public DuckDbBackup() : base() { }

        public DuckDbBackup(BackupSetting setting, ConnectionInfo connectionInfo) : base(setting, connectionInfo) { }

        public override string Backup()
        {
            return this.BackupAsync().GetAwaiter().GetResult();
        }

        private async Task<string> BackupAsync()
        {
            string dbName = string.IsNullOrEmpty(this.ConnectionInfo.Database) ? "duckdb" : Path.GetFileNameWithoutExtension(this.ConnectionInfo.Database);
            string folderName = dbName + "_" + DateTime.Now.ToString("yyyyMMddHHmmss");

            string saveFolder = this.CheckSaveFolder();
            string exportDir = Path.Combine(saveFolder, folderName);

            // DuckDB 会自行创建目标目录；目录名中的单引号需转义。
            string exportSql = $"EXPORT DATABASE '{exportDir.Replace("'", "''")}'";

            var interpreter = DbInterpreterHelper.GetDbInterpreter(DatabaseType.DuckDB, this.ConnectionInfo);

            using (var con = interpreter.CreateConnection())
            {
                await DbRowMapper.ExecuteNonQueryAsync(con, exportSql);
            }

            if (this.Setting != null && this.Setting.ZipFile)
            {
                string zipPath = Path.Combine(saveFolder, folderName + ".zip");
                System.IO.Compression.ZipFile.CreateFromDirectory(exportDir, zipPath, System.IO.Compression.CompressionLevel.Optimal, includeBaseDirectory: true);

                if (File.Exists(zipPath))
                {
                    Directory.Delete(exportDir, recursive: true);
                    return zipPath;
                }
            }

            return exportDir;
        }
    }
}
