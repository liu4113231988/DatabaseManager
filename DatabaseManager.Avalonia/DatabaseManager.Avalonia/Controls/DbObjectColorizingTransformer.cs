using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Media;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;
using DatabaseInterpreter.Model;
using DatabaseManager.AppCore.Models;

namespace DatabaseManager.Avalonia.Controls;

/// <summary>
/// SQL 编辑器动态着色器：把"已加载到对象树中的数据库对象名（表/视图/存储过程/函数/列）"
/// 用 <see cref="IdentifierBrush"/> 着色，与关键字颜色（#0000FF bold）区分。
///
/// 设计权衡：
/// - 仅上色已加载对象，未加载对象不上色（避免大库首次连接全量扫表 IO）；
/// - 不上色关键字/数据类型/函数名（避免与 XSHD 关键字冲突）；
/// - 不上色字符串/注释内的标识符（变压器运行在 XSHD 之后，但仍会扫到引号内文本，按"不去"实现即可
///   —— 列名命中表/列缓存需要做，否则 SELECT 'users' 也会高亮，所以变压器不参与引号内区域；
///   通过让 DocumentColorizingTransformer 顺序在 AvalonEdit 内置 span 之后处理并直接对 LinePart
///   染色，但关键字与标识符都在 Run 里，引号由 Span 包裹。Xshd 不会自动屏蔽 Run，
///   所以保守方案：变压器只对外层 Run 染色（不在 String/Comment 颜色区域内）。
///
/// 实际行为：AvaloniaEdit 提供的 <see cref="DocumentColorizingTransformer.Colorize"/> 在每个
/// VisualLineChunk 上对 segment 重新分配颜色。原关键字区域的 Foreground 来自 HighlightingColor，
/// 变压器在关键字上设置新 Foreground 会"覆盖"原色（失去关键字颜色）。因此这里通过
/// <see cref="IsReservedKeyword"/> 守门：保留关键字一律不重新上色。
/// </summary>
public sealed class DbObjectColorizingTransformer : DocumentColorizingTransformer
{
    /// <summary>数据库对象标识符颜色：与关键字蓝（#0000FF bold）形成冷暖对比。</summary>
    public static readonly IBrush IdentifierBrush = new SolidColorBrush(Color.FromRgb(0xC4, 0x45, 0x45));
    /// <summary>列名颜色（仅在 <c>table.column</c> 第二段启用时使用）。</summary>
    public static readonly IBrush ColumnBrush = new SolidColorBrush(Color.FromRgb(0x6B, 0x5D, 0xD3));

    private readonly Func<HashSet<string>?> _objectNamesProvider;
    private readonly Func<HashSet<string>?> _columnNamesProvider;
    private readonly HashSet<string> _reservedKeywords;

    /// <param name="objectNamesProvider">返回当前连接已加载的"表/视图/存储过程/函数"名集合（不区分大小写）。</param>
    /// <param name="columnNamesProvider">返回当前连接已加载的所有列名集合（不区分大小写），可为 null。</param>
    public DbObjectColorizingTransformer(
        Func<HashSet<string>?> objectNamesProvider,
        Func<HashSet<string>?> columnNamesProvider,
        IEnumerable<string> reservedKeywords)
    {
        _objectNamesProvider = objectNamesProvider ?? throw new ArgumentNullException(nameof(objectNamesProvider));
        _columnNamesProvider = columnNamesProvider is null
            ? (Func<HashSet<string>?>)(() => null)
            : columnNamesProvider;
        _reservedKeywords = new HashSet<string>(reservedKeywords, StringComparer.OrdinalIgnoreCase);
    }

    protected override void ColorizeLine(DocumentLine line)
    {
        var objects = _objectNamesProvider();
        if (objects is null || objects.Count == 0) return;

        var lineText = CurrentContext.Document.GetText(line);
        int lineOffset = line.Offset;
        int i = 0;
        int n = lineText.Length;

        // 状态机：跳过字符串、跳过注释，按 token 边界切分标识符。
        bool inSingleQuote = false;
        bool inDoubleQuote = false;
        bool inLineComment = false;

        while (i < n)
        {
            char c = lineText[i];

            // 跳过已行注释
            if (inLineComment)
            {
                i++;
                continue;
            }

            // 跨行字符串：用 '...' 不视为注释开头
            if (inSingleQuote)
            {
                if (c == '\'') inSingleQuote = false;
                i++;
                continue;
            }
            if (inDoubleQuote)
            {
                if (c == '"') inDoubleQuote = false;
                i++;
                continue;
            }

            // 行注释起始
            if (c == '-' && i + 1 < n && lineText[i + 1] == '-')
            {
                inLineComment = true;
                i += 2;
                continue;
            }
            // 块注释起始（简单处理：从 /* 一直跳到 */，跨行由每行 colorize 各自处理，所以这里只跳到行末）
            if (c == '/' && i + 1 < n && lineText[i + 1] == '*')
            {
                int end = lineText.IndexOf("*/", i + 2, StringComparison.Ordinal);
                if (end < 0) { i = n; break; }
                i = end + 2;
                continue;
            }

            if (c == '\'')
            {
                inSingleQuote = true;
                i++;
                continue;
            }
            if (c == '"')
            {
                inDoubleQuote = true;
                i++;
                continue;
            }

            // 标识符起点
            if (IsIdentifierStart(c))
            {
                int start = i;
                i++;
                while (i < n && IsIdentifierPart(lineText[i])) i++;
                int len = i - start;
                if (len < 2) continue;

                // 包含 / 剥离反引号、双引号
                string raw = lineText.Substring(start, len);
                string token = StripQuotes(raw);
                if (token.Length == 0) continue;
                if (_reservedKeywords.Contains(token)) continue;

                if (objects.Contains(token))
                {
                    ChangeLinePart(lineOffset + start, lineOffset + i, visualLine =>
                    {
                        visualLine.TextRunProperties.SetForegroundBrush(IdentifierBrush);
                    });
                }
                continue;
            }

            i++;
        }
    }

    private static bool IsIdentifierStart(char c)
        => char.IsLetter(c) || c == '_' || c == '`' || c == '[';

    private static bool IsIdentifierPart(char c)
        => char.IsLetterOrDigit(c) || c == '_' || c == '`' || c == ']';

    private static string StripQuotes(string s)
    {
        if (s.Length >= 2)
        {
            if ((s[0] == '`' && s[^1] == '`') || (s[0] == '[' && s[^1] == ']'))
                return s[1..^1];
        }
        return s;
    }
}
