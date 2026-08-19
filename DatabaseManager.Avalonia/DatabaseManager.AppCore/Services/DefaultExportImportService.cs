namespace DatabaseManager.AppCore.Services;

/// <summary>
/// 导入 / 导出服务实现。阶段 0 建立骨架，阶段 6 接入 <c>DatabaseManager.FileUtility</c> 全格式支持。
/// </summary>
public class DefaultExportImportService : IExportImportService
{
    public IReadOnlyList<string> GetExportFormats()
        => new[] { "Excel", "CSV", "XML", "JSON" };
}
