using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace DatabaseManager.Avalonia.Converters;

public class HighlightBrushConverter : IValueConverter
{
    private static readonly SolidColorBrush TransparentBrush = new(Colors.Transparent);

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true
            ? ThemeBrushResolver.Get("AppHighlightBrush") ?? TransparentBrush
            : TransparentBrush;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}
