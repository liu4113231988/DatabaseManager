using System;
using AtomUI;
using AtomUI.Desktop.Controls;
using AtomUI.Theme;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;
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
        // 注册代码页编码提供程序（GBK/GB18030 等在 .NET Core 后需显式注册）。
        System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

        // 初始化 Profile 数据文件（连接/账号/文件连接配置），对应原 WinForms Program.Main 中的 ProfileBaseManager.Init()
        ProfileBaseManager.Init();

        // 构建 DI 容器并注册 AppCore 服务
        _services = new ServiceCollection()
            .AddAppCore()
            .BuildServiceProvider();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                DataContext = _services.GetRequiredService<MainWindowViewModel>(),
                Icon = new WindowIcon(AssetLoader.Open(new Uri("avares://DatabaseManager.Avalonia/Assets/database-manager.ico"))),
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
