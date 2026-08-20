using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DatabaseManager.AppCore.Common;
using DatabaseManager.AppCore.Models;
using DatabaseManager.AppCore.Services;

namespace DatabaseManager.AppCore.ViewModels;

/// <summary>
/// 查询编辑器 ViewModel（AppCore 层）。
/// 阶段 2/3：管理 SQL 输入、执行查询、结果展示，以及事务核心（Auto-commit / Commit / Rollback）。
/// </summary>
public partial class QueryEditorViewModel : ViewModelBase
{
    private readonly IQueryService _queryService;

    /// <summary>查询结果列名。</summary>
    public ObservableCollection<string> Columns { get; } = new();

    /// <summary>查询结果行数据。</summary>
    public ObservableCollection<RowData> Rows { get; } = new();

    [ObservableProperty]
    private string _sqlText = string.Empty;

    [ObservableProperty]
    private bool _isExecuting;

    [ObservableProperty]
    private string _statusMessage = "就绪";

    [ObservableProperty]
    private bool _hasResult;

    /// <summary>是否显示“无结果”占位提示（与 <see cref="HasResult"/> 相反）。</summary>
    [ObservableProperty]
    private bool _showNoResult = true;

    /// <summary>是否自动提交（true=每条 SQL 自动提交；false=手动事务）。</summary>
    [ObservableProperty]
    private bool _autoCommit = true;

    /// <summary>当前是否处于活动事务中。</summary>
    [ObservableProperty]
    private bool _isTransactionActive;

    public QueryEditorViewModel(IQueryService queryService)
    {
        _queryService = queryService;
    }

    /// <summary>当前执行的连接名。</summary>
    public string ConnectionName { get; set; } = string.Empty;

    partial void OnAutoCommitChanged(bool value)
    {
        if (string.IsNullOrWhiteSpace(ConnectionName))
            return;

        _queryService.SetAutoCommit(ConnectionName, value);

        if (value)
        {
            // 切换回自动提交时刷新事务状态（可能已提交）。
            IsTransactionActive = _queryService.IsTransactionActive(ConnectionName);
            StatusMessage = "已切换为自动提交模式。";
        }
        else
        {
            StatusMessage = "已切换为手动事务模式（执行后将由你手动提交/回滚）。";
        }
    }

    /// <summary>执行当前 SQL。</summary>
    [RelayCommand]
    public async Task ExecuteAsync()
    {
        if (IsExecuting)
            return;

        if (string.IsNullOrWhiteSpace(ConnectionName))
        {
            StatusMessage = "请先在对象浏览器中选择一个连接。";
            return;
        }

        if (string.IsNullOrWhiteSpace(SqlText))
        {
            StatusMessage = "请输入要执行的 SQL 语句。";
            return;
        }

        IsExecuting = true;
        StatusMessage = "正在执行...";

        try
        {
            // 手动事务模式下且尚未开启事务：自动开启事务（对齐 dbeaver 手动提交行为）。
            if (!AutoCommit && !_queryService.IsTransactionActive(ConnectionName))
            {
                bool began = await _queryService.BeginTransactionAsync(ConnectionName);
                if (!began)
                {
                    StatusMessage = "无法自动开启事务，请检查连接。";
                    return;
                }
                IsTransactionActive = true;
            }

            var result = await _queryService.ExecuteAsync(ConnectionName, SqlText);

            ApplyResult(result);

            // 执行后同步事务状态（手动模式下开启事务、或执行后未自动提交）。
            IsTransactionActive = _queryService.IsTransactionActive(ConnectionName);
        }
        catch (Exception ex)
        {
            StatusMessage = $"执行失败：{ex.Message}";
            HasResult = false;
        }
        finally
        {
            IsExecuting = false;
        }
    }

    /// <summary>开启事务（进入手动事务模式）。</summary>
    [RelayCommand]
    public async Task BeginTransactionAsync()
    {
        if (string.IsNullOrWhiteSpace(ConnectionName))
        {
            StatusMessage = "请先选择一个连接。";
            return;
        }

        if (IsTransactionActive)
        {
            StatusMessage = "已处于事务中。";
            return;
        }

        bool ok = await _queryService.BeginTransactionAsync(ConnectionName);
        if (ok)
        {
            AutoCommit = false;
            IsTransactionActive = true;
            StatusMessage = "事务已开启。执行 SQL 后请手动提交或回滚。";
        }
        else
        {
            StatusMessage = "事务开启失败。";
        }
    }

    /// <summary>提交当前事务。</summary>
    [RelayCommand]
    public async Task CommitTransactionAsync()
    {
        if (string.IsNullOrWhiteSpace(ConnectionName))
        {
            StatusMessage = "请先选择一个连接。";
            return;
        }

        if (!IsTransactionActive)
        {
            StatusMessage = "当前没有可提交的事务。";
            return;
        }

        bool ok = await _queryService.CommitAsync(ConnectionName);
        IsTransactionActive = _queryService.IsTransactionActive(ConnectionName);
        StatusMessage = ok ? "事务已提交。" : "事务提交失败。";
    }

    /// <summary>回滚当前事务。</summary>
    [RelayCommand]
    public async Task RollbackTransactionAsync()
    {
        if (string.IsNullOrWhiteSpace(ConnectionName))
        {
            StatusMessage = "请先选择一个连接。";
            return;
        }

        if (!IsTransactionActive)
        {
            StatusMessage = "当前没有可回滚的事务。";
            return;
        }

        bool ok = await _queryService.RollbackAsync(ConnectionName);
        IsTransactionActive = _queryService.IsTransactionActive(ConnectionName);
        StatusMessage = ok ? "事务已回滚。" : "事务回滚失败。";
    }

    /// <summary>连接切换/断开时重置事务状态。</summary>
    public void OnConnectionChanged()
    {
        IsTransactionActive = _queryService.IsTransactionActive(ConnectionName);
        AutoCommit = !IsTransactionActive;
    }

    private void ApplyResult(QueryResult result)
    {
        Columns.Clear();
        Rows.Clear();

        if (!result.IsSuccess)
        {
            StatusMessage = $"执行失败：{result.ErrorMessage}";
            HasResult = false;
            ShowNoResult = true;
            return;
        }

        if (result.IsNonQuery)
        {
            StatusMessage = $"命令已执行，影响 {result.RowCount} 行。";
            HasResult = false;
            ShowNoResult = true;
            return;
        }

        foreach (var col in result.Columns)
        {
            Columns.Add(col);
        }

        foreach (var row in result.Rows)
        {
            Rows.Add(new RowData(row));
        }

        StatusMessage = $"查询完成，返回 {result.RowCount} 行，耗时 {result.ElapsedMilliseconds} ms。";
        HasResult = true;
        ShowNoResult = false;
    }
}

/// <summary>查询结果行数据封装（用于 Avalonia DataGrid 绑定）。</summary>
public class RowData
{
    private readonly IReadOnlyList<string> _values;

    public RowData(IReadOnlyList<string> values)
    {
        _values = values;
    }

    /// <summary>按索引取值（供动态列绑定）。</summary>
    public string this[int index] => index >= 0 && index < _values.Count ? _values[index] : string.Empty;

    public int Count => _values.Count;
}
