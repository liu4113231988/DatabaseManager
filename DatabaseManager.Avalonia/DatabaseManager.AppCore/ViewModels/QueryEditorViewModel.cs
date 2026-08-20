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
/// 阶段 2：管理 SQL 输入、执行查询、展示结果集与执行状态。
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

    public QueryEditorViewModel(IQueryService queryService)
    {
        _queryService = queryService;
    }

    /// <summary>当前执行的连接名。</summary>
    public string ConnectionName { get; set; } = string.Empty;

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

        if (string.IsNullOrWhiteSpace(_sqlText))
        {
            StatusMessage = "请输入要执行的 SQL 语句。";
            return;
        }

        IsExecuting = true;
        StatusMessage = "正在执行...";

        try
        {
            var result = await _queryService.ExecuteAsync(ConnectionName, _sqlText);

            ApplyResult(result);
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
