using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DatabaseManager.AppCore.Common;
using DatabaseManager.AppCore.Models;
using DatabaseManager.AppCore.Services;

namespace DatabaseManager.AppCore.ViewModels;

/// <summary>
/// 查询标签页 ViewModel（对齐 DBeaver 多标签设计）。
/// 每个标签页拥有独立的 SQL 编辑器、执行状态和结果集。
/// 结果集为单表简单 SELECT 时支持内联编辑（增删改，经 IDataEditService 保存）。
/// </summary>
public partial class QueryTabViewModel : ViewModelBase
{
    private readonly IQueryService _queryService;
    private readonly IDataEditService? _editService;
    private readonly IQueryHistoryService? _historyService;
    private static int _tabCounter = 0;

    [ObservableProperty]
    private string _title = "查询";

    private string _sqlText = string.Empty;

    /// <summary>SQL 文本（带修改跟踪）。</summary>
    public string SqlText
    {
        get => _sqlText;
        set
        {
            if (SetProperty(ref _sqlText, value))
            {
                // 当用户修改 SQL 时标记为已修改
                if (!_isSaving)
                {
                    IsModified = true;
                    // 更新标题显示修改状态
                    UpdateTitleWithModifiedMark();
                }
            }
        }
    }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CancelExecutionCommand))]
    private bool _isExecuting;

    /// <summary>当前查询的命令超时秒数。默认一分钟，避免误执行后长时间无反馈。</summary>
    [ObservableProperty]
    private int _commandTimeoutSeconds = 60;

    /// <summary>是否对可能写入或变更结构的 SQL 显示确认。每个查询标签可独立配置。</summary>
    [ObservableProperty]
    private bool _dangerousSqlConfirmationEnabled = true;

    /// <summary>最近一次 SQL 错误所在行；无法从驱动消息解析时为空。</summary>
    [ObservableProperty]
    private int? _lastErrorLine;

    private CancellationTokenSource? _executionCts;

    /// <summary>由 UI 注入的危险 SQL 执行确认回调。</summary>
    public Func<string, Task<bool>>? RequestDangerousExecution { get; set; }

    [ObservableProperty]
    private string _statusMessage = "就绪";

    [ObservableProperty]
    private bool _hasResult;

    [ObservableProperty]
    private bool _showNoResult = true;

    [ObservableProperty]
    private string _connectionName = string.Empty;

    /// <summary>当前数据库名（内联编辑定位表时使用；由主窗口在执行前同步）。</summary>
    [ObservableProperty]
    private string _databaseName = string.Empty;

    [ObservableProperty]
    private bool _isModified;

    /// <summary>原始标题（不含修改标记）。</summary>
    private string _baseTitle = "查询";

    /// <summary>是否正在保存中（防止保存时触发修改标记）。</summary>
    private bool _isSaving;

    private readonly List<QueryResultRow> _allRows = new();

    // 视图层：筛选/排序后的可见行（未启用时与 _allRows 同序）。
    private readonly List<QueryResultRow> _viewRows = new();
    private readonly List<GridFilterCondition> _activeFilters = new();
    private int _sortColumnIndex = -1;
    private bool _sortDescending;
    private int _serverSortColumnIndex = -1;
    private bool _serverSortDescending;

    /// <summary>查询结果列名。</summary>
    public ObservableCollection<string> Columns { get; } = new();

    /// <summary>当前页的行数据（绑定到结果表格；全量数据在 _allRows 中分页切片）。</summary>
    public ObservableCollection<QueryResultRow> Rows { get; } = new();

    [ObservableProperty]
    private int _pageSize = 50;

    [ObservableProperty]
    private int _currentPage = 1;

    /// <summary>结果区是否已浮动到独立窗口（主窗口停靠的结果区随之隐藏）。</summary>
    [ObservableProperty]
    private bool _isResultFloating;

    #region 内联编辑状态

    /// <summary>结果集是否可内联编辑（单表简单 SELECT + 有主键 + 结果含全部主键列）。</summary>
    [ObservableProperty]
    private bool _isResultEditable;

    /// <summary>不可编辑的原因（可编辑时为空）。</summary>
    [ObservableProperty]
    private string _editReadOnlyReason = string.Empty;

    /// <summary>是否存在未保存的内联编辑改动。</summary>
    [ObservableProperty]
    private bool _hasPendingChanges;

    /// <summary>是否有未保存改动或正在保存（供保存按钮禁用态）。</summary>
    [ObservableProperty]
    private bool _isSavingChanges;

    /// <summary>保存成功后是否自动重新执行查询并定位记录（默认开启）。</summary>
    [ObservableProperty]
    private bool _autoRefreshAfterSave = true;

    /// <summary>可编辑目标表的元数据（可编辑时有值）。</summary>
    private DataTableInfo? _editableTableInfo;

    /// <summary>输出列名 → 源表列名（列别名映射；无别名时为空）。</summary>
    private IReadOnlyDictionary<string, string> _columnAliasByDisplay =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>源表列名 → 输出列名（反向映射，用于按表列名取行值）。</summary>
    private IReadOnlyDictionary<string, string> _displayBySource =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>待删除的行（从结果集中移除，保存时统一 DELETE；还原时按原位置放回）。</summary>
    private readonly List<(QueryResultRow Row, int OriginalIndex)> _pendingDeletes = new();

    /// <summary>目标表名 / Schema（可编辑时有值）。</summary>
    private string? _editableTableName;
    private string? _editableSchema;

    /// <summary>由 UI 注入：保存刷新后在网格中定位指定行（滚动 + 选中）。</summary>
    public Action<QueryResultRow>? RequestLocateRow { get; set; }

    #endregion

    /// <summary>每页大小可选项（供下拉选择）。</summary>
    public int[] PageSizeOptions { get; } = { 50, 100, 200, 500, 1000 };

    /// <summary>总行数。</summary>
    public int TotalRows => _allRows.Count;

    /// <summary>当前筛选/排序后可见的行数。</summary>
    public int VisibleRowCount => _viewRows.Count;

    /// <summary>全量行快照（供图表等消费方读取，不暴露内部集合引用）。</summary>
    public IReadOnlyList<QueryResultRow> GetAllRowsSnapshot() => _allRows.ToList();

    /// <summary>总行数（含待删除行）。</summary>
    public int TotalRowsIncludingDeleted => _allRows.Count + _pendingDeletes.Count;

    /// <summary>总页数（按可见行数计算）。</summary>
    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling(_viewRows.Count / (double)PageSize) : 0;

    /// <summary>分页信息文案（启用筛选时显示「显示 X / 共 Y 行」）。</summary>
    public string PageInfo =>
        _viewRows.Count == 0
            ? $"共 0 行{(_activeFilters.Count > 0 ? $"（筛选自 {_allRows.Count} 行）" : string.Empty)}"
            : HasActiveViewFilters
                ? $"第 {CurrentPage} / {Math.Max(1, TotalPages)} 页 · 显示 {_viewRows.Count} / 共 {_allRows.Count} 行"
                : $"第 {CurrentPage} / {Math.Max(1, TotalPages)} 页 · 共 {TotalRows} 行";

    /// <summary>筛选/排序摘要文案（如「筛选 1 项 · 排序 id ↓」）。</summary>
    public string FilterSummary
    {
        get
        {
            var parts = new List<string>();
            if (_activeFilters.Count > 0)
            {
                parts.Add($"筛选 {_activeFilters.Count} 项：{string.Join(" 且 ", _activeFilters)}");
            }

            if (_sortColumnIndex >= 0 && _sortColumnIndex < Columns.Count)
            {
                parts.Add($"排序 {Columns[_sortColumnIndex]} {(_sortDescending ? "↓" : "↑")}");
            }

            return string.Join(" · ", parts);
        }
    }

    /// <summary>是否可以翻到上一页。</summary>
    public bool CanGoPrevPage => CurrentPage > 1;

    /// <summary>是否可以翻到下一页。</summary>
    public bool CanGoNextPage => CurrentPage < TotalPages;

    /// <summary>此标签页的唯一 ID。</summary>
    public int TabId { get; }

    public QueryTabViewModel(IQueryService queryService, IDataEditService? editService = null, string? title = null, IQueryHistoryService? historyService = null)
    {
        _queryService = queryService;
        _editService = editService;
        _historyService = historyService;
        TabId = ++_tabCounter;
        _baseTitle = title ?? $"查询 {TabId}";
        Title = _baseTitle;
    }

    /// <summary>根据修改状态更新标题（添加 * 标记）。</summary>
    private void UpdateTitleWithModifiedMark()
    {
        Title = IsModified ? $"{_baseTitle} *" : _baseTitle;
    }

    /// <summary>标记为已保存（清除修改标记）。</summary>
    public void MarkAsSaved()
    {
        IsModified = false;
        UpdateTitleWithModifiedMark();
    }

    /// <summary>执行当前 SQL（若传入 sqlOverride 且非空则执行选中片段）。</summary>
    [RelayCommand]
    public async Task ExecuteAsync() => await ExecuteWithSqlAsync(null);

    public async Task ExecuteWithSqlAsync(string? sqlOverride)
    {
        if (IsExecuting)
            return;

        if (string.IsNullOrWhiteSpace(ConnectionName))
        {
            StatusMessage = "请先在对象浏览器中选择一个连接。";
            return;
        }

        var sqlToExecute = string.IsNullOrWhiteSpace(sqlOverride) ? SqlText : sqlOverride!;

        if (string.IsNullOrWhiteSpace(sqlToExecute))
        {
            StatusMessage = "请输入要执行的 SQL 语句。";
            return;
        }

        // 参数化执行：按参数面板的值替换 @name / :name 占位符。
        string? parameterizedSql = null;
        if (ParametersEnabled)
        {
            parameterizedSql = ApplyParameters(sqlToExecute);
        }

        var effectiveSql = parameterizedSql ?? sqlToExecute;

        if (DangerousSqlConfirmationEnabled
            && IsPotentiallyDestructiveSql(effectiveSql)
            && RequestDangerousExecution is not null
            && !await RequestDangerousExecution(effectiveSql))
        {
            StatusMessage = "已取消执行危险 SQL。";
            return;
        }

        bool isSelection = !string.IsNullOrWhiteSpace(sqlOverride);
        IsExecuting = true;
        StatusMessage = isSelection ? "正在执行选中 SQL..." : "正在执行...";
        _executionCts = new CancellationTokenSource();

        QueryResult? historyResult = null;
        string? historyError = null;

        try
        {
            var timeout = Math.Clamp(CommandTimeoutSeconds, 1, 3600);
            var result = await _queryService.ExecuteAsync(ConnectionName, effectiveSql, _executionCts.Token, timeout);
            historyResult = result;
            ApplyResult(result);

            // 执行成功且返回结果集时，尝试启用内联编辑。
            if (HasResult)
            {
                await TryEnableEditingAsync();
            }
            else
            {
                ResetEditingState();
            }
        }
        catch (Exception ex)
        {
            historyError = ex.Message;
            StatusMessage = $"执行失败：{ex.Message}";
            HasResult = false;
        }
        finally
        {
            RecordHistory(effectiveSql, historyResult, historyError);
            _executionCts?.Dispose();
            _executionCts = null;
            IsExecuting = false;
        }
    }

    /// <summary>把本次执行写入查询历史（服务缺失时忽略）。</summary>
    private void RecordHistory(string sql, QueryResult? result, string? error)
    {
        if (_historyService is null)
        {
            return;
        }

        try
        {
            _historyService.Add(new QueryHistoryEntry
            {
                ConnectionName = ConnectionName,
                Database = DatabaseName,
                SqlText = sql,
                IsSuccess = result is { IsSuccess: true },
                RowCount = result?.IsNonQuery == true ? result.RowCount : (result?.Rows.Count ?? 0),
                ElapsedMilliseconds = result?.ElapsedMilliseconds ?? 0,
                ErrorMessage = result?.IsSuccess == false ? result.ErrorMessage : error,
            });
        }
        catch
        {
            // 历史记录失败不影响查询流程。
        }
    }

    [RelayCommand(CanExecute = nameof(IsExecuting))]
    private void CancelExecution()
    {
        if (_executionCts is null || _executionCts.IsCancellationRequested)
            return;

        StatusMessage = "正在取消查询...";
        _executionCts.Cancel();
    }

    /// <summary>识别会写入或改变数据库结构的 SQL，以便在 UI 中请求二次确认。</summary>
    private static bool IsPotentiallyDestructiveSql(string sql)
    {
        var withoutComments = Regex.Replace(sql, @"/\*.*?\*/|--[^\r\n]*", string.Empty, RegexOptions.Singleline);
        return Regex.IsMatch(
            withoutComments,
            @"\b(INSERT|UPDATE|DELETE|MERGE|DROP|ALTER|CREATE|TRUNCATE|GRANT|REVOKE|EXEC(?:UTE)?)\b",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    #region 参数化执行

    /// <summary>是否启用参数化执行（启用后执行前按参数面板替换占位符）。</summary>
    [ObservableProperty]
    private bool _parametersEnabled;

    /// <summary>参数列表（名称 + 值；启用参数化执行时显示在编辑器下方）。</summary>
    public ObservableCollection<QueryParameterItem> Parameters { get; } = new();

    /// <summary>提取 SQL 中的参数名（@name / :name，跳过注释与字符串字面量内部）。</summary>
    internal static List<string> ExtractParameterNames(string sql)
    {
        var cleaned = StripCommentsAndLiterals(sql);
        var names = new List<string>();
        foreach (Match m in Regex.Matches(cleaned, @"[@:]([A-Za-z_][A-Za-z0-9_]*)"))
        {
            var name = m.Groups[1].Value;
            if (!names.Contains(name, StringComparer.OrdinalIgnoreCase))
            {
                names.Add(name);
            }
        }
        return names;
    }

    /// <summary>把 SQL 中的占位符替换为参数值（数值原样，其余按字符串转义；空值替换为 NULL）。替换式参数化，非驱动绑定。</summary>
    internal string ApplyParameters(string sql)
    {
        var names = ExtractParameterNames(sql);

        // 把 SQL 中出现但面板里没有的参数补进面板（默认空值 → NULL）。
        foreach (var name in names)
        {
            if (!Parameters.Any(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)))
            {
                Parameters.Add(new QueryParameterItem { Name = name });
            }
        }

        if (names.Count == 0)
        {
            return sql;
        }

        // 逐字符扫描，仅在非字面量区替换占位符，避免误替字符串内的 @/: 文本。
        var sb = new System.Text.StringBuilder(sql.Length * 2);
        bool inSingle = false;
        bool inDouble = false;
        bool inBracket = false;
        bool inBacktick = false;
        for (int i = 0; i < sql.Length; )
        {
            char c = sql[i];
            // 处理字符串字面量边界（含 '' 转义）
            if (!inDouble && !inBracket && !inBacktick && c == '\'')
            {
                if (inSingle)
                {
                    if (i + 1 < sql.Length && sql[i + 1] == '\'')
                    {
                        sb.Append("''");
                        i += 2;
                        continue;
                    }
                    inSingle = false;
                }
                else
                {
                    inSingle = true;
                }
                sb.Append(c);
                i++;
                continue;
            }
            if (!inSingle && !inBracket && !inBacktick && c == '"')
            {
                inDouble = !inDouble;
                sb.Append(c);
                i++;
                continue;
            }
            if (!inSingle && !inDouble && !inBacktick && c == '[')
            {
                inBracket = true;
                sb.Append(c);
                i++;
                continue;
            }
            if (inBracket && c == ']')
            {
                inBracket = false;
                sb.Append(c);
                i++;
                continue;
            }
            if (!inSingle && !inDouble && !inBracket && c == '`')
            {
                inBacktick = !inBacktick;
                sb.Append(c);
                i++;
                continue;
            }
            // 注释区：-- 行注释与 /* 块注释 */ 内的占位符不替换
            if (!inSingle && !inDouble && !inBracket && !inBacktick)
            {
                if (c == '-' && i + 1 < sql.Length && sql[i + 1] == '-')
                {
                    int start = i;
                    i += 2;
                    while (i < sql.Length && sql[i] != '\r' && sql[i] != '\n') i++;
                    sb.Append(sql, start, i - start);
                    continue;
                }
                if (c == '/' && i + 1 < sql.Length && sql[i + 1] == '*')
                {
                    int start = i;
                    i += 2;
                    while (i + 1 < sql.Length && !(sql[i] == '*' && sql[i + 1] == '/')) i++;
                    if (i + 1 < sql.Length) i += 2;
                    sb.Append(sql, start, i - start);
                    continue;
                }
                if ((c == '@' || c == ':') && i + 1 < sql.Length && IsParamStart(sql[i + 1]))
                {
                    int nameStart = i + 1;
                    int nameEnd = nameStart;
                    while (nameEnd < sql.Length && IsParamPart(sql[nameEnd])) nameEnd++;
                    var name = sql.Substring(nameStart, nameEnd - nameStart);
                    var param = Parameters.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
                    sb.Append(FormatParameterValue(param?.Value));
                    i = nameEnd;
                    continue;
                }
            }
            sb.Append(c);
            i++;
        }
        return sb.ToString();
    }

    private static bool IsParamStart(char c) => char.IsLetter(c) || c == '_';
    private static bool IsParamPart(char c) => char.IsLetterOrDigit(c) || c == '_';

    private static string StripCommentsAndLiterals(string sql)
    {
        // 将注释与字面量内容替换为空格，保留长度以便正则不受影响
        var sb = new System.Text.StringBuilder(sql.Length);
        bool inSingle = false;
        bool inDouble = false;
        bool inBracket = false;
        bool inBacktick = false;
        bool inLineComment = false;
        bool inBlockComment = false;
        for (int i = 0; i < sql.Length; i++)
        {
            char c = sql[i];
            char next = i + 1 < sql.Length ? sql[i + 1] : '\0';

            if (inLineComment)
            {
                if (c == '\n' || c == '\r') { inLineComment = false; sb.Append(c); }
                else sb.Append(' ');
                continue;
            }
            if (inBlockComment)
            {
                if (c == '*' && next == '/') { inBlockComment = false; sb.Append("  "); i++; }
                else if (c == '\n' || c == '\r') sb.Append(c);
                else sb.Append(' ');
                continue;
            }
            if (!inSingle && !inDouble && !inBracket && !inBacktick)
            {
                if (c == '-' && next == '-') { inLineComment = true; sb.Append("  "); i++; continue; }
                if (c == '/' && next == '*') { inBlockComment = true; sb.Append("  "); i++; continue; }
            }
            if (!inDouble && !inBracket && !inBacktick && c == '\'')
            {
                if (inSingle)
                {
                    if (next == '\'') { sb.Append("  "); i++; continue; }
                    inSingle = false;
                }
                else inSingle = true;
                sb.Append(' ');
                continue;
            }
            if (!inSingle && !inBracket && !inBacktick && c == '"') { inDouble = !inDouble; sb.Append(' '); continue; }
            if (!inSingle && !inDouble && !inBacktick && c == '[') { inBracket = true; sb.Append(' '); continue; }
            if (inBracket && c == ']') { inBracket = false; sb.Append(' '); continue; }
            if (!inSingle && !inDouble && !inBracket && c == '`') { inBacktick = !inBacktick; sb.Append(' '); continue; }

            if (inSingle || inDouble || inBracket || inBacktick) sb.Append(' ');
            else sb.Append(c);
        }
        return sb.ToString();
    }

    private static string FormatParameterValue(string? rawValue)
    {
        var value = rawValue?.Trim() ?? string.Empty;

        if (value.Length == 0 || string.Equals(value, "NULL", StringComparison.OrdinalIgnoreCase))
        {
            return "NULL";
        }

        // 数值/科学计数法直接使用；其余按字符串字面量转义（单引号加倍）。
        if (Regex.IsMatch(value, @"^-?\d+(\.\d+)?([eE][+-]?\d+)?$"))
        {
            return value;
        }

        return "'" + value.Replace("'", "''") + "'";
    }

    #endregion

    #region 结果导出

    /// <summary>
    /// 把当前结果集导出为文件（CSV / JSON；路径由主窗口的文件对话框提供）。
    /// </summary>
    public async Task ExportResultsAsync(string filePath, string format)
    {
        if (_allRows.Count == 0 || Columns.Count == 0)
        {
            StatusMessage = "没有可导出的结果集。";
            return;
        }

        try
        {
            await File.WriteAllTextAsync(filePath, BuildResultExportText(format));
            StatusMessage = $"结果已导出：{filePath}（{_allRows.Count} 行）";
        }
        catch (Exception ex)
        {
            StatusMessage = $"导出失败：{ex.Message}";
        }
    }

    internal string BuildResultExportText(string format)
    {
        var isJson = string.Equals(format, "JSON", StringComparison.OrdinalIgnoreCase);

        var sb = new System.Text.StringBuilder();

        if (isJson)
        {
            sb.Append('[');
        }
        else
        {
            sb.AppendLine(string.Join(",", Columns.Select(EscapeCsvValue)));
        }

        for (int rowIndex = 0; rowIndex < _allRows.Count; rowIndex++)
        {
            var row = _allRows[rowIndex];
            var values = new List<string>();
            for (int i = 0; i < Columns.Count; i++)
            {
                var raw = row[i];
                values.Add(isJson ? FormatJsonValue(raw) : EscapeCsvValue(raw));
            }

            if (isJson)
            {
                sb.Append(rowIndex > 0 ? "," : string.Empty);
                sb.Append("{\"");
                sb.Append(EscapeJsonString(Columns[0]));
                sb.Append("\":");
                sb.Append(values[0]);
                for (int i = 1; i < Columns.Count; i++)
                {
                    sb.Append(",\"");
                    sb.Append(EscapeJsonString(Columns[i]));
                    sb.Append("\":");
                    sb.Append(values[i]);
                }
                sb.Append('}');
            }
            else
            {
                sb.AppendLine(string.Join(",", values));
            }
        }

        if (isJson)
        {
            sb.Append(']');
        }

        return sb.ToString();
    }

    private static string EscapeCsvValue(object? value)
    {
        var text = value?.ToString() ?? string.Empty;
        if (text.Contains(',') || text.Contains('"') || text.Contains('\n') || text.Contains('\r'))
        {
            return $"\"{text.Replace("\"", "\"\"")}\"";
        }
        return text;
    }

    /// <summary>JSON 值格式化：null/数值/布尔 原样输出，其余按字符串转义。</summary>
    private static string FormatJsonValue(object? value)
    {
        if (value is null) return "null";
        var text = value.ToString() ?? string.Empty;
        if (text.Length == 0) return "null";
        if (string.Equals(text, "null", StringComparison.OrdinalIgnoreCase)) return "null";
        if (string.Equals(text, "true", StringComparison.OrdinalIgnoreCase) || string.Equals(text, "false", StringComparison.OrdinalIgnoreCase))
            return text.ToLowerInvariant();
        // 数值与科学计数法原样输出
        if (Regex.IsMatch(text, @"^-?\d+(\.\d+)?([eE][+-]?\d+)?$"))
            return text;
        return "\"" + EscapeJsonString(text) + "\"";
    }

    private static string EscapeJsonString(string text)
        => text.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n").Replace("\t", "\\t");

    private static string FormatJsonString(object? value)
    {
        var text = value?.ToString() ?? string.Empty;
        return "\"" + EscapeJsonString(text) + "\"";
    }

    #endregion

    private void ApplyResult(QueryResult result)
    {
        Columns.Clear();
        _allRows.Clear();

        // 新结果集重置视图层（筛选/排序针对的是上一次的结果集）。
        ResetView();

        if (!result.IsSuccess)
        {
            LastErrorLine = result.ErrorLine;
            var lineHint = result.ErrorLine is > 0 ? $"（第 {result.ErrorLine} 行）" : string.Empty;
            StatusMessage = $"执行失败{lineHint}：{result.ErrorMessage}";
            HasResult = false;
            ShowNoResult = true;
            RefreshPage();
            return;
        }

        LastErrorLine = null;

        if (result.IsNonQuery)
        {
            StatusMessage = $"命令已执行，影响 {result.RowCount} 行。";
            HasResult = false;
            ShowNoResult = true;
            RefreshPage();
            return;
        }

        foreach (var col in result.Columns)
        {
            Columns.Add(col);
        }

        foreach (var row in result.Rows)
        {
            _allRows.Add(new QueryResultRow(result.Columns, row));
        }

        StatusMessage = $"查询完成，返回 {result.RowCount} 行，耗时 {result.ElapsedMilliseconds} ms。";
        HasResult = true;
        ShowNoResult = false;

        // 新结果集回到第一页并切片显示
        CurrentPage = 1;
        RefreshPage();
    }

    #region 内联编辑

    /// <summary>执行成功后判定结果集能否内联编辑：解析 SQL 定位目标表并校验主键覆盖。</summary>
    private async Task TryEnableEditingAsync()
    {
        ResetEditingState();

        if (_editService is null || string.IsNullOrWhiteSpace(DatabaseName))
        {
            EditReadOnlyReason = "缺少数据库上下文，结果只读。";
            return;
        }

        var parse = SimpleSelectParser.Parse(SqlText);
        if (!parse.IsSimpleSelect)
        {
            EditReadOnlyReason = $"结果只读：{parse.NotEditableReason}";
            return;
        }

        // 列别名映射（SELECT a.col AS X）：输出列名 ↔ 表列名。
        _columnAliasByDisplay = parse.ColumnAliases;
        _displayBySource = parse.ColumnAliases
            .GroupBy(kv => kv.Value, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().Key, StringComparer.OrdinalIgnoreCase);

        var metadata = await _editService.GetTableMetadataAsync(
            ConnectionName, DatabaseName, parse.TableName!, parse.Schema);

        if (!metadata.IsSuccess)
        {
            EditReadOnlyReason = $"结果只读：无法读取表结构（{metadata.ErrorMessage}）。";
            return;
        }

        if (!metadata.HasPrimaryKey)
        {
            EditReadOnlyReason = "结果只读：目标表没有主键，无法安全定位行。";
            return;
        }

        // 校验：结果列（经别名解析后）必须包含目标表全部主键列。
        var missingPk = metadata.TableInfo.PrimaryKeyColumns
            .FirstOrDefault(pk => FindDisplayColumnIndex(pk) < 0);

        if (missingPk is not null)
        {
            EditReadOnlyReason = $"结果只读：SELECT 未包含主键列 {missingPk}。";
            return;
        }

        _editableTableInfo = metadata.TableInfo;
        _editableTableName = parse.TableName;
        _editableSchema = parse.Schema;
        IsResultEditable = true;
        string tableLabel = string.IsNullOrEmpty(_editableSchema) ? _editableTableName! : $"{_editableSchema}.{_editableTableName}";
        StatusMessage = $"查询完成，返回 {_allRows.Count} 行（可编辑：{tableLabel}）。";
    }

    private void ResetEditingState()
    {
        IsResultEditable = false;
        HasPendingChanges = false;
        IsSavingChanges = false;
        EditReadOnlyReason = string.Empty;
        _pendingDeletes.Clear();
        _editableTableInfo = null;
        _editableTableName = null;
        _editableSchema = null;
        _columnAliasByDisplay = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        _displayBySource = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>把输出列名解析为源表列名（列别名场景）。</summary>
    private string? ResolveTableColumnName(string displayColumn)
        => _columnAliasByDisplay is { Count: > 0 } && _columnAliasByDisplay.TryGetValue(displayColumn, out var source)
            ? source
            : displayColumn;

    /// <summary>把源表列名解析回结果集中的输出列名（无别名时按同名）。</summary>
    private string DisplayNameFor(string tableColumn)
        => _displayBySource is { Count: > 0 } && _displayBySource.TryGetValue(tableColumn, out var display)
            ? display
            : tableColumn;

    /// <summary>查找源表列名在结果集中的输出列索引。</summary>
    private int FindDisplayColumnIndex(string tableColumn)
    {
        var display = DisplayNameFor(tableColumn);
        for (int i = 0; i < Columns.Count; i++)
        {
            if (string.Equals(Columns[i], display, StringComparison.OrdinalIgnoreCase))
                return i;
        }
        return -1;
    }

    /// <summary>判断结果集中第 index 列（0 基）是否允许编辑。</summary>
    public bool IsColumnEditable(int columnIndex)
    {
        if (!IsResultEditable || _editableTableInfo is null)
            return false;
        if (columnIndex < 0 || columnIndex >= Columns.Count)
            return false;

        var resolved = ResolveTableColumnName(Columns[columnIndex]);
        var col = resolved is null ? null : FindTableColumn(resolved);
        return col is not null && !col.IsReadOnly;
    }

    /// <summary>判断结果集中第 index 列（0 基）是否为主键列（供列头标识）。</summary>
    public bool IsPrimaryKeyColumn(int columnIndex)
    {
        if (!IsResultEditable || _editableTableInfo is null)
            return false;
        if (columnIndex < 0 || columnIndex >= Columns.Count)
            return false;

        var resolved = ResolveTableColumnName(Columns[columnIndex]);
        return resolved is not null
            && _editableTableInfo.PrimaryKeyColumns.Any(pk => string.Equals(pk, resolved, StringComparison.OrdinalIgnoreCase));
    }

    private DataColumnInfo? FindTableColumn(string name)
    {
        foreach (var c in _editableTableInfo!.Columns)
        {
            if (string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase))
                return c;
        }
        return null;
    }

    /// <summary>新增一行（插入当前页末尾并通知视图滚动定位）。</summary>
    public void AddRowForEdit(out QueryResultRow? newRow)
    {
        newRow = null;
        if (!IsResultEditable)
            return;

        var row = new QueryResultRow(Columns.ToList());
        row.MarkAsAdded();

        // 插入到当前页末尾对应的全量位置（保证刷新后仍在本页可见）。
        int insertIndex = Math.Min((CurrentPage - 1) * PageSize + Rows.Count, _allRows.Count);
        insertIndex = Math.Clamp(insertIndex, 0, _allRows.Count);
        _allRows.Insert(insertIndex, row);

        HasPendingChanges = true;
        RefreshPage();

        // 筛选/排序生效时新增行可能落在其它页：按视图位置跳页，保证可见可编辑。
        int viewIndex = _viewRows.IndexOf(row);
        if (viewIndex >= 0 && PageSize > 0)
        {
            int targetPage = viewIndex / PageSize + 1;
            if (targetPage != CurrentPage)
            {
                GoToPage(targetPage);
            }
        }

        newRow = row;
    }

    /// <summary>删除指定行（新增行直接丢弃；已有行移入待删除列表）。</summary>
    public void RemoveRowForEdit(QueryResultRow? row)
    {
        if (!IsResultEditable || row is null)
            return;

        if (row.State == DataRowState.Added)
        {
            _allRows.Remove(row);
        }
        else
        {
            int idx = _allRows.IndexOf(row);
            if (idx >= 0)
            {
                _pendingDeletes.Add((row, idx));
                _allRows.Remove(row);
            }
        }

        RecalculatePendingChanges();
        RefreshPage();
    }

    /// <summary>还原全部内联编辑改动（恢复原始值/放回删除行/丢弃新增行）。</summary>
    public void RevertEdits()
    {
        // 1. 移除所有新增的行（完全还原）
        var addedRows = _allRows.Where(r => r.State == DataRowState.Added).ToList();
        foreach (var row in addedRows)
        {
            _allRows.Remove(row);
        }

        // 2. 放回已删除的行
        foreach (var (row, originalIndex) in _pendingDeletes.AsEnumerable().Reverse())
        {
            var restoreIndex = Math.Clamp(originalIndex, 0, _allRows.Count);
            _allRows.Insert(restoreIndex, row);
        }
        _pendingDeletes.Clear();

        // 3. 还原所有剩余行的原始值
        foreach (var row in _allRows)
        {
            row.RevertToOriginal();
        }

        RecalculatePendingChanges();
        RefreshPage();
        StatusMessage = "已还原全部改动。";
    }

    /// <summary>保存内联编辑改动（复用 IDataEditService.SaveChangesAsync，事务内提交）。</summary>
    [RelayCommand]
    public async Task SaveEditsAsync()
    {
        if (_editService is null || !IsResultEditable || !HasPendingChanges || IsSavingChanges)
            return;

        IsSavingChanges = true;
        try
        {
            var inserts = new List<DataEditRow>();
            var updates = new List<DataEditRow>();
            var deletes = new List<DataEditRow>();

            var tableColumns = _editableTableInfo!.Columns;

            // 新增行：取全部非只读列的当前值生成 INSERT。
            foreach (var r in _allRows.Where(r => r.State == DataRowState.Added))
            {
                var dataRow = new DataEditRow(tableColumns);
                foreach (var col in tableColumns.Where(c => !c.IsReadOnly))
                {
                    var value = r.GetValue(DisplayNameFor(col.Name));
                    if (value is not null)
                    {
                        dataRow[col.Name] = value;
                    }
                }
                inserts.Add(dataRow);
            }

            // 修改行：先写入全部原始值作为 WHERE 基线并快照，再应用新值产生脏标记。
            // （两段写入，避免逐列快照把已写的新值误当作原始值。）
            foreach (var r in _allRows.Where(r => r.State == DataRowState.Modified))
            {
                var dataRow = new DataEditRow(tableColumns);

                for (int i = 0; i < tableColumns.Count; i++)
                {
                    var original = Normalize(r.GetOriginal(DisplayNameFor(tableColumns[i].Name)));
                    dataRow.SetCellValueDirect(i, original);
                }
                dataRow.MarkAsSaved();

                foreach (var col in tableColumns)
                {
                    var current = r.GetValue(DisplayNameFor(col.Name));
                    var original = r.GetOriginal(DisplayNameFor(col.Name));
                    if (!Equals(Normalize(original), Normalize(current)))
                    {
                        dataRow[col.Name] = current;
                    }
                }
                updates.Add(dataRow);
            }

            // 删除行：以原始值构造 WHERE 条件。
            foreach (var (r, _) in _pendingDeletes)
            {
                var dataRow = new DataEditRow(tableColumns);
                for (int i = 0; i < tableColumns.Count; i++)
                {
                    var original = Normalize(r.GetOriginal(DisplayNameFor(tableColumns[i].Name)));
                    if (original is not null)
                    {
                        dataRow.SetCellValueDirect(i, original);
                    }
                }
                dataRow.MarkAsSaved();
                dataRow.MarkAsDeleted();
                deletes.Add(dataRow);
            }

            if (inserts.Count == 0 && updates.Count == 0 && deletes.Count == 0)
            {
                StatusMessage = "没有需要保存的改动。";
                return;
            }

            StatusMessage = "正在保存改动...";
            var result = await _editService.SaveChangesAsync(
                ConnectionName,
                DatabaseName,
                _editableTableName!,
                _editableSchema,
                inserts,
                updates,
                deletes);

            if (!result.IsSuccess)
            {
                StatusMessage = $"保存失败：{result.ErrorMessage}";
                return;
            }

            // 必须在 MarkAsSaved 前收集主键；否则新增/修改状态被清除后无法定位。
            var savedKeys = AutoRefreshAfterSave
                ? CollectSavedPrimaryKeyValues()
                : new List<Dictionary<string, object?>>();

            // 保存成功：提交所有行状态，清空待删除列表。
            foreach (var (row, _) in _pendingDeletes)
            {
                row.MarkAsSaved();
            }
            _pendingDeletes.Clear();
            foreach (var row in _allRows)
            {
                row.MarkAsSaved();
            }

            RecalculatePendingChanges();
            RefreshPage();

            // 保存后自动重新执行查询并定位保存过的记录（自增列/默认值等数据库生成值会刷新）。
            if (savedKeys.Count > 0)
            {
                await RefreshAndLocateAsync(savedKeys);
            }
            else
            {
                StatusMessage = $"保存成功，影响 {result.RowCount} 行。建议重新执行查询以获取最新数据（自增列等）。";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"保存失败：{ex.Message}";
        }
        finally
        {
            IsSavingChanges = false;
        }
    }

    /// <summary>收集保存过（新增/修改）的行在保存后的主键值（自增主键保存前无值，无法定位则跳过）。</summary>
    private List<Dictionary<string, object?>> CollectSavedPrimaryKeyValues()
    {
        var keys = new List<Dictionary<string, object?>>();

        if (_editableTableInfo is null || _editableTableInfo.PrimaryKeyColumns.Count == 0)
        {
            return keys;
        }

        foreach (var row in _allRows.Where(r => r.State == DataRowState.Added || r.State == DataRowState.Modified))
        {
            var key = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            bool complete = true;

            foreach (var pk in _editableTableInfo.PrimaryKeyColumns)
            {
                int index = FindDisplayColumnIndex(pk);
                var value = index < 0 ? null : row[index];

                if (value is null || (value is string s && s.Length == 0))
                {
                    complete = false;
                    break;
                }

                key[pk] = value;
            }

            if (complete && key.Count > 0)
            {
                keys.Add(key);
            }
        }

        return keys;
    }

    /// <summary>重新执行当前查询，并按保存行的主键值在结果中定位（跨页跳转 + 通知 UI 滚动选中）。</summary>
    private async Task RefreshAndLocateAsync(List<Dictionary<string, object?>> keys)
    {
        await ExecuteWithSqlAsync(null);

        if (!HasResult)
        {
            StatusMessage = $"保存成功，但刷新结果集失败：{StatusMessage}";
            return;
        }

        QueryResultRow? target = null;

        foreach (var key in keys)
        {
            target = _allRows.FirstOrDefault(row => key.All(kv =>
            {
                int index = FindDisplayColumnIndex(kv.Key);
                return index >= 0 && string.Equals(row[index]?.ToString(), kv.Value?.ToString(), StringComparison.Ordinal);
            }));

            if (target is not null)
            {
                break;
            }
        }

        if (target is not null)
        {
            // 定位按可见行（视图）计算；目标被筛选隐藏时只刷新不跳页。
            int rowIndex = _viewRows.IndexOf(target);
            if (rowIndex >= 0)
            {
                int page = PageSize > 0 ? rowIndex / PageSize + 1 : 1;
                GoToPage(page);
                RequestLocateRow?.Invoke(target);
                StatusMessage = $"保存成功，已刷新并定位到目标记录。";
            }
            else
            {
                StatusMessage = "保存成功，已刷新结果集（目标记录被当前筛选隐藏）。";
            }
        }
        else
        {
            StatusMessage = $"保存成功，已自动刷新结果集（新增的自增主键行已按新数据展示）。";
        }
    }

    private void RecalculatePendingChanges()
    {
        HasPendingChanges = _pendingDeletes.Count > 0
            || _allRows.Any(r => r.State == DataRowState.Added || r.State == DataRowState.Modified || r.State == DataRowState.Deleted);
        OnPropertyChanged(nameof(PendingChangeSummary));
    }

    /// <summary>未保存改动摘要（如「新增 1 · 修改 2 · 删除 1」）。</summary>
    public string PendingChangeSummary
    {
        get
        {
            int added = _allRows.Count(r => r.State == DataRowState.Added);
            int modified = _allRows.Count(r => r.State == DataRowState.Modified);
            int deleted = _pendingDeletes.Count;

            var parts = new List<string>();
            if (added > 0) parts.Add($"新增 {added}");
            if (modified > 0) parts.Add($"修改 {modified}");
            if (deleted > 0) parts.Add($"删除 {deleted}");
            return string.Join(" · ", parts);
        }
    }

    /// <summary>查询结果的显示值（字符串化）转回存储值：空字符串视为 NULL。</summary>
    internal static object? Normalize(object? value)
        => value is string s && s.Length == 0 ? null : value;

    #endregion

    #region 结果视图（筛选与排序）

    /// <summary>筛选/排序可用的运算符。</summary>
    public static readonly string[] FilterOperators =
    {
        "包含", "等于", "不等于", "开头为", "结尾为", ">", ">=", "<", "<=", "为空", "非空",
    };

    /// <summary>是否存在生效的视图筛选。</summary>
    public bool HasActiveViewFilters => _activeFilters.Count > 0;

    /// <summary>应用一条筛选条件（作用于当前结果集的内存行）。</summary>
    public void ApplyViewFilter(int columnIndex, string op, string value)
    {
        if (columnIndex < 0 || columnIndex >= Columns.Count || string.IsNullOrWhiteSpace(op))
            return;

        // 同列重复应用时替换旧条件（一次一条，避免组合复杂度）。
        _activeFilters.RemoveAll(f => f.ColumnIndex == columnIndex);
        _activeFilters.Add(new GridFilterCondition(columnIndex, Columns[columnIndex], op, value));
        OnPropertyChanged(nameof(HasActiveViewFilters));
        OnPropertyChanged(nameof(FilterSummary));

        CurrentPage = 1;
        RefreshPage();
    }

    /// <summary>清除全部筛选（保留排序）。</summary>
    public void ClearViewFilters()
    {
        if (_activeFilters.Count == 0)
            return;

        _activeFilters.Clear();
        OnPropertyChanged(nameof(HasActiveViewFilters));
        OnPropertyChanged(nameof(FilterSummary));
        CurrentPage = 1;
        RefreshPage();
    }

    /// <summary>设置排序（descending：true=降序 false=升序 null=清除排序）。</summary>
    public void SetViewSort(int columnIndex, bool? descending)
    {
        if (descending is null)
        {
            if (_sortColumnIndex == -1)
                return;
            _sortColumnIndex = -1;
        }
        else
        {
            if (columnIndex < 0 || columnIndex >= Columns.Count)
                return;
            _sortColumnIndex = columnIndex;
            _sortDescending = descending.Value;
        }

        OnPropertyChanged(nameof(FilterSummary));
        RefreshPage();
    }

    /// <summary>按结果列序号重新执行当前 SELECT，实现跨方言的服务端排序。</summary>
    public async Task SortByServerAsync(int columnIndex)
    {
        if (columnIndex < 0 || columnIndex >= Columns.Count || IsExecuting)
            return;

        if (HasPendingChanges)
        {
            StatusMessage = "请先保存或还原内联编辑，再执行服务端排序。";
            return;
        }

        string sortedSql;
        try
        {
            _serverSortDescending = _serverSortColumnIndex == columnIndex && !_serverSortDescending;
            _serverSortColumnIndex = columnIndex;
            sortedSql = SqlQueryTransform.AppendOrdinalOrderBy(SqlText, columnIndex + 1, _serverSortDescending);
        }
        catch (ArgumentException ex)
        {
            StatusMessage = ex.Message;
            return;
        }

        // ORDER BY 列序号是 MySQL/PostgreSQL/SQL Server/Oracle/SQLite 共同支持的语法，避免将显示列名直接拼入 SQL。
        await ExecuteWithSqlAsync(sortedSql);
    }

    /// <summary>重置视图层（新结果集时调用）。</summary>
    private void ResetView()
    {
        _activeFilters.Clear();
        _sortColumnIndex = -1;
        _sortDescending = false;
        _viewRows.Clear();
        OnPropertyChanged(nameof(HasActiveViewFilters));
        OnPropertyChanged(nameof(FilterSummary));
    }

    /// <summary>重建视图行：过滤（新增行始终可见）+ 排序。</summary>
    private void RebuildViewRows()
    {
        IEnumerable<QueryResultRow> rows = _allRows;

        if (_activeFilters.Count > 0)
        {
            // 新增行（尚未保存）始终显示，避免填写中被筛选吞掉。
            rows = rows.Where(r => r.State == DataRowState.Added || MatchesAllFilters(r));
        }

        if (_sortColumnIndex >= 0 && _sortColumnIndex < Columns.Count)
        {
            int idx = _sortColumnIndex;
            var sorted = rows.OrderBy(r => (object?)r[idx], Comparer<object?>.Create(CompareSortValues)).ToList();
            if (_sortDescending)
            {
                sorted.Reverse();
            }

            rows = sorted;
        }

        _viewRows.Clear();
        _viewRows.AddRange(rows);
    }

    private bool MatchesAllFilters(QueryResultRow row)
        => _activeFilters.All(f => MatchesFilter(row, f));

    private static bool MatchesFilter(QueryResultRow row, GridFilterCondition f)
    {
        if (f.ColumnIndex < 0 || row.Values.Count <= f.ColumnIndex)
            return true;

        var cell = row[f.ColumnIndex] ?? string.Empty;
        var value = f.Value ?? string.Empty;

        switch (f.Operator)
        {
            case "包含": return cell.Contains(value, StringComparison.OrdinalIgnoreCase);
            case "等于": return string.Equals(cell, value, StringComparison.OrdinalIgnoreCase);
            case "不等于": return !string.Equals(cell, value, StringComparison.OrdinalIgnoreCase);
            case "开头为": return cell.StartsWith(value, StringComparison.OrdinalIgnoreCase);
            case "结尾为": return cell.EndsWith(value, StringComparison.OrdinalIgnoreCase);
            case "为空": return string.IsNullOrEmpty(cell);
            case "非空": return !string.IsNullOrEmpty(cell);
            case ">":
            case ">=":
            case "<":
            case "<=":
                if (double.TryParse(cell, out double a) && double.TryParse(value, out double b))
                {
                    return f.Operator switch
                    {
                        ">" => a > b,
                        ">=" => a >= b,
                        "<" => a < b,
                        _ => a <= b,
                    };
                }

                int ordinal = string.CompareOrdinal(cell, value);
                return f.Operator switch
                {
                    ">" => ordinal > 0,
                    ">=" => ordinal >= 0,
                    "<" => ordinal < 0,
                    _ => ordinal <= 0,
                };
            default:
                return true;
        }
    }

    /// <summary>排序比较：数值优先（两侧都可解析为数值按数值比较），否则按字符串序。</summary>
    private static int CompareSortValues(object? x, object? y)
    {
        string xs = x?.ToString() ?? string.Empty;
        string ys = y?.ToString() ?? string.Empty;

        bool xNum = double.TryParse(xs, out double xn);
        bool yNum = double.TryParse(ys, out double yn);

        if (xNum && yNum)
            return xn.CompareTo(yn);
        if (xNum)
            return -1; // 数值排在字符串前
        if (yNum)
            return 1;

        return string.CompareOrdinal(xs, ys);
    }

    #endregion

    /// <summary>按当前页码刷新 Rows（可见行切片）与分页状态。</summary>
    private void RefreshPage()
    {
        RebuildViewRows();

        var maxPage = Math.Max(1, TotalPages);
        var page = Math.Clamp(CurrentPage, 1, maxPage);
        if (page != CurrentPage)
        {
            CurrentPage = page; // 触发 OnCurrentPageChanged 再次进入（幂等）
            return;
        }

        Rows.Clear();
        if (_viewRows.Count > 0)
        {
            var start = (page - 1) * PageSize;
            foreach (var row in _viewRows.Skip(start).Take(PageSize))
            {
                Rows.Add(row);
            }
        }

        OnPropertyChanged(nameof(TotalRows));
        OnPropertyChanged(nameof(VisibleRowCount));
        OnPropertyChanged(nameof(TotalPages));
        OnPropertyChanged(nameof(PageInfo));
        OnPropertyChanged(nameof(CanGoPrevPage));
        OnPropertyChanged(nameof(CanGoNextPage));
        PrevPageCommand.NotifyCanExecuteChanged();
        NextPageCommand.NotifyCanExecuteChanged();
        FirstPageCommand.NotifyCanExecuteChanged();
        LastPageCommand.NotifyCanExecuteChanged();
    }

    partial void OnPageSizeChanged(int value) => RefreshPage();

    partial void OnCurrentPageChanged(int value) => RefreshPage();

    [RelayCommand(CanExecute = nameof(CanGoPrevPage))]
    private void FirstPage() => GoToPage(1);

    [RelayCommand(CanExecute = nameof(CanGoPrevPage))]
    private void PrevPage() => GoToPage(CurrentPage - 1);

    [RelayCommand(CanExecute = nameof(CanGoNextPage))]
    private void NextPage() => GoToPage(CurrentPage + 1);

    [RelayCommand(CanExecute = nameof(CanGoNextPage))]
    private void LastPage() => GoToPage(TotalPages);

    /// <summary>跳转到指定页（自动夹取到有效范围）。</summary>
    public void GoToPage(int page)
    {
        var target = Math.Clamp(page, 1, Math.Max(1, TotalPages));
        CurrentPage = target;
        RefreshPage();
    }

    /// <summary>清除结果集。</summary>
    public void ClearResults()
    {
        Columns.Clear();
        _allRows.Clear();
        ResetView();
        Rows.Clear();
        ResetEditingState();
        CurrentPage = 1;
        RefreshPage();
        HasResult = false;
        ShowNoResult = true;
    }
}

/// <summary>参数化执行的参数项（名称 + 值）。</summary>
public partial class QueryParameterItem : ViewModelBase
{
    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _value = string.Empty;
}

/// <summary>结果网格视图筛选条件（列 + 运算符 + 值，作用于当前结果集的内存行）。</summary>
public sealed class GridFilterCondition
{
    public int ColumnIndex { get; }

    public string ColumnName { get; }

    public string Operator { get; }

    public string Value { get; }

    public GridFilterCondition(int columnIndex, string columnName, string op, string value)
    {
        ColumnIndex = columnIndex;
        ColumnName = columnName;
        Operator = op;
        Value = value;
    }

    public override string ToString()
        => Operator is "为空" or "非空" ? $"{ColumnName} {Operator}" : $"{ColumnName} {Operator} \"{Value}\"";
}
