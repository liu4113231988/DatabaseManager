using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using DatabaseManager.Core.Model;

namespace DatabaseManager.Avalonia.Converters;

/// <summary>
/// 差异类型 → 颜色转换器。为结构对比结果的不同变更类型着色：
/// 新增=绿、修改=橙、删除=红。
/// </summary>
public class DiffColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is SchemaCompareDifferenceType type)
        {
            return type switch
            {
                SchemaCompareDifferenceType.Added => Brushes.Green,
                SchemaCompareDifferenceType.Modified => Brushes.DarkOrange,
                SchemaCompareDifferenceType.Deleted => Brushes.Red,
                _ => Brushes.Gray,
            };
        }

        return Brushes.Gray;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
