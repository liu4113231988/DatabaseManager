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
                dbType, ConnectionHelper.ToConnectionInfo(connection));

            var optimizer = new Optimizer(dbInterpreter);

            onFeedback?.Invoke("开始执行数据库优化...");

            var result = await optimizer.Optimize();

            var items = (result.Details ?? new())
                .Select(d => new OptimizeResultItem(d))
                .ToList();

            if (!result.IsOK && string.IsNullOrWhiteSpace(result.Message) == false)
            {
                onFeedback?.Invoke(result.Message);
            }

            onFeedback?.Invoke($"优化完成，共处理 {items.Count} 个对象。");
            return (IReadOnlyList<OptimizeResultItem>)items;
        }, cancellationToken);
    }
}
