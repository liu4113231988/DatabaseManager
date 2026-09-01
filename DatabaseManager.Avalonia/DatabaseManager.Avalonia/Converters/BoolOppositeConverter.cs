using System.Globalization;
using Avalonia.Data.Converters;

namespace DatabaseManager.Avalonia.Converters;

/// <summary>
/// 布尔值取反转换器（用于 IsEnabled 绑定等场景）。
/// </summary>
public class BoolOppositeConverter : IValueConverter
{
    public static BoolOppositeConverter Instance { get; } = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool b)
            return !b;
        return true; // 默认启用
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool b)
            return !b;
        return false;
    }
}
