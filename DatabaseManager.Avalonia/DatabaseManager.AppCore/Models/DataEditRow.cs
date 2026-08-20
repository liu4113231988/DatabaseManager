using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace DatabaseManager.AppCore.Models;

/// <summary>数据编辑行的状态。</summary>
public enum DataRowState
{
    /// <summary>未修改。</summary>
    Unchanged,

    /// <summary>新增行（尚未保存）。</summary>
    Added,

    /// <summary>已修改（有脏列）。</summary>
    Modified,

    /// <summary>已删除（待保存时删除）。</summary>
    Deleted,
}

/// <summary>
/// 数据编辑行（AppCore 领域模型，UI 无关）。
/// 以「列定义列表」为基线维护顺序值数组，并跟踪原始值快照、脏列与行状态，
/// 供可编辑数据网格绑定（整数索引）与增删改 SQL 生成使用。
/// </summary>
public class DataEditRow : INotifyPropertyChanged
{
    private readonly IReadOnlyList<DataColumnInfo> _columns;
    private readonly List<object?> _values;
    private DataRowState _state = DataRowState.Unchanged;

    /// <summary>行唯一标识（内存态，用于区分新增/修改/删除）。</summary>
    public Guid Id { get; } = Guid.NewGuid();

    /// <summary>原始值快照（按列名索引），用于生成 UPDATE WHERE 条件与脏列判定。</summary>
    public Dictionary<string, object?> OriginalValues { get; } = new();

    /// <summary>脏列名集合。</summary>
    public HashSet<string> DirtyColumns { get; } = new();

    /// <summary>行当前状态。</summary>
    public DataRowState State
    {
        get => _state;
        set
        {
            if (_state == value) return;
            _state = value;
            OnPropertyChanged(nameof(State));
            OnPropertyChanged(nameof(IsDirty));
        }
    }

    /// <summary>是否存在未保存的改动（新增/修改/删除）。</summary>
    public bool IsDirty => State != DataRowState.Unchanged;

    /// <summary>当前行是否为只读（仅用于整体提示）。</summary>
    public bool IsReadOnly => false;

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>按列名索引取值。</summary>
    public object? this[string columnName]
    {
        get
        {
            var idx = FindColumnIndex(columnName);
            return idx >= 0 ? _values[idx] : null;
        }
        set
        {
            var idx = FindColumnIndex(columnName);
            if (idx >= 0) SetValue(idx, value);
        }
    }

    /// <summary>按列索引取值。</summary>
    public object? this[int index]
    {
        get => index >= 0 && index < _values.Count ? _values[index] : null;
        set => SetValue(index, value);
    }

    public DataEditRow(IReadOnlyList<DataColumnInfo> columns)
    {
        _columns = columns;
        _values = new List<object?>(new object?[columns.Count]);
    }

    /// <summary>将某列值标记为 null（用于清除单元格）。</summary>
    public void SetCellToNull(string columnName)
    {
        var idx = FindColumnIndex(columnName);
        if (idx >= 0) SetValue(idx, null);
    }

    /// <summary>设置某列索引的值并标记脏列（若与原始值不同）。</summary>
    public void SetValue(int index, object? value)
    {
        if (index < 0 || index >= _values.Count) return;

        var column = _columns[index];
        var normalized = NormalizeValue(value);

        // 只读列（计算/自增/二进制/几何）不允许编辑。
        if (column.IsReadOnly)
        {
            return;
        }

        SetValueDirect(index, normalized);

        OnPropertyChanged($"Item[{index}]");
        OnPropertyChanged($"Item[{column.Name}]");
        OnPropertyChanged(nameof(IsDirty));
    }

    /// <summary>直接写入列值（不经过只读校验，用于数据加载），并触发属性变化通知。</summary>
    public void SetCellValueDirect(int index, object? value)
    {
        if (index < 0 || index >= _values.Count) return;
        _values[index] = NormalizeValue(value);
        OnPropertyChanged($"Item[{index}]");
        OnPropertyChanged($"Item[{_columns[index].Name}]");
    }

    private void SetValueDirect(int index, object? value)
    {
        if (index < 0 || index >= _values.Count) return;

        var column = _columns[index];
        var normalized = NormalizeValue(value);
        var original = GetOriginal(column.Name);

        _values[index] = normalized;

        bool changed = !Equals(original, normalized);
        if (changed)
        {
            DirtyColumns.Add(column.Name);
            if (State == DataRowState.Unchanged)
            {
                State = DataRowState.Modified;
            }
        }
        else
        {
            DirtyColumns.Remove(column.Name);
            if (DirtyColumns.Count == 0 && State == DataRowState.Modified)
            {
                State = DataRowState.Unchanged;
            }
        }
    }

    /// <summary>新增行的构造入口（由 ViewModel 调用）。</summary>
    public void MarkAsAdded()
    {
        State = DataRowState.Added;
    }

    /// <summary>删除当前行（标记为已删除）。</summary>
    public void MarkAsDeleted()
    {
        State = DataRowState.Deleted;
    }

    /// <summary>恢复为未修改（提交成功后调用），并将当前值作为新的原始快照。</summary>
    public void MarkAsSaved()
    {
        State = DataRowState.Unchanged;
        DirtyColumns.Clear();
        OriginalValues.Clear();
        for (int i = 0; i < _columns.Count; i++)
        {
            OriginalValues[_columns[i].Name] = _values[i];
        }
    }

    /// <summary>获取指定列的原始值。</summary>
    public object? GetOriginal(string columnName)
        => OriginalValues.TryGetValue(columnName, out var v) ? v : null;

    /// <summary>获取当前值。</summary>
    public object? GetValue(string columnName)
    {
        var idx = FindColumnIndex(columnName);
        return idx >= 0 ? _values[idx] : null;
    }

    /// <summary>获取用于 UPDATE WHERE 的主键条件列（原始值 + 当前值兜底）。</summary>
    public IEnumerable<(string Column, object? Value)> GetPrimaryKeyConditions()
    {
        foreach (var col in _columns.Where(c => c.IsPrimaryKey))
        {
            if (OriginalValues.TryGetValue(col.Name, out var orig))
            {
                yield return (col.Name, orig);
            }
            else
            {
                yield return (col.Name, _values[FindColumnIndex(col.Name)]);
            }
        }
    }

    /// <summary>获取全部脏列（列名 → 新值）。</summary>
    public IEnumerable<(DataColumnInfo Column, object? Value)> GetDirtyColumns()
    {
        foreach (var col in _columns)
        {
            if (DirtyColumns.Contains(col.Name))
            {
                yield return (col, _values[FindColumnIndex(col.Name)]);
            }
        }
    }

    /// <summary>获取所有列（列名 → 当前值），用于 INSERT。</summary>
    public IEnumerable<(DataColumnInfo Column, object? Value)> GetAllValues()
    {
        foreach (var col in _columns)
        {
            yield return (col, _values[FindColumnIndex(col.Name)]);
        }
    }

    private int FindColumnIndex(string columnName)
    {
        for (int i = 0; i < _columns.Count; i++)
        {
            if (string.Equals(_columns[i].Name, columnName, StringComparison.OrdinalIgnoreCase))
                return i;
        }
        return -1;
    }

    private static object? NormalizeValue(object? value)
    {
        if (value is null) return null;
        if (value is string s && s.Length == 0) return null;
        return value;
    }

    private void OnPropertyChanged(string name)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
