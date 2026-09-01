using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DatabaseInterpreter.Model;
using DatabaseManager.AppCore.Common;
using DatabaseManager.AppCore.Models;
using DatabaseManager.AppCore.Services;

namespace DatabaseManager.AppCore.ViewModels;

/// <summary>
/// 结构对比 ViewModel（阶段 4）。
/// 对比两个同类型数据库的结构差异：选择源/目标连接、对象类型，执行对比并展示差异树。
/// </summary>
public partial class SchemaCompareViewModel : ViewModelBase
{
    private readonly IDbConnectionService _connectionService;
    private readonly ICompareService _compareService;
    private readonly ISyncScriptService _syncScriptService;

    /// <summary>最近一次结构对比的完整上下文（供生成变更/回滚脚本）。</summary>
    private SchemaCompareContext? _context;

    /// <summary>全部已保存连接（源/目标下拉共用）。</summary>
    public ObservableCollection<ConnectionItem> Connections { get; } = new();

    /// <summary>可对比的数据库对象类型。</summary>
    public IReadOnlyList<ObjectTypeOption> ObjectTypes { get; }

    /// <summary>结构差异根节点（树）。</summary>
    public ObservableCollection<SchemaCompareItem> DifferenceRoots { get; } = new();

    /// <summary>执行日志。</summary>
    public ObservableCollection<string> Logs { get; } = new();

    [ObservableProperty]
    private ConnectionItem? _sourceConnection;

    [ObservableProperty]
    private ConnectionItem? _targetConnection;

    [ObservableProperty]
    private ObjectTypeOption? _selectedObjectType;

    [ObservableProperty]
    private SchemaCompareItem? _selectedDifference;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    /// <summary>当前选中差异的详情描述。</summary>
    [ObservableProperty]
    private string _detailText = string.Empty;

    /// <summary>由窗口注入的脚本预览窗口打开回调。</summary>
    public Action<ScriptPreviewViewModel>? RequestScriptPreview { get; set; }

    public SchemaCompareViewModel(IDbConnectionService connectionService, ICompareService compareService, ISyncScriptService syncScriptService)
    {
        _connectionService = connectionService;
        _compareService = compareService;
        _syncScriptService = syncScriptService;

        ObjectTypes = new[]
        {
            new ObjectTypeOption(DatabaseObjectType.Table, "表 (Table)"),
            new ObjectTypeOption(DatabaseObjectType.View, "视图 (View)"),
            new ObjectTypeOption(DatabaseObjectType.Function, "函数 (Function)"),
            new ObjectTypeOption(DatabaseObjectType.Procedure, "存储过程 (Procedure)"),
            new ObjectTypeOption(DatabaseObjectType.Table | DatabaseObjectType.View | DatabaseObjectType.Function | DatabaseObjectType.Procedure, "全部"),
        };

        SelectedObjectType = ObjectTypes.Last();
    }

    /// <summary>加载已保存的连接并刷新源/目标选择。</summary>
    public void RefreshConnections()
    {
        var previousSourceId = SourceConnection?.Id;
        var previousTargetId = TargetConnection?.Id;

        Connections.Clear();
        foreach (var item in _connectionService.GetConnections())
        {
            Connections.Add(item);
        }

        SourceConnection = FindConnection(previousSourceId) ?? Connections.FirstOrDefault();
        TargetConnection = FindConnection(previousTargetId) ?? Connections.Skip(1).FirstOrDefault();
    }

    private ConnectionItem? FindConnection(string? id)
        => Connections.FirstOrDefault(c => c.Id == id);

    partial void OnSelectedDifferenceChanged(SchemaCompareItem? value)
    {
        if (value is null)
        {
            DetailText = string.Empty;
            return;
        }

        var parts = new List<string>
        {
            $"类型：{value.ObjectType}",
            $"变更：{value.DifferenceTypeText}",
        };

        if (!string.IsNullOrEmpty(value.SourceName))
            parts.Add($"源对象：{value.SourceName}");
        if (!string.IsNullOrEmpty(value.TargetName))
            parts.Add($"目标对象：{value.TargetName}");
        if (!string.IsNullOrEmpty(value.Description))
            parts.Add(value.Description);

        DetailText = string.Join(Environment.NewLine, parts);
    }

