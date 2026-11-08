using System.Text.RegularExpressions;
using DatabaseInterpreter.Model;
using DatabaseManager.AppCore.Models;

namespace DatabaseManager.AppCore.Common;

public sealed record SqlCompletionTable(string Name, string? Schema, string? Database, string? Alias);

public sealed record SqlCompletionContext(int StartOffset, int EndOffset, string Prefix,
    IReadOnlyList<string> Qualifiers, bool AfterDot, bool ExpectsTable, bool ExpectsColumn,
    IReadOnlyList<SqlCompletionTable> Tables);

/// <summary>不执行 SQL 的补全上下文分析；只从当前语句的查询作用域解析物理表。</summary>
public static class SqlCompletion
{
    private static readonly Regex Tokens = new("""
        (?<line>--[^\r\n]*)|(?<block>/\*)|(?<literal>'(?:''|[^'])*(?:'|$)|\$(?<tag>[A-Za-z_][A-Za-z_0-9]*|)\$[\s\S]*?(?:\$\k<tag>\$|$))|(?<id>\[(?:\]\]|[^\]])*(?:\]|$)|"(?:""|[^"])*(?:"|$)|`(?:``|[^`])*(?:`|$)|[\p{L}_][\p{L}\p{N}_$#]*)|(?<number>\d+(?:\.\d+)?)|(?<symbol>\S)
        """, RegexOptions.CultureInvariant);
    private static readonly HashSet<string> Clauses = new(StringComparer.OrdinalIgnoreCase)
    {
        "SELECT", "FROM", "JOIN", "WHERE", "ON", "USING", "GROUP", "ORDER", "HAVING", "UPDATE",
        "INTO", "SET", "VALUES", "RETURNING", "LIMIT", "OFFSET", "UNION", "EXCEPT", "INTERSECT",
    };
    private static readonly HashSet<string> AliasStops = new(Clauses, StringComparer.OrdinalIgnoreCase)
    {
        "INNER", "LEFT", "RIGHT", "FULL", "OUTER", "CROSS", "AS", "FETCH", "FOR", "WINDOW",
    };
    private sealed record Token(string Text, int Start, int End, int Depth, bool Identifier);

