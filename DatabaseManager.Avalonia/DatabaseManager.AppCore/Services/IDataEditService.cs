using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DatabaseManager.AppCore.Models;

namespace DatabaseManager.AppCore.Services;

/// <summary>
/// 数据编辑服务。封装表数据的加载（含列/主键元数据）与增删改保存（事务内）。
/// 实现复用 <c>DatabaseInterpreter</c> 与 <c>DbScriptGenerator</c>。
/// </summary>
public interface IDataEditService
{
    /// <summary>
    /// 加载指定表的元数据（列、主键、标识列）与指定页的数据。
    /// </summary>
    /// <param name="connectionName">连接名称。</param>
    /// <param name="databaseName">目标数据库名。</param>
    /// <param name="tableName">表名。</param>
    /// <param name="schema">Schema（可为空）。</param>
    /// <param name="isView">是否为视图。</param>
    /// <param name="pageSize">每页行数。</param>
    /// <param name="pageNumber">页码（从 1 开始）。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    Task<DataLoadResult> LoadDataAsync(
        string connectionName,
        string databaseName,
        string tableName,
        string? schema,
        bool isView,
        int pageSize,
        long pageNumber,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 保存数据编辑结果（新增 / 修改 / 删除），在单个事务中执行并提交。
    /// </summary>
    /// <param name="connectionName">连接名称。</param>
    /// <param name="databaseName">目标数据库名。</param>
    /// <param name="tableName">表名。</param>
    /// <param name="schema">Schema（可为空）。</param>
    /// <param name="inserts">待新增的行。</param>
    /// <param name="updates">待修改的行。</param>
    /// <param name="deletes">待删除的行（含原始值）。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    Task<DataSaveResult> SaveChangesAsync(
        string connectionName,
        string databaseName,
        string tableName,
        string? schema,
        IReadOnlyList<DataEditRow> inserts,
        IReadOnlyList<DataEditRow> updates,
        IReadOnlyList<DataEditRow> deletes,
        CancellationToken cancellationToken = default);
}

/// <summary>数据加载结果。</summary>
public class DataLoadResult
{
    /// <summary>表元数据（列定义、主键等）。</summary>
    public DataTableInfo TableInfo { get; set; } = new();

    /// <summary>当前页的行数据。</summary>
    public IReadOnlyList<DataEditRow> Rows { get; set; } = System.Array.Empty<DataEditRow>();

    /// <summary>总行数。</summary>
    public long TotalCount { get; set; }

    /// <summary>错误信息（若失败）。</summary>
    public string? ErrorMessage { get; set; }

    /// <summary>是否成功。</summary>
    public bool IsSuccess => string.IsNullOrEmpty(ErrorMessage);
}

/// <summary>数据保存结果。</summary>
public class DataSaveResult
{
    /// <summary>是否成功。</summary>
    public bool IsSuccess { get; set; }

    /// <summary>错误信息（若失败）。</summary>
    public string? ErrorMessage { get; set; }

    /// <summary>受影响行数（成功时）。</summary>
    public int RowCount { get; set; }
}
