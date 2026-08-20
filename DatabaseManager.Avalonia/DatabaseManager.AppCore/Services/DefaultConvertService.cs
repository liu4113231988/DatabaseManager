using DatabaseConverter.Core;
using DatabaseConverter.Model;
using DatabaseInterpreter.Core;
using DatabaseInterpreter.Model;
using DatabaseInterpreter.Utility;
using DatabaseManager.AppCore.Models;

namespace DatabaseManager.AppCore.Services;

/// <summary>
/// 转换服务实现（阶段 4）。接入 <c>DatabaseConverter</c> 完成跨库结构/数据转换。
/// 支持 Schema 预览（NeedPreview → 目标 Schema 翻译）、Schema 映射加载（对应 frmSchemaMapping）。
/// </summary>
public class DefaultConvertService : IConvertService
{
    public IReadOnlyList<string> GetSupportedConverters()
    {
        // 所有非 Unknown 的数据库类型均可作为源/目标进行转换。
        var types = Enum.GetValues<DatabaseType>()
                        .Where(t => t != DatabaseType.Unknown)
                        .Select(t => t.ToString())
                        .ToList();

        return types;
    }

    public async Task<ConvertResult> ConvertAsync(
        ConnectionItem source,
        ConnectionItem target,
        string mode,
        ConvertOptions? options = null,
        Action<string>? onFeedback = null,
        SchemaInfo? targetSchemaInfo = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new ConvertOptions();
        var result = new ConvertResult { Mode = mode };

        try
        {
            if (!ValidateConnections(source, target, result, out var sourceDbType, out var targetDbType))
                return result;

            // 转换模式。
            var scriptMode = ToGenerateScriptMode(mode);
            if (scriptMode == GenerateScriptMode.None)
            {
                result.ResultType = ConvertResultType.Error;
                result.Message = "请指定转换模式（结构 / 数据 / 结构+数据）。";
                return result;
            }

            // 构建源/目标解释器。
            var sourceInterpreter = CreateInterpreter(source, sourceDbType);
            var targetInterpreter = CreateInterpreter(target, targetDbType);

            // 获取源数据库完整 Schema（含表/视图/函数/过程等对象及子对象）。
            Feedback(result, onFeedback, "正在读取源数据库对象信息...");
            var schemaInfo = await sourceInterpreter.GetSchemaInfoAsync(new SchemaInfoFilter
            {
                DatabaseObjectType =
                    DatabaseObjectType.Table | DatabaseObjectType.View |
                    DatabaseObjectType.Function | DatabaseObjectType.Procedure |
                    DatabaseObjectType.Type | DatabaseObjectType.Sequence,
            });

            var sourceInfo = new DbConveterInfo { DbInterpreter = sourceInterpreter };
            var targetInfo = new DbConveterInfo { DbInterpreter = targetInterpreter };

            using var converter = new DbConverter(sourceInfo, targetInfo);
            var option = converter.Option;

            option.GenerateScriptMode = scriptMode;
            option.ExecuteScriptOnTargetServer = options.ExecuteScriptOnTargetServer;
            option.UseTransaction = options.UseTransaction;
            option.BulkCopy = options.BulkCopy;
            option.ContinueWhenErrorOccurs = options.ContinueWhenErrorOccurs;
            option.CreateSchemaIfNotExists = options.CreateSchemaIfNotExists;
            option.NcharToDoubleChar = options.NcharToDoubleChar;
            option.SplitScriptsToExecute = true;
            option.CollectTranslateResultAfterTranslated = false;
            option.NeedPreview = false;
            option.SchemaMappings = options.SchemaMappings;

            converter.Subscribe(new FeedbackObserver(result, onFeedback));

            Feedback(result, onFeedback, "开始转换...");
            var convertResult = await converter.Convert(cancellationToken, schemaInfo, null, targetSchemaInfo);

            result.ResultType = convertResult.InfoType switch
            {
                DbConvertResultInfoType.Warnning => ConvertResultType.Warning,
                DbConvertResultInfoType.Error => ConvertResultType.Error,
                _ => ConvertResultType.Information,
            };
            result.Message = convertResult.Message;

            return result;
        }
        catch (OperationCanceledException)
        {
            result.IsCanceled = true;
            result.ResultType = ConvertResultType.Warning;
            result.Message = "转换已取消。";
            return result;
        }
        catch (Exception ex)
        {
            result.ResultType = ConvertResultType.Error;
            result.Message = $"转换失败：{ex.Message}";
            Feedback(result, onFeedback, result.Message);
            return result;
        }
    }

