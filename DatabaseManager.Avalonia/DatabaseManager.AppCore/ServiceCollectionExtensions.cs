using DatabaseManager.AppCore.Services;
using DatabaseManager.AppCore.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace DatabaseManager.AppCore;

/// <summary>
/// AppCore 服务注册扩展。
/// 供 Avalonia 入口（Program.cs）或测试项目统一注册依赖。
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>注册 AppCore 层的全部服务与 ViewModel。</summary>
    public static IServiceCollection AddAppCore(this IServiceCollection services)
    {
        // Services
        services.AddSingleton<IDbConnectionService, ProfileDbConnectionService>();
        services.AddSingleton<IDbSchemaService, DefaultDbSchemaService>();
        services.AddSingleton<IQueryService, DefaultQueryService>();
        services.AddSingleton<IDataEditService, DefaultDataEditService>();
        services.AddSingleton<ITableDesignService, DefaultTableDesignService>();
        services.AddSingleton<IConvertService, DefaultConvertService>();
        services.AddSingleton<IExportImportService, DefaultExportImportService>();

        // ViewModels
        services.AddTransient<MainWindowViewModel>();
        services.AddTransient<ConnectionManagerViewModel>();
        services.AddTransient<ObjectsExplorerViewModel>();
        services.AddTransient<QueryEditorViewModel>();
        services.AddTransient<DataEditorViewModel>();
        services.AddTransient<TableDesignerViewModel>();

        return services;
    }
}
