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
                .Select(f => new IndexFragmentationItem(f, connection.Database))
                .ToList();

            onFeedback?.Invoke($"分析完成，共 {items.Count} 个碎片索引。");
            return (IReadOnlyList<IndexFragmentationItem>)items;
        }, cancellationToken);
    }

    public Task<IReadOnlyList<IndexRebuildResult>> RebuildIndexesAsync(
        ConnectionItem connection,
        IReadOnlyList<IndexFragmentationItem> items,
        CancellationToken cancellationToken = default)
    {
        return Task.Run(async () =>
        {
            var dbType = ConnectionHelper.ParseDatabaseType(connection.DatabaseType);
            if (dbType == DatabaseType.Unknown || string.IsNullOrEmpty(connection.Database))
            {
                throw new InvalidOperationException("连接或数据库无效。");
            }

            var results = new List<IndexRebuildResult>(items.Count);
            if (items.Count == 0)
                return results;

            var dbInterpreter = DbInterpreterHelper.GetDbInterpreter(
                dbType, ConnectionHelper.ToConnectionInfo(connection));

            var analysiser = new Analysiser(dbInterpreter);

            foreach (var item in items)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    var frag = new DatabaseManager.Core.Model.IndexFragmentation
                    {
                        Schema = item.Schema,
                        TableName = item.TableName,
                        IndexName = item.IndexName,
                        FragmentationPercent = item.FragmentationPercent,
                    };

                    var result = await analysiser.RebuildIndex(frag);
                    results.Add(new IndexRebuildResult(
                        item.Schema,
                        item.TableName,
                        item.IndexName,
                        result.IsOK,
                        result.Message ?? string.Empty));
                }
                catch (Exception ex)
                {
                    results.Add(new IndexRebuildResult(
                        item.Schema,
                        item.TableName,
                        item.IndexName,
                        false,
                        ex.Message));
                }
            }

            return (IReadOnlyList<IndexRebuildResult>)results;
        }, cancellationToken);
    }
}
