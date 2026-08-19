namespace DatabaseManager.AppCore.Services;

/// <summary>
/// 导入 / 导出服务。封装数据与结构的文件导入导出。
/// 实现复用 <c>DatabaseManager.FileUtility</c>。
/// </summary>
public interface IExportImportService
{
    /// <summary>返回支持的导出格式列表。</summary>
    IReadOnlyList<string> GetExportFormats();
}
