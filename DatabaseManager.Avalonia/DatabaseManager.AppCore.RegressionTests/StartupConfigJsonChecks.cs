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
        if (!settings.RequireDeleteNameConfirmation)
            throw new InvalidOperationException("Typed delete-name confirmation must default to enabled for legacy settings.");
        if (settings.QueryResultMaxRows != 100000 || settings.QueryCommandTimeoutSeconds != 0
            || !settings.AutoSaveSession || !settings.ConfirmDangerousSql)
            throw new InvalidOperationException("New query and session settings must default for legacy settings.");

        settings.RequireDeleteNameConfirmation = false;
        settings.QueryResultMaxRows = 5000;
        settings.QueryCommandTimeoutSeconds = 120;
        settings.AutoSaveSession = false;
        settings.ConfirmDangerousSql = false;
        string saved = JsonSerializer.Serialize(settings, AppSettingsJsonContext.Default.AppSettings);
        var restored = JsonSerializer.Deserialize(saved, AppSettingsJsonContext.Default.AppSettings)!;
        if (restored.Workspace.Tabs[0].ConnectionName != "local" || Math.Abs(restored.Workspace.LeftPanelWidth - 360) > 0.0001
            || restored.RequireDeleteNameConfirmation
            || restored.QueryResultMaxRows != 5000 || restored.QueryCommandTimeoutSeconds != 120
            || restored.AutoSaveSession || restored.ConfirmDangerousSql)
            throw new InvalidOperationException("AppSettings source-generated JSON round trip must preserve workspace, delete-confirmation and new query/session state.");

        var clamped = new AppSettings
        {
            ThemeMode = "Unknown",
            FontScale = 99,
            QueryResultMaxRows = 1,
            QueryCommandTimeoutSeconds = 99999,
        };
        clamped.Normalize();
        if (clamped.ThemeMode != ThemeModes.System || Math.Abs(clamped.FontScale - 2.0) > 0.0001
            || clamped.QueryResultMaxRows != 1000 || clamped.QueryCommandTimeoutSeconds != 3600)
            throw new InvalidOperationException("AppSettings normalization must clamp visual and query values into supported ranges.");

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
