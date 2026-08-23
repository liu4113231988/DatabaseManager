using System;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using DatabaseInterpreter.Model;
using DatabaseManager.AppCore.Models;

namespace DatabaseManager.Avalonia.Converters;

/// <summary>
/// 对象树节点图标转换器：连接/数据库节点使用矢量 DrawingImage（现代化蓝色数据库图标），
/// 其他对象沿用 Assets 位图资源。对齐 dbeaver 风格的视觉同时消除位图放大模糊。
/// </summary>
public class NodeIconConverter : IValueConverter
{
    private static DrawingImage? _vectorDbIcon;

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not DbObjectTreeNode node)
            return null;

        // 连接 / 数据库节点：优先返回矢量图标（更清晰、更现代）
        if (node.NodeType is DbObjectTreeNodeType.Connection or DbObjectTreeNodeType.Database)
        {
            return _vectorDbIcon ??= BuildVectorDatabaseIcon();
        }

        var uri = GetIconUri(node);
        return uri is null ? null : LoadBitmap(uri);
    }

    /// <summary>构建一个 16×16 的现代化数据库矢量图标（蓝色三层圆柱 + 白色高光）。</summary>
    private static DrawingImage BuildVectorDatabaseIcon()
    {
        var drawings = new DrawingGroup();

        // 主色调：蓝色系渐变填充 + 深色描边
        var bodyBrush = new SolidColorBrush(Color.Parse("#3B82F6"));      // 主蓝
        var topBrush = new SolidColorBrush(Color.Parse("#60A5FA"));       // 顶部浅蓝（高光）
        var rimBrush = new SolidColorBrush(Color.Parse("#1E40AF"));       // 顶部椭圆描边深色
        var sideBrush = new SolidColorBrush(Color.Parse("#2563EB"));      // 中间分隔线
        var strokePen = new Pen(new SolidColorBrush(Color.Parse("#1D4ED8")), 0.75);

        double left = 1.2, right = 14.8;
        double ellipseH = 2.8;

        // 顶部椭圆（完整的）
        var topEllipse = new EllipseGeometry
        {
            Rect = new Rect(left, 1.0, right - left, ellipseH)
        };
        drawings.Children.Add(new GeometryDrawing { Brush = topBrush, Pen = new Pen(rimBrush, 0.75), Geometry = topEllipse });

        // 下方圆柱主体（中间长方形 + 底部椭圆）
        var bodyRectGeo = new RectangleGeometry
        {
            Rect = new Rect(left, 1.0 + ellipseH / 2, right - left, 12.5 - ellipseH / 2)
        };
        drawings.Children.Add(new GeometryDrawing { Brush = bodyBrush, Geometry = bodyRectGeo });

        var bottomEllipse = new EllipseGeometry
        {
            Rect = new Rect(left, 13.0, right - left, ellipseH)
        };
        drawings.Children.Add(new GeometryDrawing { Brush = bodyBrush, Pen = strokePen, Geometry = bottomEllipse });

        // 两条分隔椭圆线（突出层次）
        var mid1 = new EllipseGeometry
        {
            Rect = new Rect(left, 5.0, right - left, ellipseH)
        };
        drawings.Children.Add(new GeometryDrawing { Pen = new Pen(sideBrush, 0.75), Geometry = mid1 });

        var mid2 = new EllipseGeometry
        {
            Rect = new Rect(left, 9.0, right - left, ellipseH)
        };
        drawings.Children.Add(new GeometryDrawing { Pen = new Pen(sideBrush, 0.75), Geometry = mid2 });

        // 顶部椭圆再画一次（放在最上层，让层次更清晰）
        drawings.Children.Add(new GeometryDrawing { Brush = topBrush, Pen = new Pen(rimBrush, 0.75), Geometry = topEllipse });

        return new DrawingImage
        {
            Drawing = drawings,
        };
    }

    private static string? GetIconUri(DbObjectTreeNode node)
    {
        string basePath = "avares://DatabaseManager.Avalonia/Assets/";

        switch (node.NodeType)
        {
            case DbObjectTreeNodeType.Connection:
                return $"{basePath}tree_Database.png"; // 连接默认用数据库图标
            case DbObjectTreeNodeType.Database:
                return $"{basePath}tree_Database.png";
            case DbObjectTreeNodeType.Schema:
                return $"{basePath}Schema.png";
            case DbObjectTreeNodeType.Folder:
                return node.DatabaseObjectType switch
                {
                    DatabaseObjectType.Table => $"{basePath}tree_Table.png",
                    DatabaseObjectType.View => $"{basePath}tree_View.png",
                    DatabaseObjectType.Procedure => $"{basePath}tree_Procedure.png",
                    DatabaseObjectType.Function => $"{basePath}tree_Function.png",
                    DatabaseObjectType.Sequence => $"{basePath}tree_Sequence.png",
                    DatabaseObjectType.Type => $"{basePath}tree_UserDefinedType.png",
                    _ => $"{basePath}tree_Folder.png",
                };
            case DbObjectTreeNodeType.DbObject:
                return node.DatabaseObjectType switch
                {
                    DatabaseObjectType.Table => $"{basePath}tree_Table.png",
                    DatabaseObjectType.View => $"{basePath}tree_View.png",
                    DatabaseObjectType.Procedure => $"{basePath}tree_Procedure.png",
                    DatabaseObjectType.Function => $"{basePath}tree_Function.png",
                    DatabaseObjectType.Sequence => $"{basePath}tree_Sequence.png",
                    DatabaseObjectType.Type => $"{basePath}tree_UserDefinedType.png",
                    DatabaseObjectType.Trigger => $"{basePath}tree_Function_Trigger.png",
                    _ => $"{basePath}tree_Folder.png",
                };
            case DbObjectTreeNodeType.ChildFolder:
                return node.Name switch
                {
                    "Columns" => $"{basePath}tree_TableColumn.png",
                    "Triggers" => $"{basePath}tree_TableTrigger.png",
                    "Indexes" => $"{basePath}tree_TableIndex.png",
                    "Keys" => $"{basePath}tree_TablePrimaryKey.png",
                    "Constraints" => $"{basePath}tree_TableConstraint.png",
                    _ => $"{basePath}tree_Folder.png",
                };
            case DbObjectTreeNodeType.ChildObject:
                return node.DatabaseObjectType switch
                {
                    DatabaseObjectType.Column => $"{basePath}Column.png",
                    DatabaseObjectType.Trigger => $"{basePath}tree_Function_Trigger.png",
                    DatabaseObjectType.Index => $"{basePath}tree_TableIndex.png",
                    DatabaseObjectType.PrimaryKey => $"{basePath}tree_TablePrimaryKey.png",
                    DatabaseObjectType.ForeignKey => $"{basePath}tree_TableForeignKey.png",
                    DatabaseObjectType.Constraint => $"{basePath}tree_TableConstraint.png",
                    _ => $"{basePath}tree_Folder.png",
                };
            default:
                return null;
        }
    }

    private static Bitmap? LoadBitmap(string uri)
    {
        try
        {
            return new Bitmap(AssetLoader.Open(new Uri(uri)));
        }
        catch
        {
            return null;
        }
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
