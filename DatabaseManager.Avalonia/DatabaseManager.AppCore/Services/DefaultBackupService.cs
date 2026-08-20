using DatabaseInterpreter.Core;
using DatabaseInterpreter.Model;
using DatabaseManager.AppCore.Models;
using DatabaseManager.Core;
using DatabaseManager.Core.Model;

namespace DatabaseManager.AppCore.Services;

/// <summary>
/// 数据库备份服务实现（阶段 5）。接入 <c>DatabaseManager.Core.DbBackup</c> 各备份适配器。
/// </summary>
public class DefaultBackupService : IBackupService
{
    public Task<BackupResultItem> BackupAsync(
        ConnectionItem connection,
        string saveFolder,
        string? clientToolFilePath,
        bool zipFile,
        Action<string>? onFeedback = null,
        CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            var dbType = ConnectionHelper.ParseDatabaseType(connection.DatabaseType);
            if (dbType == DatabaseType.Unknown || string.IsNullOrEmpty(connection.Database))
            {
                throw new InvalidOperationException("连接或数据库无效。");
            }

            onFeedback?.Invoke("正在初始化备份器...");

            var backup = DbBackup.GetInstance(dbType);

            backup.ConnectionInfo = ConnectionHelper.ToConnectionInfo(connection);
            backup.Setting = new BackupSetting
            {
                DatabaseType = connection.DatabaseType,
                ClientToolFilePath = clientToolFilePath ?? string.Empty,
                SaveFolder = saveFolder,
                ZipFile = zipFile,
            };

            onFeedback?.Invoke($"开始备份数据库 {connection.Database} ...");

            try
            {
                var filePath = backup.Backup();
                onFeedback?.Invoke($"备份完成：{filePath}");
                return new BackupResultItem(true, string.Empty, filePath);
            }
            catch (Exception ex)
            {
                var message = ex.Message;
                onFeedback?.Invoke($"备份失败：{message}");
                return new BackupResultItem(false, message, string.Empty);
            }
        }, cancellationToken);
    }
}
