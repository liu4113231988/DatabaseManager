using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;
using AvaloniaEdit;
using AvaloniaEdit.CodeCompletion;
using AvaloniaEdit.Document;
using AvaloniaEdit.Editing;
using AvaloniaEdit.Highlighting;
using AvaloniaEdit.Highlighting.Xshd;
using AvaloniaEdit.Search;
using DatabaseInterpreter.Model;
using DatabaseManager.AppCore.Models;
using DatabaseManager.AppCore.Services;

namespace DatabaseManager.Avalonia.Controls;

/// <summary>
/// SQL 编辑器用户控件：基于 AvaloniaEdit，SQL 语法高亮 + 行号 + 关键字补全。
/// 修复：初始化时机改为附加到可视树时加载，避免构造时 FindControl 为 null 导致高亮/补全失效；
/// 高亮加载双路径（EmbeddedResource + Avalonia AssetLoader）容错；补全触发排除点号并支持更新已打开窗口。
/// </summary>
public partial class SqlEditor : UserControl
{
    public static readonly StyledProperty<string> SqlTextProperty =
        AvaloniaProperty.Register<SqlEditor, string>(
            nameof(SqlText),
            defaultValue: string.Empty);

    /// <summary>SQL 文本内容（与内部编辑器文档双向同步）。</summary>
    public string SqlText
    {
        get => GetValue(SqlTextProperty) as string ?? string.Empty;
        set => SetValue(SqlTextProperty, value);
    }

    /// <summary>对象浏览器根节点；补全时直接读取已加载的表、视图和列。</summary>
    public static readonly StyledProperty<IEnumerable<DbObjectTreeNode>?> ObjectTreeRootsProperty =
        AvaloniaProperty.Register<SqlEditor, IEnumerable<DbObjectTreeNode>?>(nameof(ObjectTreeRoots));

    public IEnumerable<DbObjectTreeNode>? ObjectTreeRoots
    {
        get => GetValue(ObjectTreeRootsProperty);
        set => SetValue(ObjectTreeRootsProperty, value);
    }

    /// <summary>当前查询所属连接，用于从多个连接的对象树中筛选补全候选。</summary>
    public static readonly StyledProperty<string> ConnectionNameProperty =
        AvaloniaProperty.Register<SqlEditor, string>(nameof(ConnectionName), string.Empty);

    public string ConnectionName
    {
        get => GetValue(ConnectionNameProperty);
        set => SetValue(ConnectionNameProperty, value);
    }

    private TextEditor? _editor;
    private SearchPanel? _searchPanel;
    private bool _syncing;
    private bool _initialized;
    private CompletionWindow? _completionWindow;
    private readonly Dictionary<string, IReadOnlyList<string>> _columnCompletionCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _loadingColumnCompletions = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _dbObjectNames = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _dbColumnNames = new(StringComparer.OrdinalIgnoreCase);
    private static readonly string[] ReservedKeywords = new[]
    {
        // 关键字（与 Sql.xshd Keywords/Datatypes/Functions 取并集）
        "SELECT","FROM","WHERE","AND","OR","NOT","IN","IS","LIKE","BETWEEN","INTO","VALUES",
        "INSERT","UPDATE","DELETE","SET","CREATE","ALTER","DROP","TABLE","VIEW","INDEX","PROCEDURE",
        "FUNCTION","TRIGGER","SCHEMA","DATABASE","IF","EXISTS","USE","GO","BEGIN","END","DECLARE",
        "EXEC","EXECUTE","RETURN","WHILE","BREAK","CONTINUE","COMMIT","ROLLBACK","SAVEPOINT",
        "TRANSACTION","WORK","GRANT","REVOKE","WITH","RECURSIVE","UNION","ALL","DISTINCT","INTERSECT",
        "EXCEPT","JOIN","INNER","OUTER","FULL","CROSS","ON","USING","GROUP","BY","HAVING","ORDER",
        "ASC","DESC","LIMIT","OFFSET","FETCH","FIRST","NEXT","ONLY","ROWS","RANGE","UNBOUNDED",
        "PRECEDING","FOLLOWING","CURRENT","ROW","PARTITION","OVER","WINDOW","CASE","WHEN","THEN",
        "ELSE","PRIMARY","FOREIGN","KEY","REFERENCES","CONSTRAINT","UNIQUE","CHECK","DEFAULT",
        "AUTO_INCREMENT","AUTOINCREMENT","IDENTITY","COMMENT","COLUMN","ADD","MODIFY","CHANGE",
        "RENAME","TO","CASCADE","RESTRICT","TRUNCATE","MERGE","MATCHED","SOURCE","TARGET","OUTPUT",
        "LOCK","SHARE","MODE","EXPLAIN","ANALYZE","VACUUM","SHOW","DESCRIBE","PRAGMA","ATTACH",
        "DETACH","RETURNING","TOP","PERCENT","TIES","COLLATE","CAST","CONVERT","CHARSET","ENGINE",
        "UNSIGNED","SIGNED","ZEROFILL","LOCAL","TEMP","TEMPORARY","GLOBAL","SESSION","ISOLATION",
        "LEVEL","READ","WRITE","SNAPSHOT","SERIALIZABLE","REPEATABLE","COMMITTED","UNCOMMITTED",
        "ACTION","NOWAIT","WAIT","STORED","GENERATED","VIRTUAL","ALWAYS","START","CACHE","INCREMENT",
        "MINVALUE","MAXVALUE","CYCLE","OWNED","SEQUENCE","PIVOT","UNPIVOT","APPLY","BULK","FOR",
        "EACH","NULL","AS",
        // 常用数据类型
        "INT","INTEGER","SMALLINT","BIGINT","TINYINT","MEDIUMINT","DECIMAL","NUMERIC","DEC",
        "FLOAT","REAL","DOUBLE","PRECISION","BIT","BOOLEAN","BOOL","CHAR","CHARACTER","VARCHAR",
        "VARCHAR2","NCHAR","NVARCHAR","TEXT","NTEXT","TINYTEXT","MEDIUMTEXT","LONGTEXT","CLOB",
        "NCLOB","BLOB","TINYBLOB","MEDIUMBLOB","LONGBLOB","BINARY","VARBINARY","IMAGE","RAW",
        "ROWID","BFILE","DATE","TIME","DATETIME","DATETIME2","SMALLDATETIME","TIMESTAMP",
        "INTERVAL","YEAR","ENUM","SET","JSON","JSONB","XML","UUID","MONEY","SMALLMONEY",
        "SERIAL","BIGSERIAL","UNIQUEIDENTIFIER","SQL_VARIANT","HIERARCHYID","GEOMETRY",
        "GEOGRAPHY","NUMBER",
    };

