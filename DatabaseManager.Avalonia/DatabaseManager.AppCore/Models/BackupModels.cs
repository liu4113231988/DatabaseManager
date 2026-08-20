namespace DatabaseManager.AppCore.Models;

/// <summary>
/// 备份结果（UI 友好）。描述一次备份执行的结果与生成的备份文件路径。
/// </summary>
public class BackupResultItem
{
    /// <summary>是否成功。</summary>
    public bool IsOK { get; }

    /// <summary>消息（错误信息）。</summary>
    public string Message { get; }

    /// <summary>备份文件路径（成功时）。</summary>
    public string FilePath { get; }

    public BackupResultItem(bool isOK, string message, string filePath)
    {
        IsOK = isOK;
        Message = message ?? string.Empty;
        FilePath = filePath ?? string.Empty;
    }
}
