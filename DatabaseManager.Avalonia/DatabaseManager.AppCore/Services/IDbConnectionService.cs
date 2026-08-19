namespace DatabaseManager.AppCore.Services;

/// <summary>
/// 数据库连接服务。封装连接配置（Profile）的增删改查。
/// 实现复用 <c>DatabaseManager.Profile</c>。
/// </summary>
public interface IDbConnectionService
{
    /// <summary>获取当前已保存的全部连接配置名称。</summary>
    IReadOnlyList<string> GetConnectionNames();
}
