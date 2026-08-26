using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DatabaseInterpreter.Model;
using DatabaseManager.AppCore.Common;
using DatabaseManager.AppCore.Models;
using DatabaseManager.AppCore.Services;

namespace DatabaseManager.AppCore.ViewModels;

/// <summary>
/// 数据库转换 ViewModel（阶段 4）。
/// 跨库结构/数据转换：选择源/目标连接、转换模式与选项，执行转换并展示反馈日志。
/// 支持：Schema 预览（翻译目标结构供编辑）与 Schema 映射。
/// </summary>
public partial class ConvertViewModel : ViewModelBase
{
    private readonly IDbConnectionService _connectionService;
    private readonly IConvertService _convertService;

    /// <summary>全部已保存连接（源/目标下拉共用）。</summary>
    public ObservableCollection<ConnectionItem> Connections { get; } = new();

    /// <summary>可用的转换模式。</summary>
    public IReadOnlyList<ConvertModeOption> Modes { get; }

    /// <summary>转换日志。</summary>
    public ObservableCollection<string> Logs { get; } = new();

    /// <summary>Schema 映射列表（源 Schema → 目标 Schema）。</summary>
    public ObservableCollection<SchemaMappingItem> SchemaMappings { get; } = new();

    /// <summary>Schema 预览表集合（预览后填充）。</summary>
    public ObservableCollection<SchemaPreviewTable> PreviewTables { get; } = new();

    [ObservableProperty]
    private ConnectionItem? _sourceConnection;

    [ObservableProperty]
    private ConnectionItem? _targetConnection;

    [ObservableProperty]
    private ConvertModeOption? _selectedMode;

    [ObservableProperty]
    private bool _executeOnTargetServer = true;

    [ObservableProperty]
    private bool _useTransaction;

    [ObservableProperty]
    private bool _bulkCopy;

    [ObservableProperty]
    private bool _continueWhenErrorOccurs;

    [ObservableProperty]
    private bool _createSchemaIfNotExists;

    [ObservableProperty]
    private bool _needPreview;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private bool _isPreviewing;

    [ObservableProperty]
    private bool _hasPreview;

    [ObservableProperty]
    private SchemaPreviewTable? _selectedPreviewTable;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    public ConvertViewModel(IDbConnectionService connectionService, IConvertService convertService)
    {
        _connectionService = connectionService;
        _convertService = convertService;

        Modes = new[]
        {
            new ConvertModeOption(ConvertMode.Schema, "仅结构 (Schema)"),
            new ConvertModeOption(ConvertMode.Data, "仅数据 (Data)"),
            new ConvertModeOption(ConvertMode.SchemaAndData, "结构 + 数据"),
        };

        SelectedMode = Modes.Last();
    }

    /// <summary>加载已保存的连接并刷新源/目标选择。</summary>
    public void RefreshConnections()
    {
        var previousSourceId = SourceConnection?.Id;
        var previousTargetId = TargetConnection?.Id;

        Connections.Clear();
        foreach (var item in _connectionService.GetConnections())
        {
            Connections.Add(item);
        }

        SourceConnection = FindConnection(previousSourceId) ?? Connections.FirstOrDefault();
        TargetConnection = FindConnection(previousTargetId) ?? Connections.Skip(1).FirstOrDefault();
    }

    private ConnectionItem? FindConnection(string? id)
        => Connections.FirstOrDefault(c => c.Id == id);

