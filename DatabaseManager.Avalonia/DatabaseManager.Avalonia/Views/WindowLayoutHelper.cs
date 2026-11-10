using System;
using Avalonia;
using Avalonia.Controls;
using DatabaseManager.AppCore.Services;

namespace DatabaseManager.Avalonia.Views;

/// <summary>
/// 主窗口布局持久化与专注模式管理。
/// </summary>
public sealed class WindowLayoutHelper
{
    private readonly Window _window;
    private readonly Grid _contentGrid;
    private readonly IAppSettingsService? _settings;
    private double _preFocusPanelWidth = 400;

    public Control MenuPanel { get; init; } = null!;
    public Control ToolbarPanel { get; init; } = null!;
    public Control StatusBarPanel { get; init; } = null!;
    public Control Splitter { get; init; } = null!;
    public Control ExplorerPanel { get; init; } = null!;

    public WindowLayoutHelper(Window window, Grid contentGrid, IAppSettingsService? settings)
    {
        _window = window;
        _contentGrid = contentGrid;
        _settings = settings;
    }

    public void Restore()
    {
        if (_settings is null) return;

        var ws = _settings.Settings.Workspace;

        if (ws.WindowWidth >= 400 && ws.WindowHeight >= 300)
        {
            _window.Width = ws.WindowWidth;
            _window.Height = ws.WindowHeight;
        }

        if (ws.LeftPanelWidth is > 260 and < 800)
        {
            _contentGrid.ColumnDefinitions[0].Width = new GridLength(ws.LeftPanelWidth);
        }

        if (ws.WindowX >= 0 && ws.WindowY >= 0)
        {
            _window.Position = new PixelPoint((int)ws.WindowX, (int)ws.WindowY);
            ClampToVisibleScreen();
        }
    }

    public bool ShouldRestoreMaximized()
    {
        if (_settings is null) return false;
        return string.Equals(_settings.Settings.Workspace.WindowState, "Maximized", StringComparison.OrdinalIgnoreCase);
    }

    public void Save()
    {
        if (_settings is null) return;

        var ws = _settings.Settings.Workspace;

        if (_window.WindowState == WindowState.Normal)
        {
            ws.WindowX = _window.Position.X;
            ws.WindowY = _window.Position.Y;
            ws.WindowWidth = _window.Width;
            ws.WindowHeight = _window.Height;
        }

        ws.WindowState = _window.WindowState == WindowState.Maximized ? "Maximized" : "Normal";

        if (!ws.FocusMode)
        {
            var leftColumn = _contentGrid.ColumnDefinitions[0].Width;
            ws.LeftPanelWidth = leftColumn.IsAbsolute ? leftColumn.Value : 400;
        }

        _settings.Save();
    }

    public void ApplyFocusMode(bool focus)
    {
        var leftColumn = _contentGrid.ColumnDefinitions[0];

        if (focus)
        {
            if (leftColumn.Width.IsAbsolute && leftColumn.Width.Value > 0)
            {
                _preFocusPanelWidth = leftColumn.Width.Value;
            }

            MenuPanel.IsVisible = false;
            ToolbarPanel.IsVisible = false;
            StatusBarPanel.IsVisible = false;
            Splitter.IsVisible = false;
            ExplorerPanel.IsVisible = false;
            leftColumn.MinWidth = 0;
            leftColumn.Width = new GridLength(0);
        }
        else
        {
            MenuPanel.IsVisible = true;
            ToolbarPanel.IsVisible = true;
            StatusBarPanel.IsVisible = true;
            Splitter.IsVisible = true;
            ExplorerPanel.IsVisible = true;
            leftColumn.MinWidth = 260;
            leftColumn.Width = new GridLength(_preFocusPanelWidth);
        }
    }

    public void PersistFocusMode(bool focus)
    {
        if (_settings is null) return;
        _settings.Settings.Workspace.FocusMode = focus;
        _settings.Save();
    }

    public void ClampToVisibleScreen()
    {
        try
        {
            var screen = _window.Screens.ScreenFromPoint(_window.Position) ?? _window.Screens.Primary;
            if (screen is null) return;

            var area = screen.WorkingArea;
            int w = (int)(_window.Width * _window.RenderScaling);
            int h = (int)(_window.Height * _window.RenderScaling);
            int x = Math.Clamp(_window.Position.X, area.X, Math.Max(area.X, area.X + area.Width - w));
            int y = Math.Clamp(_window.Position.Y, area.Y, Math.Max(area.Y, area.Y + area.Height - h));
            _window.Position = new PixelPoint(x, y);
        }
        catch
        {
            // 屏幕信息不可用时跳过钳制。
        }
    }
}