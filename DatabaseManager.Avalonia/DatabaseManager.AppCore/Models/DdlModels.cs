namespace DatabaseManager.AppCore.Models;

/// <summary>对象脚本类型（Generate SQL 子菜单）。</summary>
public enum ObjectScriptType
{
    /// <summary>SELECT * 全列查询。</summary>
    Select,

    /// <summary>SELECT TOP N 查询（按方言生成 LIMIT / TOP / FETCH FIRST）。</summary>
    SelectTopN,

    /// <summary>INSERT 语句模板（基于真实列结构，排除自增/计算列）。</summary>
    Insert,

    /// <summary>UPDATE 语句模板（基于真实列结构与主键）。</summary>
    Update,

    /// <summary>DELETE 语句模板（基于主键生成 WHERE 条件）。</summary>
    Delete,

    /// <summary>CREATE TABLE 建表脚本（由各数据库方言脚本生成器产出）。</summary>
    CreateTable,
}

/// <summary>DDL 脚本预览结果。</summary>
public class DdlScriptResult
{
    public bool IsSuccess { get; set; }
    public string? Script { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>DDL 执行结果。</summary>
public class DdlExecuteResult
{
    public bool IsSuccess { get; set; }
    public string? Script { get; set; }
    public string? ErrorMessage { get; set; }
    public int AffectedCount { get; set; }
}
