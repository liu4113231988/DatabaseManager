using System.Globalization;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using DatabaseManager.AppCore.Services;

namespace DatabaseManager.Avalonia.Views;

/// <summary>
/// 统一设置窗口：外观、删除确认、会话恢复与查询执行策略。
/// 编辑在保存时才写回 <see cref="AppSettings"/> 并持久化，取消不会改变现有设置。
/// </summary>
public partial class SettingsWindow : Window
{
    private readonly IAppSettingsService _settingsService = null!;

    public SettingsWindow()
    {
        InitializeComponent();
        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                Close();
                e.Handled = true;
            }
        };
    }

    public SettingsWindow(IAppSettingsService settingsService)
        : this()
    {
        _settingsService = settingsService;
        LoadValues(settingsService.Settings);
    }

    /// <summary>用户是否已保存设置；调用方据此决定是否重新应用主题与缩放。</summary>
    public bool Saved { get; private set; }

    private void LoadValues(AppSettings settings)
    {
        if (!SelectByTag(CmbTheme, settings.ThemeMode) && CmbTheme.ItemCount > 0)
            CmbTheme.SelectedIndex = 0;
        SelectFontScale(settings.FontScale);
        ChkDeleteConfirm.IsChecked = settings.RequireDeleteNameConfirmation;
        ChkAutoSaveSession.IsChecked = settings.AutoSaveSession;
        ChkDangerousConfirm.IsChecked = settings.ConfirmDangerousSql;
        TxtMaxRows.Text = settings.QueryResultMaxRows.ToString(CultureInfo.InvariantCulture);
        TxtCommandTimeout.Text = settings.QueryCommandTimeoutSeconds.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>选中预设缩放档位；手工配置的非预设值以「自定义」项保留，避免保存时被重置。</summary>
    private void SelectFontScale(double scale)
    {
        for (int i = CmbFontScale.ItemCount - 1; i >= 0; i--)
        {
            if (CmbFontScale.Items[i] is ComboBoxItem { Name: "custom-font-scale" })
                CmbFontScale.Items.RemoveAt(i);
        }

        string tag = scale.ToString(CultureInfo.InvariantCulture);
        if (SelectByTag(CmbFontScale, tag))
            return;

        var custom = new ComboBoxItem
        {
            Name = "custom-font-scale",
            Content = $"自定义 ({scale * 100:0.#}%)",
            Tag = tag,
        };
        CmbFontScale.Items.Add(custom);
        CmbFontScale.SelectedItem = custom;
    }

    private void BtnSave_Click(object? sender, RoutedEventArgs e)
    {
        var settings = _settingsService.Settings;

        settings.ThemeMode = ReadTag(CmbTheme) ?? ThemeModes.System;
        settings.FontScale = ReadNumber(CmbFontScale, settings.FontScale);
        settings.RequireDeleteNameConfirmation = ChkDeleteConfirm.IsChecked == true;
        settings.AutoSaveSession = ChkAutoSaveSession.IsChecked == true;
        settings.ConfirmDangerousSql = ChkDangerousConfirm.IsChecked == true;
        settings.QueryResultMaxRows = (int)ParseNumber(TxtMaxRows.Text, settings.QueryResultMaxRows, 1000, 1_000_000);
        settings.QueryCommandTimeoutSeconds = (int)ParseNumber(TxtCommandTimeout.Text, settings.QueryCommandTimeoutSeconds, 0, 3600);
        settings.Normalize();

        _settingsService.Save();
        Saved = true;
        Close();
    }

    private void BtnReset_Click(object? sender, RoutedEventArgs e)
    {
        // 仅重置本地控件，用户仍需点击「保存」才会写回。
        LoadValues(new AppSettings());
    }

    private void BtnCancel_Click(object? sender, RoutedEventArgs e) => Close();

    private static bool SelectByTag(ComboBox comboBox, string tag)
    {
        for (int i = 0; i < comboBox.ItemCount; i++)
        {
            if (comboBox.Items[i] is ComboBoxItem item
                && string.Equals(item.Tag?.ToString(), tag, StringComparison.OrdinalIgnoreCase))
            {
                comboBox.SelectedIndex = i;
                return true;
            }
        }
        return false;
    }

    private static string? ReadTag(ComboBox comboBox)
        => (comboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString();

    private static double ReadNumber(ComboBox comboBox, double fallback)
        => double.TryParse(ReadTag(comboBox), NumberStyles.Float, CultureInfo.InvariantCulture, out double value)
            ? value
            : fallback;

    private static double ParseNumber(string? text, double fallback, double min, double max)
        => double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double value)
            ? Math.Clamp(value, min, max)
            : fallback;
}
