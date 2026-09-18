using DatabaseManager.AppCore.Models;

namespace DatabaseManager.AppCore.Common;

/// <summary>
/// 应用内 URI 直达对象树：dbm://&lt;连接名&gt;/&lt;数据库&gt;/[&lt;schema&gt;/]&lt;类型&gt;/&lt;对象名&gt;[?rows=N]
/// 类型：table / view / procedure / function / sequence（大小写不敏感）。
/// 含空格或特殊字符的段需做 %XX（URL）转义。
/// </summary>
public class DbObjectUri
{
    public const string Scheme = "dbm://";

    /// <summary>连接名。</summary>
    public string ConnectionName { get; init; } = string.Empty;

    /// <summary>数据库名。</summary>
    public string DatabaseName { get; init; } = string.Empty;

    /// <summary>Schema 名（无 schema 的数据库可省略该段）。</summary>
    public string? Schema { get; init; }

    /// <summary>对象类别。</summary>
    public SearchObjectKind Kind { get; init; }

    /// <summary>对象名。</summary>
    public string ObjectName { get; init; } = string.Empty;

    /// <summary>可选：直达后预览行数（?rows=N，当前版本保留字段）。</summary>
    public int? Rows { get; init; }

    /// <summary>解析 dbm:// URI；失败时给出面向用户的错误说明。</summary>
    public static bool TryParse(string? text, out DbObjectUri? uri, out string? error)
    {
        uri = null;
        error = null;

        if (string.IsNullOrWhiteSpace(text))
        {
            error = "URI 为空。";
            return false;
        }

        var value = text.Trim();
        if (!value.StartsWith(Scheme, StringComparison.OrdinalIgnoreCase))
        {
            error = $"URI 必须以 {Scheme} 开头。";
            return false;
        }

        var rest = value[Scheme.Length..].TrimStart('/');

        // 去掉可选查询参数 ?rows=N。
        int? rows = null;
        int queryIndex = rest.IndexOf('?');
        if (queryIndex >= 0)
        {
            var query = rest[(queryIndex + 1)..];
            rest = rest[..queryIndex];

            foreach (var pair in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                var kv = pair.Split('=', 2);
                if (kv.Length == 2
                    && kv[0].Equals("rows", StringComparison.OrdinalIgnoreCase)
                    && int.TryParse(kv[1], out var rowCount) && rowCount > 0)
                {
                    rows = rowCount;
                }
            }
        }

        // 段数：4 = 连接/库/类型/对象名；5 = 连接/库/schema/类型/对象名。
        var segments = rest.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length is not (4 or 5))
        {
            error = "URI 段数不正确，应为 dbm://<连接名>/<数据库>/[<schema>/]<类型>/<对象名>。";
            return false;
        }

        var parts = segments.Select(Uri.UnescapeDataString).ToArray();
        var kind = ParseKind(parts[^2]);
        if (kind is null)
        {
            error = $"不支持的对象类型「{parts[^2]}」，可用：table / view / procedure / function / sequence。";
            return false;
        }

        uri = new DbObjectUri
        {
            ConnectionName = parts[0],
            DatabaseName = parts[1],
            Schema = parts.Length == 5 ? parts[2] : null,
            Kind = kind.Value,
            ObjectName = parts[^1],
            Rows = rows,
        };

        return true;
    }

    private static SearchObjectKind? ParseKind(string text) => text.Trim().ToLowerInvariant() switch
    {
        "table" or "tables" => SearchObjectKind.Table,
        "view" or "views" => SearchObjectKind.View,
        "procedure" or "procedures" or "proc" => SearchObjectKind.Procedure,
        "function" or "functions" or "func" => SearchObjectKind.Function,
        "sequence" or "sequences" or "seq" => SearchObjectKind.Sequence,
        _ => null,
    };
}
