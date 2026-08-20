using System.Threading;
using System.Threading.Tasks;
using DatabaseManager.AppCore.Models;

namespace DatabaseManager.AppCore.Services;

/// <summary>
/// 表设计服务。封装表结构（列/主键/索引/外键/约束）的加载与 CREATE/ALTER 脚本生成与执行。
/// </summary>
public interface ITableDesignService
{
    /// <summary>
    /// 加载指定表的完整结构定义。isNew=true 时仅返回空壳（列集合为空），供新建表设计使用。
    /// </summary>
    Task<TableDesignLoadResult> LoadTableAsync(
        string connectionName,
        string databaseName,
        string tableName,
        string? schema,
        bool isNew,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 根据设计信息生成 CREATE（新建）或 ALTER（修改）脚本并返回脚本内容（不执行）。
    /// </summary>
    Task<TableDesignScriptResult> GenerateScriptsAsync(
        string connectionName,
        string databaseName,
        TableDesignInfo design,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 生成脚本并在单事务内顺序执行（保存表结构）。返回执行结果。
    /// </summary>
    Task<TableDesignSaveResult> SaveAsync(
        string connectionName,
        string databaseName,
        TableDesignInfo design,
        CancellationToken cancellationToken = default);
}

/// <summary>表设计加载结果。</summary>
public class TableDesignLoadResult
{
    public bool IsSuccess { get; set; }

    public string ErrorMessage { get; set; } = string.Empty;

    public TableDesignInfo Design { get; set; } = new();
}

/// <summary>表设计脚本生成结果。</summary>
public class TableDesignScriptResult
{
    public bool IsSuccess { get; set; }

    public string ErrorMessage { get; set; } = string.Empty;

    /// <summary>生成的 SQL 脚本内容（多条以分隔符连接）。</summary>
    public string Script { get; set; } = string.Empty;

    /// <summary>是否包含需要执行的脚本（false 表示无改动）。</summary>
    public bool HasScripts { get; set; }
}

/// <summary>表设计保存结果。</summary>
public class TableDesignSaveResult
{
    public bool IsSuccess { get; set; }

    public string ErrorMessage { get; set; } = string.Empty;

    /// <summary>执行影响的脚本条数。</summary>
    public int ScriptCount { get; set; }
}
