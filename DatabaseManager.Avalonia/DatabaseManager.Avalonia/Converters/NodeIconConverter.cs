using System;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Avalonia.Styling;
using DatabaseInterpreter.Model;
using DatabaseManager.AppCore.Models;

namespace DatabaseManager.Avalonia.Converters;

/// <summary>
/// 对象树节点图标转换器：使用统一的矢量 DrawingImage，避免旧位图在高分屏模糊。
/// </summary>
public class NodeIconConverter : IValueConverter
{
    /// <summary>图标缓存（键含主题变体，深色/高对比下浅色填充自动适配）。</summary>
    private static readonly Dictionary<string, DrawingImage> IconCache = new(StringComparer.Ordinal);

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not DbObjectTreeNode node)
            return null;

        var kind = GetIconKind(node);
        var variant = Application.Current?.ActualThemeVariant ?? ThemeVariant.Default;
        var cacheKey = $"{variant.Key}:{kind}";

        return IconCache.TryGetValue(cacheKey, out var icon)
            ? icon
            : IconCache[cacheKey] = BuildVectorIcon(kind);
    }

    private static string GetIconKind(DbObjectTreeNode node)
    {
        if (node.NodeType is DbObjectTreeNodeType.Connection or DbObjectTreeNodeType.Database) return "database";
        if (node.NodeType == DbObjectTreeNodeType.Schema) return "schema";
        if (node.NodeType is DbObjectTreeNodeType.Folder or DbObjectTreeNodeType.ChildFolder)
        {
            return node.Name switch
            {
                "Columns" => "column",
                "Indexes" => "index",
                "Keys" => "key",
                "Constraints" => "constraint",
                "Triggers" => "trigger",
                _ => node.DatabaseObjectType switch
                {
                    DatabaseObjectType.Table => "table-folder",
                    DatabaseObjectType.View => "view-folder",
                    DatabaseObjectType.Procedure => "code-folder",
                    DatabaseObjectType.Function => "code-folder",
                    _ => "folder",
                },
            };
        }

        return node.DatabaseObjectType switch
        {
            DatabaseObjectType.Table => "table",
            DatabaseObjectType.View => "view",
            DatabaseObjectType.Column => "column",
            DatabaseObjectType.Index => "index",
            DatabaseObjectType.PrimaryKey or DatabaseObjectType.ForeignKey => "key",
            DatabaseObjectType.Constraint => "constraint",
            DatabaseObjectType.Trigger => "trigger",
            DatabaseObjectType.Procedure or DatabaseObjectType.Function => "code",
            DatabaseObjectType.Sequence => "sequence",
            _ => "object",
        };
    }

    private static DrawingImage BuildVectorIcon(string kind)
    {
        var group = new DrawingGroup();
        var primary = new SolidColorBrush(Color.Parse("#3B82F6"));
        var teal = new SolidColorBrush(Color.Parse("#0F8B8D"));
        var violet = new SolidColorBrush(Color.Parse("#6B5DD3"));
        var amber = new SolidColorBrush(Color.Parse("#D97706"));
        var slate = new SolidColorBrush(Color.Parse("#52657F"));
        // 浅色填充随主题变体切换（深色/高对比下避免刺眼亮块）。
        var lightBlue = ThemeBrushResolver.Get("AppSelectedBrush") ?? new SolidColorBrush(Color.Parse("#EAF2FF"));
        var folderFill = ThemeBrushResolver.Get("AppNodeBadgeBrush") ?? new SolidColorBrush(Color.Parse("#FFF3D6"));
        var pen = new Pen(slate, 1.15);

        void Add(string path, IBrush? fill = null, IPen? outline = null) =>
            group.Children.Add(new GeometryDrawing { Geometry = StreamGeometry.Parse(path), Brush = fill, Pen = outline });

        switch (kind)
        {
            case "database":
                group.Children.Add(new GeometryDrawing { Geometry = new EllipseGeometry(new Rect(2.5, 2, 11, 4)), Brush = lightBlue, Pen = new Pen(primary, 1.1) });
                Add("M2.5,4 L2.5,12.5 C2.5,14.7 13.5,14.7 13.5,12.5 L13.5,4 M2.5,8.2 C2.5,10.3 13.5,10.3 13.5,8.2", lightBlue, new Pen(primary, 1.1));
                break;
            case "table": case "view":
                Add("M2.5,2.5 L13.5,2.5 L13.5,13.5 L2.5,13.5 Z M2.5,6 L13.5,6 M6.2,2.5 L6.2,13.5 M9.9,2.5 L9.9,13.5", lightBlue, new Pen(kind == "view" ? teal : primary, 1));
                break;
            case "table-folder": case "view-folder": case "code-folder": case "folder":
                var folderColor = kind == "code-folder" ? violet : kind == "view-folder" ? teal : amber;
                Add("M1.8,4.5 L6.5,4.5 L7.8,6 L14.2,6 L14.2,13.5 L1.8,13.5 Z", folderFill, new Pen(folderColor, 1.1));
                if (kind == "table-folder") Add("M7,8.3 L12,8.3 M7,10.6 L12,10.6", null, new Pen(folderColor, 0.9));
                break;
            case "schema":
                Add("M3,4 L11,4 L13,6 L5,6 Z M3,8 L11,8 L13,10 L5,10 Z M3,12 L11,12 L13,14 L5,14 Z", lightBlue, new Pen(violet, 0.9));
                break;
            case "column":
                Add("M3,3 L13,3 L13,13 L3,13 Z M5.5,6 L11,6 M5.5,8.5 L11,8.5 M5.5,11 L9,11", null, new Pen(slate, 1.1));
                break;
            case "key":
                Add("M6.5,8 A3,3 0 1 1 9.5,11 M8.8,10.2 L14,15.4 M11.5,12.9 L13,11.4 M13,14.4 L14.5,12.9", null, new Pen(amber, 1.4));
                break;
            case "index":
                Add("M3,3 L13,3 L13,13 L3,13 Z M5,6 L11,6 M5,9 L9,9 M5,12 L10,12", null, new Pen(teal, 1.1));
                break;
            case "constraint":
                Add("M8,2.5 L13,4.5 L12.2,10.5 L8,13.5 L3.8,10.5 L3,4.5 Z M5.8,8 L7.3,9.5 L10.5,6.3", lightBlue, new Pen(violet, 1));
                break;
            case "trigger":
                Add("M9,2 L4,9 L8,9 L7,14 L12,7 L8,7 Z", folderFill, new Pen(amber, 1));
                break;
            case "code":
                Add("M6.5,4 L3,8 L6.5,12 M9.5,4 L13,8 L9.5,12", null, new Pen(violet, 1.35));
                break;
            case "sequence":
                Add("M4,5 L12,5 M9.5,2.5 L12,5 L9.5,7.5 M12,11 L4,11 M6.5,8.5 L4,11 L6.5,13.5", null, new Pen(teal, 1.2));
                break;
            default:
                Add("M3,2.5 L10,2.5 L13,5.5 L13,13.5 L3,13.5 Z M10,2.5 L10,5.5 L13,5.5", lightBlue, pen);
                break;
        }
        return new DrawingImage { Drawing = group };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
