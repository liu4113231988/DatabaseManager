using System;
using AtomUI;
using AtomUI.Desktop.Controls;
using AtomUI.Theme;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;
using Avalonia.Threading;
using DatabaseManager.AppCore;
using DatabaseManager.AppCore.ViewModels;
using DatabaseManager.Avalonia.Views;
using DatabaseManager.Profile.Manager;
using Microsoft.Extensions.DependencyInjection;

namespace DatabaseManager.Avalonia;

public partial class App : Application
{
    private IServiceProvider? _services;

    /// <summary>全局 DI 服务容器（供主窗口打开子窗口时解析依赖）。</summary>
    public IServiceProvider? Services => _services;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);

        // 初始化 AtomUI（Ant Design 风格主题，对齐原 AntdUI 视觉）
        this.UseAtomUI(builder =>
        {
            builder.UseDesktopControls();
        });
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            try
            {
                // 注册代码页编码提供程序（GBK/GB18030 等在 .NET Core 后需显式注册）。
                System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

                // 配置文件必须在构建服务和窗口前完成初始化，避免 async void 把失败抛到进程级。
                ProfileBaseManager.Init();

                _services = new ServiceCollection()
                    .AddAppCore()
                    .BuildServiceProvider();

                desktop.MainWindow = Program.SmokeArgs.Any(a => a is "--p0" or "--p2") ? new global::Avalonia.Controls.Window() : new MainWindow
                {
                    DataContext = _services.GetRequiredService<MainWindowViewModel>(),
                    Icon = new WindowIcon(AssetLoader.Open(new Uri("avares://DatabaseManager.Avalonia/Assets/database-manager.ico"))),
                };
            }
            catch (Exception ex)
            {
                AppExceptionHandler.Report(ex, "应用初始化", showDialog: false);
                desktop.MainWindow = CreateStartupErrorWindow(ex);
            }

            // 冒烟测试模式：主窗口就绪后异步驱动全界面拍屏；完成后自动 Shutdown。
            if (Program.SmokeRequested)
            {
                _ = Dispatcher.UIThread.InvokeAsync(async () =>
                {
                    try
                    {
                        if (Program.SmokeArgs.Contains("--p0")) await DatabaseManager.Avalonia.Smoke.P0SmokeHarness.RunAsync();
                        else if (Program.SmokeArgs.Contains("--p2")) await DatabaseManager.Avalonia.Smoke.P2SmokeHarness.RunAsync();
                        else await DatabaseManager.Avalonia.Smoke.SmokeHarness.RunAsync(Program.SmokeArgs);
                    }
                    catch (Exception ex)
                    {
                        if (Program.SmokeArgs.Any(a => a is "--p0" or "--p2")) Environment.ExitCode = 1;
                        try { System.IO.File.AppendAllText(DatabaseManager.Avalonia.Smoke.SmokeHarness.LogFile, $"[smoke] 失败：{ex}\n"); } catch { }
                    }
                    finally
                    {
                        try { desktop.Shutdown(Environment.ExitCode); } catch { /* ignore */ }
                    }
                });
            }
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static global::Avalonia.Controls.Window CreateStartupErrorWindow(Exception exception)
    {
        var window = new global::Avalonia.Controls.Window
        {
            Title = "DatabaseManager - 启动失败",
            Width = 620,
            Height = 300,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
        };
        var closeButton = new global::Avalonia.Controls.Button
        {
            Content = "关闭",
            HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Right,
            MinWidth = 90,
        };
        closeButton.Click += (_, _) => window.Close();
        window.Content = new StackPanel
        {
            Margin = new Thickness(24),
            Spacing = 16,
            Children =
            {
                new global::Avalonia.Controls.TextBlock
                {
                    Text = "应用初始化失败",
                    FontSize = 22,
                    FontWeight = global::Avalonia.Media.FontWeight.SemiBold,
                },
                new global::Avalonia.Controls.TextBlock
                {
                    Text = AppExceptionHandler.BuildUserMessage(exception, "应用初始化"),
                    TextWrapping = global::Avalonia.Media.TextWrapping.Wrap,
                },
                closeButton,
            },
        };
        return window;
    }
}
