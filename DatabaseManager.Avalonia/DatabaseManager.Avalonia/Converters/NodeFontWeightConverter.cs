using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using DatabaseManager.AppCore.Models;

namespace DatabaseManager.Avalonia.Converters;

public class NodeFontWeightConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is DbObjectTreeNodeType type)
        {
            return type is DbObjectTreeNodeType.Folder or DbObjectTreeNodeType.Database or DbObjectTreeNodeType.Schema or DbObjectTreeNodeType.Connection
                ? FontWeight.SemiBold
                : FontWeight.Regular;
        }
        return FontWeight.Regular;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}
