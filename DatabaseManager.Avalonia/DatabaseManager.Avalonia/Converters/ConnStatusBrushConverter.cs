using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using DatabaseManager.AppCore.Models;

namespace DatabaseManager.Avalonia.Converters;

/// <summary>
/// 连接状态点画刷：根据 IsConnectionActive + ConnectionState 给出绿/灰/红。
/// </summary>
public class ConnStatusBrushConverter : IMultiValueConverter
{
    public static ConnStatusBrushConverter Instance { get; } = new();

    private static readonly IBrush ActiveBrush = new SolidColorBrush(Color.Parse("#13A24A"));
    private static readonly IBrush ConnectingBrush = new SolidColorBrush(Color.Parse("#D97706"));
    private static readonly IBrush FailedBrush = new SolidColorBrush(Color.Parse("#D93025"));
    private static readonly IBrush IdleBrush = new SolidColorBrush(Color.Parse("#A0A6AD"));

    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        var isActive = values.Count > 0 && values[0] is bool b && b;
        var state = values.Count > 1 ? values[1] as string : null;

        if (isActive) return ActiveBrush;
        return state switch
        {
            "Connecting" => ConnectingBrush,
            "Failed" => FailedBrush,
            _ => IdleBrush,
        };
    }
}
