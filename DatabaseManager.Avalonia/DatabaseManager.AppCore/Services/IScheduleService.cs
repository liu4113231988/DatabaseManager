using DatabaseManager.AppCore.Models;
using Newtonsoft.Json;

namespace DatabaseManager.AppCore.Services;

/// <summary>定时任务的执行类型。</summary>
public static class ScheduleTaskTypes
{
    public const string SqlScript = "SqlScript";
    public const string Backup = "Backup";
    public const string Export = "Export";
    public const string Import = "Import";
    public const string Migration = "Migration";
    public const string SchemaSync = "SchemaSync";
    public const string DataSync = "DataSync";

    public static readonly string[] All = { SqlScript, Backup, Export, Import, Migration, SchemaSync, DataSync };
}

/// <summary>定时任务计划定义（持久化于 Profiles\schedules.json）。</summary>
public class ScheduleDefinition
{
    public string? TargetConnectionName { get; set; }
    public string? TargetDatabaseName { get; set; }
    public string MigrationMode { get; set; } = ConvertMode.SchemaAndData;
    public List<ScheduleDefinition> Steps { get; set; } = new();
    public bool ContinueOnFailure { get; set; }
    public string? NotificationRecipient { get; set; }
    public string? SmtpHost { get; set; }
    public int SmtpPort { get; set; } = 587;
    public string? SmtpFrom { get; set; }
    public string? SmtpUser { get; set; }
    public string? SmtpPasswordEnvironmentVariable { get; set; }
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public string Name { get; set; } = string.Empty;

    /// <summary>任务类型（ScheduleTaskTypes）。</summary>
    public string TaskType { get; set; } = ScheduleTaskTypes.SqlScript;

    public string ConnectionName { get; set; } = string.Empty;

    /// <summary>数据库覆盖（空 = 连接默认库）。</summary>
    public string? DatabaseName { get; set; }

    // --- SqlScript ---
    public string? SqlText { get; set; }

    // --- Backup ---
    public string? SaveFolder { get; set; }

    public bool ZipBackup { get; set; } = true;

    public string? ClientToolPath { get; set; }

    // --- Export ---
    public string? ExportTable { get; set; }

    public string? ExportSchema { get; set; }

    public string ExportFormat { get; set; } = "Csv";

    public string? ExportFilePath { get; set; }

    // --- 计划 ---
    /// <summary>计划类型：EveryDay（每天 HH:mm）/ EveryNMinutes（每 N 分钟）/ Cron（五段表达式）。</summary>
    public string ScheduleKind { get; set; } = "EveryDay";

    public string DailyTime { get; set; } = "02:00";

    public int IntervalMinutes { get; set; } = 60;

    /// <summary>Cron 表达式（分 时 日 月 周），仅应用运行期间参与调度。</summary>
    public string CronExpression { get; set; } = "0 2 * * *";

    public bool Enabled { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public DateTime? LastRunAt { get; set; }

    public string? LastResult { get; set; }
}

/// <summary>
/// 任务定时调度服务：计划定义持久化 + 到期检查；
/// 到期任务经 ITaskCenterService 提交为后台任务（复用任务中心的取消/日志/历史/通知）。
/// 主窗口需定期调用 <see cref="CheckAndRunDueAsync"/>（UI 线程，如 30 秒一次）。
/// </summary>
public interface IScheduleService
{
    /// <summary>读取全部计划。</summary>
    IReadOnlyList<ScheduleDefinition> GetAll();

    /// <summary>计算下一次运行时间（自 from 起）。</summary>
    DateTime ComputeNextRun(ScheduleDefinition definition, DateTime from);

    /// <summary>新增或更新计划。</summary>
    void Save(ScheduleDefinition definition);

    /// <summary>删除计划。</summary>
    void Delete(string id);

    /// <summary>检查并提交到期的计划任务（幂等，可在 UI 线程频繁调用）。</summary>
    void CheckAndRunDue(DateTime now);

    /// <summary>立即运行指定计划（等同到期触发）。</summary>
    void RunNow(ScheduleDefinition definition);

    /// <summary>计划状态变化（保存/删除/运行结束）时触发（UI 线程）。</summary>
    event Action? SchedulesChanged;
}

/// <summary>定时调度服务默认实现。</summary>
public class DefaultScheduleService : IScheduleService
{
    private static readonly object FileLock = new();
    private readonly string _filePath;
    private List<ScheduleDefinition> _items;
    private readonly HashSet<string> _runningIds = new();

