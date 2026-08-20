using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DatabaseManager.AppCore.Common;
using DatabaseManager.AppCore.Models;
using DatabaseManager.AppCore.Services;
using DatabaseManager.Core.Model;

namespace DatabaseManager.AppCore.ViewModels;

/// <summary>
/// 诊断 ViewModel（阶段 4）。
/// 对单个数据库执行表 / 脚本诊断：选择连接与诊断类型，执行并查看检出结果与日志。
/// </summary>
public partial class DiagnoseViewModel : ViewModelBase
{
    private readonly IDbConnectionService _connectionService;
    private readonly IDiagnoseService _diagnoseService;

    /// <summary>全部已保存连接。</summary>
    public ObservableCollection<ConnectionItem> Connections { get; } = new();

    /// <summary>诊断类型选项（含类别：表 / 脚本）。</summary>
    public ObservableCollection<DiagnoseTypeOption> DiagnoseTypes { get; } = new();

    /// <summary>表诊断结果。</summary>
    public ObservableCollection<TableDiagnoseResultItem> TableResults { get; } = new();

    /// <summary>脚本诊断结果。</summary>
    public ObservableCollection<ScriptDiagnoseResultItem> ScriptResults { get; } = new();

    /// <summary>执行日志。</summary>
    public ObservableCollection<string> Logs { get; } = new();

    [ObservableProperty]
    private ConnectionItem? _selectedConnection;

    [ObservableProperty]
    private DiagnoseTypeOption? _selectedDiagnoseType;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    public DiagnoseViewModel(IDbConnectionService connectionService, IDiagnoseService diagnoseService)
    {
        _connectionService = connectionService;
        _diagnoseService = diagnoseService;

        DiagnoseTypes.Add(new DiagnoseTypeOption("非空字段存在空值（NotNullWithEmpty）", "Table"));
        DiagnoseTypes.Add(new DiagnoseTypeOption("字符字段含首尾空白（LeadingOrTrailingWhitespace）", "Table"));
        DiagnoseTypes.Add(new DiagnoseTypeOption("自引用同值外键（SelfReferenceSame）", "Table"));
        DiagnoseTypes.Add(new DiagnoseTypeOption("空值而非 NULL（EmptyValueRatherThanNull）", "Table"));
        DiagnoseTypes.Add(new DiagnoseTypeOption("主键列可空（PrimaryKeyColumnIsNullable）", "Table"));
        DiagnoseTypes.Add(new DiagnoseTypeOption("视图列别名缺少引号（ViewColumnAlias）", "Script"));
        DiagnoseTypes.Add(new DiagnoseTypeOption("脚本对象名不匹配（NameNotMatch）", "Script"));

        SelectedDiagnoseType = DiagnoseTypes.FirstOrDefault();
    }

    /// <summary>加载已保存连接并刷新选择。</summary>
    public void RefreshConnections()
    {
        var previousId = SelectedConnection?.Id;

        Connections.Clear();
        foreach (var item in _connectionService.GetConnections())
        {
            Connections.Add(item);
        }

        SelectedConnection = Connections.FirstOrDefault(c => c.Id == previousId) ?? Connections.FirstOrDefault();
    }

    partial void OnSelectedDiagnoseTypeChanged(DiagnoseTypeOption? value)
    {
        ClearResults();
    }

    [RelayCommand]
    private async Task ExecuteAsync()
    {
        if (SelectedConnection is null)
        {
            StatusMessage = "请选择连接。";
            return;
        }

        var diagnoseType = SelectedDiagnoseType;
        if (diagnoseType is null)
        {
            StatusMessage = "请选择诊断类型。";
            return;
        }

        IsBusy = true;
        StatusMessage = string.Empty;
        ClearResults();

        var feedbackBuffer = new List<string>();
        void CollectFeedback(string message) => feedbackBuffer.Add(message);

        try
        {
            AppendLog($"连接：{SelectedConnection.Description}");
            AppendLog($"诊断类型：{diagnoseType.DisplayName}");
            AppendLog("开始诊断...");

            bool isTable = string.Equals(diagnoseType.Category, "Table", StringComparison.OrdinalIgnoreCase);

            if (isTable)
            {
                var tableType = ParseTableDiagnoseType(diagnoseType.DisplayName);
                var results = await _diagnoseService.DiagnoseTableAsync(
                    SelectedConnection, tableType, null, CollectFeedback);

                foreach (var line in feedbackBuffer)
                {
                    AppendLog(line);
                }

                foreach (var result in results)
                {
                    TableResults.Add(result);
                }

                StatusMessage = $"表诊断完成，共检出 {results.Count} 处问题。";
            }
            else
            {
                var scriptType = ParseScriptDiagnoseType(diagnoseType.DisplayName);
                var results = await _diagnoseService.DiagnoseScriptAsync(
                    SelectedConnection, scriptType, null, CollectFeedback);

                foreach (var line in feedbackBuffer)
                {
                    AppendLog(line);
                }

                foreach (var result in results)
                {
                    ScriptResults.Add(result);
                }

                StatusMessage = $"脚本诊断完成，共检出 {results.Count} 处对象异常。";
            }

            AppendLog(StatusMessage);
        }
        catch (Exception ex)
        {
            StatusMessage = $"诊断失败：{ex.Message}";
            AppendLog(StatusMessage);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ClearResults()
    {
        TableResults.Clear();
        ScriptResults.Clear();
    }

    private static TableDiagnoseType ParseTableDiagnoseType(string displayName)
    {
        if (displayName.Contains("NotNullWithEmpty")) return TableDiagnoseType.NotNullWithEmpty;
        if (displayName.Contains("LeadingOrTrailingWhitespace")) return TableDiagnoseType.WithLeadingOrTrailingWhitespace;
        if (displayName.Contains("SelfReferenceSame")) return TableDiagnoseType.SelfReferenceSame;
        if (displayName.Contains("EmptyValueRatherThanNull")) return TableDiagnoseType.EmptyValueRatherThanNull;
        if (displayName.Contains("PrimaryKeyColumnIsNullable")) return TableDiagnoseType.PrimaryKeyColumnIsNullable;
        return TableDiagnoseType.NotNullWithEmpty;
    }

    private static ScriptDiagnoseType ParseScriptDiagnoseType(string displayName)
    {
        if (displayName.Contains("ViewColumnAlias")) return ScriptDiagnoseType.ViewColumnAliasWithoutQuotationChar;
        if (displayName.Contains("NameNotMatch")) return ScriptDiagnoseType.NameNotMatch;
        return ScriptDiagnoseType.NameNotMatch;
    }

    private void AppendLog(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;

        var time = DateTime.Now.ToString("HH:mm:ss");
        Logs.Add($"[{time}] {message}");
    }
}
