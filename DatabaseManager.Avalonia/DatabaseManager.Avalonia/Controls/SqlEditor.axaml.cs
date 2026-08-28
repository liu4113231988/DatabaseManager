using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using AvaloniaEdit;
using AvaloniaEdit.CodeCompletion;
using AvaloniaEdit.Document;
using AvaloniaEdit.Editing;
using AvaloniaEdit.Highlighting;
using AvaloniaEdit.Highlighting.Xshd;
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
    private bool _syncing;
    private bool _initialized;
    private CompletionWindow? _completionWindow;
    private readonly Dictionary<string, IReadOnlyList<string>> _columnCompletionCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _loadingColumnCompletions = new(StringComparer.OrdinalIgnoreCase);

    private static IHighlightingDefinition? _cachedHighlighting;

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
        if (e.Key == Key.Space && e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            ShowCompletion(autoTriggered: false);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape && _completionWindow is not null)
        {
            _completionWindow.Close();
        }
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

        // `table.` 后只提示已加载的列；其余位置混合 SQL 关键字与数据库对象。
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
        while (pos > 0 && (char.IsLetterOrDigit(doc.GetCharAt(pos - 1)) || doc.GetCharAt(pos - 1) == '_'))
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
        while (start > 0 && (char.IsLetterOrDigit(doc.GetCharAt(start - 1)) || doc.GetCharAt(start - 1) == '_'))
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

        var tableName = GetObjectNameBeforeDot();
        if (string.IsNullOrWhiteSpace(tableName))
            return null;

        var connection = ObjectTreeRoots.FirstOrDefault(node =>
            node.NodeType == DbObjectTreeNodeType.Connection &&
            string.Equals(node.Name, ConnectionName, StringComparison.OrdinalIgnoreCase));
        if (connection is null)
            return null;

        return Descendants(connection).FirstOrDefault(node =>
            node.NodeType == DbObjectTreeNodeType.DbObject &&
            (node.DatabaseObjectType is DatabaseInterpreter.Model.DatabaseObjectType.Table or DatabaseInterpreter.Model.DatabaseObjectType.View) &&
            string.Equals(node.Name, tableName, StringComparison.OrdinalIgnoreCase));
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
        int pos = _editor.CaretOffset - GetCurrentWord().Length - 1;
        if (pos < 0 || document.GetCharAt(pos) != '.') return string.Empty;

        int end = pos;
        while (pos > 0 && (char.IsLetterOrDigit(document.GetCharAt(pos - 1)) || document.GetCharAt(pos - 1) == '_'))
            pos--;
        return document.GetText(pos, end - pos);
    }

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
}
