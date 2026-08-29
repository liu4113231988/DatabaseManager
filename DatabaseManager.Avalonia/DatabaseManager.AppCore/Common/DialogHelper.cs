using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using Avalonia.Media;

namespace DatabaseManager.AppCore.Common;

/// <summary>
/// 统一的对话框辅助工具（确认 / 输入 / 提示）。
/// 供 ObjectTreeContextMenuBuilder 等非 Window 类使用；Window 内的轻量提示可继续使用 MsBox.Avalonia。
/// </summary>
public static class DialogHelper
{
    /// <summary>显示确认对话框。返回 true 表示用户点击了「确定」，否则为 false；无法获取主窗口时返回 null。</summary>
    public static async Task<bool?> ShowConfirmAsync(string title, string message)
    {
        var mainWindow = GetMainWindow();
        if (mainWindow is null) return null;

        var dialog = new ContentDialog
        {
            Title = title,
            Content = message,
            PrimaryButtonText = "确定",
            SecondaryButtonText = "取消",
        };
        var result = await dialog.ShowAsync(mainWindow);
        return result == ContentDialog.ContentDialogResult.Primary;
    }

    /// <summary>显示输入对话框，返回用户输入内容（取消时返回 null）。</summary>
    public static async Task<string?> ShowInputAsync(string title, string message, string defaultValue = "")
    {
        var mainWindow = GetMainWindow();
        if (mainWindow is null) return null;

        var inputDialog = new InputDialog(title, message, defaultValue);
        return await inputDialog.ShowAsync(mainWindow);
    }

    private static Window? GetMainWindow()
        => Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime lifetime
            ? lifetime.MainWindow
            : null;
}

#region 简单对话框实现

internal class ContentDialog : Control
{
    public string Title { get; set; } = string.Empty;
    public object? Content { get; set; }
    public string PrimaryButtonText { get; set; } = "确定";
    public string SecondaryButtonText { get; set; } = "取消";

    public async Task<ContentDialogResult> ShowAsync(Window parent)
    {
        var window = new Window
        {
            Title = Title,
            Width = 400,
            Height = 200,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
        };

        var panel = new StackPanel { Margin = new Thickness(16), Spacing = 12 };

        var textBlock = new TextBlock { Text = Content?.ToString(), TextWrapping = TextWrapping.Wrap };
        panel.Children.Add(textBlock);

        var buttonPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Spacing = 8 };

        var okButton = new Button { Content = PrimaryButtonText, MinWidth = 80, Padding = new Thickness(12, 6) };
        var cancelBtn = new Button { Content = SecondaryButtonText, MinWidth = 80, Padding = new Thickness(12, 6) };

        ContentDialogResult result = ContentDialogResult.None;

        okButton.Click += (_, _) => { result = ContentDialogResult.Primary; window.Close(); };
        cancelBtn.Click += (_, _) => { result = ContentDialogResult.Secondary; window.Close(); };

        buttonPanel.Children.Add(okButton);
        buttonPanel.Children.Add(cancelBtn);
        panel.Children.Add(buttonPanel);

        window.Content = panel;
        await window.ShowDialog(parent);

        return result;
    }

    internal enum ContentDialogResult { None, Primary, Secondary }
}

internal class InputDialog : Window
{
    private readonly TextBox _textBox;
    private string? _result;

    public InputDialog(string title, string message, string defaultValue = "")
    {
        Title = title;
        Width = 450;
        Height = 200;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        CanResize = false;

        var panel = new StackPanel { Margin = new Thickness(16), Spacing = 12 };

        var textBlock = new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap };
        panel.Children.Add(textBlock);

        _textBox = new TextBox { Text = defaultValue, MinWidth = 300 };
        panel.Children.Add(_textBox);

        var buttonPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Spacing = 8 };

        var okButton = new Button { Content = "确定", MinWidth = 80, Padding = new Thickness(12, 6) };
        var cancelBtn = new Button { Content = "取消", MinWidth = 80, Padding = new Thickness(12, 6) };

        okButton.Click += (_, _) => { _result = _textBox.Text; Close(); };
        cancelBtn.Click += (_, _) => { _result = null; Close(); };

        buttonPanel.Children.Add(okButton);
        buttonPanel.Children.Add(cancelBtn);
        panel.Children.Add(buttonPanel);

        Content = panel;
    }

    public new async Task<string?> ShowAsync(Window parent)
    {
        await ShowDialog(parent);
        return _result;
    }
}

#endregion
