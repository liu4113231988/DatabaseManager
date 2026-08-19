using CommunityToolkit.Mvvm.ComponentModel;
using DatabaseManager.AppCore.Common;
using DatabaseManager.AppCore.Services;

namespace DatabaseManager.AppCore.ViewModels;

/// <summary>
/// 主窗口 ViewModel（AppCore 层）。
/// 阶段 0：注入服务，验证 AppCore 能复用核心引擎并枚举支持的数据库类型。
/// </summary>
public partial class MainWindowViewModel : ViewModelBase
{
    private readonly IDbSchemaService _schemaService;
    private readonly IDbConnectionService _connectionService;

    [ObservableProperty]
    private string _supportedDatabases = string.Empty;

    [ObservableProperty]
    private string _connectionSummary = string.Empty;

    public MainWindowViewModel(IDbSchemaService schemaService, IDbConnectionService connectionService)
    {
        _schemaService = schemaService;
        _connectionService = connectionService;
    }

    /// <summary>初始化：枚举受支持的数据库类型，并统计已保存连接。</summary>
    public void Initialize()
    {
        SupportedDatabases = string.Join(", ", _schemaService.GetSupportedDatabaseTypes());

        var names = _connectionService.GetConnectionNames();
        ConnectionSummary = $"{names.Count} 个已保存连接";
    }
}