    private static IHighlightingDefinition? _cachedHighlighting;

    /// <summary>
    /// 当前进程内已初始化的所有 SqlEditor 实例列表（弱引用），供主窗口在对象树展开后群发刷新通知。
    /// 主窗口的 NotifyAllSqlEditorsObjectTreeChanged 会枚举这个列表。
    /// </summary>
    private static readonly List<WeakReference<SqlEditor>> _liveInstances = new();

    public static IReadOnlyList<SqlEditor> LiveInstances
    {
        get
        {
            // 清理已被 GC 的弱引用并返回强引用快照
            var alive = new List<SqlEditor>(_liveInstances.Count);
            var dead = new List<int>();
            for (int i = 0; i < _liveInstances.Count; i++)
            {
                if (_liveInstances[i].TryGetTarget(out var editor))
                    alive.Add(editor);
                else
                    dead.Add(i);
            }
            for (int i = dead.Count - 1; i >= 0; i--)
                _liveInstances.RemoveAt(dead[i]);
            return alive;
        }
    }

    public SqlEditor()
    {
        InitializeComponent();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        EnsureInitialized();
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        EnsureInitialized();
    }

    private void EnsureInitialized()
    {
        if (_initialized) return;
        _editor = this.FindControl<TextEditor>("PART_TextEditor");
        if (_editor is null) return;
        _initialized = true;

        // 浅色主题细节：光标与选区颜色固定，避免任何主题态下不可见
        _editor.TextArea.Caret.CaretBrush = Brushes.Black;
        _editor.TextArea.SelectionBrush = new SolidColorBrush(Color.FromRgb(0xAD, 0xD6, 0xFF));
        _editor.TextArea.SelectionForeground = null;
        _editor.Options.EnableHyperlinks = false;
        _editor.Options.HighlightCurrentLine = false;

        // 加载内置 SQL 高亮定义（失败时自动降级为纯文本）
        var highlighting = LoadSqlHighlighting();
        if (highlighting != null)
        {
            _editor.SyntaxHighlighting = highlighting;
        }

        // 注入动态着色器：把已加载的"数据库对象"用另一种颜色区分于关键字。
        // 每次 TextChanged 都会触发 Colorize，扫描开销取决于标识符数量，可接受。
        _editor.TextArea.TextView.LineTransformers.Add(new DbObjectColorizingTransformer(
            objectNamesProvider: () => _dbObjectNames.Count == 0 ? null : _dbObjectNames,
            columnNamesProvider: () => _dbColumnNames.Count == 0 ? null : _dbColumnNames,
            reservedKeywords: ReservedKeywords));

        // 同步初始文本（可能在初始化前已通过属性设置）
        if (!string.IsNullOrEmpty(SqlText) && _editor.Document.Text != SqlText)
        {
            _editor.Document.Text = SqlText;
        }

        // 编辑器文本变化 → 写回 SqlText 属性（经 TwoWay 绑定同步到 ViewModel）
        _editor.Document.TextChanged += (_, _) =>
        {
            if (_syncing)
                return;
            SetCurrentValue(SqlTextProperty, _editor.Document.Text);
        };

        _editor.TextArea.TextEntered += OnTextEntered;
        _editor.TextArea.TextEntering += OnTextEntering;
        _editor.TextArea.KeyDown += OnKeyDown;
        _editor.ContextRequested += OnEditorContextRequested;

        // 安装 AvaloniaEdit 自带的查找/替换面板（Ctrl+F 打开、Esc 关闭）。
        _searchPanel = SearchPanel.Install(_editor);

        // 初始化时若对象树已绑定，立刻构建一次对象名缓存，避免首次显示无高亮。
        RefreshDbObjectCache();

        // 注册到活动实例列表（弱引用，自动 GC 失效的实例）
        lock (_liveInstances) { _liveInstances.Add(new WeakReference<SqlEditor>(this)); }
    }

    private void OnTextEntering(object? sender, TextInputEventArgs e)
    {
        // 输入非触发字符时，若补全窗口已打开且当前词不再匹配，则保持窗口由内部过滤处理
        // 此处不做关闭，避免频繁闪烁
    }

    private void OnTextEntered(object? sender, TextInputEventArgs e)
    {
        if (e.Text is null || e.Text.Length == 0)
            return;
        char c = e.Text[0];
        // 标识符触发关键字/对象补全；点号触发表字段补全，不会展示全量关键字。
        if (char.IsLetterOrDigit(c) || c == '_' || c == '.')
        {
            ShowCompletion(autoTriggered: true);
        }
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        // 查找/替换面板打开时是 TextArea 的可视子元素，其搜索框的按键同样会冒泡到这里；
        // 面板自己的 Enter / Esc / F3 由 AvaloniaEdit 处理，其余按键不应触发编辑器命令。
        if (IsFromSearchPanel(e))
        {
            return;
        }

        if (e.Key == Key.Space && e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            ShowCompletion(autoTriggered: false);
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Escape && _completionWindow is not null)
        {
            _completionWindow.Close();
            return;
        }

        if (TryHandleEditShortcut(e))
        {
            e.Handled = true;
        }
    }

    /// <summary>
    /// 编辑器快捷键（对齐 SSMS / DBeaver / DataGrip 的常用键位）。
    /// 返回 true 表示已消费该按键；Ctrl+F 等由 SearchPanel 自身处理，此处不再接管。
    /// </summary>
    private bool TryHandleEditShortcut(KeyEventArgs e)
    {
        bool ctrl = e.KeyModifiers.HasFlag(KeyModifiers.Control);
        bool shift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
        bool alt = e.KeyModifiers.HasFlag(KeyModifiers.Alt);

        switch (e.Key)
        {
            case Key.Enter when ctrl && !shift:  // Ctrl+Enter：执行选中 SQL（DBeaver）
                RaiseCommand(SqlEditorCommandKind.Execute);
                return true;
            case Key.Enter when ctrl && shift:   // Ctrl+Shift+Enter：在新标签执行选中 SQL
                RaiseCommand(SqlEditorCommandKind.ExecuteInNewTab);
                return true;
            case Key.L when ctrl && !shift:      // Ctrl+L：显示执行计划（SSMS）
                RaiseCommand(SqlEditorCommandKind.Explain);
                return true;
            case Key.L when ctrl && shift:       // Ctrl+Shift+L：转小写（SSMS）
                ChangeSelectionCase(upper: false);
                return true;
            case Key.S when ctrl && shift:       // Ctrl+Shift+S：保存到脚本库
                RaiseCommand(SqlEditorCommandKind.SaveToLibrary);
                return true;
            case Key.F when ctrl && shift:       // Ctrl+Shift+F：美化 SQL（选区优先）
                Format();
                return true;
            case Key.R when ctrl && !shift:      // Ctrl+R：打开查找/替换面板（展开替换行）
                OpenSearchPanel(replaceMode: true);
                return true;
            case Key.C when ctrl && shift:       // Ctrl+Shift+C：注释 / 取消注释行
                ToggleLineComment();
                return true;
            case Key.U when ctrl && shift:       // Ctrl+Shift+U：转大写（SSMS）
                ChangeSelectionCase(upper: true);
                return true;
            case Key.D when ctrl && !shift:      // Ctrl+D：复制当前行 / 选区（DataGrip）
                DuplicateLineOrSelection();
                return true;
            case Key.K when ctrl && shift:       // Ctrl+Shift+K：删除行（VS Code）
                DeleteLines();
                return true;
            case Key.Up when alt:                // Alt+↑：行上移
                MoveSelectedLines(up: true);
                return true;
            case Key.Down when alt:              // Alt+↓：行下移
                MoveSelectedLines(up: false);
                return true;
        }

        return false;
    }

