using Avalonia.Controls;
using Avalonia.Interactivity;
using DatabaseManager.AppCore.Models;
using DatabaseManager.AppCore.Services;

namespace DatabaseManager.Avalonia.Views;

/// <summary>计划列表行展示模型（包装 ScheduleDefinition 的显示文案）。</summary>
public class ScheduleRow
{
    public ScheduleDefinition Definition { get; }

    public string Name => Definition.Name;

    public string TaskTypeText => Definition.TaskType switch
    {
        ScheduleTaskTypes.Backup => "备份",
        ScheduleTaskTypes.Export => "导出",
        ScheduleTaskTypes.Import => "导入",
        ScheduleTaskTypes.Migration => "迁移",
        ScheduleTaskTypes.SchemaSync => "结构同步",
        ScheduleTaskTypes.DataSync => "数据同步",
        _ => "SQL 脚本",
    };

    public string ConnectionName => Definition.ConnectionName;

    public string ScheduleText => Definition.ScheduleKind switch
    {
        "EveryNMinutes" => $"每 {Definition.IntervalMinutes} 分钟",
        "Cron" => $"Cron: {Definition.CronExpression}",
        _ => $"每天 {Definition.DailyTime}",
    };

    public string NextRunText => Definition.Enabled
        ? FormatTime(OwnerService?.ComputeNextRun(Definition, Definition.LastRunAt ?? Definition.CreatedAt))
        : "未启用";

    public string LastRunText => FormatTime(Definition.LastRunAt);

    public string LastResult => Definition.LastResult ?? string.Empty;

    public bool Enabled => Definition.Enabled;

    /// <summary>用于下次运行时间计算的服务引用（窗口赋值）。</summary>
    internal IScheduleService? OwnerService { get; set; }

    public ScheduleRow(ScheduleDefinition definition)
    {
        Definition = definition;
    }

    private static string FormatTime(DateTime? time)
        => time?.ToString("MM-dd HH:mm:ss") ?? "—";
}

/// <summary>
/// 任务定时调度窗口：计划的增删改查、立即运行与状态查看。
/// </summary>
public partial class ScheduleWindow : Window
{
    private readonly IScheduleService _scheduleService;
    private readonly IDbConnectionService _connectionService;
    private ScheduleDefinition? _editing;
    private readonly List<ScheduleDefinition> _steps = new();
    private bool _addingStep;

    public ScheduleWindow(IScheduleService scheduleService, IDbConnectionService connectionService)
    {
        InitializeComponent();

        _scheduleService = scheduleService;
        _connectionService = connectionService;

        ComboConnection.ItemsSource = _connectionService.GetConnections();
        ComboTarget.ItemsSource = _connectionService.GetConnections();
        _scheduleService.SchedulesChanged += RefreshGrid;

        RefreshGrid();
    }

    protected override void OnClosed(EventArgs e)
    {
        _scheduleService.SchedulesChanged -= RefreshGrid;
        base.OnClosed(e);
    }

    private void RefreshGrid()
    {
        if (!global::Avalonia.Threading.Dispatcher.UIThread.CheckAccess())
        {
            global::Avalonia.Threading.Dispatcher.UIThread.Post(RefreshGrid);
            return;
        }
        var rows = _scheduleService.GetAll()
            .OrderByDescending(d => d.Enabled)
            .ThenBy(d => d.Name, StringComparer.OrdinalIgnoreCase)
            .Select(d => new ScheduleRow(d) { OwnerService = _scheduleService })
            .ToList();

        SchedulesGrid.ItemsSource = rows;
        TxtSummary.Text = $"共 {rows.Count} 个计划，启用 {rows.Count(r => r.Enabled)} 个。";
    }

    private ScheduleDefinition? SelectedDefinition
        => (SchedulesGrid.SelectedItem as ScheduleRow)?.Definition;

