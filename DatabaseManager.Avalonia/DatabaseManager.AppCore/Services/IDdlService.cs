using System.Threading;
using System.Threading.Tasks;
using DatabaseInterpreter.Model;
using DatabaseManager.AppCore.Models;

namespace DatabaseManager.AppCore.Services;

/// <summary>
/// 执行数据库 DDL：删除（Drop）/ 重命名（Rename）/ 预览定义。
/// 与对象树右键菜单中的「删除 / 重命名 / 新建 / 查看定义」对齐。
/// </summary>
public interface IDdlService
{
    /// <summary>生成对象删除脚本（仅预览，不执行）。</summary>
    DdlScriptResult PreviewDrop(string connectionName, string databaseName, DatabaseObject dbObject);

    /// <summary>在事务内删除指定数据库对象（表/视图/函数/存储过程/触发器/列/索引/主键/外键/约束）。</summary>
    Task<DdlExecuteResult> DropAsync(string connectionName, string databaseName, DatabaseObject dbObject, CancellationToken ct = default);

    /// <summary>重命名表。</summary>
    Task<DdlExecuteResult> RenameTableAsync(string connectionName, string databaseName, Table table, string newName, CancellationToken ct = default);

    /// <summary>重命名列。</summary>
    Task<DdlExecuteResult> RenameTableColumnAsync(string connectionName, string databaseName, Table table, TableColumn column, string newName, CancellationToken ct = default);

    /// <summary>
    /// 生成新建对象的通用 SQL 模板。不同数据库语法有差异，生成一个可编辑占位的通用模板。
    /// </summary>
    DdlScriptResult GetCreateTemplate(DatabaseObjectType objectType, string? schema);

    /// <summary>
    /// 读取已有对象（视图/函数/存储过程/触发器）的完整 CREATE 定义脚本。
    /// </summary>
    Task<DdlScriptResult> GetObjectDefinitionAsync(string connectionName, string databaseName, DatabaseObject dbObject, CancellationToken ct = default);
}