    private readonly ITaskCenterService _taskCenter;
    private readonly IDbConnectionService _connectionService;
    private readonly IQueryService _queryService;
    private readonly IBackupService _backupService;
    private readonly IExportImportService _exportImportService;
    private readonly ScheduledOperations _operations;

    public event Action? SchedulesChanged;

    public DefaultScheduleService(
        ITaskCenterService taskCenter,
        IDbConnectionService connectionService,
        IQueryService queryService,
        IBackupService backupService,
        IExportImportService exportImportService,
        IConvertService? convertService = null, ICompareService? compareService = null, ISyncScriptService? syncService = null,
        string? storageDirectory = null)
    {
        _taskCenter = taskCenter;
        _connectionService = connectionService;
        _queryService = queryService;
        _backupService = backupService;
        _exportImportService = exportImportService;
        _operations = new ScheduledOperations(connectionService, exportImportService, convertService, compareService, syncService);

        var dir = storageDirectory ?? Path.Combine(AppContext.BaseDirectory, "Profiles");
        Directory.CreateDirectory(dir);
        _filePath = Path.Combine(dir, "schedules.json");
        _items = Load();

        // 任务结束时同步计划状态（完成/失败/取消都记录 LastRunAt，避免失败后立即重试风暴）。
        _taskCenter.TaskFinished += OnTaskFinished;
    }

    public IReadOnlyList<ScheduleDefinition> GetAll() => _items.ToList();

    public DateTime ComputeNextRun(ScheduleDefinition definition, DateTime from)
    {
        if (definition.ScheduleKind == "Cron")
        {
            return CronSchedule.TryGetNextOccurrence(definition.CronExpression, from, out var next, out _)
                ? next
                : from.AddDays(1);
        }

        if (definition.ScheduleKind == "EveryNMinutes")
        {
            int minutes = Math.Max(1, definition.IntervalMinutes);
            return from.AddMinutes(minutes);
        }

        if (TimeSpan.TryParse(definition.DailyTime, out var time))
        {
            var today = from.Date.Add(time);
            return today > from ? today : today.AddDays(1);
        }

        return from.Date.AddDays(1);
    }

    public void Save(ScheduleDefinition definition)
    {
        Validate(definition);
        var existing = _items.FirstOrDefault(d => d.Id == definition.Id);
        if (existing is not null)
        {
            int index = _items.IndexOf(existing);
            _items[index] = definition;
        }
        else
        {
            _items.Add(definition);
        }

        Persist();
        SchedulesChanged?.Invoke();
    }

    public void Delete(string id)
    {
        int removed = _items.RemoveAll(d => d.Id == id);
        if (removed > 0)
        {
            Persist();
            SchedulesChanged?.Invoke();
        }
    }

    public void RunNow(ScheduleDefinition definition)
    {
        lock (_runningIds)
        {
            if (_runningIds.Contains(definition.Id))
            {
                return;
            }

            _runningIds.Add(definition.Id);
        }

        SubmitScheduled(definition, DateTime.Now);
    }

    public void CheckAndRunDue(DateTime now)
    {
        List<ScheduleDefinition> due;

        lock (_runningIds)
        {
            due = _items
                .Where(d => d.Enabled
                            && !_runningIds.Contains(d.Id)
                            && ComputeNextRun(d, d.LastRunAt ?? (d.CreatedAt == default ? now : d.CreatedAt)) <= now)
                .ToList();

            foreach (var d in due)
            {
                _runningIds.Add(d.Id);
            }
        }

        foreach (var definition in due)
        {
            SubmitScheduled(definition, now);
        }
    }

