using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DatabaseManager.AppCore.Common;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

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
            var obj = JsonConvert.DeserializeObject(JsonText);
            JsonText = JsonConvert.SerializeObject(obj, Formatting.Indented);
            StatusMessage = "格式化完成。";
            BuildTree();
        }
        catch (Exception ex)
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
            var token = JToken.Parse(JsonText);
            var root = CreateNode(null, token);
            TreeNodes.Add(root);
            StatusMessage = "已加载 JSON 树。";
        }
        catch (Exception ex)
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

    private static JsonTreeNode CreateNode(string? name, JToken token)
    {
        string display;
        var node = new JsonTreeNode();
        node.Name = name;

        switch (token.Type)
        {
            case JTokenType.Object:
                display = name is null ? "{}" : $"{name}: {{}}";
                node.DisplayText = display;
                node.ValueText = "{}";
                foreach (var prop in ((JObject)token).Properties())
                {
                    node.Children.Add(CreateNode(prop.Name, prop.Value));
                }
                break;
            case JTokenType.Array:
                display = name is null ? "[]" : $"{name}: []";
                node.DisplayText = display;
                node.ValueText = "[]";
                int idx = 0;
                foreach (var child in ((JArray)token).Children())
                {
                    node.Children.Add(CreateNode($"[{idx}]", child));
                    idx++;
                }
                break;
            default:
                var jValue = token as JValue;
                object? v = jValue?.Value;
                string valueStr;
                if (v is null) valueStr = "null";
                else if (v is string s) valueStr = $"\"{s}\"";
                else if (v is bool b) valueStr = b ? "true" : "false";
                else valueStr = v.ToString() ?? string.Empty;

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