    private void ShowCompletion(bool autoTriggered)
    {
        if (_editor is null) return;

        // 若已有窗口，先关闭以便用最新前缀重新过滤
        if (_completionWindow != null)
        {
            // 对于自动触发，若窗口已存在则让其内部过滤更新即可，无需重建；
            // 但为确保前缀更新，关闭后重建更可靠
            if (autoTriggered)
            {
                // 让 AvaloniaEdit 内部过滤处理，不重建，避免闪烁
                return;
            }
            _completionWindow.Close();
            _completionWindow = null;
        }

        var word = GetCurrentWord();
        // 自动触发时，空词通常不弹出；但 `table.` 后需要展示该表的字段。
        if (autoTriggered && string.IsNullOrEmpty(word) && !IsAfterDot())
            return;

        // `table.` 后优先提示列；未展开对象时会异步读取并缓存列信息。
        IEnumerable<string> candidates = IsAfterDot()
            ? GetColumnCandidatesAfterDot()
            : GetSqlKeywords().Concat(GetDatabaseObjectCandidates());

        // 自动触发：前缀过滤；手动触发：空词时展示全量（取 80），有前缀时同样过滤
        if (!string.IsNullOrEmpty(word))
        {
            candidates = candidates.Where(k => k.StartsWith(word, StringComparison.OrdinalIgnoreCase));
        }

        var filtered = candidates.Take(80).ToList();
        if (filtered.Count == 0) return;

        _completionWindow = new CompletionWindow(_editor.TextArea);
        _completionWindow.Closed += (_, _) => _completionWindow = null;

        var data = _completionWindow.CompletionList.CompletionData;
        foreach (var kw in filtered)
        {
            data.Add(new SqlCompletionData(kw));
        }

        // 对于自动触发，若仅有一个候选且与当前词完全相等（大小写不敏感），不弹出
        if (autoTriggered && data.Count == 1 && string.Equals(data[0].Text, word, StringComparison.OrdinalIgnoreCase))
            return;

        _completionWindow.Show();
        // 手动触发时选中首项
        if (!autoTriggered && _completionWindow.CompletionList.ListBox.ItemCount > 0)
        {
            _completionWindow.CompletionList.SelectedItem = _completionWindow.CompletionList.CompletionData.FirstOrDefault();
        }
    }

    private bool IsAfterDot()
    {
        if (_editor is null) return false;
        int offset = _editor.CaretOffset;
        if (offset == 0) return false;
        var doc = _editor.Document;
        // 光标可能位于 `table.` 后，也可能已输入字段前缀（`table.col`）。
        // 因此先跳过当前标识符，再检查其前面是否为点号。
        int pos = offset;
        while (pos > 0 && IsIdentifierCharacter(doc.GetCharAt(pos - 1)))
            pos--;
        return pos > 0 && doc.GetCharAt(pos - 1) == '.';
    }

    private string GetCurrentWord()
    {
        if (_editor is null) return string.Empty;
        int offset = _editor.CaretOffset;
        if (offset == 0) return string.Empty;
        var doc = _editor.Document;
        int start = offset;
        while (start > 0 && IsIdentifierCharacter(doc.GetCharAt(start - 1)))
            start--;
        return doc.GetText(start, offset - start);
    }

    private IEnumerable<string> GetDatabaseObjectCandidates()
    {
        if (ObjectTreeRoots is null || string.IsNullOrWhiteSpace(ConnectionName))
            return Enumerable.Empty<string>();

        var connection = ObjectTreeRoots.FirstOrDefault(node =>
            node.NodeType == DbObjectTreeNodeType.Connection &&
            string.Equals(node.Name, ConnectionName, StringComparison.OrdinalIgnoreCase));
        if (connection is null)
            return Enumerable.Empty<string>();

        return Descendants(connection)
            .Where(node => node.NodeType == DbObjectTreeNodeType.DbObject && !node.IsPlaceholder)
            .Where(node => node.DatabaseObjectType is DatabaseInterpreter.Model.DatabaseObjectType.Table
                or DatabaseInterpreter.Model.DatabaseObjectType.View
                or DatabaseInterpreter.Model.DatabaseObjectType.Procedure
                or DatabaseInterpreter.Model.DatabaseObjectType.Function
                or DatabaseInterpreter.Model.DatabaseObjectType.Sequence)
            .Select(node => node.Name)
            .Where(name => !string.IsNullOrWhiteSpace(name));
    }

    private IEnumerable<string> GetColumnCandidatesAfterDot()
    {
        var table = FindTableForCurrentDot();
        if (table is null)
            return Enumerable.Empty<string>();

        string cacheKey = GetColumnCacheKey(table);
        if (_columnCompletionCache.TryGetValue(cacheKey, out var cachedColumns))
            return cachedColumns;

        // 已展开 Columns 文件夹时直接复用对象树；未展开时异步读取并缓存，不要求用户展开树。
        var columnsFolder = table.Children.FirstOrDefault(node =>
            node.NodeType == DbObjectTreeNodeType.ChildFolder &&
            string.Equals(node.Name, "Columns", StringComparison.OrdinalIgnoreCase));
        if (columnsFolder?.IsLoaded == true)
        {
            var columns = columnsFolder.Children
                .Where(node => node.NodeType == DbObjectTreeNodeType.ChildObject && !node.IsPlaceholder)
                .Select(node => node.Name)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .ToList();
            _columnCompletionCache[cacheKey] = columns;
            return columns;
        }

        if (_loadingColumnCompletions.Add(cacheKey))
            _ = LoadColumnCompletionAsync(table, cacheKey);
        return Enumerable.Empty<string>();
    }

