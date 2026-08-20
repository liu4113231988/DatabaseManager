using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DatabaseManager.AppCore.Common;
using DatabaseManager.AppCore.Models;
using DatabaseManager.AppCore.Services;

namespace DatabaseManager.AppCore.ViewModels;

/// <summary>
/// 代码生成 ViewModel（阶段 5）。
/// 选择连接，加载表/视图并勾选，配置语言/命名空间/输出目录，生成实体类代码。
/// </summary>
public partial class CodeGenerateViewModel : ViewModelBase
{
    private readonly IDbConnectionService _connectionService;
    private readonly ICodeGenerateService _codeGenerateService;

    /// <summary>全部已保存连接。</summary>
    public ObservableCollection<ConnectionItem> Connections { get; } = new();

    /// <summary>语言选项。</summary>
    public ObservableCollection<CodeGenerateLanguageOption> Languages { get; } = new()
    {
        new("C#", "CSharp"),
        new("Java", "Java"),
    };

    /// <summary>可勾选的表/视图目标。</summary>
    public ObservableCollection<CodeGenerateTarget> Targets { get; } = new();

    /// <summary>执行日志。</summary>
    public ObservableCollection<string> Logs { get; } = new();

    [ObservableProperty]
    private ConnectionItem? _selectedConnection;

    [ObservableProperty]
    private CodeGenerateLanguageOption? _selectedLanguage;

    [ObservableProperty]
    private string _namespaceName = "MyApp.Models";

    [ObservableProperty]
    private string _outputFolder = "Generated";

    [ObservableProperty]
    private bool _generateComments = true;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    public CodeGenerateViewModel(IDbConnectionService connectionService, ICodeGenerateService codeGenerateService)
    {
        _connectionService = connectionService;
        _codeGenerateService = codeGenerateService;
        SelectedLanguage = Languages.FirstOrDefault();
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

    /// <summary>加载当前连接下的表/视图列表。</summary>
    [RelayCommand]
    private async Task LoadTargetsAsync()
    {
        if (SelectedConnection is null)
        {
            StatusMessage = "请选择连接。";
            return;
        }

        IsBusy = true;
        StatusMessage = string.Empty;
        Targets.Clear();

        try
        {
            AppendLog($"正在加载 {SelectedConnection.Description} 的表/视图...");

            var targets = await _codeGenerateService.GetTargetsAsync(SelectedConnection);
            foreach (var target in targets)
            {
                Targets.Add(target);
            }

            StatusMessage = $"加载完成，共 {targets.Count} 个对象。";
            AppendLog(StatusMessage);
        }
        catch (Exception ex)
        {
            StatusMessage = $"加载失败：{ex.Message}";
            AppendLog(StatusMessage);
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>已勾选的目标对象。</summary>
    private List<CodeGenerateTarget> GetCheckedTargets()
        => Targets.Where(t => t.IsChecked).ToList();

    [RelayCommand]
    private async Task GenerateAsync()
    {
        if (SelectedConnection is null)
        {
            StatusMessage = "请选择连接。";
            return;
        }

        if (SelectedLanguage is null)
        {
            StatusMessage = "请选择代码语言。";
            return;
        }

        var selected = GetCheckedTargets();
        if (selected.Count == 0)
        {
            StatusMessage = "请至少勾选一个表或视图。";
            return;
        }

        if (string.IsNullOrWhiteSpace(OutputFolder))
        {
            StatusMessage = "请设置输出文件夹。";
            return;
        }

        IsBusy = true;
        StatusMessage = string.Empty;

        var feedbackBuffer = new List<string>();
        void CollectFeedback(string message) => feedbackBuffer.Add(message);

        try
        {
            AppendLog($"连接：{SelectedConnection.Description}");
            AppendLog($"语言：{SelectedLanguage.DisplayName}，命名空间：{NamespaceName}");
            AppendLog($"输出目录：{OutputFolder}，生成注释：{GenerateComments}");

            var result = await _codeGenerateService.GenerateAsync(
                SelectedConnection, selected, SelectedLanguage.Value,
                NamespaceName, GenerateComments, OutputFolder, CollectFeedback);

            foreach (var line in feedbackBuffer)
            {
                AppendLog(line);
            }

            StatusMessage = result.IsOK
                ? $"代码生成成功，已输出到 {OutputFolder}。"
                : $"代码生成失败：{result.Message}";
            AppendLog(StatusMessage);
        }
        catch (Exception ex)
        {
            StatusMessage = $"代码生成失败：{ex.Message}";
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