    private void ShowForm(ScheduleDefinition? definition)
    {
        _editing = definition;
        _steps.Clear();
        if (definition is not null) _steps.AddRange(definition.Steps);
        RefreshSteps();
        ChkContinueFailure.IsChecked = definition?.ContinueOnFailure ?? false;
        TxtMailTo.Text = definition?.NotificationRecipient;
        TxtSmtpHost.Text = definition?.SmtpHost;
        TxtSmtpPort.Text = (definition?.SmtpPort ?? 587).ToString();
        TxtMailFrom.Text = definition?.SmtpFrom;
        TxtSmtpUser.Text = definition?.SmtpUser;
        TxtSmtpSecretVariable.Text = definition?.SmtpPasswordEnvironmentVariable;
        ComboTarget.SelectedItem = ComboTarget.ItemsSource?.Cast<ConnectionItem>().FirstOrDefault(c => c.Name == definition?.TargetConnectionName);
        TxtTargetDatabase.Text = definition?.TargetDatabaseName;
        ComboMigrationMode.SelectedIndex = definition?.MigrationMode switch { ConvertMode.Schema => 1, ConvertMode.Data => 2, _ => 0 };

        FormPanel.IsVisible = true;
        TxtName.Text = definition?.Name ?? string.Empty;
        TxtDatabase.Text = definition?.DatabaseName ?? string.Empty;
        TxtSql.Text = definition?.SqlText ?? string.Empty;
        TxtSaveFolder.Text = definition?.SaveFolder ?? string.Empty;
        ChkZip.IsChecked = definition?.ZipBackup ?? true;
        TxtClientTool.Text = definition?.ClientToolPath ?? string.Empty;
        TxtExportTable.Text = definition?.ExportTable ?? string.Empty;
        TxtExportSchema.Text = definition?.ExportSchema ?? string.Empty;
        TxtExportFilePath.Text = definition?.ExportFilePath ?? string.Empty;
        ChkEnabled.IsChecked = definition?.Enabled ?? true;

        ComboTaskType.SelectedIndex = (definition?.TaskType ?? ScheduleTaskTypes.SqlScript) switch
        {
            ScheduleTaskTypes.Backup => 1,
            ScheduleTaskTypes.Export => 2,
            ScheduleTaskTypes.Import => 3,
            ScheduleTaskTypes.Migration => 4,
            ScheduleTaskTypes.SchemaSync => 5,
            ScheduleTaskTypes.DataSync => 6,
            _ => 0,
        };

        ComboScheduleKind.SelectedIndex = definition?.ScheduleKind switch
        {
            "EveryNMinutes" => 1,
            "Cron" => 2,
            _ => 0,
        };
        TxtDailyTime.Text = definition?.DailyTime ?? "02:00";
        TxtIntervalMinutes.Text = (definition?.IntervalMinutes ?? 60).ToString();
        TxtCronExpression.Text = definition?.CronExpression ?? "0 2 * * *";

        if (definition is not null)
        {
            var target = ComboConnection.ItemsSource?.Cast<ConnectionItem>()
                .FirstOrDefault(c => string.Equals(c.Name, definition.ConnectionName, StringComparison.OrdinalIgnoreCase));
            if (target is not null)
            {
                ComboConnection.SelectedItem = target;
            }
        }
        else
        {
            ComboConnection.SelectedIndex = ComboConnection.ItemCount > 0 ? 0 : -1;
        }

        UpdateTypePanels();
    }

    private void UpdateTypePanels()
    {
        PanelSql.IsVisible = ComboTaskType.SelectedIndex == 0;
        PanelBackup.IsVisible = ComboTaskType.SelectedIndex == 1;
        PanelExport.IsVisible = ComboTaskType.SelectedIndex is 2 or 3 or 6;
        if (PanelTarget is not null) PanelTarget.IsVisible = ComboTaskType.SelectedIndex >= 4;
    }

    private void UpdateScheduleKindPanels()
    {
        PanelDailyTime.IsVisible = ComboScheduleKind.SelectedIndex == 0;
        PanelInterval.IsVisible = ComboScheduleKind.SelectedIndex == 1;
        PanelCron.IsVisible = ComboScheduleKind.SelectedIndex == 2;
    }

    private void ComboTaskType_SelectionChanged(object? sender, SelectionChangedEventArgs e) => UpdateTypePanels();

    private void ComboScheduleKind_SelectionChanged(object? sender, SelectionChangedEventArgs e) => UpdateScheduleKindPanels();

    private void BtnNew_Click(object? sender, RoutedEventArgs e) => ShowForm(null);

    private void BtnEdit_Click(object? sender, RoutedEventArgs e)
    {
        if (SelectedDefinition is null)
        {
            TxtSummary.Text = "请先选择要编辑的计划。";
            return;
        }

        ShowForm(SelectedDefinition);
    }

