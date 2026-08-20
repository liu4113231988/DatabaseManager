using System.Data;
using System.IO;
using DatabaseInterpreter.Core;
using DatabaseInterpreter.Model;
using DatabaseInterpreter.Utility;
using DatabaseManager.AppCore.Models;
using DatabaseManager.Core;
using DatabaseManager.Core.Model;
using DatabaseManager.FileUtility.Model;

namespace DatabaseManager.AppCore.Services;

/// <summary>
/// 导入 / 导出服务实现（阶段 6 / M6）。
/// 复用 <c>DatabaseManager.Core.DataExporter</c> / <c>DataImporter</c> 与 <c>DatabaseManager.FileUtility</c>
/// 完成表/视图数据到 CSV / Excel 的导入导出，支持列映射。
/// </summary>
public class DefaultExportImportService : IExportImportService
{
    private readonly IDbConnectionService _connectionService;

    public DefaultExportImportService(IDbConnectionService connectionService)
    {
        _connectionService = connectionService;
    }

    public IReadOnlyList<string> GetExportFormats()
        => new[] { "Excel", "CSV" };

    public async Task<IReadOnlyList<TableItem>> GetTablesAsync(
        ConnectionItem connection,
        CancellationToken cancellationToken = default)
    {
        var interpreter = CreateInterpreter(connection);
        var tables = await interpreter.GetTablesAsync();
        return tables
            .Select(t => new TableItem(t.Name, t.Schema, t.Schema is null ? t.Name : $"{t.Schema}.{t.Name}"))
            .ToList();
    }

    public async Task<IReadOnlyList<string>> GetTableColumnsAsync(
        ConnectionItem connection,
        string tableName,
        string? schema,
        CancellationToken cancellationToken = default)
    {
        var interpreter = CreateInterpreter(connection);
        var filter = new SchemaInfoFilter { Schema = schema, TableNames = new[] { tableName } };
        var columns = await interpreter.GetTableColumnsAsync(filter);
        return columns.Select(c => c.Name).ToList();
    }

    public async Task<ExportResult> ExportDataAsync(
        ConnectionItem connection,
        string tableName,
        string? schema,
        bool isView,
        string format,
        string filePath,
        bool showColumnNames = true,
        Action<string>? onFeedback = null,
        CancellationToken cancellationToken = default)
    {
        var result = new ExportResult();

        try
        {
            var interpreter = CreateInterpreter(connection);
            var option = new ExportSpecificDataOption
            {
                FileType = ParseExportFileType(format),
                ShowColumnNames = showColumnNames,
                FilePath = filePath,
                ExportAllThatMeetCondition = true,
                PageSize = 1000,
            };

            DatabaseObject dbObject;
            if (isView)
            {
                dbObject = new View { Schema = schema, Name = tableName };
            }
            else
            {
                dbObject = new Table { Schema = schema, Name = tableName };
            }

            // 读取列定义（供导出列选择）。
            var filter = new SchemaInfoFilter { Schema = schema, TableNames = new[] { tableName } };
            var tableColumns = await interpreter.GetTableColumnsAsync(filter);

            var columns = tableColumns.Select(c => new DataExportColumn
            {
                ColumnName = c.Name,
                DataType = c.DataType,
                DisplayName = c.Name,
            }).ToList();

            onFeedback?.Invoke($"正在导出 {tableName} 数据到 {format}...");

            var exporter = new DataExporter();
            exporter.Subscribe(new ExportFeedbackObserver(onFeedback));

            var exportResult = await exporter.Export(interpreter, dbObject, columns, option, cancellationToken);

            if (exportResult.IsOK)
            {
                result.IsSuccess = true;
                result.FilePath = exportResult.FilePath;
                result.Message = $"导出成功：{exportResult.FilePath}";
                onFeedback?.Invoke(result.Message);
            }
            else
            {
                result.IsSuccess = false;
                result.Message = exportResult.Message ?? "导出失败。";
                onFeedback?.Invoke(result.Message);
            }

            return result;
        }
        catch (OperationCanceledException)
        {
            result.Message = "导出已取消。";
            onFeedback?.Invoke(result.Message);
            return result;
        }
        catch (Exception ex)
        {
            result.Message = $"导出失败：{ex.Message}";
            onFeedback?.Invoke(result.Message);
            return result;
        }
    }