    private void SubmitScheduled(ScheduleDefinition definition, DateTime now)
    {
        // Freeze the definition so editing a running plan cannot change its remaining steps.
        definition = JsonConvert.DeserializeObject<ScheduleDefinition>(JsonConvert.SerializeObject(definition))!;
        _taskCenter.Run($"[定时] {definition.Name}", $"定时/{definition.TaskType}", async (run, ct) =>
        {
            run.Report($"计划到期，开始执行（类型：{definition.TaskType}）。");

            try
            {
                Validate(definition);
                var steps = definition.Steps.Count > 0 ? definition.Steps : new List<ScheduleDefinition> { definition };
                var failures = new List<string>();
                var summaries = new List<string>();
                for (int i = 0; i < steps.Count; i++)
                {
                    ct.ThrowIfCancellationRequested();
                    var step = steps[i];
                    run.Report($"步骤 {i + 1}/{steps.Count}：{step.Name}（{step.TaskType}）");
                    try
                    {
                        string stepSummary = step.TaskType switch
                        {
                            ScheduleTaskTypes.Backup => await RunBackupAsync(step, run, ct),
                            ScheduleTaskTypes.Export => await RunExportAsync(step, run, ct),
                            ScheduleTaskTypes.SqlScript => await RunSqlScriptAsync(step, run, ct),
                            _ => await _operations.RunAsync(step, run, ct),
                        };
                        summaries.Add(stepSummary);
                        run.Report($"步骤 {i + 1} 完成：{stepSummary}");
                    }
                    catch (OperationCanceledException) { throw; }
                    catch (Exception ex)
                    {
                        failures.Add($"步骤 {i + 1}：{ex.Message}");
                        run.Report(failures[^1]);
                        if (!definition.ContinueOnFailure) throw;
                    }
                }
                if (failures.Count > 0) throw new InvalidOperationException(string.Join("；", failures));
                string summary = string.Join("；", summaries);

                definition.LastResult = $"成功：{summary}";
                run.ResultSummary = summary;
            }
            catch (OperationCanceledException)
            {
                definition.LastResult = "已取消";
                throw;
            }
            catch (Exception ex)
            {
                definition.LastResult = $"失败：{ex.Message}";
                throw;
            }
            finally
            {
                definition.LastRunAt = DateTime.Now;
                try
                {
                    var existing = _items.FirstOrDefault(d => d.Id == definition.Id);
                    if (existing is not null)
                    {
                        existing.LastRunAt = definition.LastRunAt;
                        existing.LastResult = definition.LastResult;
                        Persist();
                    }
                    using var notifyTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
                    try { await ScheduledOperations.NotifyAsync(definition, notifyTimeout.Token); }
                    catch (Exception ex) { run.Report($"邮件通知失败：{ex.Message}"); }
                }
                finally
                {
                    lock (_runningIds) _runningIds.Remove(definition.Id);
                }
            }
        });
    }

    private void OnTaskFinished(TaskRun run)
    {
        // 定时任务结束时刷新界面状态（LastResult 已在工作内写入并持久化）。
        if (run.Title.StartsWith("[定时]", StringComparison.Ordinal))
        {
            SchedulesChanged?.Invoke();
        }
    }

    private ConnectionItem ResolveConnection(ScheduleDefinition definition)
    {
        var connection = _connectionService.GetConnections()
            .FirstOrDefault(c => string.Equals(c.Name, definition.ConnectionName, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"找不到连接「{definition.ConnectionName}」。");

        if (!string.IsNullOrWhiteSpace(definition.DatabaseName)
            && !string.Equals(connection.Database, definition.DatabaseName, StringComparison.Ordinal))
        {
            var clone = new ConnectionItem
            {
                Ssh = connection.Ssh,
                Id = connection.Id,
                AccountId = connection.AccountId,
                DatabaseType = connection.DatabaseType,
                Name = connection.Name,
                Server = connection.Server,
                Port = connection.Port,
                ServerVersion = connection.ServerVersion,
                Database = definition.DatabaseName,
                IntegratedSecurity = connection.IntegratedSecurity,
                UserId = connection.UserId,
                Password = connection.Password,
                IsDba = connection.IsDba,
                UseSsl = connection.UseSsl,
                RememberPassword = connection.RememberPassword,
            };
            return clone;
        }

        return connection;
    }

    private async Task<string> RunSqlScriptAsync(ScheduleDefinition definition, TaskRun run, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(definition.SqlText))
        {
            throw new InvalidOperationException("未配置 SQL 脚本。");
        }

        run.Report("执行 SQL 脚本...");
        var connection = ResolveConnection(definition);
        var result = await _queryService.ExecuteStandaloneAsync(connection, definition.SqlText, ct, 600);

        if (!result.IsSuccess)
        {
            throw new InvalidOperationException(result.ErrorMessage ?? "SQL 执行失败。");
        }

        return result.IsNonQuery
            ? $"影响 {result.RowCount} 行"
            : $"返回 {result.RowCount} 行";
    }

    private async Task<string> RunBackupAsync(ScheduleDefinition definition, TaskRun run, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(definition.SaveFolder))
        {
            throw new InvalidOperationException("未配置备份保存文件夹。");
        }

