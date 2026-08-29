using Avalonia;
using Avalonia.Media;

namespace DatabaseManager.Avalonia.Converters;

/// <summary>
/// 主题画刷解析：按当前主题变体从应用资源取令牌画刷（供 C# 转换器/代码使用，
/// 与 XAML 的 DynamicResource 等价，主题切换后取值跟随）。
/// </summary>
public static class ThemeBrushResolver
{
    public static IBrush? Get(string resourceKey)
    {
        if (Application.Current is not { } app)
        {
            return null;
        }

        return app.TryGetResource(resourceKey, app.ActualThemeVariant, out var value) && value is IBrush brush
            ? brush
            : null;
    }
}
