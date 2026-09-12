using DatabaseManager.AppCore.Models;
using DatabaseInterpreter.Model;
using System.Net;
using System.Net.Mail;

namespace DatabaseManager.AppCore.Services;

public sealed class ScheduledOperations(IDbConnectionService connections, IExportImportService files,
    IConvertService? convert, ICompareService? compare, ISyncScriptService? sync)
{
    private ConnectionItem Resolve(string name, string? database)
    {
        var item = connections.GetConnections().FirstOrDefault(c => c.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"找不到连接：{name}");
        var copy = Newtonsoft.Json.JsonConvert.DeserializeObject<ConnectionItem>(Newtonsoft.Json.JsonConvert.SerializeObject(item))!;
        copy.Ssh = item.Ssh;
        if (!string.IsNullOrWhiteSpace(database)) copy.Database = database;
        return copy;
    }
    public async Task<string> RunAsync(ScheduleDefinition step, TaskRun run, CancellationToken ct)
    {
        var source = Resolve(step.ConnectionName, step.DatabaseName);
        if (step.TaskType == ScheduleTaskTypes.Import)
        {
            var imported = await files.ImportDataAsync(source, step.ExportTable!, step.ExportSchema, step.ExportFilePath!, onFeedback: run.Report, cancellationToken: ct);
            ct.ThrowIfCancellationRequested();
            if (!imported.IsSuccess) throw new InvalidOperationException(imported.Message);
            return imported.Message;
        }
        var target = Resolve(step.TargetConnectionName!, step.TargetDatabaseName);
        if (source.DatabaseType == target.DatabaseType && source.Server == target.Server && source.Port == target.Port && source.Database == target.Database)
            throw new InvalidOperationException("源库与目标库不能相同。");
        if (step.TaskType == ScheduleTaskTypes.Migration)
        {
            if (convert is null) throw new InvalidOperationException("迁移服务不可用。");
            if (step.MigrationMode is not (ConvertMode.Schema or ConvertMode.Data or ConvertMode.SchemaAndData)) throw new InvalidOperationException("迁移模式无效。");
            var result = await convert.ConvertAsync(source, target, step.MigrationMode, new ConvertOptions { UseTransaction = true, ContinueWhenErrorOccurs = false }, onFeedback: run.Report, cancellationToken: ct);
            if (result.IsCanceled) throw new OperationCanceledException(ct);
            if (result.ResultType != ConvertResultType.Information) throw new InvalidOperationException(result.Message);
            return result.Message;
        }
        if (compare is null || sync is null) throw new InvalidOperationException("同步服务不可用。");
        if (!source.DatabaseType.Equals(target.DatabaseType, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("同步作业要求源与目标数据库类型相同。");
        IReadOnlyList<ScriptItem> scripts;
        if (step.TaskType == ScheduleTaskTypes.SchemaSync)
        {
            var context = await compare.CompareSchemaAsync(source, target, DatabaseObjectType.Table, run.Report, ct);
            ct.ThrowIfCancellationRequested();
            if (context.ErrorMessage is not null) throw new InvalidOperationException(context.ErrorMessage);
            scripts = await sync.GenerateStructuralScriptsAsync(context, context.Roots, run.Report, ct);
        }
        else if (step.TaskType == ScheduleTaskTypes.DataSync)
        {
            var requestedTables = step.ExportTable!.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            var results = await compare.CompareDataAsync(source, target, requestedTables, onFeedback: run.Report, cancellationToken: ct);
            ct.ThrowIfCancellationRequested();
            if (results.Count == 0) throw new InvalidOperationException("数据对比未返回指定表的结果，请检查表名、权限和日志。");
            if (requestedTables.Any(name => results.Count(r => r.TableName.Equals(name, StringComparison.OrdinalIgnoreCase)) != 1))
                throw new InvalidOperationException("有指定表被跳过或存在同名表歧义，已停止同步；请检查表名和主键。");
            scripts = await sync.GenerateDataSyncScriptsAsync(source, target, results, run.Report, ct);
        }
        else throw new InvalidOperationException("未知任务类型。");
        var folder = Path.Combine(AppContext.BaseDirectory, "Profiles", "schedule-scripts");
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, $"{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}.sql");
        await File.WriteAllTextAsync(path, string.Join(Environment.NewLine, scripts.Select(s => s.SqlText)), ct);
        run.Report($"本次同步脚本：{path}");
        var executed = await sync.ExecuteScriptsAsync(target, scripts, run.Report, ct);
        if (!executed.IsSuccess) throw new InvalidOperationException(executed.Message);
        return executed.Message;
    }
    public static async Task NotifyAsync(ScheduleDefinition definition, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(definition.NotificationRecipient)) return;
        using var message = new MailMessage(definition.SmtpFrom!, definition.NotificationRecipient)
        {
            Subject = $"数据库作业：{definition.Name}", Body = $"完成时间：{definition.LastRunAt}\n{definition.LastResult}",
        };
        using var smtp = new SmtpClient(definition.SmtpHost, definition.SmtpPort) { EnableSsl = true, Timeout = 15000 };
        if (!string.IsNullOrWhiteSpace(definition.SmtpUser))
            smtp.Credentials = new NetworkCredential(definition.SmtpUser, Environment.GetEnvironmentVariable(definition.SmtpPasswordEnvironmentVariable ?? "") ?? "");
        await smtp.SendMailAsync(message, ct);
    }
}
