using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using AtomUI.Controls;
using AtomUI.Theme;
using DatabaseManager.AppCore;
using DatabaseManager.AppCore.ViewModels;
using DatabaseManager.Avalonia.Views;
using Microsoft.Extensions.DependencyInjection;

namespace DatabaseManager.Avalonia;

public partial class App : Application
{
    private IServiceProvider? _services;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        // 初始化 AtomUI（Ant Design 风格主题，对齐原 AntdUI 视觉）
        this.UseAtomUI(builder =>
        {
            builder.UseOSSControls();
        });

        // 构建 DI 容器并注册 AppCore 服务
        _services = new ServiceCollection()
            .AddAppCore()
            .BuildServiceProvider();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                DataContext = _services.GetRequiredService<MainWindowViewModel>(),
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
