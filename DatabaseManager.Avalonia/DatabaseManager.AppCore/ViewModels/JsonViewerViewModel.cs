using System.Collections.ObjectModel;
using System.Text;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DatabaseManager.AppCore.Common;

namespace DatabaseManager.AppCore.ViewModels;

/// <summary>
/// JSON 查看器 ViewModel（工具菜单）。
/// 对应原 WinForms frmJsonViewer：输入/粘贴 JSON，格式化，树形展示。
/// </summary>
public partial class JsonViewerViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _jsonText = string.Empty;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    public ObservableCollection<JsonTreeNode> TreeNodes { get; } = new();

    [RelayCommand]
    private void Format()
    {
        if (string.IsNullOrWhiteSpace(JsonText))
        {
            StatusMessage = "请输入 JSON 内容。";
            return;
        }
        try
        {
            using var document = JsonDocument.Parse(JsonText);
            using var buffer = new MemoryStream();
            using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = true }))
            {
                document.RootElement.WriteTo(writer);
            }
            JsonText = Encoding.UTF8.GetString(buffer.ToArray());
            StatusMessage = "格式化完成。";
            BuildTree();
        }
        catch (JsonException ex)
        {
            StatusMessage = $"格式化失败：{ex.Message}";
        }
    }

    [RelayCommand]
    private void BuildTree()
    {
        TreeNodes.Clear();
        if (string.IsNullOrWhiteSpace(JsonText))
        {
            StatusMessage = "请输入 JSON 内容。";
            return;
        }
        try
        {
            using var document = JsonDocument.Parse(JsonText);
            var root = CreateNode(null, document.RootElement);
            TreeNodes.Add(root);
            StatusMessage = "已加载 JSON 树。";
        }
        catch (JsonException ex)
        {
            StatusMessage = $"解析失败：{ex.Message}";
        }
    }

    [RelayCommand]
    private void Clear()
    {
        JsonText = string.Empty;
        TreeNodes.Clear();
        StatusMessage = string.Empty;
    }

    private static JsonTreeNode CreateNode(string? name, JsonElement token)
    {
        string display;
        var node = new JsonTreeNode { Name = name };

        switch (token.ValueKind)
        {
            case JsonValueKind.Object:
                display = name is null ? "{}" : $"{name}: {{}}";
                node.DisplayText = display;
                node.ValueText = "{}";
                foreach (var prop in token.EnumerateObject())
                {
                    node.Children.Add(CreateNode(prop.Name, prop.Value));
                }
                break;
            case JsonValueKind.Array:
                display = name is null ? "[]" : $"{name}: []";
                node.DisplayText = display;
                node.ValueText = "[]";
                int idx = 0;
                foreach (var child in token.EnumerateArray())
                {
                    node.Children.Add(CreateNode($"[{idx}]", child));
                    idx++;
                }
                break;
            default:
                string valueStr = token.ValueKind switch
                {
                    JsonValueKind.Null or JsonValueKind.Undefined => "null",
                    JsonValueKind.String => $"\"{token.GetString()}\"",
                    JsonValueKind.True => "true",
                    JsonValueKind.False => "false",
                    _ => token.GetRawText(),
                };
                node.ValueText = valueStr;
                node.DisplayText = name is null ? valueStr : $"\"{name}\": {valueStr}";
                break;
        }

        return node;
    }
}

/// <summary>JSON 树节点（用于 Avalonia TreeView）。</summary>
public partial class JsonTreeNode : ObservableObject
{
    [ObservableProperty]
    private string? _name;

    [ObservableProperty]
    private string _displayText = string.Empty;

    [ObservableProperty]
    private string _valueText = string.Empty;

    public ObservableCollection<JsonTreeNode> Children { get; } = new();

    public bool HasChildren => Children.Count > 0;
}
