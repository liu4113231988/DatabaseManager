using System;
using System.Globalization;
using Avalonia.Data.Converters;
using DatabaseManager.AppCore.Models;

namespace DatabaseManager.Avalonia.Converters;

/// <summary>
/// 仅当节点类型为 Connection 时显示（控制连接状态点的可见性）。
/// </summary>
public class ShowConnStatusConverter : IValueConverter
{
    public static ShowConnStatusConverter Instance { get; } = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is DbObjectTreeNodeType t && t == DbObjectTreeNodeType.Connection;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
