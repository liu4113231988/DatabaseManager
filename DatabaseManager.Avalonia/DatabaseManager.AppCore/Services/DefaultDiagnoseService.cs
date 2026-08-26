using DatabaseInterpreter.Core;
using DatabaseInterpreter.Model;
using DatabaseInterpreter.Utility;
using DatabaseManager.AppCore.Models;
using DatabaseManager.Core;
using DatabaseManager.Core.Model;

namespace DatabaseManager.AppCore.Services;

/// <summary>
/// 诊断服务实现（阶段 4）。接入 <c>DbDiagnosis</c> 完成表 / 脚本诊断。
/// </summary>
public class DefaultDiagnoseService : IDiagnoseService
{
    /// <summary>DbDiagnosis.GetInstance 返回按 (DbType,ConnectionString) 缓存的单例，
    /// 并发设置 Schema / OnFeedback 会竞态，这里用全局锁序列化诊断调用。</summary>
    private static readonly object DbDiagnosisLock = new();

    public Task<IReadOnlyList<TableDiagnoseResultItem>> DiagnoseTableAsync(
        ConnectionItem connection,
        TableDiagnoseType diagnoseType,
        string? schema = null,
        Action<string>? onFeedback = null,
        CancellationToken cancellationToken = default)
    {
        return Task.Run(async () =>
        {
            var dbType = ParseDatabaseType(connection.DatabaseType);
            if (dbType == DatabaseType.Unknown || string.IsNullOrEmpty(connection.Database))
            {
                throw new InvalidOperationException("连接或数据库无效。");
            }

            cancellationToken.ThrowIfCancellationRequested();
            onFeedback?.Invoke("正在初始化诊断器...");

            // 锁内部：获取单例 → 设置状态 → 执行诊断 → 读取结果，整段串行化避免竞态。
            lock (DbDiagnosisLock)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var dbDiagnosis = DbDiagnosis.GetInstance(dbType, ToConnectionInfo(connection));
                dbDiagnosis.Schema = schema;

                var feedback = new FeedbackObserver(onFeedback);
                dbDiagnosis.OnFeedback = feedback.Notify;

                var diagnoseTask = dbDiagnosis.DiagnoseTable(diagnoseType);
                // 阻塞等待（在锁内必须同步完成以免后续并发覆盖回调）。
                var result = diagnoseTask.GetAwaiter().GetResult();

                cancellationToken.ThrowIfCancellationRequested();

                var items = result.Details.Select(detail =>
                {
                    var obj = detail.DatabaseObject;
                    return new TableDiagnoseResultItem(
                        GetObjectTypeName(obj),
                        obj.Schema,
                        GetTableName(obj),
                        obj.Name,
                        detail.RecordCount,
                        detail.Sql);
                }).ToList();

                onFeedback?.Invoke($"表诊断完成，共检出 {items.Count} 处问题。");
                return (IReadOnlyList<TableDiagnoseResultItem>)items;
            }
        }, cancellationToken);
    }

    public Task<IReadOnlyList<ScriptDiagnoseResultItem>> DiagnoseScriptAsync(
        ConnectionItem connection,
        ScriptDiagnoseType diagnoseType,
        string? schema = null,
        Action<string>? onFeedback = null,
        CancellationToken cancellationToken = default)
    {
        return Task.Run(async () =>
        {
            var dbType = ParseDatabaseType(connection.DatabaseType);
            if (dbType == DatabaseType.Unknown || string.IsNullOrEmpty(connection.Database))
            {
                throw new InvalidOperationException("连接或数据库无效。");
            }

            cancellationToken.ThrowIfCancellationRequested();
            onFeedback?.Invoke("正在初始化诊断器...");

            lock (DbDiagnosisLock)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var dbDiagnosis = DbDiagnosis.GetInstance(dbType, ToConnectionInfo(connection));
                dbDiagnosis.Schema = schema;

                var feedback = new FeedbackObserver(onFeedback);
                dbDiagnosis.OnFeedback = feedback.Notify;

                var diagnoseTask = dbDiagnosis.DiagnoseScript(diagnoseType);
                var results = diagnoseTask.GetAwaiter().GetResult();

                cancellationToken.ThrowIfCancellationRequested();

                var items = results.Select(r =>
                {
                    var obj = r.DbObject;
                    var details = r.Details
                        .Select(d => $"{d.ObjectType}: {d.InvalidName} → {d.Name} (位置 {d.Index})")
                        .ToList();

                    return new ScriptDiagnoseResultItem(
                        obj?.GetType().Name ?? string.Empty,
                        obj?.Schema ?? string.Empty,
                        obj?.Name ?? string.Empty,
                        details);
                }).ToList();

                onFeedback?.Invoke($"脚本诊断完成，共检出 {items.Count} 处对象异常。");
                return (IReadOnlyList<ScriptDiagnoseResultItem>)items;
            }
        }, cancellationToken);
    }

    private static string GetObjectTypeName(DatabaseObject obj) => obj switch
    {
        TableColumn => "列",
        TableForeignKey => "外键",
        _ => obj.GetType().Name,
    };

    private static string GetTableName(DatabaseObject obj) => obj switch
    {
        TableChild child => child.TableName,
        _ => string.Empty,
    };

    private static DatabaseType ParseDatabaseType(string databaseType)
        => Enum.TryParse<DatabaseType>(databaseType, true, out var type) ? type : DatabaseType.Unknown;

    private static ConnectionInfo ToConnectionInfo(ConnectionItem connection) => new()
    {
        Server = connection.Server,
        Port = connection.Port,
        ServerVersion = connection.ServerVersion,
        Database = connection.Database,
        IntegratedSecurity = connection.IntegratedSecurity,
        UserId = connection.UserId,
        Password = connection.Password,
        IsDba = connection.IsDba,
        UseSsl = connection.UseSsl,
    };

    /// <summary>
    /// 诊断反馈观察者：将 <see cref="FeedbackInfo"/> 消息转发到回调。
    /// </summary>
    private sealed class FeedbackObserver
    {
        private readonly Action<string>? _onFeedback;

        public FeedbackObserver(Action<string>? onFeedback)
        {
            _onFeedback = onFeedback;
        }

        public void Notify(FeedbackInfo value)
        {
            if (!string.IsNullOrWhiteSpace(value.Message))
            {
                _onFeedback?.Invoke(value.Message);
            }
        }
    }
}
