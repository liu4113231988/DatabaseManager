namespace DatabaseManager.AppCore.Services;

/// <summary>主题模式。</summary>
public static class ThemeModes
{
    public const string System = "System";
    public const string Light = "Light";
    public const string Dark = "Dark";
    public const string HighContrast = "HighContrast";

    /// <summary>全部可选模式（显示名 → 值由 UI 层映射）。</summary>
    public static readonly string[] All = { System, Light, Dark, HighContrast };
}

/// <summary>查询标签页会话快照（用于退出恢复未保存的 SQL 草稿与连接上下文）。</summary>
public class QueryTabState
{
    public string Title { get; set; } = string.Empty;

    public string SqlText { get; set; } = string.Empty;

    public string? ConnectionName { get; set; }

    public string? DatabaseName { get; set; }
}

/// <summary>工作区布局状态（主窗口与查询标签会话）。</summary>
public class WorkspaceState
{
    public double WindowX { get; set; } = -1;

    public double WindowY { get; set; } = -1;

    public double WindowWidth { get; set; }

    public double WindowHeight { get; set; }

    /// <summary>Normal / Maximized。</summary>
    public string WindowState { get; set; } = "Normal";

    /// <summary>左侧对象浏览器面板宽度（DIP）。</summary>
    public double LeftPanelWidth { get; set; } = 400;

    /// <summary>退出时打开的查询标签页（含未保存草稿）。</summary>
    public List<QueryTabState> Tabs { get; set; } = new();
}

/// <summary>应用设置。</summary>
public class AppSettings
{
    /// <summary>主题模式（ThemeModes 常量）。</summary>
    public string ThemeMode { get; set; } = ThemeModes.System;

    /// <summary>主窗口字体缩放（0.9 / 1.0 / 1.1 / 1.25）。</summary>
    public double FontScale { get; set; } = 1.0;

    public WorkspaceState Workspace { get; set; } = new();
}

/// <summary>
/// 应用设置服务：主题/缩放/工作区状态，JSON 持久化（Profiles\app-settings.json）。
/// </summary>
public interface IAppSettingsService
{
    /// <summary>当前设置（启动时加载，修改后调用 Save 持久化）。</summary>
    AppSettings Settings { get; }

    /// <summary>持久化当前设置。</summary>
    void Save();
}
