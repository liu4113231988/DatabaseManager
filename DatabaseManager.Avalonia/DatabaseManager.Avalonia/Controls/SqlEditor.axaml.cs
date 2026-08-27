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

    private TextEditor? _editor;
    private bool _syncing;
    private bool _initialized;
    private CompletionWindow? _completionWindow;

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
        // 仅字母/数字/下划线触发自动补全；点号不触发（避免 SELECT * FROM t. 后弹出全量关键字）
        if (char.IsLetterOrDigit(c) || c == '_')
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
        // 自动触发时，词长度 <1 不弹出；手动触发（Ctrl+Space）则允许空词弹出全量
        if (autoTriggered && string.IsNullOrEmpty(word))
            return;

        // 点号后不做关键字补全
        if (IsAfterDot())
            return;

        IEnumerable<string> candidates = GetSqlKeywords();

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
        // 回溯跳过空白，检查前一非空白字符是否为 '.'
        int pos = offset - 1;
        while (pos >= 0 && char.IsWhiteSpace(doc.GetCharAt(pos)))
            pos--;
        if (pos >= 0 && doc.GetCharAt(pos) == '.')
            return true;
        return false;
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
            // 保留原始大小写前缀的匹配长度替换，插入大写关键字
            textArea.Document.Replace(completionSegment, Text);
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
