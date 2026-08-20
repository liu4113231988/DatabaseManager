using DatabaseInterpreter.Core;
using DatabaseInterpreter.Model;
using DatabaseInterpreter.Utility;
using DatabaseManager.AppCore.Models;
using DatabaseManager.Core;
using DatabaseManager.Core.Model;

namespace DatabaseManager.AppCore.Services;

/// <summary>
/// 代码生成服务实现（阶段 5）。接入 <c>DatabaseManager.Core.CodeGenerator</c>。
/// </summary>
public class DefaultCodeGenerateService : ICodeGenerateService
{
    public Task<IReadOnlyList<CodeGenerateTarget>> GetTargetsAsync(
        ConnectionItem connection,
        CancellationToken cancellationToken = default)
    {
        return Task.Run(async () =>
        {
            var dbType = ConnectionHelper.ParseDatabaseType(connection.DatabaseType);
            if (dbType == DatabaseType.Unknown || string.IsNullOrEmpty(connection.Database))
            {
                throw new InvalidOperationException("连接或数据库无效。");
            }

            var interpreter = DbInterpreterHelper.GetDbInterpreter(
                dbType, ConnectionHelper.ToConnectionInfo(connection),
                new DbInterpreterOption { ObjectFetchMode = DatabaseObjectFetchMode.Simple });

            var schemaInfo = await interpreter.GetSchemaInfoAsync(new SchemaInfoFilter
            {
                DatabaseObjectType = DatabaseObjectType.Table | DatabaseObjectType.View,
            });

            var targets = new List<CodeGenerateTarget>();
            foreach (var table in schemaInfo.Tables.OrderBy(t => t.Name))
            {
                targets.Add(new CodeGenerateTarget("Table", table.Name, table.Schema));
            }

            foreach (var view in schemaInfo.Views.OrderBy(v => v.Name))
            {
                targets.Add(new CodeGenerateTarget("View", view.Name, view.Schema));
            }

            return (IReadOnlyList<CodeGenerateTarget>)targets;
        }, cancellationToken);
    }

    public Task<CodeGenerateResultItem> GenerateAsync(
        ConnectionItem connection,
        IReadOnlyList<CodeGenerateTarget> targets,
        string language,
        string? namespaceName,
        bool generateComments,
        string outputFolder,
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

            if (targets == null || targets.Count == 0)
            {
                throw new InvalidOperationException("请选择要生成代码的表或视图。");
            }

            var interpreter = DbInterpreterHelper.GetDbInterpreter(
                dbType, ConnectionHelper.ToConnectionInfo(connection));

            var option = new CodeGenerateOption
            {
                OutputFolder = outputFolder,
                Language = ParseLanguage(language),
                Namespace = namespaceName ?? string.Empty,
                GenerateComments = generateComments,
                Tables = new List<Table>(),
                Views = new List<View>(),
            };

            // 按名称加载所选表 / 视图对象（含 Schema）。
            var tableNames = targets.Where(t => t.ObjectType == "Table").Select(t => t.Name).ToArray();
            var viewNames = targets.Where(t => t.ObjectType == "View").Select(t => t.Name).ToArray();

            var schemaFilter = new SchemaInfoFilter();
            if (tableNames.Length > 0) schemaFilter.TableNames = tableNames;
            if (viewNames.Length > 0) schemaFilter.ViewNames = viewNames;

            var schemaInfo = await interpreter.GetSchemaInfoAsync(schemaFilter);

            foreach (var table in schemaInfo.Tables)
            {
                option.Tables.Add(table);
            }

            foreach (var view in schemaInfo.Views)
            {
                option.Views.Add(view);
            }

            if (option.Tables.Count == 0 && option.Views.Count == 0)
            {
                throw new InvalidOperationException("未能加载所选对象的结构。");
            }

            var generator = new CodeGenerator(interpreter, option);
            var feedback = new FeedbackObserver(onFeedback);
            generator.Subscribe(feedback);

            onFeedback?.Invoke($"开始生成代码（{targets.Count} 个对象，输出目录：{outputFolder}）...");

            var result = await generator.Generate(cancellationToken);

            if (result.IsOK)
            {
                onFeedback?.Invoke("代码生成完成。");
                return new CodeGenerateResultItem(true, string.Empty);
            }

            onFeedback?.Invoke($"代码生成失败：{result.Message}");
            return new CodeGenerateResultItem(false, result.Message ?? string.Empty);
        }, cancellationToken);
    }

    private static ProgrammingLanguage ParseLanguage(string language)
        => language switch
        {
            "CSharp" => ProgrammingLanguage.CSharp,
            "Java" => ProgrammingLanguage.Java,
            _ => ProgrammingLanguage.CSharp,
        };

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
