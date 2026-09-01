using AtomUI;
using Avalonia;
using System;

namespace DatabaseManager.Avalonia;

sealed class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args) => BuildAvaloniaApp()
        .StartWithClassicDesktopLifetime(args);

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UseAtomUIPlatformDetect() // AtomUI 6.x：AtomUI 依赖的平台初始化
            .WithAtomUIDefaultOptions() // AtomUI 6.x：默认主题选项
            .LogToTrace();
}
