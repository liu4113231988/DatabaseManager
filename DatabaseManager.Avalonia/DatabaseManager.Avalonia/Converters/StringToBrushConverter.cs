using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace DatabaseManager.Avalonia.Converters;

/// <summary>
/// 连接颜色标签字符串 → 画刷：解析 hex 颜色；为空或解析失败时返回透明画刷。
/// 与对象树节点的 DbObjectTreeNode.ColorTagBrush 行为一致，复用同一种色彩识别逻辑。
/// </summary>
public class StringToBrushConverter : IValueConverter
{
    public static StringToBrushConverter Instance { get; } = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string s && !string.IsNullOrEmpty(s))
        {
            try
            {
                return new SolidColorBrush(Color.Parse(s));
            }
            catch
            {
                // 无效颜色按无色处理。
            }
        }
        return Brushes.Transparent;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
