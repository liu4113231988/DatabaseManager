using System.Collections.Generic;
using DatabaseInterpreter.Utility;

namespace DatabaseManager.AppCore.Models;

/// <summary>
/// 数据编辑的表元数据（AppCore 领域模型，UI 无关）。
/// 描述目标表的列定义、主键列等，供数据网格展示与增删改生成 SQL 使用。
/// </summary>
public class DataTableInfo
{
    /// <summary>表所属数据库。</summary>
    public string DatabaseName { get; set; } = string.Empty;

    /// <summary>表所属 Schema（可为空）。</summary>
    public string? Schema { get; set; }

    /// <summary>表名。</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>是否视图（视图通常只读）。</summary>
    public bool IsView { get; set; }

    /// <summary>列定义列表。</summary>
    public IReadOnlyList<DataColumnInfo> Columns { get; set; } = System.Array.Empty<DataColumnInfo>();

    /// <summary>主键列名列表。</summary>
    public IReadOnlyList<string> PrimaryKeyColumns { get; set; } = System.Array.Empty<string>();

    /// <summary>标识（自增）列名。</summary>
    public IReadOnlyList<string> IdentityColumns { get; set; } = System.Array.Empty<string>();
}

/// <summary>数据编辑的列定义（AppCore 领域模型）。</summary>
public class DataColumnInfo
{
    /// <summary>列名。</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>数据类型（小写）。</summary>
    public string DataType { get; set; } = string.Empty;

    /// <summary>是否主键列。</summary>
    public bool IsPrimaryKey { get; set; }

    /// <summary>是否标识（自增）列。</summary>
    public bool IsIdentity { get; set; }

    /// <summary>是否计算列（只读）。</summary>
    public bool IsComputed { get; set; }

    /// <summary>是否允许为空。</summary>
    public bool IsNullable { get; set; } = true;

    /// <summary>列顺序。</summary>
    public int Order { get; set; }

    /// <summary>是否为只读列（主键外、计算列、标识列或二进制/几何列）。</summary>
    public bool IsReadOnly =>
        IsComputed
        || (IsIdentity)
        || DataTypeHelper.IsBinaryType(DataType)
        || DataTypeHelper.IsGeometryType(DataType);
}