    public async Task<ConvertPreviewResult> PreviewAsync(
        ConnectionItem source,
        ConnectionItem target,
        ConvertOptions? options = null,
        Action<string>? onFeedback = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new ConvertOptions();
        var result = new ConvertPreviewResult();

        try
        {
            if (!ValidateConnections(source, target, result, out var sourceDbType, out var targetDbType))
                return result;

            // 构建源/目标解释器（Details 模式 + 按引用排序）。
            var sourceInterpreter = CreateInterpreter(source, sourceDbType);
            var targetInterpreter = CreateInterpreter(target, targetDbType);

            Feedback(result, onFeedback, "正在读取源数据库对象信息...");
            var schemaInfo = await sourceInterpreter.GetSchemaInfoAsync(new SchemaInfoFilter
            {
                DatabaseObjectType =
                    DatabaseObjectType.Table | DatabaseObjectType.View |
                    DatabaseObjectType.Function | DatabaseObjectType.Procedure |
                    DatabaseObjectType.Type | DatabaseObjectType.Sequence,
            });

            var sourceInfo = new DbConveterInfo { DbInterpreter = sourceInterpreter };
            var targetInfo = new DbConveterInfo { DbInterpreter = targetInterpreter };

            using var converter = new DbConverter(sourceInfo, targetInfo);
            var option = converter.Option;

            option.GenerateScriptMode = GenerateScriptMode.Schema;
            option.ExecuteScriptOnTargetServer = false;
            option.UseTransaction = false;
            option.BulkCopy = false;
            option.ContinueWhenErrorOccurs = true;
            option.CreateSchemaIfNotExists = options.CreateSchemaIfNotExists;
            option.NcharToDoubleChar = options.NcharToDoubleChar;
            option.SplitScriptsToExecute = true;
            option.CollectTranslateResultAfterTranslated = false;
            option.NeedPreview = true;   // 关键：仅翻译，不执行。
            option.SchemaMappings = options.SchemaMappings;

            var observer = new PreviewFeedbackObserver(result, onFeedback);
            converter.Subscribe(observer);

            Feedback(result, onFeedback, "正在生成目标 Schema 预览（翻译结构，不执行转换）...");
            var convertResult = await converter.Convert(cancellationToken, schemaInfo);

            if (convertResult.InfoType == DbConvertResultInfoType.Error)
            {
                result.IsSuccess = false;
                result.ResultType = ConvertResultType.Error;
                result.Message = convertResult.Message ?? "生成预览失败。";
                Feedback(result, onFeedback, result.Message);
                return result;
            }

            result.TranslatedSchemaInfo = convertResult.TranslatedSchemaInfo;
            result.IsSuccess = true;
            result.ResultType = ConvertResultType.Information;
            result.Message = $"预览生成完成，共 {result.TranslatedSchemaInfo?.Tables?.Count ?? 0} 张表。";
            Feedback(result, onFeedback, result.Message);

            return result;
        }
        catch (OperationCanceledException)
        {
            result.IsCanceled = true;
            result.ResultType = ConvertResultType.Warning;
            result.Message = "预览已取消。";
            return result;
        }
        catch (Exception ex)
        {
            result.IsSuccess = false;
            result.ResultType = ConvertResultType.Error;
            result.Message = $"生成预览失败：{ex.Message}";
            Feedback(result, onFeedback, result.Message);
            return result;
        }
    }

    public async Task<SchemaMappingLoadResult> LoadSchemaMappingsAsync(
        ConnectionItem source,
        ConnectionItem target,
        CancellationToken cancellationToken = default)
    {
        var result = new SchemaMappingLoadResult();

        try
        {
            var sourceDbType = ParseDatabaseType(source.DatabaseType);
            var targetDbType = ParseDatabaseType(target.DatabaseType);

            if (sourceDbType == DatabaseType.Unknown || targetDbType == DatabaseType.Unknown)
            {
                result.IsSuccess = false;
                result.Message = "源/目标数据库类型无效。";
                return result;
            }

            // 复用 DbConverter 的静态方法加载两侧 Schema 并自动映射。
            var sourceInfo = ConnectionHelper.ToConnectionInfo(source);
            var targetInfo = ConnectionHelper.ToConnectionInfo(target);

            (List<string> SourceSchemas, List<string> TargetSchemas) schemas =
                await DbConverter.GetSourceAndTargetSchemas(
                    sourceDbType, targetDbType, sourceInfo, targetInfo);

            result.SourceSchemas = schemas.SourceSchemas;
            result.TargetSchemas = schemas.TargetSchemas;
            result.Mappings = DbConverter.GetAutoMappedSchemas(result.SourceSchemas, result.TargetSchemas);
            result.IsSuccess = true;

            return result;
        }
        catch (Exception ex)
        {
            result.IsSuccess = false;
            result.Message = $"加载 Schema 映射失败：{ex.Message}";
            return result;
        }
    }

    #region Helpers

    private static bool ValidateConnections(
        ConnectionItem source,
        ConnectionItem target,
        ConvertResult result,
        out DatabaseType sourceDbType,
        out DatabaseType targetDbType)
    {
        sourceDbType = DatabaseType.Unknown;
        targetDbType = DatabaseType.Unknown;

        // 源/目标必须存在。
        if (string.IsNullOrEmpty(source.Database) || string.IsNullOrEmpty(target.Database))
        {
            result.ResultType = ConvertResultType.Error;
            result.Message = "源/目标数据库不能为空。";
            return false;
        }

        if (IsSameDatabase(source, target))
        {
            result.ResultType = ConvertResultType.Error;
            result.Message = "源数据库与目标数据库不能相同。";
            return false;
        }

        sourceDbType = ParseDatabaseType(source.DatabaseType);
        targetDbType = ParseDatabaseType(target.DatabaseType);

        if (sourceDbType == DatabaseType.Unknown || targetDbType == DatabaseType.Unknown)
        {
            result.ResultType = ConvertResultType.Error;
            result.Message = "源/目标数据库类型无效。";
            return false;
        }

        return true;
    }