    /// <summary>加载 Schema 映射（自动映射 + 可编辑）。</summary>
    [RelayCommand]
    private async Task LoadSchemaMappingsAsync()
    {
        if (SourceConnection is null || TargetConnection is null)
        {
            StatusMessage = "请先选择源连接和目标连接。";
            return;
        }

        IsBusy = true;
        StatusMessage = string.Empty;

        try
        {
            var result = await _convertService.LoadSchemaMappingsAsync(SourceConnection, TargetConnection);

            if (!result.IsSuccess)
            {
                StatusMessage = result.Message;
                return;
            }

            SchemaMappings.Clear();
            foreach (var mapping in result.Mappings)
            {
                SchemaMappings.Add(new SchemaMappingItem
                {
                    SourceSchema = mapping.SourceSchema,
                    TargetSchema = mapping.TargetSchema,
                    SourceSchemas = result.SourceSchemas,
                    TargetSchemas = result.TargetSchemas,
                });
            }

            // 无自动映射时，填充一个空白行供用户编辑。
            if (SchemaMappings.Count == 0)
            {
                SchemaMappings.Add(new SchemaMappingItem
                {
                    SourceSchemas = result.SourceSchemas,
                    TargetSchemas = result.TargetSchemas,
                });
            }

            StatusMessage = $"已加载 Schema 映射：{SchemaMappings.Count} 条（源库 {result.SourceSchemas.Count} 个 Schema / 目标库 {result.TargetSchemas.Count} 个 Schema）。";
        }
        catch (Exception ex)
        {
            StatusMessage = $"加载 Schema 映射失败：{ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>新增一条空白 Schema 映射。</summary>
    [RelayCommand]
    private void AddSchemaMapping()
    {
        var sourceSchemas = SchemaMappings.FirstOrDefault()?.SourceSchemas ?? new List<string>();
        var targetSchemas = SchemaMappings.FirstOrDefault()?.TargetSchemas ?? new List<string>();

        SchemaMappings.Add(new SchemaMappingItem
        {
            SourceSchemas = sourceSchemas,
            TargetSchemas = targetSchemas,
        });
    }

    /// <summary>移除指定 Schema 映射。</summary>
    [RelayCommand]
    private void RemoveSchemaMapping(SchemaMappingItem? item)
    {
        if (item is not null)
        {
            SchemaMappings.Remove(item);
        }
    }

    /// <summary>预览后编辑过的目标 Schema（从 PreviewTables 重建），供执行转换时直接使用。</summary>
    private SchemaInfo? _editedTargetSchema;

    /// <summary>防止重复预览的守卫（已成功执行过预览，即使 0 表也不再重复触发）。</summary>
    private bool _previewCompleted;

    /// <summary>NeedPreview 开关切换时重置预览完成守卫，允许重新预览。</summary>
    partial void OnNeedPreviewChanged(bool value)
    {
        _previewCompleted = false;
    }

    /// <summary>生成转换预览（目标 Schema 结构，不执行转换）。</summary>
    [RelayCommand]
    private async Task PreviewAsync()
    {
        if (SourceConnection is null || TargetConnection is null)
        {
            StatusMessage = "请选择源连接和目标连接。";
            return;
        }

        IsPreviewing = true;
        StatusMessage = string.Empty;
        _editedTargetSchema = null;
        _previewCompleted = false;

        var feedbackBuffer = new List<string>();
        void CollectFeedback(string message) => feedbackBuffer.Add(message);

        try
        {
            var options = BuildOptions();
            options.NeedPreview = true;

            AppendLog("正在生成 Schema 预览...");
            var result = await _convertService.PreviewAsync(
                SourceConnection,
                TargetConnection,
                options,
                CollectFeedback);

            foreach (var line in feedbackBuffer)
            {
                AppendLog(line);
            }

            if (!result.IsSuccess)
            {
                StatusMessage = result.Message;
                AppendLog(result.Message);
                HasPreview = false;
                PreviewTables.Clear();
                return;
            }

            PopulatePreviewTables(result.TranslatedSchemaInfo);
            // 即使 0 表也标记为预览成功（避免 NeedPreview 分支重复触发形成死循环）。
            _previewCompleted = true;
            HasPreview = true;
            StatusMessage = PreviewTables.Count == 0
                ? "预览生成完成，但源库未包含任何可转换的表/视图对象。"
                : result.Message;
            AppendLog(StatusMessage);
        }
        catch (Exception ex)
        {
            StatusMessage = $"生成预览失败：{ex.Message}";
            AppendLog(StatusMessage);
            HasPreview = false;
            PreviewTables.Clear();
        }
        finally
        {
            IsPreviewing = false;
        }
    }

    /// <summary>从当前预览编辑状态重建目标 Schema（将预览列编辑写回翻译结构）。</summary>
    private SchemaInfo? BuildEditedTargetSchema()
    {
        if (!HasPreview || PreviewTables.Count == 0)
            return null;

        var schemaInfo = new SchemaInfo();

        foreach (var previewTable in PreviewTables)
        {
            schemaInfo.Tables.Add(new Table
            {
                Schema = string.IsNullOrEmpty(previewTable.Schema) ? null : previewTable.Schema,
                Name = previewTable.Name,
            });

            foreach (var col in previewTable.Columns)
            {
                // 克隆后回写，避免污染预览源对象
                var src = col.SourceColumn;
                var tableColumn = new DatabaseInterpreter.Model.TableColumn
                {
                    Name = src.Name,
                    Schema = src.Schema,
                    TableName = src.TableName,
                    DataType = col.DataType,
                    DataTypeSchema = src.DataTypeSchema,
                    MaxLength = col.MaxLength,
                    Precision = col.Precision,
                    Scale = col.Scale,
                    DefaultValue = string.IsNullOrEmpty(col.DefaultValue) ? null : col.DefaultValue,
                    IsNullable = src.IsNullable,
                    IsIdentity = src.IsIdentity,
                    ComputeExp = src.ComputeExp,
                    Order = src.Order,
                    Comment = src.Comment,
                };
                schemaInfo.TableColumns.Add(tableColumn);
            }
        }

        return schemaInfo;
    }

    /// <summary>填充预览表集合（从翻译后的 SchemaInfo）。</summary>
    private void PopulatePreviewTables(SchemaInfo? schemaInfo)
    {
        PreviewTables.Clear();

        if (schemaInfo is null)
            return;

        foreach (var table in schemaInfo.Tables)
        {
            var previewTable = new SchemaPreviewTable
            {
                Schema = table.Schema ?? string.Empty,
                Name = table.Name,
            };

            var columns = schemaInfo.TableColumns
                .Where(c => string.Equals(c.TableName, table.Name, StringComparison.OrdinalIgnoreCase)
                            && (string.IsNullOrEmpty(table.Schema) || string.Equals(c.Schema, table.Schema, StringComparison.OrdinalIgnoreCase)))
                .OrderBy(c => c.Order)
                .ToList();

            foreach (var column in columns)
            {
                previewTable.Columns.Add(new SchemaPreviewColumn
                {
                    Name = column.Name,
                    DataType = column.DataType ?? string.Empty,
                    MaxLength = column.MaxLength,
                    Precision = column.Precision,
                    Scale = column.Scale,
                    DefaultValue = column.DefaultValue ?? string.Empty,
                    // 深拷贝源列，避免后续编辑通过引用污染翻译器的原始 SchemaInfo。
                    SourceColumn = CloneTableColumn(column),
                });
            }

            PreviewTables.Add(previewTable);
        }
    }

    /// <summary>深拷贝 TableColumn（仅拷贝会参与后续写回的关键字段）。</summary>
    private static TableColumn CloneTableColumn(TableColumn src)
    {
        return new TableColumn
        {
            Name = src.Name,
            Schema = src.Schema,
            TableName = src.TableName,
            DataType = src.DataType,
            DataTypeSchema = src.DataTypeSchema,
            MaxLength = src.MaxLength,
            Precision = src.Precision,
            Scale = src.Scale,
            DefaultValue = src.DefaultValue,
            IsNullable = src.IsNullable,
            IsIdentity = src.IsIdentity,
            ComputeExp = src.ComputeExp,
            IsUserDefined = src.IsUserDefined,
            IsPersisted = src.IsPersisted,
            IsGeneratedAlways = src.IsGeneratedAlways,
            ScriptComment = src.ScriptComment,
            Values = src.Values,
            Order = src.Order,
            Comment = src.Comment,
        };
    }

    /// <summary>执行转换（若勾选预览，则先执行预览 → 编辑 → 确认后再转换）。</summary>
    [RelayCommand]
    private async Task ExecuteAsync()
    {
        if (SelectedMode is null)
        {
            StatusMessage = "请选择转换模式。";
            return;
        }

        if (SourceConnection is null || TargetConnection is null)
        {
            StatusMessage = "请选择源连接和目标连接。";
            return;
        }

        // 勾选预览时，先生成预览供用户确认（使用 _previewCompleted 守卫，避免 0 表时重复触发）。
        if (NeedPreview && !_previewCompleted)
        {
            await PreviewAsync();
            if (!_previewCompleted)
            {
                StatusMessage = "预览生成失败，请检查连接与选项。";
                return;
            }
        }

        IsBusy = true;
        StatusMessage = string.Empty;
        Logs.Clear();

        try
        {
            var options = BuildOptions();
            options.NeedPreview = false;

            // 若已生成预览并编辑，则基于编辑后的目标 Schema 执行转换。
            var editedSchema = BuildEditedTargetSchema();

            // 转换过程反馈在后台线程触发，这里先收集到临时缓冲，
            // await 回到 UI 线程后一次性刷新到 Logs，避免跨线程修改 UI 集合。
            var feedbackBuffer = new List<string>();
            void CollectFeedback(string message) => feedbackBuffer.Add(message);

            AppendLog($"源：{SourceConnection.Description}");
            AppendLog($"目标：{TargetConnection.Description}");
            AppendLog($"模式：{SelectedMode.DisplayName}");
            if (editedSchema is not null)
            {
                AppendLog("基于 Schema 预览编辑后的目标结构执行转换。");
            }
            AppendLog("开始转换...");

            var result = await _convertService.ConvertAsync(
                SourceConnection,
                TargetConnection,
                SelectedMode.Value,
                options,
                CollectFeedback,
                editedSchema);

            foreach (var line in feedbackBuffer)
            {
                AppendLog(line);
            }

            if (result.IsCanceled)
            {
                StatusMessage = "转换已取消。";
            }
            else
            {
                StatusMessage = result.Message;
                AppendLog(result.Message);
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"转换失败：{ex.Message}";
            AppendLog(StatusMessage);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private ConvertOptions BuildOptions()
    {
        return new ConvertOptions
        {
            ExecuteScriptOnTargetServer = ExecuteOnTargetServer,
            UseTransaction = UseTransaction,
            BulkCopy = BulkCopy,
            ContinueWhenErrorOccurs = ContinueWhenErrorOccurs,
            CreateSchemaIfNotExists = CreateSchemaIfNotExists,
            NeedPreview = NeedPreview,
            SchemaMappings = SchemaMappings
                .Where(m => !string.IsNullOrWhiteSpace(m.SourceSchema) || !string.IsNullOrWhiteSpace(m.TargetSchema))
                .Select(m => new SchemaMappingInfo
                {
                    SourceSchema = m.SourceSchema,
                    TargetSchema = m.TargetSchema,
                })
                .ToList(),
        };
    }

    private void AppendLog(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;

        var time = DateTime.Now.ToString("HH:mm:ss");
        Logs.Add($"[{time}] {message}");
    }
}

/// <summary>转换模式下拉选项。</summary>
public sealed record ConvertModeOption(string Value, string DisplayName);

/// <summary>Schema 映射项（UI 友好，含可选 Schema 列表）。</summary>
public class SchemaMappingItem : ObservableObject
{
    private string _sourceSchema = string.Empty;
    private string _targetSchema = string.Empty;

    /// <summary>源 Schema。</summary>
    public string SourceSchema
    {
        get => _sourceSchema;
        set => SetProperty(ref _sourceSchema, value);
    }

    /// <summary>目标 Schema。</summary>
    public string TargetSchema
    {
        get => _targetSchema;
        set => SetProperty(ref _targetSchema, value);
    }

    /// <summary>可选的源 Schema 列表。</summary>
    public List<string> SourceSchemas { get; set; } = new();

    /// <summary>可选的目标 Schema 列表。</summary>
    public List<string> TargetSchemas { get; set; } = new();
}
