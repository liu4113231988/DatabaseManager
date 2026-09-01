using Avalonia.Styling;

namespace DatabaseManager.Avalonia;

/// <summary>
/// 应用自定义主题变体。XAML 主题字典只能以内置变体或静态 ThemeVariant 实例作为键。
/// </summary>
public static class AppThemeVariants
{
    public static ThemeVariant HighContrast { get; } = new("HighContrast", ThemeVariant.Light);
}
