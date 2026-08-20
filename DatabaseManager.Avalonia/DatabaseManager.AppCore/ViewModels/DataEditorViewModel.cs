using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DatabaseManager.AppCore.Common;
using DatabaseManager.AppCore.Models;
using DatabaseManager.AppCore.Services;

namespace DatabaseManager.AppCore.ViewModels;

/// <summary>
/// 数据编辑器 ViewModel（AppCore 层）。
/// 负责表数据的加载（分页）、可编辑行集合管理、增删改（Add/Remove）、保存（Commit）与还原（Revert）。
/// </summary>
public partial class DataEditorViewModel : ViewModelBase
{
    private readonly IDataEditService _editService;

    /// <summary>当前编辑表的列定义。</summary>
    public ObservableCollection<DataColumnInfo> Columns { get; } = new();

    /// <summary>可编辑数据行。</summary>
    public ObservableCollection<DataEditRow> Rows { get; } = new();

    /// <summary>已标记删除的行（保存时统一删除，展示时从网格移除）。</summary>
    private readonly List<DataEditRow> _deletedRows = new();

    /// <summary>当前连接名。</summary>
    private string _connectionName = string.Empty;

    /// <summary>当前是否加载了数据（可编辑状态）。</summary>
    [ObservableProperty]
    private bool _isLoaded;

    /// <summary>当前是否正忙（加载/保存中）。</summary>
    [ObservableProperty]
    private bool _isBusy;

    /// <summary>状态消息。</summary>
    [ObservableProperty]
    private string _statusMessage = string.Empty;

    /// <summary>当前表名。</summary>
    [ObservableProperty]
    private string _tableName = string.Empty;

    /// <summary>当前数据库名。</summary>
    [ObservableProperty]
    private string _databaseName = string.Empty;

    /// <summary>当前 Schema（可为空）。</summary>
    [ObservableProperty]
    private string _schema = string.Empty;

    /// <summary>是否为视图（只读提示）。</summary>
    [ObservableProperty]
    private bool _isView;

    /// <summary>每页行数。</summary>
    [ObservableProperty]
    private int _pageSize = 100;

    /// <summary>当前页码。</summary>
    [ObservableProperty]
    private long _pageNumber = 1;

    /// <summary>总行数。</summary>
    [ObservableProperty]
    private long _totalCount;

    /// <summary>是否有未保存的改动。</summary>
    [ObservableProperty]
    private bool _hasChanges;

    /// <summary>总页数。</summary>
    public long PageCount => PageSize <= 0 ? 1 : (TotalCount + PageSize - 1) / PageSize;

    /// <summary>是否可新增/编辑（表可编辑；视图仅查看）。</summary>
    public bool IsEditable => !IsView;

    public DataEditorViewModel(IDataEditService editService)
    {
        _editService = editService;
    }

    /// <summary>加载指定表的数据。返回是否成功。</summary>
    public async Task<bool> LoadAsync(
        string connectionName,
        string databaseName,
        string tableName,
        string? schema,
        bool isView)
    {
        _connectionName = connectionName;
        DatabaseName = databaseName;
        TableName = tableName;
        Schema = schema ?? string.Empty;
        IsView = isView;
        PageNumber = 1;
        _deletedRows.Clear();

        return await LoadCurrentPageAsync();
    }

    /// <summary>加载指定页的数据。返回是否成功。</summary>
    [RelayCommand]
    private async Task LoadPageAsync()
    {
        await LoadCurrentPageAsync();
    }

    /// <summary>上一页。</summary>
    [RelayCommand]
    private async Task PreviousPageAsync()
    {
        if (PageNumber <= 1) return;
        PageNumber--;
        await LoadCurrentPageAsync();
    }

    /// <summary>下一页。</summary>
    [RelayCommand]
    private async Task NextPageAsync()
    {
        if (PageNumber >= PageCount) return;
        PageNumber++;
        await LoadCurrentPageAsync();
    }

