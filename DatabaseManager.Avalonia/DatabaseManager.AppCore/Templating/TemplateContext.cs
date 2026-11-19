using System.Globalization;
using DatabaseInterpreter.Model;

namespace DatabaseManager.AppCore.Templating;

/// <summary>
/// 模板渲染上下文（T2）：连接、库、schema、表、列、时间与方言等内置变量。
/// 所有键均为小写，和 {name} 占位符语法一致；显式传入的变量优先于上下文。
/// </summary>
public sealed class TemplateContext
{
    public string ConnectionName { get; set; } = string.Empty;
    public string DatabaseName { get; set; } = string.Empty;
    public string Schema { get; set; } = string.Empty;
    public string Table { get; set; } = string.Empty;
    public string Column { get; set; } = string.Empty;
    public DatabaseType Dialect { get; set; } = DatabaseType.Unknown;
    public DateTime Timestamp { get; set; } = DateTime.Now;

    public static TemplateContext CreateDefault() => new();

    public IReadOnlyDictionary<string, string> ToVariables()
    {
        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["connection"] = ConnectionName,
            ["database"] = DatabaseName,
            ["schema"] = Schema,
            ["table"] = Table,
            ["column"] = Column,
            ["dialect"] = Dialect.ToString(),
            ["date"] = Timestamp.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ["time"] = Timestamp.ToString("HH:mm:ss", CultureInfo.InvariantCulture),
            ["datetime"] = Timestamp.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
            ["year"] = Timestamp.Year.ToString(CultureInfo.InvariantCulture),
        };
    }
}
