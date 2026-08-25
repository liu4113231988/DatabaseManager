using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace DatabaseManager.AppCore.Models;

/// <summary>
/// 查询结果（AppCore 领域模型，UI 无关）。
/// 封装查询返回的数据表、受影响行数、执行耗时等信息。
/// </summary>
public class QueryResult
{
    /// <summary>列名列表。</summary>
    public IReadOnlyList<string> Columns { get; init; } = System.Array.Empty<string>();

    /// <summary>行数据（每行为列值的字符串化集合）。</summary>
    public IReadOnlyList<IReadOnlyList<string>> Rows { get; init; } = System.Array.Empty<IReadOnlyList<string>>();

    /// <summary>受影响/返回的行数。</summary>
    public int RowCount { get; init; }

    /// <summary>执行耗时（毫秒）。</summary>
    public long ElapsedMilliseconds { get; init; }

    /// <summary>是否为非查询语句（仅受影响行数，无结果集）。</summary>
    public bool IsNonQuery { get; init; }

    /// <summary>错误信息（若执行失败）。</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>是否执行成功。</summary>
    public bool IsSuccess => string.IsNullOrEmpty(ErrorMessage);

    /// <summary>从 DataTable 转换为 UI 无关的查询结果。</summary>
    public static QueryResult FromDataTable(DataTable table, long elapsedMilliseconds)
    {
        var columns = table.Columns.Cast<DataColumn>().Select(c => c.ColumnName).ToList();

        var rows = new List<IReadOnlyList<string>>();
        foreach (DataRow row in table.Rows)
        {
            var values = new List<string>(table.Columns.Count);
            foreach (DataColumn col in table.Columns)
            {
                var value = row[col];
                values.Add(value is null || value == System.DBNull.Value ? string.Empty : value.ToString() ?? string.Empty);
            }
            rows.Add(values);
        }

        return new QueryResult
        {
            Columns = columns,
            Rows = rows,
            RowCount = rows.Count,
            ElapsedMilliseconds = elapsedMilliseconds,
        };
    }
}

/// <summary>
/// 查询结果行（支持内联编辑）。
/// 维护当前值数组、原始值快照、脏列与行状态，供查询结果网格绑定（整数/列名索引）与增删改保存使用。
/// </summary>
public class QueryResultRow : System.ComponentModel.INotifyPropertyChanged
{
    private readonly IReadOnlyList<string> _columnNames;
    private readonly object?[] _values;
    private readonly object?[] _originalValues;
    private readonly HashSet<int> _dirtyIndexes = new();
    private DataRowState _state = DataRowState.Unchanged;

    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

    public QueryResultRow(IReadOnlyList<string> columnNames, IReadOnlyList<string?>? values = null)
    {
        _columnNames = columnNames;
        _values = new object?[columnNames.Count];
        _originalValues = new object?[columnNames.Count];

        if (values is not null)
        {
            for (int i = 0; i < _values.Length && i < values.Count; i++)
            {
                _values[i] = values[i];
            }
        }

        Array.Copy(_values, _originalValues, _values.Length);
    }

    /// <summary>行状态。</summary>
    public DataRowState State
    {
        get => _state;
        private set
        {
            if (_state == value) return;
            _state = value;
            OnPropertyChanged(nameof(State));
            OnPropertyChanged(nameof(IsDirty));
        }
    }

    /// <summary>是否存在未保存的改动。</summary>
    public bool IsDirty => State != DataRowState.Unchanged;

    /// <summary>脏列索引集合。</summary>
    public IReadOnlyCollection<int> DirtyIndexes => _dirtyIndexes;

    /// <summary>按列索引取值（返回字符串化值，供 DataGridTextColumn 直接显示）。</summary>
    public string? this[int index]
    {
        get => index >= 0 && index < _values.Length ? _values[index]?.ToString() ?? string.Empty : null;
        set => SetValue(index, value);
    }

    /// <summary>Values 包装（供 DataGrid 绑定 Values[i]，避免直接索引器路径解析在主题下的兼容性问题）。</summary>
    public IList<object?> Values => _valuesWrapper ??= new ValuesWrapper(this);

    private IList<object?>? _valuesWrapper;

