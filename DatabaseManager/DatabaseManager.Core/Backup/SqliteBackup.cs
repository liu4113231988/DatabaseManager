using DatabaseInterpreter.Core;
using DatabaseInterpreter.Model;
using DatabaseManager.Core.Model;
using System;
using System.IO;

namespace DatabaseManager.Core
{
    /// <summary>
    /// SQLite 备份：复制数据库文件。加密库复制后仍为加密文件；内存库（:memory:）不支持文件备份。
    /// </summary>
    public class SqliteBackup : DbBackup
    {
        public SqliteBackup() : base() { }

        public SqliteBackup(BackupSetting setting, ConnectionInfo connectionInfo) : base(setting, connectionInfo) { }

        public override string Backup()
        {
            string sourceFile = this.ConnectionInfo.Database?.Trim();

            if (string.IsNullOrEmpty(sourceFile) || sourceFile == ":memory:")
            {
                throw new ArgumentException("SQLite 内存数据库（:memory:）无法备份到文件，请使用文件型数据库。");
            }

            if (!File.Exists(sourceFile))
            {
                throw new ArgumentException($"SQLite 数据库文件不存在：{sourceFile}");
            }

            // WAL 模式下最新已提交事务可能还在 -wal 中，直接复制主文件会丢失数据，先做检查点。
            if (File.Exists(sourceFile + "-wal"))
            {
                CheckpointWal();
            }

            string fileNameWithoutExt = Path.GetFileNameWithoutExtension(sourceFile) + "_" + DateTime.Now.ToString("yyyyMMddHHmmss");
            string ext = Path.GetExtension(sourceFile);
            if (string.IsNullOrEmpty(ext))
            {
                ext = ".db";
            }

            string saveFolder = this.CheckSaveFolder();
            string saveFilePath = Path.Combine(saveFolder, fileNameWithoutExt + ext);

            File.Copy(sourceFile, saveFilePath, overwrite: true);

            if (this.Setting != null && this.Setting.ZipFile)
            {
                saveFilePath = this.ZipFile(saveFilePath, Path.Combine(saveFolder, fileNameWithoutExt + ".zip"));
            }

            return saveFilePath;
        }

        /// <summary>把 WAL 中的已提交事务合并进主文件；被其他写入阻塞时拒绝生成不一致的备份。</summary>
        private void CheckpointWal()
        {
            var interpreter = DbInterpreterHelper.GetDbInterpreter(DatabaseType.Sqlite, this.ConnectionInfo);
            using var connection = interpreter.CreateConnection();
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA wal_checkpoint(TRUNCATE);";
            using var reader = command.ExecuteReader();
            if (reader.Read() && Convert.ToInt32(reader.GetValue(0)) != 0)
            {
                throw new InvalidOperationException("SQLite 正在被其他连接写入，WAL 检查点未完成；请停止写入后重试备份。");
            }
        }
    }
}