    private DbObjectTreeNode? FindTableForCurrentDot()
    {
        if (ObjectTreeRoots is null || string.IsNullOrWhiteSpace(ConnectionName))
            return null;

        var objectOrAlias = GetObjectNameBeforeDot();
        if (string.IsNullOrWhiteSpace(objectOrAlias))
            return null;

        var connection = ObjectTreeRoots.FirstOrDefault(node =>
            node.NodeType == DbObjectTreeNodeType.Connection &&
            string.Equals(node.Name, ConnectionName, StringComparison.OrdinalIgnoreCase));
        if (connection is null)
            return null;

        var tables = Descendants(connection).Where(node =>
            node.NodeType == DbObjectTreeNodeType.DbObject &&
            node.DatabaseObjectType is DatabaseInterpreter.Model.DatabaseObjectType.Table or DatabaseInterpreter.Model.DatabaseObjectType.View);

        // `table.column` / `[table].column` / `"table".column`。
        var directMatch = tables.FirstOrDefault(node => IdentifierEquals(node.Name, objectOrAlias));
        if (directMatch is not null)
            return directMatch;

        // `FROM table t` 或 `JOIN schema.table AS t`：字段提示也支持常用别名。
        // 仅从当前编辑器文本解析，失败时宁可不给候选，也不猜测错误对象。
        if (_editor is null)
            return null;

        foreach (Match match in Regex.Matches(
                     _editor.Text,
                     "\\b(?:FROM|JOIN)\\s+(?<source>(?:\\[[^\\]]+\\]|\"[^\"]+\"|`[^`]+`|[A-Za-z_][\\w]*)(?:\\s*\\.\\s*(?:\\[[^\\]]+\\]|\"[^\"]+\"|`[^`]+`|[A-Za-z_][\\w]*))?)(?:\\s+(?:AS\\s+)?(?<alias>\\[[^\\]]+\\]|\"[^\"]+\"|`[^`]+`|[A-Za-z_][\\w]*))?",
                     RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
        {
            var alias = NormalizeIdentifier(match.Groups["alias"].Value);
            if (!IdentifierEquals(alias, objectOrAlias))
                continue;

            var source = match.Groups["source"].Value;
            var sourceName = NormalizeIdentifier(source[(source.LastIndexOf('.') + 1)..]);
            return tables.FirstOrDefault(node => IdentifierEquals(node.Name, sourceName));
        }

        return null;
    }

    private string GetColumnCacheKey(DbObjectTreeNode table)
        => $"{ConnectionName}|{table.DatabaseName}|{table.Schema}|{table.Name}";

    private async Task LoadColumnCompletionAsync(DbObjectTreeNode table, string cacheKey)
    {
        try
        {
            var app = Application.Current as global::DatabaseManager.Avalonia.App;
            var schemaService = app?.Services?.GetService(typeof(IDbSchemaService)) as IDbSchemaService;
            if (schemaService is null || table.DbObject is null || string.IsNullOrWhiteSpace(table.DatabaseName))
                return;

            bool isView = table.DatabaseObjectType == DatabaseInterpreter.Model.DatabaseObjectType.View;
            var nodes = await schemaService.GetTableChildNodesAsync(
                ConnectionName,
                table.DatabaseName,
                DbObjectChildType.Column,
                table.DbObject,
                isView);

            _columnCompletionCache[cacheKey] = nodes
                .Where(node => !node.IsPlaceholder)
                .Select(node => node.Name)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .ToList();

            // 请求期间用户仍停留在同一个 `table.` 位置时，数据回来后自动显示补全窗口。
            if (_editor is not null && IsAfterDot() && string.Equals(GetObjectNameBeforeDot(), table.Name, StringComparison.OrdinalIgnoreCase))
                ShowCompletion(autoTriggered: true);
        }
        catch
        {
            // 补全是辅助能力；读取失败不影响编辑与执行 SQL。
        }
        finally
        {
            _loadingColumnCompletions.Remove(cacheKey);
        }
    }

    private string GetObjectNameBeforeDot()
    {
        if (_editor is null) return string.Empty;
        var document = _editor.Document;
        int pos = _editor.CaretOffset - 1;
        while (pos >= 0 && IsIdentifierCharacter(document.GetCharAt(pos)))
            pos--;
        if (pos < 0 || document.GetCharAt(pos) != '.')
            return string.Empty;

        int end = pos;
        pos--;
        while (pos >= 0 && IsIdentifierCharacter(document.GetCharAt(pos)))
            pos--;
        return NormalizeIdentifier(document.GetText(pos + 1, end - pos - 1));
    }

    private static bool IsIdentifierCharacter(char value)
        => char.IsLetterOrDigit(value) || value is '_' or '[' or ']' or '"' or '`';

    private static string NormalizeIdentifier(string value)
    {
        var trimmed = value.Trim();
        return trimmed.Length >= 2
               && ((trimmed[0] == '[' && trimmed[^1] == ']')
                   || (trimmed[0] == '"' && trimmed[^1] == '"')
                   || (trimmed[0] == '`' && trimmed[^1] == '`'))
            ? trimmed[1..^1]
            : trimmed;
    }

    private static bool IdentifierEquals(string? left, string? right)
        => string.Equals(NormalizeIdentifier(left ?? string.Empty), NormalizeIdentifier(right ?? string.Empty), StringComparison.OrdinalIgnoreCase);

    private static IEnumerable<DbObjectTreeNode> Descendants(DbObjectTreeNode node)
    {
        foreach (var child in node.Children)
        {
            yield return child;
            foreach (var descendant in Descendants(child))
                yield return descendant;
        }
    }

    private static IEnumerable<string> GetSqlKeywords() => new[]
    {
        "SELECT","FROM","WHERE","AND","OR","NOT","IN","IS","LIKE","BETWEEN","JOIN","INNER","LEFT","RIGHT","FULL","OUTER","CROSS","ON","USING","GROUP","BY","HAVING","ORDER","ASC","DESC","LIMIT","OFFSET","UNION","ALL","DISTINCT","INSERT","UPDATE","DELETE","INTO","VALUES","CREATE","ALTER","DROP","TABLE","VIEW","INDEX","TRIGGER","PROCEDURE","FUNCTION","DATABASE","SCHEMA","IF","EXISTS","AS","CASE","WHEN","THEN","ELSE","END","PRIMARY","KEY","FOREIGN","REFERENCES","CONSTRAINT","UNIQUE","CHECK","DEFAULT","NULL","EXISTS","EXPLAIN","ANALYZE","WITH","RECURSIVE"
    };

    private sealed class SqlCompletionData : ICompletionData
    {
        public SqlCompletionData(string text) => Text = text;
        public string Text { get; }
        public object Content => Text;
        public object Description => $"SQL 关键字: {Text}";
        public double Priority => 0;
        public IImage? Image => null;
        public void Complete(TextArea textArea, ISegment completionSegment, EventArgs insertionRequestEventArgs)
        {
            // AvaloniaEdit 在自动弹窗后有时会把 completionSegment 设为零长度，
            // 直接使用该区间会导致 Tab 接受补全时把关键字追加到已有前缀后。
            // 因此始终从当前光标反向定位 SQL 标识符并完整替换。
            int end = textArea.Caret.Offset;
            int start = end;
            var document = textArea.Document;
            while (start > 0)
            {
                char c = document.GetCharAt(start - 1);
                if (!char.IsLetterOrDigit(c) && c != '_')
                    break;
                start--;
            }

            document.Replace(start, end - start, Text);
            textArea.Caret.Offset = start + Text.Length;
        }
    }

    /// <summary>SqlText 属性被外部赋值（打开文件/切换标签）时更新编辑器内容。</summary>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);

        if (e.Property == ObjectTreeRootsProperty || e.Property == ConnectionNameProperty)
        {
            _columnCompletionCache.Clear();
            _loadingColumnCompletions.Clear();
            RefreshDbObjectCache();
        }

        if (e.Property != SqlTextProperty)
            return;

        if (_editor is null)
            return;

        var newText = e.NewValue as string ?? string.Empty;
        if (_editor.Document.Text == newText)
            return;

        _syncing = true;
        try
        {
            _editor.Document.Text = newText;
            _editor.CaretOffset = Math.Min(_editor.CaretOffset, _editor.Document.TextLength);
        }
        finally
        {
            _syncing = false;
        }
    }

    /// <summary>
    /// 重建当前连接的"已加载数据库对象名"与"已加载列名"缓存，供动态着色器命中。
    /// 仅遍历对象树已展开的节点（懒加载语义：未展开的表/列不上色），不发起任何 IO。
    /// </summary>
    private void RefreshDbObjectCache()
    {
        _dbObjectNames.Clear();
        _dbColumnNames.Clear();

        if (ObjectTreeRoots is null || string.IsNullOrWhiteSpace(ConnectionName))
        {
            InvalidateEditorLines();
            return;
        }

        var connection = ObjectTreeRoots.FirstOrDefault(node =>
            node.NodeType == DbObjectTreeNodeType.Connection &&
            string.Equals(node.Name, ConnectionName, StringComparison.OrdinalIgnoreCase));
        if (connection is null)
        {
            InvalidateEditorLines();
            return;
        }

        foreach (var node in EnumerateLoadedDescendants(connection))
        {
            if (node.NodeType != DbObjectTreeNodeType.DbObject || node.IsPlaceholder) continue;
            if (string.IsNullOrWhiteSpace(node.Name)) continue;

            if (node.DatabaseObjectType is DatabaseObjectType.Table
                or DatabaseObjectType.View
                or DatabaseObjectType.Procedure
                or DatabaseObjectType.Function
                or DatabaseObjectType.Sequence)
            {
                _dbObjectNames.Add(node.Name);
            }

            if (node.DatabaseObjectType is DatabaseObjectType.Column && !string.IsNullOrWhiteSpace(node.Name))
            {
                _dbColumnNames.Add(node.Name);
            }
        }

        InvalidateEditorLines();
    }

    private void InvalidateEditorLines()
    {
        if (_editor?.Document is null) return;
        // 触发 LineTransformers 重跑：变更文档 TextLength 不会改变内容但会通知重绘。
        _editor.TextArea.TextView.Redraw();
    }

    private static IEnumerable<DbObjectTreeNode> EnumerateLoadedDescendants(DbObjectTreeNode node)
    {
        // 仅访问已展开的子树（Children 已填充即视为已加载）。
        foreach (var child in node.Children)
        {
            yield return child;
            if (child.IsLoaded)
            {
                foreach (var descendant in EnumerateLoadedDescendants(child))
                    yield return descendant;
            }
        }
    }

    /// <summary>将光标定位到数据库返回的错误行，便于用户立即修正 SQL。</summary>
    public void GoToLine(int lineNumber)
    {
        if (_editor is null || lineNumber < 1 || lineNumber > _editor.Document.LineCount)
            return;

        var line = _editor.Document.GetLineByNumber(lineNumber);
        _editor.CaretOffset = line.Offset;
        _editor.TextArea.Caret.BringCaretToView();
        _editor.Focus();
    }

    /// <summary>
    /// 公开给主窗口的对象树展开事件使用：当用户在对象树里展开 Schema / 表后，
    /// 主动调一下让 SqlEditor 重建对象名缓存并重绘。
    /// </summary>
    public void NotifyObjectTreeChanged()
    {
        if (!_initialized) return;
        RefreshDbObjectCache();
    }

    /// <summary>
    /// 测试/演示用：手动注入一组视为"已加载数据库对象"的名称，
    /// 立即让 DbObjectColorizingTransformer 用红色刷这些标识符。
    /// 生产路径不调用此方法（仅在冒烟截图脚本里使用）。
    /// </summary>
    public void SeedDemoObjectNames(IEnumerable<string> names)
    {
        if (!_initialized) return;
        foreach (var n in names ?? Array.Empty<string>())
        {
            if (!string.IsNullOrWhiteSpace(n))
                _dbObjectNames.Add(n);
        }
        // 强制 TextView 重建可见行，让 DocumentColorizingTransformer 重新跑一遍。
        _editor?.TextArea.TextView.Redraw();
    }

    /// <summary>
    /// 测试/演示用：直接把 SQL 文本写入编辑器（绕过 TwoWay 绑定回写，避免被会话恢复覆盖）。
    /// </summary>
    public void SeedDemoSqlText(string sql)
    {
        if (!_initialized || _editor is null) return;
        _syncing = true;
        try
        {
            _editor.Document.Text = sql ?? string.Empty;
            _editor.CaretOffset = 0;
        }
        finally
        {
            _syncing = false;
        }
    }

    /// <summary>从嵌入资源或 Avalonia 资源加载 SQL 高亮定义（带缓存）。</summary>
    private static IHighlightingDefinition? LoadSqlHighlighting()
    {
        if (_cachedHighlighting != null) return _cachedHighlighting;
        // 1) EmbeddedResource 路径（csproj 中 Assets/Sql.xshd 设为 EmbeddedResource）
        try
        {
            var assembly = typeof(SqlEditor).Assembly;
            // 尝试多种命名变体以兼容根命名空间差异
            string[] names = new[]
            {
                "DatabaseManager.Avalonia.Assets.Sql.xshd",
                "DatabaseManager.Avalonia.Assets.Sql.xshd",
                assembly.GetName().Name + ".Assets.Sql.xshd"
            };
            foreach (var n in names.Distinct())
            {
                using var stream = assembly.GetManifestResourceStream(n);
                if (stream != null)
                {
                    using var reader = XmlReader.Create(stream);
                    var def = HighlightingLoader.Load(reader, HighlightingManager.Instance);
                    _cachedHighlighting = def;
                    return def;
                }
            }
        }
        catch { /* ignore, fallback to AssetLoader */ }

        // 2) Avalonia AssetLoader 路径（若 csproj 改为 AvaloniaResource）
        try
        {
            if (global::Avalonia.Platform.AssetLoader.Exists(new Uri("avares://DatabaseManager.Avalonia/Assets/Sql.xshd")))
            {
                using var stream = global::Avalonia.Platform.AssetLoader.Open(new Uri("avares://DatabaseManager.Avalonia/Assets/Sql.xshd"));
                using var reader = XmlReader.Create(stream);
                var def = HighlightingLoader.Load(reader, HighlightingManager.Instance);
                _cachedHighlighting = def;
                return def;
            }
        }
        catch { /* ignore */ }

        return null;
    }

    /// <summary>追加文本到编辑器末尾。</summary>
    public void AppendText(string text)
    {
        if (_editor?.Document is not null)
        {
            _editor.Document.Insert(_editor.Document.TextLength, text ?? string.Empty);
        }
        else
        {
            // 尚未初始化时，直接累加到 SqlText 属性
            SqlText = (SqlText ?? string.Empty) + (text ?? string.Empty);
        }
    }

    /// <summary>在光标处插入文本（供脚本库/查询历史插入片段使用）。</summary>
    public void InsertAtCaret(string text)
    {
        if (_editor?.Document is not null)
        {
            var offset = _editor.CaretOffset;
            _editor.Document.Insert(offset, text ?? string.Empty);
            _editor.CaretOffset = offset + (text?.Length ?? 0);
            _editor.Focus();
        }
        else
        {
            AppendText(text ?? string.Empty);
        }
    }

    /// <summary>获取当前选中的文本。</summary>
    public string GetSelectedText()
    {
        return _editor?.TextArea.Selection.GetText() ?? string.Empty;
    }

    /// <summary>美化当前选中 SQL（无选区则美化全文）。</summary>
    public void Format()
    {
        if (_editor?.Document is null) return;

        var selection = _editor.TextArea.Selection;
        string original;
        int offset;
        int length;

        if (!selection.IsEmpty)
        {
            var seg = selection.SurroundingSegment;
            if (seg is null) return;
            offset = seg.Offset;
            length = seg.Length;
            original = _editor.Document.GetText(offset, length);
        }
        else
        {
            offset = 0;
            length = _editor.Document.TextLength;
            original = _editor.Document.Text;
        }

        if (string.IsNullOrWhiteSpace(original)) return;

        var formatted = DatabaseManager.AppCore.Common.SqlFormatter.Format(original);
        if (formatted == original) return;

        _editor.Document.Replace(offset, length, formatted);
        _editor.Select(offset, formatted.Length);
    }

    #region 选中文本：右键菜单与编辑辅助

    /// <summary>编辑器抛给宿主的命令（需要连接/服务上下文，控件自身无法完成）。</summary>
    public enum SqlEditorCommandKind
    {
        Execute,
        ExecuteInNewTab,
        Explain,
        SaveToLibrary,
    }

    /// <summary>右键菜单「复制选中 SQL 为…」支持的格式。</summary>
    public enum SqlCopyAsKind
    {
        SingleLine,
        JsonString,
        CSharpString,
    }

    public sealed class SqlEditorCommandEventArgs : EventArgs
    {
        public SqlEditorCommandEventArgs(SqlEditorCommandKind kind, string sql)
        {
            Kind = kind;
            Sql = sql;
        }

        public SqlEditorCommandKind Kind { get; }

        /// <summary>选中文本；无选区时为空字符串，由宿主决定是否回退到全文。</summary>
        public string Sql { get; }
    }

    /// <summary>需要主窗口介入的命令（执行 / 执行计划 / 保存脚本库）。</summary>
    public event EventHandler<SqlEditorCommandEventArgs>? CommandRequested;

    private void RaiseCommand(SqlEditorCommandKind kind)
        => CommandRequested?.Invoke(this, new SqlEditorCommandEventArgs(kind, GetSelectedText()));

    /// <summary>打开查找/替换面板（<paramref name="replaceMode"/> 为 true 时展开替换行）。</summary>
    private void OpenSearchPanel(bool replaceMode)
    {
        if (_searchPanel is null)
        {
            return;
        }

        _searchPanel.IsReplaceMode = replaceMode;
        _searchPanel.Open();
    }

    /// <summary>
    /// 判断事件是否源自查找/替换面板。面板打开时由 AvaloniaEdit 挂到 TextArea 下，
    /// 是编辑器的可视后代，因此其搜索框的按键与右键都会冒泡到编辑器，需要显式排除。
    /// </summary>
    private bool IsFromSearchPanel(RoutedEventArgs e)
    {
        if (_searchPanel is null || e.Source is not Visual source)
        {
            return false;
        }

        for (Visual? current = source; current is not null; current = current.GetVisualParent())
        {
            if (ReferenceEquals(current, _searchPanel))
            {
                return true;
            }

            if (ReferenceEquals(current, _editor))
            {
                break;
            }
        }

        return false;
    }

    private void OnEditorContextRequested(object? sender, ContextRequestedEventArgs e)
    {
        if (_editor is null || IsFromSearchPanel(e))
        {
            return;
        }

        // 与对象树/结果网格一致：显式 Open，避免首次右键不弹出。
        var menu = BuildContextMenu();
        menu.Open(_editor);
        e.Handled = true;
    }

    /// <summary>构建编辑器右键菜单（对齐 SSMS / DBeaver 的常用项）。</summary>
    private ContextMenu BuildContextMenu()
    {
        var menu = new ContextMenu();
        if (_editor is null)
        {
            return menu;
        }

        bool hasSelection = !_editor.TextArea.Selection.IsEmpty;

        void AddItem(string header, Action action, KeyGesture? gesture = null, bool enabled = true)
        {
            var item = new MenuItem { Header = header, IsEnabled = enabled, InputGesture = gesture };
            item.Click += (_, _) => action();
            menu.Items.Add(item);
        }

        AddItem("执行选中 SQL", () => RaiseCommand(SqlEditorCommandKind.Execute), Gesture(Key.Enter, control: true), hasSelection);
        AddItem("在新标签执行选中 SQL", () => RaiseCommand(SqlEditorCommandKind.ExecuteInNewTab), Gesture(Key.Enter, control: true, shift: true), hasSelection);
        AddItem("显示执行计划", () => RaiseCommand(SqlEditorCommandKind.Explain), Gesture(Key.L, control: true));
        AddItem("保存选中到脚本库", () => RaiseCommand(SqlEditorCommandKind.SaveToLibrary), Gesture(Key.S, control: true, shift: true), hasSelection);

        menu.Items.Add(new Separator());

        AddItem("美化 SQL（选区优先）", Format, Gesture(Key.F, control: true, shift: true));
        AddItem("查找 / 替换…", () => OpenSearchPanel(replaceMode: true), Gesture(Key.F, control: true));

        menu.Items.Add(new Separator());

        AddItem("撤销", () => _editor.Undo(), Gesture(Key.Z, control: true), _editor.CanUndo);
        AddItem("恢复", () => _editor.Redo(), Gesture(Key.Z, control: true, shift: true), _editor.CanRedo);

        menu.Items.Add(new Separator());

        AddItem("剪切", () => _editor.Cut(), Gesture(Key.X, control: true), hasSelection);
        AddItem("复制", () => _editor.Copy(), Gesture(Key.C, control: true), hasSelection);
        AddItem("粘贴", () => _editor.Paste(), Gesture(Key.V, control: true));
        AddItem("全选", () => _editor.SelectAll(), Gesture(Key.A, control: true));

        menu.Items.Add(new Separator());

        AddItem("注释 / 取消注释行", ToggleLineComment, Gesture(Key.C, control: true, shift: true));
        AddItem("选中内容转大写", () => ChangeSelectionCase(upper: true), Gesture(Key.U, control: true, shift: true), hasSelection);
        AddItem("选中内容转小写", () => ChangeSelectionCase(upper: false), Gesture(Key.L, control: true, shift: true), hasSelection);
        AddItem("增加缩进", () => IndentSelection(indent: true));
        AddItem("减少缩进", () => IndentSelection(indent: false));

        menu.Items.Add(new Separator());

        AddItem("复制当前行 / 选区", DuplicateLineOrSelection, Gesture(Key.D, control: true));
        AddItem("删除当前行", DeleteLines, Gesture(Key.K, control: true, shift: true));
        AddItem("行上移", () => MoveSelectedLines(up: true), Gesture(Key.Up, alt: true));
        AddItem("行下移", () => MoveSelectedLines(up: false), Gesture(Key.Down, alt: true));

        menu.Items.Add(new Separator());

        var copyAsMenu = new MenuItem { Header = "复制选中 SQL 为…", IsEnabled = hasSelection };
        foreach (var (label, kind) in new[]
                 {
                     ("压缩为一行", SqlCopyAsKind.SingleLine),
                     ("JSON 字符串", SqlCopyAsKind.JsonString),
                     ("C# 字符串字面量", SqlCopyAsKind.CSharpString),
                 })
        {
            var captured = kind;
            var item = new MenuItem { Header = label };
            item.Click += async (_, _) => await CopySqlAsAsync(captured);
            copyAsMenu.Items.Add(item);
        }

        menu.Items.Add(copyAsMenu);

        return menu;
    }

    private static KeyGesture? Gesture(Key key, bool control = false, bool shift = false, bool alt = false)
    {
        var modifiers = KeyModifiers.None;
        if (control) modifiers |= KeyModifiers.Control;
        if (shift) modifiers |= KeyModifiers.Shift;
        if (alt) modifiers |= KeyModifiers.Alt;
        return modifiers == KeyModifiers.None ? null : new KeyGesture(key, modifiers);
    }

    /// <summary>把选中的 SQL 以指定形式复制到剪贴板。</summary>
    private async Task CopySqlAsAsync(SqlCopyAsKind kind)
    {
        var text = GetSelectedText();
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        var result = kind switch
        {
            // 压缩空白为单个空格，便于粘贴到聊天/文档里排版
            SqlCopyAsKind.SingleLine => Regex.Replace(text, "\\s+", " ").Trim(),
            SqlCopyAsKind.JsonString => System.Text.Json.JsonSerializer.Serialize(text),
            // 逐行原样保留的 C# 逐字字符串
            SqlCopyAsKind.CSharpString => "@\"" + text.Replace("\"", "\"\"") + "\"",
            _ => text,
        };

        if (TopLevel.GetTopLevel(this)?.Clipboard is { } clipboard)
        {
            await clipboard.SetTextAsync(result);
        }
    }

    /// <summary>注释或取消注释选区覆盖的行（无选区则为光标所在行）。</summary>
    private void ToggleLineComment()
    {
        if (_editor?.Document is null)
        {
            return;
        }

        var (first, last) = GetSelectedLineRange();
        bool allCommented = true;
        for (int i = first; i <= last; i++)
        {
            var line = _editor.Document.GetLineByNumber(i);
            var text = _editor.Document.GetText(line);
            if (string.IsNullOrWhiteSpace(text))
            {
                continue;
            }

            if (!text.TrimStart().StartsWith("--", StringComparison.Ordinal))
            {
                allCommented = false;
                break;
            }
        }

        ModifyLines(first, last, text =>
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return text;
            }

            if (!allCommented)
            {
                return InsertAfterIndent(text, "-- ");
            }

            // 取消注释：去掉标识符后的一个可选空格，保留原有缩进
            var trimmed = text.TrimStart();
            var indent = text[..(text.Length - trimmed.Length)];
            var rest = trimmed.Length >= 2 ? trimmed[2..] : trimmed;
            if (rest.StartsWith(' '))
            {
                rest = rest[1..];
            }

            return indent + rest;
        });
    }

    /// <summary>选区或光标处标识符的大小写转换。</summary>
    private void ChangeSelectionCase(bool upper)
    {
        if (_editor?.Document is null)
        {
            return;
        }

        var document = _editor.Document;
        var selection = _editor.TextArea.Selection;
        if (!selection.IsEmpty)
        {
            var segment = selection.SurroundingSegment;
            if (segment is null)
            {
                return;
            }

            var original = document.GetText(segment);
            var converted = upper ? original.ToUpperInvariant() : original.ToLowerInvariant();
            if (converted == original)
            {
                return;
            }

            document.Replace(segment.Offset, segment.Length, converted);
            _editor.Select(segment.Offset, converted.Length);
            return;
        }

        // 无选区：作用于光标处的 SQL 标识符（与补全的词边界规则一致）
        int caret = _editor.CaretOffset;
        int start = caret;
        int end = caret;
        while (start > 0 && IsIdentifierCharacter(document.GetCharAt(start - 1)))
        {
            start--;
        }

        while (end < document.TextLength && IsIdentifierCharacter(document.GetCharAt(end)))
        {
            end++;
        }

        if (end <= start)
        {
            return;
        }

        var word = document.GetText(start, end - start);
        var convertedWord = upper ? word.ToUpperInvariant() : word.ToLowerInvariant();
        document.Replace(start, end - start, convertedWord);
        _editor.CaretOffset = start + convertedWord.Length;
    }

    /// <summary>选区各行增加/减少两个空格缩进。</summary>
    private void IndentSelection(bool indent)
    {
        var (first, last) = GetSelectedLineRange();
        ModifyLines(first, last, text =>
        {
            if (indent)
            {
                return "  " + text;
            }

            if (text.StartsWith("  ", StringComparison.Ordinal))
            {
                return text[2..];
            }

            return text.StartsWith(' ') || text.StartsWith('\t') ? text[1..] : text;
        });
    }

    /// <summary>原地复制选区；无选区时整行复制插入到下一行（DataGrip Ctrl+D）。</summary>
    private void DuplicateLineOrSelection()
    {
        if (_editor?.Document is null)
        {
            return;
        }

        var document = _editor.Document;
        if (!_editor.TextArea.Selection.IsEmpty)
        {
            var segment = _editor.TextArea.Selection.SurroundingSegment;
            if (segment is not null)
            {
                document.Insert(segment.EndOffset, document.GetText(segment));
            }

            return;
        }

        var line = document.GetLineByOffset(_editor.CaretOffset);
        var newline = GetNewLine();
        var content = document.GetText(line);
        document.Insert(line.EndOffset, newline + content);
        _editor.CaretOffset = line.EndOffset + newline.Length + content.Length;
    }

    /// <summary>删除选区覆盖的行（无选区则删除光标所在行）。</summary>
    private void DeleteLines()
    {
        if (_editor?.Document is null)
        {
            return;
        }

        var document = _editor.Document;
        var (first, last) = GetSelectedLineRange();
        for (int i = last; i >= first; i--)
        {
            if (i > document.LineCount)
            {
                continue;
            }

            var line = document.GetLineByNumber(i);
            if (line.NextLine is null)
            {
                // 末行没有行尾换行符：连带删掉上一行的换行，避免留下空行
                int from = line.PreviousLine?.EndOffset ?? line.Offset;
                document.Remove(from, line.EndOffset - from);
            }
            else
            {
                document.Remove(line.Offset, line.TotalLength);
            }
        }

        _editor.TextArea.ClearSelection();
    }

    /// <summary>选区所列整块行上移/下移一行（Alt+↑ / Alt+↓）。</summary>
    private void MoveSelectedLines(bool up)
    {
        if (_editor?.Document is null)
        {
            return;
        }

        var document = _editor.Document;
        var (first, last) = GetSelectedLineRange();
        if (up ? first <= 1 : last >= document.LineCount)
        {
            return;
        }

        var lines = Enumerable.Range(1, document.LineCount)
            .Select(number => document.GetText(document.GetLineByNumber(number)))
            .ToList();

        var block = lines.GetRange(first - 1, last - first + 1);
        lines.RemoveRange(first - 1, block.Count);
        lines.InsertRange(up ? first - 2 : first, block);

        var newFirst = up ? first - 1 : first + 1;
        var newLast = up ? last - 1 : last + 1;

        document.Text = string.Join(GetNewLine(), lines);

        var startLine = document.GetLineByNumber(newFirst);
        var endLine = document.GetLineByNumber(newLast);
        _editor.Select(startLine.Offset, endLine.EndOffset - startLine.Offset);
    }

    /// <summary>当前选区覆盖的行号区间（起止行）；无选区时为光标所在行。</summary>
    private (int First, int Last) GetSelectedLineRange()
    {
        if (_editor?.Document is null)
        {
            return (1, 1);
        }

        var document = _editor.Document;
        int caretLine = document.GetLineByOffset(Math.Min(_editor.CaretOffset, document.TextLength)).LineNumber;
        if (_editor.TextArea.Selection.IsEmpty)
        {
            return (caretLine, caretLine);
        }

        var segment = _editor.TextArea.Selection.SurroundingSegment;
        if (segment is null)
        {
            return (caretLine, caretLine);
        }

        int first = document.GetLineByOffset(segment.Offset).LineNumber;
        int last = document.GetLineByOffset(segment.EndOffset).LineNumber;

        // 选区正好在换行处结束时，不把下一行算进来（避免整行操作时多选一行）
        if (last > first && document.GetLineByNumber(last).Offset == segment.EndOffset)
        {
            last--;
        }

        return (first, last);
    }

    /// <summary>从后往前逐行改写（保持 offset 有效），并合并为一次撤销步骤。</summary>
    private void ModifyLines(int first, int last, Func<string, string> transform)
    {
        if (_editor?.Document is null)
        {
            return;
        }

        var document = _editor.Document;
        document.BeginUpdate();
        try
        {
            for (int i = last; i >= first; i--)
            {
                var line = document.GetLineByNumber(i);
                var original = document.GetText(line);
                var updated = transform(original);
                if (!string.Equals(updated, original, StringComparison.Ordinal))
                {
                    document.Replace(line.Offset, line.Length, updated);
                }
            }
        }
        finally
        {
            document.EndUpdate();
        }
    }

    private static string InsertAfterIndent(string line, string insert)
    {
        int index = 0;
        while (index < line.Length && (line[index] == ' ' || line[index] == '\t'))
        {
            index++;
        }

        return string.Concat(line.AsSpan(0, index), insert, line.AsSpan(index));
    }

    private string GetNewLine()
    {
        if (_editor?.Document is null)
        {
            return "\n";
        }

        var document = _editor.Document;
        for (int i = 0; i < document.TextLength; i++)
        {
            char c = document.GetCharAt(i);
            if (c == '\r')
            {
                return "\r\n";
            }

            if (c == '\n')
            {
                return "\n";
            }
        }

        return "\n";
    }

    #endregion
}