    private static bool ValidateConnections(
        ConnectionItem source,
        ConnectionItem target,
        ConvertPreviewResult result,
        out DatabaseType sourceDbType,
        out DatabaseType targetDbType)
    {
        sourceDbType = DatabaseType.Unknown;
        targetDbType = DatabaseType.Unknown;

        if (string.IsNullOrEmpty(source.Database) || string.IsNullOrEmpty(target.Database))
        {
            result.IsSuccess = false;
            result.ResultType = ConvertResultType.Error;
            result.Message = "源/目标数据库不能为空。";
            return false;
        }

        if (IsSameDatabase(source, target))
        {
            result.IsSuccess = false;
            result.ResultType = ConvertResultType.Error;
            result.Message = "源数据库与目标数据库不能相同。";
            return false;
        }

        sourceDbType = ParseDatabaseType(source.DatabaseType);
        targetDbType = ParseDatabaseType(target.DatabaseType);

        if (sourceDbType == DatabaseType.Unknown || targetDbType == DatabaseType.Unknown)
        {
            result.IsSuccess = false;
            result.ResultType = ConvertResultType.Error;
            result.Message = "源/目标数据库类型无效。";
            return false;
        }

        return true;
    }

    private static bool IsSameDatabase(ConnectionItem a, ConnectionItem b)
        => string.Equals(a.Server, b.Server, StringComparison.OrdinalIgnoreCase)
           && string.Equals(a.Port, b.Port, StringComparison.OrdinalIgnoreCase)
           && string.Equals(a.Database, b.Database, StringComparison.OrdinalIgnoreCase);

    private static GenerateScriptMode ToGenerateScriptMode(string mode)
        => mode switch
        {
            ConvertMode.Schema => GenerateScriptMode.Schema,
            ConvertMode.Data => GenerateScriptMode.Data,
            ConvertMode.SchemaAndData => GenerateScriptMode.Schema | GenerateScriptMode.Data,
            _ => GenerateScriptMode.None,
        };

    private static DbInterpreter CreateInterpreter(ConnectionItem connection, DatabaseType dbType)
    {
        var connectionInfo = new ConnectionInfo
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

        var option = new DbInterpreterOption
        {
            ObjectFetchMode = DatabaseObjectFetchMode.Details,
            ScriptOutputMode = GenerateScriptOutputMode.WriteToString,
            SortObjectsByReference = true,
            GetTableAllObjects = true,
            ThrowExceptionWhenErrorOccurs = true,
        };

        return DbInterpreterHelper.GetDbInterpreter(dbType, connectionInfo, option);
    }

    private static DatabaseType ParseDatabaseType(string databaseType)
    {
        if (Enum.TryParse<DatabaseType>(databaseType, true, out var type))
            return type;
        return DatabaseType.Unknown;
    }

    private static void Feedback(ConvertResult result, Action<string>? onFeedback, string message)
    {
        result.Logs.Add(message);
        onFeedback?.Invoke(message);
    }

    private static void Feedback(ConvertPreviewResult result, Action<string>? onFeedback, string message)
    {
        result.Logs.Add(message);
        onFeedback?.Invoke(message);
    }

    /// <summary>
    /// 转换过程反馈观察者：捕获 <see cref="DbConverter"/> 的反馈日志。
    /// </summary>
    private sealed class FeedbackObserver : IObserver<FeedbackInfo>
    {
        private readonly ConvertResult _result;
        private readonly Action<string>? _onFeedback;

        public FeedbackObserver(ConvertResult result, Action<string>? onFeedback)
        {
            _result = result;
            _onFeedback = onFeedback;
        }

        public void OnCompleted() { }

        public void OnError(Exception error)
            => Feedback(_result, _onFeedback, $"错误：{error.Message}");

        public void OnNext(FeedbackInfo value)
        {
            if (string.IsNullOrWhiteSpace(value.Message))
                return;
            Feedback(_result, _onFeedback, value.Message);
        }
    }

    /// <summary>
    /// 预览过程反馈观察者。
    /// </summary>
    private sealed class PreviewFeedbackObserver : IObserver<FeedbackInfo>
    {
        private readonly ConvertPreviewResult _result;
        private readonly Action<string>? _onFeedback;

        public PreviewFeedbackObserver(ConvertPreviewResult result, Action<string>? onFeedback)
        {
            _result = result;
            _onFeedback = onFeedback;
        }

        public void OnCompleted() { }

        public void OnError(Exception error)
            => Feedback(_result, _onFeedback, $"错误：{error.Message}");

        public void OnNext(FeedbackInfo value)
        {
            if (string.IsNullOrWhiteSpace(value.Message))
                return;
            Feedback(_result, _onFeedback, value.Message);
        }
    }

    #endregion
}