    [RelayCommand]
    private async Task ExecuteAsync()
    {
        if (SelectedObjectType is null)
        {
            StatusMessage = "请选择要对比的对象类型。";
            return;
        }

        if (SourceConnection is null || TargetConnection is null)
        {
            StatusMessage = "请选择源连接和目标连接。";
            return;
        }

        IsBusy = true;
        StatusMessage = string.Empty;
        Logs.Clear();
        DifferenceRoots.Clear();

        try
        {
            var feedbackBuffer = new List<string>();
            void CollectFeedback(string message) => feedbackBuffer.Add(message);

            AppendLog($"源：{SourceConnection.Description}");
            AppendLog($"目标：{TargetConnection.Description}");
            AppendLog($"对象类型：{SelectedObjectType.DisplayName}");
            AppendLog("开始对比...");

            var context = await _compareService.CompareSchemaAsync(
                SourceConnection,
                TargetConnection,
                SelectedObjectType.Value,
                CollectFeedback);

            _context = context;

            foreach (var line in feedbackBuffer)
            {
                AppendLog(line);
            }

            foreach (var root in context.Roots)
            {
                DifferenceRoots.Add(root);
            }

            StatusMessage = context.Roots.Count == 0
                ? "对比完成，未发现差异。"
                : $"对比完成，共 {context.Roots.Count} 类对象存在差异。";
            AppendLog(StatusMessage);
        }
        catch (Exception ex)
        {
            StatusMessage = $"对比失败：{ex.Message}";
            AppendLog(StatusMessage);
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>生成选中差异的变更脚本（应用到目标库）并打开脚本预览窗口。</summary>
    [RelayCommand]
    private async Task GenerateScriptsAsync()
    {
        await GenerateAndPreviewAsync(isRollback: false);
    }

    /// <summary>生成选中差异的回滚脚本（恢复目标库为对比前状态）并打开脚本预览窗口。</summary>
    [RelayCommand]
    private async Task GenerateRollbackAsync()
    {
        await GenerateAndPreviewAsync(isRollback: true);
    }

    private async Task GenerateAndPreviewAsync(bool isRollback)
    {
        if (_context is null || _context.Roots.Count == 0)
        {
            StatusMessage = "请先执行结构对比。";
            return;
        }

        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        try
        {
            var feedbackBuffer = new List<string>();
            void CollectFeedback(string message) => feedbackBuffer.Add(message);

            var scripts = isRollback
                ? await _syncScriptService.GenerateStructuralRollbackScriptsAsync(_context, _context.Roots, CollectFeedback)
                : await _syncScriptService.GenerateStructuralScriptsAsync(_context, _context.Roots, CollectFeedback);

            foreach (var line in feedbackBuffer)
            {
                AppendLog(line);
            }

            if (scripts.Count == 0)
            {
                StatusMessage = isRollback
                    ? "选中的差异没有可生成的回滚脚本。"
                    : "选中的差异没有可生成的变更脚本。";
                AppendLog(StatusMessage);
                return;
            }

            var previewVm = new ScriptPreviewViewModel(_syncScriptService)
            {
                TargetConnection = _context.Target,
                SourceDescription = isRollback
                    ? $"结构对比回滚（{_context.Source.Database} → 恢复 {_context.Target.Database}）"
                    : $"结构对比（{_context.Source.Database} → 应用到 {_context.Target.Database}）",
            };
            foreach (var script in scripts)
            {
                previewVm.Scripts.Add(script);
            }

            StatusMessage = $"已生成 {scripts.Count} 项脚本，请审阅后选择执行。";
            AppendLog(StatusMessage);
            RequestScriptPreview?.Invoke(previewVm);
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "脚本生成已取消。";
        }
        catch (Exception ex)
        {
            StatusMessage = $"生成脚本失败：{ex.Message}";
            AppendLog(StatusMessage);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void AppendLog(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;

        var time = DateTime.Now.ToString("HH:mm:ss");
        Logs.Add($"[{time}] {message}");
    }
}

/// <summary>对象类型下拉选项。</summary>
public sealed record ObjectTypeOption(DatabaseObjectType Value, string DisplayName);
