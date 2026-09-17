using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace DatabaseManager.Avalonia.Views.Workbench;

/// <summary>
/// 数据工作台各页面中"动态生成"部分的轻量构建辅助（如按表结构逐列生成的规则行）。
/// 页面静态布局一律写在各自窗口的 axaml 中，这里只服务于运行时生成的内容。
/// </summary>
public static class WorkbenchBuilders
{
    public static StackPanel Panel(params Control[] controls)
    {
        var panel = new StackPanel { Spacing = 2, Margin = new Thickness(0, 2) };
        foreach (var control in controls) panel.Children.Add(control);
        return panel;
    }

    public static WrapPanel Row(params Control[] controls)
    {
        var row = new WrapPanel { Orientation = Orientation.Horizontal };
        foreach (var control in controls) { control.Margin = new Thickness(0, 0, 8, 2); row.Children.Add(control); }
        return row;
    }

    public static TextBlock Label(string value) => new() { Text = value, VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap };

    public static TextBox Text(string value = "", int width = 170) => new() { Text = value, Width = width, VerticalContentAlignment = VerticalAlignment.Center };

    public static ComboBox Choice(params string[] choices) => new() { ItemsSource = choices, SelectedIndex = 0, MinWidth = 105 };

    public static string Chosen(ComboBox combo) => combo.SelectedItem?.ToString() ?? "";
}