    private sealed class ValuesWrapper : IList<object?>, System.ComponentModel.INotifyPropertyChanged
    {
        private readonly QueryResultRow _row;
        public ValuesWrapper(QueryResultRow row) => _row = row;
        public object? this[int index] { get => _row[index]; set => _row.SetValue(index, value); }
        public int Count => _row._values.Length;
        public bool IsReadOnly => false;
        public void Add(object? item) => throw new NotSupportedException();
        public void Clear() => throw new NotSupportedException();
        public bool Contains(object? item) => ((IList<object?>)_row._values).Contains(item);
        public void CopyTo(object?[] array, int arrayIndex) => _row._values.CopyTo(array, arrayIndex);
        public IEnumerator<object?> GetEnumerator() => ((IEnumerable<object?>)_row._values).GetEnumerator();
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
        public int IndexOf(object? item) => Array.IndexOf(_row._values, item);
        public void Insert(int index, object? item) => throw new NotSupportedException();
        public bool Remove(object? item) => throw new NotSupportedException();
        public void RemoveAt(int index) => throw new NotSupportedException();
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
        internal void Notify(int index)
        {
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs($"Item[{index}]"));
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs("Item[]"));
        }
    }

    /// <summary>按列名取值。</summary>
    public object? this[string columnName]
    {
        get
        {
            int idx = IndexOf(columnName);
            return idx >= 0 ? _values[idx] : null;
        }
        set
        {
            int idx = IndexOf(columnName);
            if (idx >= 0) SetValue(idx, value);
        }
    }

    /// <summary>设置某列的值并标记脏列（供编辑模式使用）。</summary>
    public void SetValue(int index, object? value)
    {
        if (index < 0 || index >= _values.Length) return;

        // 空字符串归一化为 NULL（与数据编辑器行为一致）。
        object? normalized = value is string s && s.Length == 0 ? null : value;

        if (Equals(_values[index], normalized)) return;

        _values[index] = normalized;

        if (!Equals(_originalValues[index], normalized))
        {
            _dirtyIndexes.Add(index);
        }
        else
        {
            _dirtyIndexes.Remove(index);
        }

        if (State == DataRowState.Unchanged && _dirtyIndexes.Count > 0)
        {
            State = DataRowState.Modified;
        }
        else if (State == DataRowState.Modified && _dirtyIndexes.Count == 0)
        {
            State = DataRowState.Unchanged;
        }

        OnPropertyChanged($"Item[{index}]");
        OnPropertyChanged($"Item[{_columnNames[index]}]");
        OnPropertyChanged($"[{_columnNames[index]}]");
        OnPropertyChanged($"[{index}]");
        OnPropertyChanged($"Values[{index}]");
        OnPropertyChanged(nameof(Values));
        OnPropertyChanged("Item[]");
        OnPropertyChanged(nameof(IsDirty));
        (_valuesWrapper as ValuesWrapper)?.Notify(index);
    }

    /// <summary>标记为新增行。</summary>
    public void MarkAsAdded() => State = DataRowState.Added;

    /// <summary>标记为已删除。</summary>
    public void MarkAsDeleted() => State = DataRowState.Deleted;

    /// <summary>提交成功后将当前值作为新的原始快照。</summary>
    public void MarkAsSaved()
    {
        Array.Copy(_values, _originalValues, _values.Length);
        _dirtyIndexes.Clear();
        State = DataRowState.Unchanged;
    }

    /// <summary>还原为原始值（撤销修改；新增/删除行由 ViewModel 层处理）。</summary>
    public void RevertToOriginal()
    {
        for (int i = 0; i < _values.Length; i++)
        {
            _values[i] = _originalValues[i];
            OnPropertyChanged($"Item[{i}]");
            OnPropertyChanged($"Item[{_columnNames[i]}]");
            OnPropertyChanged($"[{i}]");
            OnPropertyChanged($"[{_columnNames[i]}]");
            OnPropertyChanged($"Values[{i}]");
            (_valuesWrapper as ValuesWrapper)?.Notify(i);
        }
        OnPropertyChanged("Item[]");
        OnPropertyChanged(nameof(Values));
        _dirtyIndexes.Clear();
        State = DataRowState.Unchanged;
    }

    /// <summary>获取指定列的原始值。</summary>
    public object? GetOriginal(string columnName)
    {
        int idx = IndexOf(columnName);
        return idx >= 0 ? _originalValues[idx] : null;
    }

    /// <summary>获取指定列的当前值。</summary>
    public object? GetValue(string columnName)
    {
        int idx = IndexOf(columnName);
        return idx >= 0 ? _values[idx] : null;
    }

    private int IndexOf(string columnName)
    {
        for (int i = 0; i < _columnNames.Count; i++)
        {
            if (string.Equals(_columnNames[i], columnName, StringComparison.OrdinalIgnoreCase))
                return i;
        }
        return -1;
    }

    private void OnPropertyChanged(string name)
        => PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(name));
}
