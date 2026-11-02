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

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanChangeTransaction))]
    [NotifyCanExecuteChangedFor(nameof(BeginTransactionCommand), nameof(CommitTransactionCommand), nameof(RollbackTransactionCommand))]
    private bool _isTransactionBusy;

    public bool CanChangeTransaction => !IsTransactionBusy && !string.IsNullOrWhiteSpace(ConnectionName);

    public QueryEditorViewModel(IQueryService queryService)
    {
        _queryService = queryService;
    }

    /// <summary>当前执行的连接名。</summary>
    public string ConnectionName { get; set; } = string.Empty;
    public string DatabaseName { get; set; } = string.Empty;

    partial void OnAutoCommitChanged(bool value)
        => _ = SetAutoCommitAsync(value);

    public async Task SetAutoCommitAsync(bool enabled)
    {
        if (!CanChangeTransaction)
        {
            OnConnectionChanged();
            return;
        }
        var connectionName = ConnectionName;
        IsTransactionBusy = true;
        StatusMessage = enabled ? "正在切换为自动提交..." : "正在切换为手动事务...";
        try
        {
            bool ok = await _queryService.SetAutoCommitAsync(connectionName, enabled);
            if (ConnectionName == connectionName)
                StatusMessage = ok ? enabled ? "已切换为自动提交模式。" : "已切换为手动事务模式（执行后请手动提交/回滚）。"
                    : "切换提交模式失败，请检查连接和事务状态。";
        }
        catch (Exception ex)
        {
            if (ConnectionName == connectionName) StatusMessage = $"切换提交模式失败：{ex.Message}";
        }
        finally
        {
            IsTransactionBusy = false;
            OnConnectionChanged();
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
        var connectionName = ConnectionName;
        var sql = SqlText;

        try
        {
            var result = await _queryService.ExecuteAsync(connectionName, sql, databaseName: DatabaseName);
            if (ConnectionName != connectionName) return;

            ApplyResult(result);

            // 执行后同步事务状态（手动模式下开启事务、或执行后未自动提交）。
            IsTransactionActive = _queryService.IsTransactionActive(ConnectionName);
        }
        catch (Exception ex)
        {
            if (ConnectionName == connectionName)
            {
                StatusMessage = $"执行失败：{ex.Message}";
                HasResult = false;
            }
        }
        finally
        {
            IsExecuting = false;
        }
    }

    /// <summary>开启事务（进入手动事务模式）。</summary>
    [RelayCommand(CanExecute = nameof(CanChangeTransaction))]
    public async Task BeginTransactionAsync()
    {
        OnConnectionChanged();
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

        var connectionName = ConnectionName;
        var databaseName = DatabaseName;
        await RunTransactionAsync(connectionName,
            () => _queryService.BeginTransactionAsync(connectionName, databaseName: databaseName),
            $"事务已开启（{databaseName}）。执行 SQL 后请手动提交或回滚。", "事务开启失败。");
    }

    /// <summary>提交当前事务。</summary>
    [RelayCommand(CanExecute = nameof(CanChangeTransaction))]
    public async Task CommitTransactionAsync()
    {
        OnConnectionChanged();
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

        var connectionName = ConnectionName;
        await RunTransactionAsync(connectionName, () => _queryService.CommitAsync(connectionName), "事务已提交。", "事务提交失败。");
    }

    /// <summary>回滚当前事务。</summary>
    [RelayCommand(CanExecute = nameof(CanChangeTransaction))]
    public async Task RollbackTransactionAsync()
    {
        OnConnectionChanged();
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

        var connectionName = ConnectionName;
        await RunTransactionAsync(connectionName, () => _queryService.RollbackAsync(connectionName), "事务已回滚。", "事务回滚失败。");
    }

    private async Task RunTransactionAsync(string connectionName, Func<Task<bool>> operation, string success, string failure)
    {
        if (!CanChangeTransaction) return;
        IsTransactionBusy = true;
        StatusMessage = "正在处理事务...";
        try
        {
            bool ok = await operation();
            if (ConnectionName == connectionName) StatusMessage = ok ? success : failure;
        }
        catch (Exception ex)
        {
            if (ConnectionName == connectionName) StatusMessage = $"{failure} {ex.Message}";
        }
        finally
        {
            IsTransactionBusy = false;
            OnConnectionChanged();
        }
    }

    /// <summary>连接切换/断开时重置事务状态。</summary>
    public void OnConnectionChanged()
    {
        OnPropertyChanged(nameof(CanChangeTransaction));
        BeginTransactionCommand.NotifyCanExecuteChanged();
        CommitTransactionCommand.NotifyCanExecuteChanged();
        RollbackTransactionCommand.NotifyCanExecuteChanged();
        IsTransactionActive = _queryService.IsTransactionActive(ConnectionName);
        // 刷新显示不能触发 OnAutoCommitChanged，否则切换标签可能提交事务。
#pragma warning disable MVVMTK0034
        SetProperty(ref _autoCommit, _queryService.IsAutoCommit(ConnectionName), nameof(AutoCommit));
#pragma warning restore MVVMTK0034
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
