using DatabaseInterpreter.Core;
using DatabaseInterpreter.Model;
using DatabaseManager.AppCore.Models;
using DatabaseManager.Core;

namespace DatabaseManager.AppCore.Services;

/// <summary>
/// 数据库优化服务实现（阶段 4）。接入 <c>DatabaseManager.Core.Optimizer</c>。
/// </summary>
public class DefaultOptimizeService : IOptimizeService
{
    public Task<IReadOnlyList<OptimizeResultItem>> OptimizeAsync(
        ConnectionItem connection,
        Action<string>? onFeedback = null,
        CancellationToken cancellationToken = default)
    {
        return Task.Run(async () =>
        {
            var dbType = ConnectionHelper.ParseDatabaseType(connection.DatabaseType);
            if (dbType == DatabaseType.Unknown || string.IsNullOrEmpty(connection.Database))
            {
                throw new InvalidOperationException("连接或数据库无效。");
            }

            onFeedback?.Invoke("正在初始化优化器...");

            var dbInterpreter = DbInterpreterHelper.GetDbInterpreter(
                dbType,
                ConnectionHelper.ToConnectionInfo(connection),
                // 优化操作可能失败，让解释器抛出异常以便定位；不开启 BulkCopy 等数据专用选项。
                new DbInterpreterOption
                {
                    ThrowExceptionWhenErrorOccurs = true,
                });

            var optimizer = new Optimizer(dbInterpreter);

            onFeedback?.Invoke("开始执行数据库优化（注意：优化语句按数据库方言自动提交，无法跨对象回滚）...");

            var result = await optimizer.Optimize();

            var items = (result.Details ?? new())
                .Select(d => new OptimizeResultItem(d))
                .ToList();

            // 逐条输出成功/失败明细，避免成功日志丢失。
            foreach (var detail in items)
            {
                var prefix = detail.IsOK ? "[OK]" : "[FAIL]";
                var hasSize = detail.DataLengthBeforeOptimization > 0 || detail.DataLengthAfterOptimization > 0;
                var sizeInfo = hasSize
                    ? $" ({detail.DataLengthBeforeOptimization:0.##}MB → {detail.DataLengthAfterOptimization:0.##}MB)"
                    : string.Empty;
                var msg = string.IsNullOrWhiteSpace(detail.Message) ? null : $"：{detail.Message}";
                onFeedback?.Invoke($"{prefix} {detail.ObjectType} {detail.ObjectName}{sizeInfo}{msg}");
            }

            if (!result.IsOK && !string.IsNullOrWhiteSpace(result.Message))
            {
                onFeedback?.Invoke($"错误汇总：{result.Message}");
            }

            var okCount = items.Count(i => i.IsOK);
            var failCount = items.Count - okCount;
            if (!result.IsOK && items.Count == 0)
            {
                // 整体失败且无明细：抛出友好异常供上层展示为“优化失败”
                throw new InvalidOperationException(result.Message ?? "优化失败：未返回任何优化明细。");
            }
            onFeedback?.Invoke($"优化完成，共处理 {items.Count} 个对象（成功 {okCount}，失败 {failCount}）。");
            return (IReadOnlyList<OptimizeResultItem>)items;
        }, cancellationToken);
    }
}