    private async Task<bool> LoadCurrentPageAsync()
    {
        if (string.IsNullOrWhiteSpace(_connectionName) || string.IsNullOrWhiteSpace(TableName))
            return false;

        IsBusy = true;
        StatusMessage = $"正在加载 {TableName} 数据...";

        try
        {
            var result = await _editService.LoadDataAsync(
                _connectionName,
                DatabaseName,
                TableName,
                string.IsNullOrEmpty(Schema) ? null : Schema,
                IsView,
                PageSize,
                PageNumber);

            if (!result.IsSuccess)
            {
                StatusMessage = $"加载失败：{result.ErrorMessage}";
                return false;
            }

            Columns.Clear();
            foreach (var col in result.TableInfo.Columns)
            {
                Columns.Add(col);
            }

            Rows.Clear();
            foreach (var row in result.Rows)
            {
                Rows.Add(row);
                SubscribeRow(row);
            }

            TotalCount = result.TotalCount;
            IsLoaded = true;
            HasChanges = false;
            _deletedRows.Clear();
            StatusMessage = $"已加载 {Rows.Count} / {TotalCount} 行，共 {PageCount} 页。";
            return true;
        }
        catch (Exception ex)
        {
            StatusMessage = $"加载失败：{ex.Message}";
            return false;
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>新增一行。</summary>
    [RelayCommand]
    private void AddRow()
    {
        if (!IsEditable) return;

        var row = new DataEditRow(Columns.ToList());
        row.MarkAsAdded();
        Rows.Add(row);
        SubscribeRow(row);
        HasChanges = true;
        StatusMessage = "已新增一行，填写后点击「保存」。";
    }

    /// <summary>删除指定行（标记为已删除并从网格移除，保存时统一删除）。</summary>
    [RelayCommand]
    private void RemoveRow(DataEditRow? row)
    {
        if (!IsEditable || row is null) return;

        if (row.State == DataRowState.Added)
        {
            // 新增行直接丢弃，不产生 DELETE。
            Rows.Remove(row);
        }
        else
        {
            row.MarkAsDeleted();
            Rows.Remove(row);
            _deletedRows.Add(row);
        }

        HasChanges = true;
        StatusMessage = "已删除所选行，点击「保存」生效。";
    }

    /// <summary>还原全部改动。</summary>
    [RelayCommand]
    private async Task RevertAsync()
    {
        if (!HasChanges && _deletedRows.Count == 0)
        {
            StatusMessage = "当前没有需要还原的改动。";
            return;
        }

        _deletedRows.Clear();
        await LoadCurrentPageAsync();
        StatusMessage = "已还原改动。";
    }

    /// <summary>保存全部改动。</summary>
    [RelayCommand]
    private async Task SaveAsync()
    {
        if (!HasChanges && _deletedRows.Count == 0)
        {
            StatusMessage = "当前没有需要保存的改动。";
            return;
        }

        IsBusy = true;
        StatusMessage = "正在保存改动...";

        try
        {
            var inserts = Rows.Where(r => r.State == DataRowState.Added).ToList();
            var updates = Rows.Where(r => r.State == DataRowState.Modified).ToList();
            var deletes = _deletedRows.Where(r => r.State == DataRowState.Deleted).ToList();

            var result = await _editService.SaveChangesAsync(
                _connectionName,
                DatabaseName,
                TableName,
                string.IsNullOrEmpty(Schema) ? null : Schema,
                inserts,
                updates,
                deletes);

            if (!result.IsSuccess)
            {
                StatusMessage = $"保存失败：{result.ErrorMessage}";
                return;
            }

            // 保存成功后刷新最新数据（提交后重载当前页，保证主键/自增值正确）。
            await LoadPageAsync();

            StatusMessage = $"保存成功，影响 {result.RowCount} 行。";
        }
        catch (Exception ex)
        {
            StatusMessage = $"保存失败：{ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>清除当前编辑状态。</summary>
    public void Clear()
    {
        foreach (var row in Rows)
        {
            row.PropertyChanged -= Row_PropertyChanged;
        }

        Columns.Clear();
        Rows.Clear();
        _deletedRows.Clear();
        IsLoaded = false;
        HasChanges = false;
        TotalCount = 0;
        PageNumber = 1;
        TableName = string.Empty;
        StatusMessage = string.Empty;
    }

    /// <summary>订阅行属性变化，以实时刷新 HasChanges。</summary>
    private void SubscribeRow(DataEditRow row)
    {
        row.PropertyChanged -= Row_PropertyChanged;
        row.PropertyChanged += Row_PropertyChanged;
    }

    private void Row_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(DataEditRow.IsDirty))
        {
            HasChanges = Rows.Any(r => r.IsDirty) || _deletedRows.Count > 0;
        }
    }
}
