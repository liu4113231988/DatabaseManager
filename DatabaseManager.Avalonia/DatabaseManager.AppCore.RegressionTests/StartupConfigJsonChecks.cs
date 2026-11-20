using System.Text.Json;
using DatabaseManager.AppCore.Services;

internal static class StartupConfigJsonChecks
{
    public static void Run()
    {
        const string legacySettings = """
            {
              // old format kept for compatibility
              "themeMode": "Dark",
              "fontScale": "1.1",
              "workspace": {
                "windowX": 10,
                "windowY": 20,
                "windowWidth": 1280,
                "windowHeight": 800,
                "windowState": "Maximized",
                "leftPanelWidth": 360,
                "focusMode": true,
                "tabs": [ { "title": "q1", "sqlText": "select 1", "connectionName": "local" } ]
              }
            }
            """;
        var settings = JsonSerializer.Deserialize(legacySettings, AppSettingsJsonContext.Default.AppSettings)
            ?? throw new InvalidOperationException("AppSettings source-generated JSON must load legacy files.");
        if (settings.ThemeMode != ThemeModes.Dark || Math.Abs(settings.FontScale - 1.1) > 0.0001
            || settings.Workspace.WindowState != "Maximized" || !settings.Workspace.FocusMode
            || settings.Workspace.Tabs.Count != 1 || settings.Workspace.Tabs[0].SqlText != "select 1")
            throw new InvalidOperationException("AppSettings legacy JSON fields must round trip.");

        string saved = JsonSerializer.Serialize(settings, AppSettingsJsonContext.Default.AppSettings);
        var restored = JsonSerializer.Deserialize(saved, AppSettingsJsonContext.Default.AppSettings)!;
        if (restored.Workspace.Tabs[0].ConnectionName != "local" || Math.Abs(restored.Workspace.LeftPanelWidth - 360) > 0.0001)
            throw new InvalidOperationException("AppSettings source-generated JSON round trip must preserve workspace state.");

        const string legacyVisuals = """
            [
              {
                "connectionId": "c1",
                "connectionName": "测试连接",
                "group": "生产",
                "colorTag": "#1E88E5",
                "kingbaseCompatibilityMode": "PG",
                "duckDbReadOnly": true
              }
            ]
            """;
        var visuals = JsonSerializer.Deserialize(legacyVisuals, ConnectionVisualJsonContext.Default.ListConnectionVisualInfo)
            ?? throw new InvalidOperationException("Connection visual source-generated JSON must load legacy files.");
        if (visuals.Count != 1 || visuals[0].Group != "生产" || visuals[0].ColorTag != "#1E88E5"
            || visuals[0].KingbaseCompatibilityMode != "PG" || !visuals[0].DuckDbReadOnly)
            throw new InvalidOperationException("Connection visual legacy JSON fields must round trip.");

        Console.WriteLine("Startup config JSON checks passed: source-generated settings and connection visuals with legacy compatibility.");
    }
}
