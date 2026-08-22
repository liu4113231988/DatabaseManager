using System;
using System.Xml;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using AvaloniaEdit;
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
}