    private void BtnSave_Click(object? sender, RoutedEventArgs e)
    {
        string name = TxtName.Text?.Trim() ?? string.Empty;
        if (name.Length == 0)
        {
            TxtSummary.Text = "请填写计划名称。";
            return;
        }

        if (ComboConnection.SelectedItem is not ConnectionItem connection)
        {
            TxtSummary.Text = "请选择连接。";
            return;
        }

        var definition = _editing is null || _addingStep ? new ScheduleDefinition() : Newtonsoft.Json.JsonConvert.DeserializeObject<ScheduleDefinition>(Newtonsoft.Json.JsonConvert.SerializeObject(_editing))!;

        definition.Name = name;
        definition.ConnectionName = connection.Name;
        definition.DatabaseName = string.IsNullOrWhiteSpace(TxtDatabase.Text) ? null : TxtDatabase.Text.Trim();
        definition.TaskType = ComboTaskType.SelectedIndex switch
        {
            1 => ScheduleTaskTypes.Backup,
            2 => ScheduleTaskTypes.Export,
            3 => ScheduleTaskTypes.Import,
            4 => ScheduleTaskTypes.Migration,
            5 => ScheduleTaskTypes.SchemaSync,
            6 => ScheduleTaskTypes.DataSync,
            _ => ScheduleTaskTypes.SqlScript,
        };
        definition.SqlText = TxtSql.Text;
        definition.SaveFolder = TxtSaveFolder.Text?.Trim();
        definition.ZipBackup = ChkZip.IsChecked == true;
        definition.ClientToolPath = string.IsNullOrWhiteSpace(TxtClientTool.Text) ? null : TxtClientTool.Text.Trim();
        var clientToolError = DbAdminGuidance.ValidateClientToolPath(definition.ClientToolPath);
        if (clientToolError is not null)
        {
            TxtSummary.Text = clientToolError;
            return;
        }
        definition.ExportTable = TxtExportTable.Text?.Trim();
        definition.ExportSchema = string.IsNullOrWhiteSpace(TxtExportSchema.Text) ? null : TxtExportSchema.Text.Trim();
        definition.ExportFormat = (ComboExportFormat.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Csv";
        definition.ExportFilePath = TxtExportFilePath.Text?.Trim();
        definition.ScheduleKind = ComboScheduleKind.SelectedIndex switch
        {
            1 => "EveryNMinutes",
            2 => "Cron",
            _ => "EveryDay",
        };
        definition.DailyTime = string.IsNullOrWhiteSpace(TxtDailyTime.Text) ? "02:00" : TxtDailyTime.Text.Trim();
        definition.IntervalMinutes = int.TryParse(TxtIntervalMinutes.Text, out int minutes) ? Math.Max(1, minutes) : 60;
        definition.CronExpression = string.IsNullOrWhiteSpace(TxtCronExpression.Text) ? "0 2 * * *" : TxtCronExpression.Text.Trim();
        if (definition.ScheduleKind == "Cron"
            && !CronSchedule.TryGetNextOccurrence(definition.CronExpression, DateTime.Now, out _, out var cronError))
        {
            TxtSummary.Text = cronError ?? "Cron 表达式无效。";
            return;
        }
        definition.Enabled = ChkEnabled.IsChecked == true;
        definition.TargetConnectionName = (ComboTarget.SelectedItem as ConnectionItem)?.Name;
        definition.TargetDatabaseName = TxtTargetDatabase.Text?.Trim();
        definition.MigrationMode = ComboMigrationMode.SelectedIndex switch { 1 => ConvertMode.Schema, 2 => ConvertMode.Data, _ => ConvertMode.SchemaAndData };
        definition.ContinueOnFailure = ChkContinueFailure.IsChecked == true;
        definition.NotificationRecipient = TxtMailTo.Text?.Trim();
        definition.SmtpHost = TxtSmtpHost.Text?.Trim();
        definition.SmtpPort = int.TryParse(TxtSmtpPort.Text, out var smtpPort) ? smtpPort : 587;
        definition.SmtpFrom = TxtMailFrom.Text?.Trim();
        definition.SmtpUser = TxtSmtpUser.Text?.Trim();
        definition.SmtpPasswordEnvironmentVariable = TxtSmtpSecretVariable.Text?.Trim();
        definition.Steps = _addingStep ? new() : _steps.ToList();
        try
        {
            DefaultScheduleService.Validate(definition);
            if (_addingStep) { _steps.Add(definition); RefreshSteps(); return; }
            _scheduleService.Save(definition);
        }
        catch (Exception ex) { TxtSummary.Text = ex.Message; return; }
        FormPanel.IsVisible = false;
        _editing = null;
    }

    private void BtnFormCancel_Click(object? sender, RoutedEventArgs e)
    {
        FormPanel.IsVisible = false;
        _editing = null;
    }

    private void RefreshSteps() => TxtSteps.Text = _steps.Count == 0 ? "无步骤时执行当前操作；添加步骤后按下列顺序执行。" : string.Join(" → ", _steps.Select((s, i) => $"{i + 1}. {s.Name} ({s.TaskType}, {s.ConnectionName})"));
    private void BtnAddStep_Click(object? sender, RoutedEventArgs e)
    {
        _addingStep = true;
        try { BtnSave_Click(sender, e); } finally { _addingStep = false; }
    }
    private void BtnRemoveStep_Click(object? sender, RoutedEventArgs e)
    {
        if (_steps.Count > 0) _steps.RemoveAt(_steps.Count - 1);
        RefreshSteps();
    }

    private void BtnRunNow_Click(object? sender, RoutedEventArgs e)
    {
        if (SelectedDefinition is null)
        {
            TxtSummary.Text = "请先选择要运行的计划。";
            return;
        }

        _scheduleService.RunNow(SelectedDefinition);
        TxtSummary.Text = $"已提交运行：{SelectedDefinition.Name}（可在任务中心查看）。";
    }

    private async void BtnDelete_Click(object? sender, RoutedEventArgs e)
    {
        if (SelectedDefinition is null)
        {
            TxtSummary.Text = "请先选择要删除的计划。";
            return;
        }

        var confirm = await AppCore.Common.DialogHelper.ShowConfirmAsync(
            "删除计划", $"确定删除计划「{SelectedDefinition.Name}」吗？");

        if (confirm != true)
        {
            return;
        }

        _scheduleService.Delete(SelectedDefinition.Id);
    }

    private void BtnRefresh_Click(object? sender, RoutedEventArgs e) => RefreshGrid();

    private void BtnClose_Click(object? sender, RoutedEventArgs e) => Close();
}
