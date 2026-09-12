using AtomUI;
using Avalonia;
using System;
using System.Linq;

namespace DatabaseManager.Avalonia;

sealed class Program
{
    /// <summary>冒烟测试模式开关。设置后由 App.OnFrameworkInitializationCompleted 在主窗口就绪后驱动冒烟流程。</summary>
    public static bool SmokeRequested;
    public static string[] SmokeArgs = Array.Empty<string>();

    [STAThread]
    public static int Main(string[] args)
    {
        // 冒烟测试模式：仅用于自动拍屏验证，启动后自动连接默认测试库、打开关键窗口并渲染若干画面后退出。
        if (args.Any(a => string.Equals(a, "--smoke", StringComparison.OrdinalIgnoreCase)))
        {
            SmokeRequested = true;
            SmokeArgs = args;
            // 立即初始化日志（WinExe 无 console，必须写到文件），让 App 启动阶段任何异常都能被记录。
            DatabaseManager.Avalonia.Smoke.SmokeHarness.Run(args);
        }

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        return Environment.ExitCode;
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UseAtomUIPlatformDetect()
            .WithAtomUIDefaultOptions()
            .LogToTrace();
}