    public async Task<ImportResult> ImportDataAsync(
        ConnectionItem connection,
        string tableName,
        string? schema,
        string filePath,
        bool firstRowIsColumnName = true,
        IReadOnlyList<ColumnMappingItem>? columnMappings = null,
        Action<string>? onFeedback = null,
        CancellationToken cancellationToken = default)
    {
        var result = new ImportResult();

        try
        {
            var interpreter = CreateInterpreter(connection);
            var table = new Table { Schema = schema, Name = tableName };

            var sourceFileInfo = new SourceFileInfo
            {
                FilePath = filePath,
                FirstRowIsColumnName = firstRowIsColumnName,
            };

            // 将 UI 友好的列映射转换为核心库模型。
            List<DataImportColumnMapping>? mappings = null;
            if (columnMappings is { Count: > 0 })
            {
                mappings = columnMappings.Select(m => new DataImportColumnMapping
                {
                    TableColumName = m.SourceColumn,
                    FileColumnName = m.TargetColumn,
                }).ToList();
            }

            onFeedback?.Invoke($"正在从 {Path.GetFileName(filePath)} 导入数据到 {tableName}...");

            var importer = new DataImporter();
            importer.Subscribe(new ExportFeedbackObserver(onFeedback));

            var (success, validateResult) = await importer.Import(
                interpreter, table, sourceFileInfo, mappings, cancellationToken);

            if (success)
            {
                result.IsSuccess = true;
                result.Message = "导入成功。";
                onFeedback?.Invoke(result.Message);
            }
            else
            {
                result.IsSuccess = false;
                result.Message = validateResult?.IsValid == false
                    ? $"导入失败：数据校验未通过（共 {validateResult.Rows?.Count ?? 0} 行待检查）。"
                    : "导入失败。";
                onFeedback?.Invoke(result.Message);
            }

            return result;
        }
        catch (OperationCanceledException)
        {
            result.Message = "导入已取消。";
            onFeedback?.Invoke(result.Message);
            return result;
        }
        catch (Exception ex)
        {
            result.Message = $"导入失败：{ex.Message}";
            onFeedback?.Invoke(result.Message);
            return result;
        }
    }

    private static ExportFileType ParseExportFileType(string format)
        => format?.Trim().Equals("CSV", StringComparison.OrdinalIgnoreCase) == true
            ? ExportFileType.CSV
            : ExportFileType.EXCEL;

    private DbInterpreter CreateInterpreter(ConnectionItem connection)
    {
        var dbType = ParseDatabaseType(connection.DatabaseType);

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
            ObjectFetchMode = DatabaseObjectFetchMode.Simple,
            ShowTextForGeometry = true,
        };

        return DbInterpreterHelper.GetDbInterpreter(dbType, connectionInfo, option);
    }

    private ConnectionItem? FindConnection(string connectionName)
        => _connectionService.GetConnections().FirstOrDefault(c =>
            string.Equals(c.Name, connectionName, StringComparison.OrdinalIgnoreCase));

    private static DatabaseType ParseDatabaseType(string databaseType)
    {
        if (Enum.TryParse<DatabaseType>(databaseType, true, out var type))
            return type;
        return DatabaseType.Unknown;
    }

    /// <summary>反馈观察者：将核心库 <see cref="FeedbackInfo"/> 转发到回调日志。</summary>
    private sealed class ExportFeedbackObserver : IObserver<FeedbackInfo>
    {
        private readonly Action<string>? _onFeedback;

        public ExportFeedbackObserver(Action<string>? onFeedback)
        {
            _onFeedback = onFeedback;
        }

        public void OnCompleted() { }

        public void OnError(Exception error)
            => _onFeedback?.Invoke($"错误：{error.Message}");

        public void OnNext(FeedbackInfo value)
        {
            if (string.IsNullOrWhiteSpace(value.Message))
                return;
            _onFeedback?.Invoke(value.Message);
        }
    }
}
