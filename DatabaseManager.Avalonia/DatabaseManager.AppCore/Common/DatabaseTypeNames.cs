using DatabaseInterpreter.Model;

namespace DatabaseManager.AppCore.Common;

/// <summary>
/// 连接配置 DatabaseType 字符串常量，与 <see cref="DatabaseType"/> 成员名保持同源，
/// 供方言判断使用，避免散落的字符串字面量产生拼写漂移。
/// </summary>
public static class DatabaseTypeNames
{
    public const string SqlServer = nameof(DatabaseType.SqlServer);
    public const string MySql = nameof(DatabaseType.MySql);
    public const string Oracle = nameof(DatabaseType.Oracle);
    public const string Postgres = nameof(DatabaseType.Postgres);
    public const string Sqlite = nameof(DatabaseType.Sqlite);
    public const string KingbaseES = nameof(DatabaseType.KingbaseES);
    public const string DuckDB = nameof(DatabaseType.DuckDB);
    public const string DM = nameof(DatabaseType.DM);
}