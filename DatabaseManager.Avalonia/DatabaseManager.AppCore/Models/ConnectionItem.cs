namespace DatabaseManager.AppCore.Models;

/// <summary>
/// 连接项（AppCore 领域模型），对应底层 <c>ConnectionProfileInfo</c> / <c>AccountProfileInfo</c> 的 UI 无关抽象。
/// 用于连接管理窗口（列表展示）、连接测试等场景。
/// </summary>
public class ConnectionItem
{
    /// <summary>连接（Connection）唯一标识。新增时为空。</summary>
    public string? Id { get; set; }

    /// <summary>账号（Account）标识，用于关联账号级配置。</summary>
    public string? AccountId { get; set; }

    /// <summary>数据库类型（SqlServer / MySql / Oracle / Postgres / Sqlite 等）。</summary>
    public string DatabaseType { get; set; } = string.Empty;

    /// <summary>连接名称（Profile 名称）。</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>服务器地址。</summary>
    public string Server { get; set; } = string.Empty;

    /// <summary>端口。</summary>
    public string? Port { get; set; }

    /// <summary>服务器版本。</summary>
    public string? ServerVersion { get; set; }

    /// <summary>目标数据库。</summary>
    public string Database { get; set; } = string.Empty;

    /// <summary>是否集成认证。</summary>
    public bool IntegratedSecurity { get; set; }

    /// <summary>用户名。</summary>
    public string? UserId { get; set; }

    /// <summary>密码。</summary>
    public string? Password { get; set; }

    /// <summary>是否 DBA。</summary>
    public bool IsDba { get; set; }

    /// <summary>是否使用 SSL。</summary>
    public bool UseSsl { get; set; }

    /// <summary>排序优先级。</summary>
    public int Priority { get; set; }

    /// <summary>是否记住密码。</summary>
    public bool RememberPassword { get; set; }

    /// <summary>分组名（由 IConnectionVisualService 侧车存储合并而来；空表示未分组）。</summary>
    public string? Group { get; set; }

    /// <summary>颜色标签（hex，如 #1E88E5；空表示无色）。</summary>
    public string? ColorTag { get; set; }

    /// <summary>展示用描述信息。</summary>
    public string Description =>
        string.IsNullOrEmpty(Name)
            ? $"{Server}{(string.IsNullOrEmpty(Port) ? "" : ":" + Port)}/{Database}"
            : $"{Name} ({Server}{(string.IsNullOrEmpty(Port) ? "" : ":" + Port)}/{Database})";

    /// <summary>生成一个新的空连接项（用于新增连接）。</summary>
    public static ConnectionItem New(string databaseType) => new()
    {
        DatabaseType = databaseType,
        RememberPassword = true,
    };
}
