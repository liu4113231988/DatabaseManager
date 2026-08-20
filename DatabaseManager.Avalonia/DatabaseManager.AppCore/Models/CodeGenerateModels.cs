using CommunityToolkit.Mvvm.ComponentModel;

namespace DatabaseManager.AppCore.Models;

/// <summary>
/// 代码生成语言选项（UI 友好）。
/// </summary>
public sealed record CodeGenerateLanguageOption(string DisplayName, string Value);

/// <summary>
/// 可选择的表/视图对象（UI 友好，支持勾选）。
/// </summary>
public partial class CodeGenerateTarget : ObservableObject
{
    /// <summary>对象类型（表 / 视图）。</summary>
    public string ObjectType { get; }

    /// <summary>对象名。</summary>
    public string Name { get; }

    /// <summary>Schema。</summary>
    public string? Schema { get; }

    /// <summary>展示名。</summary>
    public string DisplayName { get; }

    /// <summary>是否勾选。</summary>
    [ObservableProperty]
    private bool _isChecked;

    public CodeGenerateTarget(string objectType, string name, string? schema)
    {
        ObjectType = objectType;
        Name = name;
        Schema = schema;
        DisplayName = string.IsNullOrEmpty(schema) ? name : $"{schema}.{name}";
    }
}

/// <summary>
/// 代码生成结果（UI 友好）。
/// </summary>
public class CodeGenerateResultItem
{
    /// <summary>是否成功。</summary>
    public bool IsOK { get; }

    /// <summary>消息。</summary>
    public string Message { get; }

    public CodeGenerateResultItem(bool isOK, string message)
    {
        IsOK = isOK;
        Message = message ?? string.Empty;
    }
}
