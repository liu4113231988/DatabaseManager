using System.Diagnostics;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using DatabaseManager.AppCore.Services;
using DatabaseManager.AppCore.ViewModels;
using DatabaseManager.Avalonia;
using DatabaseManager.Avalonia.Views;
using Microsoft.Extensions.DependencyInjection;

static class StartupProbe
{
    [STAThread]
    public static void Main(string[] args)
    {
        var listener = new StartupListener();
        Trace.Listeners.Add(listener);
        // 复用正式入口；反射仅用于诊断工具访问 internal Program。
        var entry = typeof(App).Assembly.GetType("DatabaseManager.Avalonia.Program")!;
        var builder = (AppBuilder)entry.GetMethod("BuildAvaloniaApp")!.Invoke(null, null)!;
        builder.AfterSetup(appBuilder => Dispatcher.UIThread.Post(() =>
        {
            var app = (App)appBuilder.Instance!;
            var desktop = (IClassicDesktopStyleApplicationLifetime)app.ApplicationLifetime!;
            if (desktop.MainWindow is not MainWindow { DataContext: MainWindowViewModel vm })
                throw new InvalidOperationException("主窗口未能完成初始化。");
            if (!desktop.MainWindow.IsVisible || !listener.Marks.ContainsKey("opened-end"))
                throw new InvalidOperationException("异步初始化后必须显示并打开主窗口。");

            listener.WriteLine("startup|ui-idle");
            var savedTabs = app.Services!.GetRequiredService<IAppSettingsService>().Settings.Workspace.Tabs
                .Where(t => !string.IsNullOrWhiteSpace(t.SqlText) || !string.IsNullOrEmpty(t.ConnectionName)).ToArray();
            if (vm.QueryTabs.Count != Math.Max(1, savedTabs.Length) || vm.SelectedQueryTab is null)
                throw new InvalidOperationException("查询标签恢复数量不正确。");
            for (int i = 0; i < savedTabs.Length; i++)
            {
                if (vm.QueryTabs[i].SqlText != (savedTabs[i].SqlText ?? string.Empty)
                    || vm.QueryTabs[i].ConnectionName != (savedTabs[i].ConnectionName ?? string.Empty)
                    || vm.QueryTabs[i].DatabaseName != (savedTabs[i].DatabaseName ?? string.Empty))
                    throw new InvalidOperationException("查询草稿或执行上下文恢复不完整。");
            }

            Console.WriteLine(JsonSerializer.Serialize(new { MilestonesMs = listener.Marks, Tabs = vm.QueryTabs.Count }));
            desktop.Shutdown();
        }, DispatcherPriority.Background)).StartWithClassicDesktopLifetime(args);
    }
}

sealed class StartupListener : TraceListener
{
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    public Dictionary<string, double> Marks { get; } = new();

    public override void Write(string? message) { }

    public override void WriteLine(string? message)
    {
        if (message?.StartsWith("startup|", StringComparison.Ordinal) == true)
            Marks[message[8..]] = Math.Round(_clock.Elapsed.TotalMilliseconds, 2);
    }
}
