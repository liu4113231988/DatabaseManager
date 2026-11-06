using DatabaseConverter.Core;
using DatabaseInterpreter.Model;

namespace DatabaseManager.AppCore.Common;

/// <summary>编辑器 SQL 排版，使用连接方言并在无法保真时保留原文。</summary>
public static class SqlFormatter
{
    public static string Format(string sql, DatabaseType databaseType = DatabaseType.Unknown)
        => SqlFormattingHelper.Format(sql, databaseType, out _) ?? string.Empty;
}
