using DatabaseInterpreter.Model;
using DatabaseManager.Core.Model;
using System;
using System.IO;

namespace DatabaseManager.Core
{
    public abstract class DbBackup
    {
        public string DefaultBackupFolderName = "Backup";
        public BackupSetting Setting { get; set; }
        public ConnectionInfo ConnectionInfo { get; set; }

        public DbBackup() { }

        public DbBackup(BackupSetting setting, ConnectionInfo connectionInfo)
        {
            this.Setting = setting;
            this.ConnectionInfo = connectionInfo;
        }

        public abstract string Backup();

        protected string CheckSaveFolder()
        {
            string saveFolder = this.Setting.SaveFolder;

            if (string.IsNullOrEmpty(saveFolder))
            {
                saveFolder = this.DefaultBackupFolderName;
            }

            if (!Directory.Exists(saveFolder))
            {
                Directory.CreateDirectory(saveFolder);
            }

            return saveFolder;
        }
       
        protected virtual string ZipFile(string backupFilePath, string zipFilePath)
        {
            if (File.Exists(backupFilePath))
            {
                FileHelper.Zip(backupFilePath, zipFilePath);

                if (File.Exists(zipFilePath))
                {
                    File.Delete(backupFilePath);

                    backupFilePath = zipFilePath;
                }              
            }

            return backupFilePath;
        }

        public static DbBackup GetInstance(DatabaseType databaseType)
        {
            return databaseType switch
            {
                DatabaseType.SqlServer => new SqlServerBackup(),
                DatabaseType.Oracle => new OracleBackup(),
                DatabaseType.MySql => new MySqlBackup(),
                DatabaseType.Postgres => new PostgresBackup(),
                DatabaseType.KingbaseES => new KingbaseBackup(),
                DatabaseType.Sqlite => new SqliteBackup(),
                DatabaseType.DuckDB => new DuckDbBackup(),
                DatabaseType.DM => new DmBackup(),
                _ => throw new NotImplementedException($"Not implemente for backup {databaseType}.")
            };
        }

    }
}
