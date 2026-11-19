using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

namespace DatabaseManager.AppCore.Models;

/// <summary>表设计器可编辑子对象的通知基类。</summary>
public abstract class TableDesignObservable : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

/// <summary>
/// 表设计器领域模型（AppCore 层，UI 无关）。
/// 描述一张表的完整结构：列、主键、索引、外键、约束。
/// 由 <see cref="Services.ITableDesignService"/> 从数据库加载或由 UI 编辑后生成 CREATE/ALTER 脚本。
/// </summary>
public class TableDesignInfo
{
    /// <summary>所属数据库。</summary>
    public string DatabaseName { get; set; } = string.Empty;

    /// <summary>所属 Schema（可为空）。</summary>
    public string? Schema { get; set; }

    /// <summary>表名。</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>是否为新建表（true=CREATE，false=ALTER）。</summary>
    public bool IsNew { get; set; }

    /// <summary>表注释。</summary>
    public string Comment { get; set; } = string.Empty;

    /// <summary>列定义列表。</summary>
    public List<TableDesignColumn> Columns { get; set; } = new();

    /// <summary>主键定义（可为空）。</summary>
    public TableDesignKey? PrimaryKey { get; set; }

    /// <summary>索引列表。</summary>
    public List<TableDesignIndex> Indexes { get; set; } = new();

    /// <summary>外键列表。</summary>
    public List<TableDesignForeignKey> ForeignKeys { get; set; } = new();

    /// <summary>约束列表。</summary>
    public List<TableDesignConstraint> Constraints { get; set; } = new();
}

/// <summary>表设计器列定义。</summary>
public class TableDesignColumn
{
    /// <summary>列名。</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>数据类型。</summary>
    public string DataType { get; set; } = string.Empty;

    /// <summary>长度。</summary>
    public long? MaxLength { get; set; }

    /// <summary>精度。</summary>
    public long? Precision { get; set; }

    /// <summary>小数位。</summary>
    public long? Scale { get; set; }

    /// <summary>是否允许为空。</summary>
    public bool IsNullable { get; set; } = true;

    /// <summary>是否自增（标识列）。</summary>
    public bool IsIdentity { get; set; }

    /// <summary>默认值。</summary>
    public string DefaultValue { get; set; } = string.Empty;

    /// <summary>是否计算列。</summary>
    public bool IsComputed => !string.IsNullOrEmpty(ComputeExp);

    /// <summary>计算表达式。</summary>
    public string ComputeExp { get; set; } = string.Empty;

    /// <summary>列注释。</summary>
    public string Comment { get; set; } = string.Empty;

    /// <summary>列顺序。</summary>
    public int Order { get; set; }
}

/// <summary>表设计器主键定义。</summary>
public class TableDesignKey : TableDesignObservable
{
    /// <summary>主键名称。</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>主键包含的列名。</summary>
    private List<string> _columns = new();

    public List<string> Columns
    {
        get => _columns;
        set
        {
            _columns = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ColumnsDisplay));
        }
    }

    /// <summary>是否聚集。</summary>
    public bool Clustered { get; set; } = true;

    /// <summary>列显示（英文逗号连接，可直接内联编辑）。</summary>
    public string ColumnsDisplay
    {
        get => TableDesignColumnParser.ToDisplay(Columns);
        set => Columns = TableDesignColumnParser.ParseColumns(value);
    }
}

/// <summary>表设计器索引定义。</summary>
public class TableDesignIndex : TableDesignObservable
{
    /// <summary>索引名称。</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>是否唯一。</summary>
    public bool IsUnique { get; set; }

    /// <summary>索引包含的列名。</summary>
    private List<string> _columns = new();

    public List<string> Columns
    {
        get => _columns;
        set
        {
            _columns = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ColumnsDisplay));
        }
    }

    /// <summary>列显示（英文逗号连接，可直接内联编辑）。</summary>
    public string ColumnsDisplay
    {
        get => TableDesignColumnParser.ToDisplay(Columns);
        set => Columns = TableDesignColumnParser.ParseColumns(value);
    }
}

/// <summary>表设计器外键定义。</summary>
public class TableDesignForeignKey : TableDesignObservable
{
    /// <summary>外键名称。</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>外键列 → 引用列 映射。</summary>
    private List<ForeignKeyMapping> _columns = new();

    public List<ForeignKeyMapping> Columns
    {
        get => _columns;
        set
        {
            _columns = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ColumnsDisplay));
        }
    }

    /// <summary>引用表 Schema。</summary>
    public string ReferencedSchema { get; set; } = string.Empty;

    /// <summary>引用表名。</summary>
    public string ReferencedTableName { get; set; } = string.Empty;

    /// <summary>更新级联。</summary>
    public bool UpdateCascade { get; set; }

    /// <summary>删除级联。</summary>
    public bool DeleteCascade { get; set; }

    /// <summary>列映射显示（本表列=引用列，可直接内联编辑）。</summary>
    public string ColumnsDisplay
    {
        get => TableDesignColumnParser.ToDisplay(Columns);
        set => Columns = TableDesignColumnParser.ParseMappings(value);
    }

    public void SelectLocalColumns(IEnumerable<string> selectedColumns)
    {
        Columns = selectedColumns.Select(name =>
            Columns.FirstOrDefault(mapping => string.Equals(mapping.ColumnName, name, StringComparison.Ordinal))
            ?? new ForeignKeyMapping { ColumnName = name, ReferencedColumnName = name }).ToList();
    }
}

/// <summary>外键列映射（本表列 → 引用表列）。</summary>
public class ForeignKeyMapping
{
    /// <summary>本表列名。</summary>
    public string ColumnName { get; set; } = string.Empty;

    /// <summary>引用表列名。</summary>
    public string ReferencedColumnName { get; set; } = string.Empty;
}

/// <summary>表设计器检查约束定义。</summary>
public class TableDesignConstraint
{
    /// <summary>约束名称。</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>约束表达式（Check 定义）。</summary>
    public string Definition { get; set; } = string.Empty;
}

/// <summary>表分区只读展示项。</summary>
public class TablePartitionItem
{
    public string Name { get; set; } = string.Empty;
    public string HighValue { get; set; } = string.Empty;
    public string Tablespace { get; set; } = string.Empty;
    public int Order { get; set; }
}
