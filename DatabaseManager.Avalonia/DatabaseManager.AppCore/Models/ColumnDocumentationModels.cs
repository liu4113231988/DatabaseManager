namespace DatabaseManager.AppCore.Models;

/// <summary>
/// 文档列属性选项（UI 友好）。对应 <c>TableColumnProperty</c> 枚举的展示项。
/// </summary>
public partial class ColumnDocumentationProperty : CommunityToolkit.Mvvm.ComponentModel.ObservableObject
{
    /// <summary>属性名（对应枚举名）。</summary>
    public string PropertyName { get; }

    /// <summary>展示名。</summary>
    public string DisplayName { get; }

    /// <summary>是否勾选。</summary>
    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
    private bool _isChecked;

    public ColumnDocumentationProperty(string propertyName, string displayName)
    {
        PropertyName = propertyName;
        DisplayName = displayName;
        IsChecked = true;
    }
}

/// <summary>
/// 文档生成结果（UI 友好）。
/// </summary>
public class ColumnDocumentationResultItem
{
    /// <summary>是否成功。</summary>
    public bool IsOK { get; }

    /// <summary>消息。</summary>
    public string Message { get; }

    /// <summary>生成的文档路径。</summary>
    public string FilePath { get; }

    public ColumnDocumentationResultItem(bool isOK, string message, string filePath)
    {
        IsOK = isOK;
        Message = message ?? string.Empty;
        FilePath = filePath ?? string.Empty;
    }
}
