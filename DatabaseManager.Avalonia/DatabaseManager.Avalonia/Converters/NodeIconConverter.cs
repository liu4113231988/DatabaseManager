using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using DatabaseInterpreter.Model;
using DatabaseManager.AppCore.Models;

namespace DatabaseManager.Avalonia.Converters;

/// <summary>
/// 对象树节点图标转换器：根据节点类型/数据库对象类型返回对应的图标资源（Assets 图片）。
/// 对齐 dbeaver 的对象树图标风格。
/// </summary>
public class NodeIconConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not DbObjectTreeNode node)
            return null;

        var uri = GetIconUri(node);
        return uri is null ? null : LoadBitmap(uri);
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
