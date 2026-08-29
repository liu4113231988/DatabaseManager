using System;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;
using DatabaseManager.AppCore.Models;

namespace DatabaseManager.Avalonia.Converters;

/// <summary>
/// 行状态 → 背景色：新增=浅绿、修改=浅黄、删除=浅红、未修改=默认（UnsetValue）。
/// 绑定对象为结果行 <see cref="QueryResultRow"/>（其 State 变化带通知，单元格背景可实时更新）。
/// </summary>
public class RowStateToBrushConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is DataRowState state)
        {
            return GetBrush(state);
        }

        if (value is QueryResultRow row)
        {
            return GetBrush(row.State);
        }

        return AvaloniaProperty.UnsetValue;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => AvaloniaProperty.UnsetValue;

    /// <summary>按行状态取背景色（主题令牌，随深色/高对比切换）；未修改返回 UnsetValue（保持默认外观）。</summary>
    public static object GetBrush(DataRowState state)
        => state switch
        {
            DataRowState.Added => ThemeBrushResolver.Get("AppRowAddedBrush") ?? AvaloniaProperty.UnsetValue,
            DataRowState.Modified => ThemeBrushResolver.Get("AppRowModifiedBrush") ?? AvaloniaProperty.UnsetValue,
            DataRowState.Deleted => ThemeBrushResolver.Get("AppRowDeletedBrush") ?? AvaloniaProperty.UnsetValue,
            _ => AvaloniaProperty.UnsetValue,
        };
}
