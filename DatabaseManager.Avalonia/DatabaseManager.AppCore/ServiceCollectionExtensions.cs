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
        services.AddSingleton<ICompareService, DefaultCompareService>();
        services.AddSingleton<ISyncScriptService, DefaultSyncScriptService>();
        services.AddSingleton<IDiagnoseService, DefaultDiagnoseService>();
        services.AddSingleton<IExportImportService, DefaultExportImportService>();
        services.AddSingleton<IOptimizeService, DefaultOptimizeService>();
        services.AddSingleton<IDependencyService, DefaultDependencyService>();
        services.AddSingleton<IStatisticService, DefaultStatisticService>();
        services.AddSingleton<IIndexFragmentationService, DefaultIndexFragmentationService>();
        services.AddSingleton<IBackupService, DefaultBackupService>();
        services.AddSingleton<ICodeGenerateService, DefaultCodeGenerateService>();
        services.AddSingleton<IColumnDocumentationService, DefaultColumnDocumentationService>();
        services.AddSingleton<IDdlService, DefaultDdlService>();
        services.AddSingleton<IQueryHistoryService, DefaultQueryHistoryService>();
        services.AddSingleton<IScriptLibraryService, DefaultScriptLibraryService>();
        services.AddSingleton<IExecutionPlanService, DefaultExecutionPlanService>();
        services.AddSingleton<ITaskCenterService, DefaultTaskCenterService>();
        services.AddSingleton<IAppSettingsService, DefaultAppSettingsService>();
        services.AddSingleton<IConnectionVisualService, DefaultConnectionVisualService>();
        services.AddSingleton<IFullDataSearchService, DefaultFullDataSearchService>();
        services.AddSingleton<IDbSessionService, DefaultDbSessionService>();
        services.AddSingleton<IDbUserService, DefaultDbUserService>();
        services.AddSingleton<IQueryProfilerService, DefaultQueryProfilerService>();
        services.AddSingleton<IDashboardService, DefaultDashboardService>();
        services.AddSingleton<IScheduleService, DefaultScheduleService>();

        // ViewModels
        services.AddTransient<MainWindowViewModel>();
        services.AddTransient<ConnectionManagerViewModel>();
        services.AddTransient<ObjectsExplorerViewModel>();
        services.AddTransient<QueryEditorViewModel>();
        services.AddTransient<DataEditorViewModel>();
        services.AddTransient<TableDesignerViewModel>();
        services.AddTransient<ConvertViewModel>();
        services.AddTransient<SchemaCompareViewModel>();
        services.AddTransient<DataCompareViewModel>();
        services.AddTransient<ScriptPreviewViewModel>();
        services.AddTransient<QueryHistoryViewModel>();
        services.AddTransient<ScriptLibraryViewModel>();
        services.AddTransient<ExecutionPlanViewModel>();
        services.AddTransient<TaskCenterViewModel>();
        services.AddTransient<DiagnoseViewModel>();
        services.AddTransient<OptimizeViewModel>();
        services.AddTransient<DependencyViewModel>();
        services.AddTransient<StatisticViewModel>();
        services.AddTransient<IndexFragmentationViewModel>();
        services.AddTransient<BackupViewModel>();
        services.AddTransient<CodeGenerateViewModel>();
        services.AddTransient<ColumnDocumentationViewModel>();
        services.AddTransient<ExportViewModel>();
        services.AddTransient<ImportViewModel>();
        services.AddTransient<SearchViewModel>();
        services.AddTransient<JsonViewerViewModel>();
        services.AddTransient<ImageViewerViewModel>();

        return services;
    }
}