        var connection = ResolveConnection(definition);
        run.Report($"开始备份 {connection.Name}/{connection.Database} ...");

        var result = await _backupService.BackupAsync(
            connection, definition.SaveFolder, definition.ClientToolPath, definition.ZipBackup,
            msg => run.Report(msg), ct);

        if (!result.IsOK)
        {
            throw new InvalidOperationException(result.Message ?? "备份失败。");
        }

        return result.Message ?? "备份完成";
    }

    private async Task<string> RunExportAsync(ScheduleDefinition definition, TaskRun run, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(definition.ExportTable) || string.IsNullOrWhiteSpace(definition.ExportFilePath))
        {
            throw new InvalidOperationException("未配置导出表或导出文件路径。");
        }

        var connection = ResolveConnection(definition);
        run.Report($"导出 {definition.ExportTable} → {definition.ExportFilePath} ...");

        var result = await _exportImportService.ExportDataAsync(
            connection, definition.ExportTable, definition.ExportSchema, false,
            definition.ExportFormat ?? "Csv", definition.ExportFilePath,
            onFeedback: msg => run.Report(msg), cancellationToken: ct);

        if (!result.IsSuccess)
        {
            throw new InvalidOperationException(result.Message ?? "导出失败。");
        }

        return result.Message ?? "导出完成";
    }

    private List<ScheduleDefinition> Load()
    {
        try
        {
            if (File.Exists(_filePath))
            {
                return JsonConvert.DeserializeObject<List<ScheduleDefinition>>(File.ReadAllText(_filePath))
                       ?? new List<ScheduleDefinition>();
            }
        }
        catch
        {
            // 损坏时静默重建。
        }

        return new List<ScheduleDefinition>();
    }

    private void Persist()
    {
        lock (FileLock)
        {
            var temp = _filePath + ".tmp";
            File.WriteAllText(temp, JsonConvert.SerializeObject(_items, Formatting.Indented));
            File.Move(temp, _filePath, true);
        }
    }

    public static void Validate(ScheduleDefinition definition)
    {
        if (!string.IsNullOrWhiteSpace(definition.NotificationRecipient))
        {
            if (string.IsNullOrWhiteSpace(definition.SmtpHost) || string.IsNullOrWhiteSpace(definition.SmtpFrom) || definition.SmtpPort is < 1 or > 65535)
                throw new InvalidOperationException("邮件通知需要 SMTP 主机、有效端口和发件邮箱。");
            try { _ = new System.Net.Mail.MailAddress(definition.SmtpFrom); _ = new System.Net.Mail.MailAddress(definition.NotificationRecipient); }
            catch (FormatException) { throw new InvalidOperationException("发件或收件邮箱格式无效。"); }
        }
        if (definition.Steps.Count > 50) throw new InvalidOperationException("每个计划最多 50 个步骤。");
        foreach (var step in definition.Steps.Count > 0 ? definition.Steps : new List<ScheduleDefinition> { definition })
        {
            if (step != definition && step.Steps.Count > 0) throw new InvalidOperationException("不支持嵌套批处理。");
            if (!ScheduleTaskTypes.All.Contains(step.TaskType)) throw new InvalidOperationException("未知任务类型。");
            if (string.IsNullOrWhiteSpace(step.ConnectionName)) throw new InvalidOperationException("步骤缺少连接。");
            if (step.TaskType == ScheduleTaskTypes.SqlScript && string.IsNullOrWhiteSpace(step.SqlText)) throw new InvalidOperationException("SQL 脚本不能为空。");
            if (step.TaskType is ScheduleTaskTypes.Import or ScheduleTaskTypes.Export && (string.IsNullOrWhiteSpace(step.ExportTable) || string.IsNullOrWhiteSpace(step.ExportFilePath))) throw new InvalidOperationException("导入导出需要表和文件路径。");
            if (step.TaskType is ScheduleTaskTypes.Migration or ScheduleTaskTypes.SchemaSync or ScheduleTaskTypes.DataSync && string.IsNullOrWhiteSpace(step.TargetConnectionName)) throw new InvalidOperationException("迁移或同步需要目标连接。");
            if (step.TaskType == ScheduleTaskTypes.DataSync && string.IsNullOrWhiteSpace(step.ExportTable)) throw new InvalidOperationException("数据同步需要指定表。");
        }
    }
}
