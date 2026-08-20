using DatabaseManager.AppCore.Models;

namespace DatabaseManager.AppCore.Services;

/// <summary>
/// 数据库备份服务（阶段 5）。
/// 复用 <c>DatabaseManager.Core.DbBackup</c> 各数据库备份适配器执行备份。
/// </summary>
public interface IBackupService
{
    /// <summary>
    /// 对指定连接执行数据库备份。
    /// </summary>
    /// <param name="connection">目标连接。</param>
    /// <param name="saveFolder">备份保存文件夹。</param>
    /// <param name="clientToolFilePath">客户端工具路径（MySQL 需 mysqldump.exe）。</param>
    /// <param name="zipFile">是否压缩备份文件。</param>
    /// <param name="onFeedback">实时反馈回调。</param>
    Task<BackupResultItem> BackupAsync(
        ConnectionItem connection,
        string saveFolder,
        string? clientToolFilePath,
        bool zipFile,
        Action<string>? onFeedback = null,
        CancellationToken cancellationToken = default);
}
