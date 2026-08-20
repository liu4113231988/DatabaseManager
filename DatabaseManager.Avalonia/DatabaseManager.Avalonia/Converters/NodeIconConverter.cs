using System;
using System.Globalization;
using Avalonia.Data.Converters;
using DatabaseInterpreter.Model;
using DatabaseManager.AppCore.Models;

namespace DatabaseManager.Avalonia.Converters;

/// <summary>
/// 对象树节点图标转换器：根据节点类型/数据库对象类型返回对应的显示字形（Unicode 图标）。
/// </summary>
public class NodeIconConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is DbObjectTreeNode node)
        {
            switch (node.NodeType)
            {
                case DbObjectTreeNodeType.Database:
                    return "\u1F4BE"; // 💾
                case DbObjectTreeNodeType.Schema:
                    return "\u1F4C1"; // 📁
                case DbObjectTreeNodeType.ChildFolder:
                    return "\u1F4C2"; // 📂
                case DbObjectTreeNodeType.ChildObject:
                    return node.DatabaseObjectType switch
                    {
                        DatabaseObjectType.Column => "\u2751",
                        DatabaseObjectType.Trigger => "\u26A1",
                        DatabaseObjectType.Index => "\u2613",
                        DatabaseObjectType.PrimaryKey => "\uD83D\uDD11",
                        DatabaseObjectType.ForeignKey => "\uD83D\uDD17",
                        DatabaseObjectType.Constraint => "\u2696",
                        _ => "\u2022",
                    };
                case DbObjectTreeNodeType.Folder:
                    return "\u1F4C1"; // 📁
                case DbObjectTreeNodeType.DbObject:
                    return node.DatabaseObjectType switch
                    {
                        DatabaseObjectType.Table => "\u1F4CA", // 📊
                        DatabaseObjectType.View => "\u1F4D1", // 📑
                        DatabaseObjectType.Function => "\u0192",
                        DatabaseObjectType.Procedure => "\u2699",
                        DatabaseObjectType.Sequence => "\u2116",
                        DatabaseObjectType.Type => "\uD83D\uDCD0",
                        _ => "\u2022",
                    };
            }
        }

        return "\u2022";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