    public static SqlCompletionContext? Analyze(string sql, int caret)
    {
        if (caret < 0 || caret > sql.Length) throw new ArgumentOutOfRangeException(nameof(caret));
        // 先线性定位当前语句，避免为历史脚本的每个标记创建正则对象。
        var (statementStart, statementEnd) = StatementRange(sql, caret);
        var tokens = new List<Token>();
        int skippedUntil = 0, depth = 0;
        foreach (Match match in Tokens.Matches(sql, statementStart))
        {
            if (match.Index >= statementEnd) break;
            if (match.Index < skippedUntil) continue;
            int end = match.Index + match.Length;
            bool ignored = match.Groups["line"].Success || match.Groups["literal"].Success || match.Groups["block"].Success;
            if (match.Groups["block"].Success)
            {
                int nesting = 1;
                while (end < sql.Length && nesting > 0)
                {
                    if (end + 1 < sql.Length && sql[end] == '/' && sql[end + 1] == '*') { nesting++; end += 2; }
                    else if (end + 1 < sql.Length && sql[end] == '*' && sql[end + 1] == '/') { nesting--; end += 2; }
                    else end++;
                }
                skippedUntil = end;
            }
            if (ignored)
            {
                if (caret > match.Index && caret <= end) return null;
                continue;
            }
            if (match.Value == ")") depth = Math.Max(0, depth - 1);
            tokens.Add(new(match.Value, match.Index, end, depth, match.Groups["id"].Success));
            if (match.Value == "(") depth++;
        }

        var word = tokens.FirstOrDefault(t => t.Identifier && t.Start < caret && caret <= t.End);
        int start = word?.Start ?? caret, finish = word?.End ?? caret;
        string prefix = word is null ? string.Empty : Unquote(sql[start..caret]);
        if (start > 0 && sql[start - 1] is '@' or ':') return null;
        int before = tokens.FindLastIndex(t => t.End <= start);
        bool afterDot = before >= 0 && tokens[before].Text == ".";
        var qualifiers = new List<string>();
        int previous = before;
        if (afterDot)
        {
            previous--;
            while (previous >= 0 && tokens[previous].Identifier)
            {
                qualifiers.Insert(0, tokens[previous--].Text);
                if (previous < 0 || tokens[previous].Text != ".") break;
                previous--;
            }
        }
        bool expectsTable = previous >= 0 && tokens[previous].Text.ToUpperInvariant() is "FROM" or "JOIN" or "UPDATE" or "INTO" or "TABLE";

        var queries = new Stack<Token>();
        foreach (var token in tokens.TakeWhile(t => t.Start < caret))
        {
            if (token.Text == ")")
                while (queries.TryPeek(out var query) && query.Depth > token.Depth) queries.Pop();
            if (token.Text.Equals("SELECT", StringComparison.OrdinalIgnoreCase) || token.Text.Equals("UNION", StringComparison.OrdinalIgnoreCase))
            {
                while (queries.TryPeek(out var query) && query.Depth >= token.Depth) queries.Pop();
                if (token.Text.Equals("SELECT", StringComparison.OrdinalIgnoreCase)) queries.Push(token);
            }
        }
        queries.TryPeek(out var select);
        int scopeDepth = select?.Depth ?? 0;
        int scopeStart = select?.Start ?? statementStart;
        scopeStart = Math.Max(scopeStart, tokens.LastOrDefault(t => t.Depth == scopeDepth && t.End <= caret
            && t.Text.Equals("UNION", StringComparison.OrdinalIgnoreCase))?.End ?? 0);
        int scopeEnd = tokens.FirstOrDefault(t => t.Start >= caret && (t.Depth < scopeDepth
            || (t.Start > scopeStart && t.Depth == scopeDepth && t.Text.Equals("UNION", StringComparison.OrdinalIgnoreCase))))?.Start ?? statementEnd;
        var scope = tokens.Where(t => t.Start >= scopeStart && t.Start < scopeEnd && t.Depth == scopeDepth).ToList();
        var clause = scope.LastOrDefault(t => t.End <= start && Clauses.Contains(t.Text))?.Text.ToUpperInvariant();
        bool expectsColumn = clause is "SELECT" or "WHERE" or "ON" or "GROUP" or "ORDER" or "HAVING" or "SET" or "RETURNING";
        if (before >= 0 && tokens[before].Text == "," && clause == "FROM") expectsTable = true;

        var cteNames = new List<string>();
        for (int i = 0; i < tokens.Count; i++)
        {
            if (!tokens[i].Text.Equals("WITH", StringComparison.OrdinalIgnoreCase)) continue;
            int cteDepth = tokens[i].Depth;
            if (cteDepth > scopeDepth || tokens[i].Start > scopeStart
                || tokens.Skip(i + 1).Any(t => t.Start < scopeStart && t.Depth < cteDepth)) continue;
            int next = i + 1;
            if (next < tokens.Count && tokens[next].Text.Equals("RECURSIVE", StringComparison.OrdinalIgnoreCase)) next++;
            while (next < tokens.Count && tokens[next].Identifier)
            {
                cteNames.Add(tokens[next++].Text);
                // 跳过可选列名与 CTE 定义；逗号表示后续 CTE。
                while (next < tokens.Count && !(tokens[next].Text == ")" && tokens[next].Depth == cteDepth)) next++;
                if (next < tokens.Count) next++;
                if (next < tokens.Count && tokens[next].Text.Equals("AS", StringComparison.OrdinalIgnoreCase))
                {
                    while (next < tokens.Count && !(tokens[next].Text == ")" && tokens[next].Depth == cteDepth)) next++;
                    if (next < tokens.Count) next++;
                }
                if (next >= tokens.Count || tokens[next].Text != ",") break;
                next++;
            }
        }
        var tables = new List<SqlCompletionTable>();
        string? sourceClause = null;
        for (int i = 0; i < scope.Count - 1; i++)
        {
            string keyword = scope[i].Text.ToUpperInvariant();
            if (Clauses.Contains(keyword)) sourceClause = keyword;
            if (keyword is not ("FROM" or "JOIN" or "UPDATE" or "INTO") && !(keyword == "," && sourceClause == "FROM")) continue;
            int next = i + 1;
            var parts = new List<string>();
            if (scope[next].Text == "(")
            {
                while (next < scope.Count && scope[next].Text != ")") next++;
                next++;
            }
            else
            {
                while (next < scope.Count && scope[next].Identifier)
                {
                    parts.Add(scope[next++].Text);
                    if (next >= scope.Count || scope[next].Text != ".") break;
                    next++;
                }
            }
            if (next < scope.Count && scope[next].Text.Equals("AS", StringComparison.OrdinalIgnoreCase)) next++;
            string? alias = next < scope.Count && scope[next].Identifier && !AliasStops.Contains(scope[next].Text) ? scope[next].Text : null;
            if (parts.Count > 3) continue;
            // shortcut: 不推断 CTE/派生表的输出列，接入完整语义分析后再支持。
            bool cte = parts.Count == 1 && cteNames.Any(name =>
                string.Equals(Unquote(name), Unquote(parts[0]), StringComparison.OrdinalIgnoreCase));
            tables.Add(new(cte ? string.Empty : parts.LastOrDefault() ?? string.Empty,
                parts.Count >= 2 ? parts[^2] : null, parts.Count == 3 ? parts[0] : null,
                alias ?? (cte ? parts.LastOrDefault() : null)));
        }
        return new(start, finish, prefix, qualifiers, afterDot, expectsTable, expectsColumn, tables);
    }

