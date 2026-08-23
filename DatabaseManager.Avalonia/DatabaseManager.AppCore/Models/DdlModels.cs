namespace DatabaseManager.AppCore.Models;

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
