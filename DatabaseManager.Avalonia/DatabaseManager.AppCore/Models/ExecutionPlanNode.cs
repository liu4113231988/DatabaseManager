namespace DatabaseManager.AppCore.Models;

public sealed class ExecutionPlanNode
{
    public string Operation { get; init; } = string.Empty;
    public string ObjectName { get; init; } = string.Empty;
    public double? Cost { get; init; }
    public double? Milliseconds { get; init; }
    public string Details { get; init; } = string.Empty;
    public bool IsHotspot { get; set; }
    public List<ExecutionPlanNode> Children { get; } = new();
    public string Label => $"{(IsHotspot ? "🔥 " : "")}{Operation} {ObjectName}" +
        (Cost.HasValue ? $" · 成本 {Cost:0.###}" : "") +
        (Milliseconds.HasValue ? $" · {Milliseconds:0.###} ms/循环" : "");
}
