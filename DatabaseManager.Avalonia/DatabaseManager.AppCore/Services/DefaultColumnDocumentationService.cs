using DatabaseInterpreter.Core;
using DatabaseInterpreter.Model;
using DatabaseInterpreter.Utility;
using DatabaseManager.AppCore.Models;
using DatabaseManager.Core;
using DatabaseManager.Core.Model;

namespace DatabaseManager.AppCore.Services;

/// <summary>
/// 文档生成服务实现（阶段 5）。接入 <c>DatabaseManager.Core.DocumentationGenerator</c>。
/// </summary>
public class DefaultColumnDocumentationService : IColumnDocumentationService
{
    public IReadOnlyList<ColumnDocumentationProperty> GetDefaultProperties()
        => new List<ColumnDocumentationProperty>
        {
            new(nameof(TableColumnProperty.Name), "列名"),
            new(nameof(TableColumnProperty.DataType), "数据类型"),
            new(nameof(TableColumnProperty.IsNullable), "是否可空"),
            new(nameof(TableColumnProperty.IsPrimary), "是否主键"),
            new(nameof(TableColumnProperty.IsIdentity), "是否自增"),
            new(nameof(TableColumnProperty.DefaultValue), "默认值"),
            new(nameof(TableColumnProperty.Comment), "注释"),
        };

    public Task<ColumnDocumentationResultItem> GenerateAsync(
        ConnectionItem connection,
        IReadOnlyList<ColumnDocumentationProperty> properties,
        bool showTableComment,
        string filePath,
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

            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new InvalidOperationException("请设置文档输出文件路径。");
            }

            var selected = properties?.Where(p => p.IsChecked).ToList() ?? new List<ColumnDocumentationProperty>();
            if (selected.Count == 0)
            {
                throw new InvalidOperationException("请至少勾选一个列属性。");
            }

            var interpreter = DbInterpreterHelper.GetDbInterpreter(
                dbType, ConnectionHelper.ToConnectionInfo(connection));

            var option = new GenerateColumnDocumentationOption
            {
                FilePath = filePath,
                ShowTableComment = showTableComment,
                Properties = selected
                    .Select(p => new CustomProperty
                    {
                        PropertyName = p.PropertyName,
                        DisplayName = p.DisplayName,
                    })
                    .ToList(),
            };

            var generator = new DocumentationGenerator();
            var feedback = new FeedbackObserver(onFeedback);
            generator.Subscribe(feedback);

            onFeedback?.Invoke($"开始生成文档（{selected.Count} 个列属性）...");

            var result = await generator.Generate(interpreter, option, cancellationToken);

            if (result.IsOK)
            {
                onFeedback?.Invoke($"文档生成完成：{result.FilePath}");
                return new ColumnDocumentationResultItem(true, string.Empty, result.FilePath ?? string.Empty);
            }

            onFeedback?.Invoke($"文档生成失败：{result.Message}");
            return new ColumnDocumentationResultItem(false, result.Message ?? string.Empty, string.Empty);
        }, cancellationToken);
    }

    /// <summary>反馈观察者：将 <see cref="FeedbackInfo"/> 消息转发到回调。</summary>
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
