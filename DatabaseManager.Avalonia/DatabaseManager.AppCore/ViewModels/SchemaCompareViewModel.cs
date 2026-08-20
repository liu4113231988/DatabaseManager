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

    public SchemaCompareViewModel(IDbConnectionService connectionService, ICompareService compareService)
    {
        _connectionService = connectionService;
        _compareService = compareService;

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

            var roots = await _compareService.CompareSchemaAsync(
                SourceConnection,
                TargetConnection,
                SelectedObjectType.Value,
                CollectFeedback);

            foreach (var line in feedbackBuffer)
            {
                AppendLog(line);
            }

            foreach (var root in roots)
            {
                DifferenceRoots.Add(root);
            }

            StatusMessage = roots.Count == 0
                ? "对比完成，未发现差异。"
                : $"对比完成，共 {roots.Count} 类对象存在差异。";
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
