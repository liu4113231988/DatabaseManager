using DatabaseInterpreter.Core;
using DatabaseInterpreter.Model;
using DatabaseInterpreter.Utility;
using DatabaseManager.AppCore.Models;
using DatabaseManager.Core;

namespace DatabaseManager.AppCore.Services;

/// <summary>
/// 索引碎片分析服务实现（阶段 5）。接入 <c>DatabaseManager.Core.Analysiser</c>。
/// </summary>
public class DefaultIndexFragmentationService : IIndexFragmentationService
{
    public Task<IReadOnlyList<IndexFragmentationItem>> GetIndexFragmentationsAsync(
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

            var dbInterpreter = DbInterpreterHelper.GetDbInterpreter(
                dbType, ConnectionHelper.ToConnectionInfo(connection));

            var analysiser = new Analysiser(dbInterpreter);

            onFeedback?.Invoke("正在获取索引碎片信息...");

            var results = await analysiser.GetIndexFragmentations();

            var items = (results ?? Enumerable.Empty<DatabaseManager.Core.Model.IndexFragmentation>())
                .Select(f => new IndexFragmentationItem(f))
                .ToList();

            onFeedback?.Invoke($"分析完成，共 {items.Count} 个碎片索引。");
            return (IReadOnlyList<IndexFragmentationItem>)items;
        }, cancellationToken);
    }

    public Task<(bool IsOK, string Message)> RebuildIndexAsync(
        ConnectionItem connection,
        IndexFragmentationItem item,
        CancellationToken cancellationToken = default)
    {
        return Task.Run(async () =>
        {
            var dbType = ConnectionHelper.ParseDatabaseType(connection.DatabaseType);
            if (dbType == DatabaseType.Unknown || string.IsNullOrEmpty(connection.Database))
            {
                throw new InvalidOperationException("连接或数据库无效。");
            }

            var dbInterpreter = DbInterpreterHelper.GetDbInterpreter(
                dbType, ConnectionHelper.ToConnectionInfo(connection));

            var analysiser = new Analysiser(dbInterpreter);

            var frag = new DatabaseManager.Core.Model.IndexFragmentation
            {
                Schema = item.Schema,
                TableName = item.TableName,
                IndexName = item.IndexName,
                FragmentationPercent = item.FragmentationPercent,
            };

            var result = await analysiser.RebuildIndex(frag);
            return (result.IsOK, result.Message ?? string.Empty);
        }, cancellationToken);
    }
}