    private static (int Start, int End) StatementRange(string sql, int caret)
    {
        int start = 0;
        for (int i = 0; i < sql.Length;)
        {
            char c = sql[i];
            if (c == '-' && i + 1 < sql.Length && sql[i + 1] == '-')
            {
                while (i < sql.Length && sql[i] is not ('\r' or '\n')) i++;
                continue;
            }
            if (c == '/' && i + 1 < sql.Length && sql[i + 1] == '*')
            {
                int nesting = 1;
                i += 2;
                while (i < sql.Length && nesting > 0)
                {
                    if (i + 1 < sql.Length && sql[i] == '/' && sql[i + 1] == '*') { nesting++; i += 2; }
                    else if (i + 1 < sql.Length && sql[i] == '*' && sql[i + 1] == '/') { nesting--; i += 2; }
                    else i++;
                }
                continue;
            }
            if (c is '\'' or '"' or '[' or '`')
            {
                char close = c == '[' ? ']' : c;
                i++;
                while (i < sql.Length)
                {
                    if (sql[i++] != close) continue;
                    if (i < sql.Length && sql[i] == close) { i++; continue; }
                    break;
                }
                continue;
            }
            if (char.IsLetter(c) || c == '_')
            {
                do { i++; } while (i < sql.Length && (char.IsLetterOrDigit(sql[i]) || sql[i] is '_' or '$' or '#'));
                continue;
            }
            if (c == '$')
            {
                int tagEnd = i + 1;
                while (tagEnd < sql.Length && (char.IsAsciiLetterOrDigit(sql[tagEnd]) || sql[tagEnd] == '_')) tagEnd++;
                if (tagEnd < sql.Length && sql[tagEnd] == '$'
                    && (tagEnd == i + 1 || char.IsAsciiLetter(sql[i + 1]) || sql[i + 1] == '_'))
                {
                    var tag = sql[i..(tagEnd + 1)];
                    int end = sql.IndexOf(tag, tagEnd + 1, StringComparison.Ordinal);
                    i = end < 0 ? sql.Length : end + tag.Length;
                    continue;
                }
            }
            if (c == ';')
            {
                if (i >= caret) return (start, i);
                start = i + 1;
            }
            i++;
        }
        return (start, sql.Length);
    }

    public static IEnumerable<DbObjectTreeNode> Objects(IEnumerable<DbObjectTreeNode>? roots, string connectionName, string? databaseName)
    {
        if (roots is null || string.IsNullOrWhiteSpace(connectionName)) return Enumerable.Empty<DbObjectTreeNode>();
        var connection = Connection(roots, connectionName);
        if (connection is null) return Enumerable.Empty<DbObjectTreeNode>();
        string? database = string.IsNullOrWhiteSpace(databaseName) ? connection.Connection?.Database : databaseName;
        var comparison = connection.Connection?.DatabaseType is "Postgres" or "KingbaseES" ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        return Descendants(connection).Where(n => n.NodeType == DbObjectTreeNodeType.DbObject && !n.IsPlaceholder
            && (string.IsNullOrWhiteSpace(database) || string.Equals(n.DatabaseName, database, comparison)));
    }

    public static DbObjectTreeNode? Connection(IEnumerable<DbObjectTreeNode>? roots, string connectionName)
        => roots?.SelectMany(root => new[] { root }.Concat(root.Children))
            .FirstOrDefault(n => n.NodeType == DbObjectTreeNodeType.Connection && n.Name.Equals(connectionName, StringComparison.OrdinalIgnoreCase));

