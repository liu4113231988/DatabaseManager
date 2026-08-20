using DatabaseInterpreter.Core;
using DatabaseInterpreter.Model;
using DatabaseInterpreter.Utility;
using DatabaseManager.AppCore.Models;
using DatabaseManager.Core;

namespace DatabaseManager.AppCore.Services;

/// <summary>
/// 数据库统计服务实现（阶段 5）。接入 <c>DatabaseManager.Core.DbStatistic</c>。
/// </summary>
public class DefaultStatisticService : IStatisticService
{
    public Task<IReadOnlyList<RecordCountItem>> CountTableRecordsAsync(
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

            var statistic = new DbStatistic(dbType, ConnectionHelper.ToConnectionInfo(connection));
            var feedback = new FeedbackObserver(onFeedback);
            statistic.Subscribe(feedback);

            onFeedback?.Invoke("开始统计表记录数...");

            var results = await statistic.CountTableRecords();

            var items = (results ?? Enumerable.Empty<DatabaseManager.Core.Model.TableRecordCount>())
                .OrderByDescending(r => r.RecordCount)
                .Select(r =>
                {
                    var name = string.IsNullOrEmpty(r.Schema) ? r.TableName : $"{r.Schema}.{r.TableName}";
                    return new RecordCountItem(name, r.RecordCount);
                })
                .ToList();

            onFeedback?.Invoke($"统计完成，共 {items.Count} 张表。");
            return (IReadOnlyList<RecordCountItem>)items;
        }, cancellationToken);
    }

    public Task<IReadOnlyList<ColumnLengthItem>> GetTableColumnLengthsAsync(
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

            var statistic = new DbStatistic(dbType, ConnectionHelper.ToConnectionInfo(connection));
            var feedback = new FeedbackObserver(onFeedback);
            statistic.Subscribe(feedback);

            onFeedback?.Invoke("开始统计列内容最大长度...");

            var results = await statistic.GetTableColumnContentLengths();

            var items = (results ?? Enumerable.Empty<DatabaseManager.Core.Model.TableColumnContentMaxLength>())
                .Select(r =>
                {
                    var name = string.IsNullOrEmpty(r.Schema) ? r.TableName : $"{r.Schema}.{r.TableName}";
                    return new ColumnLengthItem(name, r.ColumnName, r.ContentMaxLength);
                })
                .ToList();

            onFeedback?.Invoke($"统计完成，共 {items.Count} 个字符列。");
            return (IReadOnlyList<ColumnLengthItem>)items;
        }, cancellationToken);
    }

    /// <summary>统计反馈观察者：将 <see cref="FeedbackInfo"/> 消息转发到回调。</summary>
    private sealed class FeedbackObserver : IObserver<FeedbackInfo>
    {
        private readonly Action<string>? _onFeedback;

        public FeedbackObserver(Action<string>? onFeedback)
        {
            _onFeedback = onFeedback;
        }

        public void OnNext(FeedbackInfo value)
        {
            if (!string.IsNullOrWhiteSpace(value.Message))
            {
                _onFeedback?.Invoke(value.Message);
            }
        }

        public void OnError(Exception error)
        {
            _onFeedback?.Invoke(error?.Message ?? string.Empty);
        }

        public void OnCompleted()
        {
        }
    }
}
