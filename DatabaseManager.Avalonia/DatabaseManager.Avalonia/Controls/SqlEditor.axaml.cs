using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using AvaloniaEdit;
using AvaloniaEdit.CodeCompletion;
using AvaloniaEdit.Document;
using AvaloniaEdit.Editing;
using AvaloniaEdit.Highlighting;
using AvaloniaEdit.Highlighting.Xshd;

namespace DatabaseManager.Avalonia.Controls;

/// <summary>
/// SQL 编辑器用户控件：基于 AvaloniaEdit，深色主题 + SQL 语法高亮 + 行号。
/// 颜色均由本控件显式指定，不受 AtomUI 主题状态样式（如 pointerover）影响。
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

    public SqlEditor()
    {
        InitializeComponent();

        _editor = this.FindControl<TextEditor>("PART_TextEditor");
        if (_editor is null)
            return;

        // 浅色主题细节：光标与选区颜色固定，避免任何主题态下不可见
        _editor.TextArea.Caret.CaretBrush = Brushes.Black;
        _editor.TextArea.SelectionBrush = new SolidColorBrush(Color.FromRgb(0xAD, 0xD6, 0xFF));

        // 加载内置 SQL 高亮定义（失败时自动降级为纯文本）
        _editor.SyntaxHighlighting = LoadSqlHighlighting();

        // 编辑器文本变化 → 写回 SqlText 属性（经 TwoWay 绑定同步到 ViewModel）
        _editor.Document.TextChanged += (_, _) =>
        {
            if (_syncing)
                return;
            SetCurrentValue(SqlTextProperty, _editor.Document.Text);
        };

        _editor.TextArea.TextEntered += OnTextEntered;
        _editor.TextArea.KeyDown += OnKeyDown;
    }

    private CompletionWindow? _completionWindow;

    private void OnTextEntered(object? sender, TextInputEventArgs e)
    {
        if (e.Text is null || e.Text.Length == 0)
            return;
        // 字母/下划线/点号触发补全
        char c = e.Text[0];
        if (char.IsLetterOrDigit(c) || c == '_' || c == '.')
        {
            ShowCompletion();
        }
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Space && e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            ShowCompletion();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape && _completionWindow is not null)
        {
            _completionWindow.Close();
        }
    }

    private void ShowCompletion()
    {
        if (_editor is null) return;
        if (_completionWindow is not null) return;

        var word = GetCurrentWord();
        var keywords = GetSqlKeywords().Where(k => k.StartsWith(word, StringComparison.OrdinalIgnoreCase)).Take(50);
        if (!keywords.Any() && string.IsNullOrEmpty(word)) return;

        _completionWindow = new CompletionWindow(_editor.TextArea);
        _completionWindow.Closed += (_, _) => _completionWindow = null;

        var data = _completionWindow.CompletionList.CompletionData;
        foreach (var kw in keywords)
        {
            data.Add(new SqlCompletionData(kw));
        }

        // 若无匹配则不显示
        if (data.Count == 0) { _completionWindow.Close(); return; }

        _completionWindow.Show();
    }

    private string GetCurrentWord()
    {
        if (_editor is null) return string.Empty;
        int offset = _editor.CaretOffset;
        if (offset == 0) return string.Empty;
        var doc = _editor.Document;
        int start = offset;
        while (start > 0 && (char.IsLetterOrDigit(doc.GetCharAt(start - 1)) || doc.GetCharAt(start - 1) == '_' ))
            start--;
        return doc.GetText(start, offset - start);
    }

    private static IEnumerable<string> GetSqlKeywords() => new[]
    {
        "SELECT","FROM","WHERE","AND","OR","NOT","IN","IS","LIKE","BETWEEN","JOIN","INNER","LEFT","RIGHT","FULL","OUTER","CROSS","ON","USING","GROUP","BY","HAVING","ORDER","ASC","DESC","LIMIT","OFFSET","UNION","ALL","DISTINCT","INSERT","UPDATE","DELETE","INTO","VALUES","CREATE","ALTER","DROP","TABLE","VIEW","INDEX","TRIGGER","PROCEDURE","FUNCTION","DATABASE","SCHEMA","IF","EXISTS","AS","CASE","WHEN","THEN","ELSE","END","PRIMARY","KEY","FOREIGN","REFERENCES","CONSTRAINT","UNIQUE","CHECK","DEFAULT","NULL","NOT","EXISTS","EXPLAIN","ANALYZE","WITH","RECURSIVE"
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
            textArea.Document.Replace(completionSegment, Text);
        }
    }

    /// <summary>SqlText 属性被外部赋值（打开文件/切换标签）时更新编辑器内容。</summary>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);

        if (e.Property != SqlTextProperty || _editor is null)
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

    /// <summary>从嵌入资源加载 SQL 高亮定义。</summary>
    private static IHighlightingDefinition? LoadSqlHighlighting()
    {
        try
        {
            using var stream = typeof(SqlEditor).Assembly.GetManifestResourceStream(
                "DatabaseManager.Avalonia.Assets.Sql.xshd");
            if (stream is null)
                return null;

            using var reader = XmlReader.Create(stream);
            return HighlightingLoader.Load(reader, HighlightingManager.Instance);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>追加文本到编辑器末尾。</summary>
    public void AppendText(string text)
    {
        if (_editor?.Document is not null)
        {
            _editor.Document.Insert(_editor.Document.TextLength, text ?? string.Empty);
        }
    }

    /// <summary>获取当前选中的文本。</summary>
    public string GetSelectedText()
    {
        return _editor?.TextArea.Selection.GetText() ?? string.Empty;
    }

    /// <summary>美化当前选中 SQL（无选区则美化全文），由 ViewModel 或工具栏调用。</summary>
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
        // 选中美化后的片段以便用户感知
        _editor.Select(offset, formatted.Length);
    }
}