    public static DbObjectTreeNode? ResolveTable(IEnumerable<DbObjectTreeNode>? roots, string connection, string? database,
        SqlCompletionTable table)
    {
        if (string.IsNullOrEmpty(table.Name)) return null;
        Enum.TryParse<DatabaseType>(Connection(roots, connection)?.Connection?.DatabaseType, true, out var databaseType);
        string? targetDatabase = table.Database is null ? database : Unquote(table.Database);
        string? schema = table.Schema;
        if (databaseType == DatabaseType.MySql && schema is not null)
        {
            if (table.Database is not null) return null;
            targetDatabase = Unquote(schema);
            schema = null;
        }
        if (table.Database is not null && databaseType is DatabaseType.Postgres or DatabaseType.KingbaseES)
        {
            var current = string.IsNullOrWhiteSpace(database) ? Connection(roots, connection)?.Connection?.Database : database;
            if (!string.Equals(current, targetDatabase, StringComparison.Ordinal)) return null;
        }
        var matches = Objects(roots, connection, targetDatabase)
            .Where(n => n.DatabaseObjectType is DatabaseObjectType.Table or DatabaseObjectType.View)
            .Where(n => IdentifierEquals(n.Name, table.Name, databaseType) && (schema is null || IdentifierEquals(n.Schema, schema, databaseType)))
            .Take(2).ToArray();
        return matches.Length == 1 ? matches[0] : null;
    }

    public static DbObjectTreeNode? TableAtDot(IEnumerable<DbObjectTreeNode>? roots, string connection, string? database, SqlCompletionContext context)
    {
        var parts = context.Qualifiers;
        if (!context.AfterDot || parts.Count is 0 or > 3 || context.ExpectsTable) return null;
        if (parts.Count == 1)
        {
            Enum.TryParse<DatabaseType>(Connection(roots, connection)?.Connection?.DatabaseType, true, out var databaseType);
            var reference = context.Tables.Where(t => AliasEquals(t.Alias ?? t.Name, parts[0], databaseType)).ToArray();
            if (reference.Length > 0) return reference.Length == 1 ? ResolveTable(roots, connection, database, reference[0]) : null;
            if (context.Tables.Count > 0) return null;
        }
        return ResolveTable(roots, connection, database, new(parts[^1], parts.Count >= 2 ? parts[^2] : null, parts.Count == 3 ? parts[0] : null, null));
    }

    public static string Unquote(string value)
    {
        if (value.Length == 0 || value[0] is not ('[' or '"' or '`')) return value;
        char close = value[0] == '[' ? ']' : value[0];
        var body = value[1..];
        if (body.EndsWith(close)) body = body[..^1];
        return body.Replace(new string(close, 2), close.ToString(), StringComparison.Ordinal);
    }

    public static bool IdentifierEquals(string? value, string identifier, DatabaseType databaseType = DatabaseType.Unknown)
    {
        if (IsQuoted(identifier)) return string.Equals(value, Unquote(identifier), StringComparison.Ordinal);
        return databaseType switch
        {
            DatabaseType.Postgres or DatabaseType.KingbaseES => string.Equals(value, identifier.ToLowerInvariant(), StringComparison.Ordinal),
            DatabaseType.Oracle or DatabaseType.DM => string.Equals(value, identifier.ToUpperInvariant(), StringComparison.Ordinal),
            _ => string.Equals(value, identifier, StringComparison.OrdinalIgnoreCase),
        };
    }

    public static bool AliasEquals(string declared, string identifier, DatabaseType databaseType)
    {
        string name = Unquote(declared);
        if (!IsQuoted(declared))
            name = databaseType is DatabaseType.Postgres or DatabaseType.KingbaseES ? name.ToLowerInvariant()
                : databaseType is DatabaseType.Oracle or DatabaseType.DM ? name.ToUpperInvariant() : name;
        return IdentifierEquals(name, identifier, databaseType);
    }

    private static bool IsQuoted(string value) => value.Length > 0 && value[0] is '[' or '"' or '`';

    private static IEnumerable<DbObjectTreeNode> Descendants(DbObjectTreeNode node)
    {
        foreach (var child in node.Children)
        {
            yield return child;
            if (child.Children.Count == 0) continue;
            foreach (var descendant in Descendants(child)) yield return descendant;
        }
    }
}
