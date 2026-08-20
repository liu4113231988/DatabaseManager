using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DatabaseManager.AppCore.Common;
using DatabaseManager.AppCore.Models;
using DatabaseManager.AppCore.Services;

namespace DatabaseManager.AppCore.ViewModels;

/// <summary>
/// 表设计器 ViewModel（AppCore 层）。
/// 负责表结构（列/主键/索引/外键/约束）的加载、编辑集合维护、CREATE/ALTER 脚本生成与保存。
/// </summary>
public partial class TableDesignerViewModel : ViewModelBase
{
    private readonly ITableDesignService _designService;

    /// <summary>列定义集合。</summary>
    public ObservableCollection<TableDesignColumn> Columns { get; } = new();

    /// <summary>主键（可为空）。</summary>
    [ObservableProperty]
    private TableDesignKey? _primaryKey;

    /// <summary>索引集合。</summary>
    public ObservableCollection<TableDesignIndex> Indexes { get; } = new();

    /// <summary>外键集合。</summary>
    public ObservableCollection<TableDesignForeignKey> ForeignKeys { get; } = new();

    /// <summary>约束集合。</summary>
    public ObservableCollection<TableDesignConstraint> Constraints { get; } = new();

    /// <summary>当前连接名。</summary>
    [ObservableProperty]
    private string _connectionName = string.Empty;

    /// <summary>当前数据库名。</summary>
    [ObservableProperty]
    private string _databaseName = string.Empty;

    /// <summary>当前 Schema（可为空）。</summary>
    [ObservableProperty]
    private string _schema = string.Empty;

    /// <summary>表名。</summary>
    [ObservableProperty]
    private string _tableName = string.Empty;

    /// <summary>表注释。</summary>
    [ObservableProperty]
    private string _comment = string.Empty;

    /// <summary>是否为新建表。</summary>
    [ObservableProperty]
    private bool _isNew;

    /// <summary>是否已加载。</summary>
    [ObservableProperty]
    private bool _isLoaded;

    /// <summary>是否正忙。</summary>
    [ObservableProperty]
    private bool _isBusy;

    /// <summary>状态消息。</summary>
    [ObservableProperty]
    private string _statusMessage = string.Empty;

    /// <summary>生成的脚本预览。</summary>
    [ObservableProperty]
    private string _previewScript = string.Empty;

    /// <summary>是否有未保存改动。</summary>
    [ObservableProperty]
    private bool _hasChanges;

    public TableDesignerViewModel(ITableDesignService designService)
    {
        _designService = designService;
    }

    /// <summary>加载指定表的结构（isNew=true 时为新建空表）。</summary>
    public async Task<bool> LoadAsync(
        string connectionName,
        string databaseName,
        string tableName,
        string? schema,
        bool isNew)
    {
        ConnectionName = connectionName;
        DatabaseName = databaseName;
        TableName = tableName;
        Schema = schema ?? string.Empty;
        IsNew = isNew;

        return await LoadCoreAsync();
    }

    private async Task<bool> LoadCoreAsync()
    {
        IsBusy = true;
        StatusMessage = IsNew ? $"准备新建表 {TableName}..." : $"正在加载表 {TableName} 结构...";

        try
        {
            var result = await _designService.LoadTableAsync(
                ConnectionName,
                DatabaseName,
                TableName,
                string.IsNullOrEmpty(Schema) ? null : Schema,
                IsNew);

            if (!result.IsSuccess)
            {
                StatusMessage = $"加载失败：{result.ErrorMessage}";
                return false;
            }

            Apply(result.Design);
            IsLoaded = true;
            HasChanges = false;
            PreviewScript = string.Empty;
            StatusMessage = IsNew ? "新建表：请填写列定义后点击「保存」。"
                : $"已加载表 {TableName} 结构，共 {Columns.Count} 列。";
            return true;
        }
        catch (Exception ex)
        {
            StatusMessage = $"加载失败：{ex.Message}";
            return false;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void Apply(TableDesignInfo design)
    {
        TableName = design.Name;
        Schema = design.Schema ?? string.Empty;
        Comment = design.Comment;
        IsNew = design.IsNew;

        Columns.Clear();
        foreach (var col in design.Columns)
        {
            Columns.Add(col);
        }

        PrimaryKey = design.PrimaryKey is null
            ? null
            : new TableDesignKey
            {
                Name = design.PrimaryKey.Name,
                Clustered = design.PrimaryKey.Clustered,
                Columns = new(design.PrimaryKey.Columns),
            };

        Indexes.Clear();
        foreach (var idx in design.Indexes)
        {
            Indexes.Add(idx);
        }

        ForeignKeys.Clear();
        foreach (var fk in design.ForeignKeys)
        {
            ForeignKeys.Add(fk);
        }

        Constraints.Clear();
        foreach (var c in design.Constraints)
        {
            Constraints.Add(c);
        }
    }

    /// <summary>新增一列。</summary>
    [RelayCommand]
    private void AddColumn()
    {
        var col = new TableDesignColumn
        {
            Name = $"Column_{Columns.Count + 1}",
            DataType = "varchar",
            MaxLength = 100,
            IsNullable = true,
            Order = Columns.Count + 1,
        };
        Columns.Add(col);
        HasChanges = true;
        StatusMessage = $"已新增列 {col.Name}。";
    }

    /// <summary>删除指定列。</summary>
    [RelayCommand]
    private void RemoveColumn(TableDesignColumn? column)
    {
        if (column is null || !Columns.Remove(column))
            return;

        HasChanges = true;
        StatusMessage = $"已删除列 {column.Name}。";
    }

    /// <summary>新增索引。</summary>
    [RelayCommand]
    private void AddIndex()
    {
        var idx = new TableDesignIndex { Name = $"IX_{TableName}_{Indexes.Count + 1}" };
        Indexes.Add(idx);
        HasChanges = true;
        StatusMessage = "已新增索引，请选择列。";
    }

    /// <summary>删除指定索引。</summary>
    [RelayCommand]
    private void RemoveIndex(TableDesignIndex? index)
    {
        if (index is null || !Indexes.Remove(index))
            return;

        HasChanges = true;
        StatusMessage = $"已删除索引 {index.Name}。";
    }

    /// <summary>新增外键。</summary>
    [RelayCommand]
    private void AddForeignKey()
    {
        var fk = new TableDesignForeignKey { Name = $"FK_{TableName}_{ForeignKeys.Count + 1}" };
        ForeignKeys.Add(fk);
        HasChanges = true;
        StatusMessage = "已新增外键，请填写引用表与列映射。";
    }

    /// <summary>删除指定外键。</summary>
    [RelayCommand]
    private void RemoveForeignKey(TableDesignForeignKey? fk)
    {
        if (fk is null || !ForeignKeys.Remove(fk))
            return;

        HasChanges = true;
        StatusMessage = $"已删除外键 {fk.Name}。";
    }

    /// <summary>新增约束。</summary>
    [RelayCommand]
    private void AddConstraint()
    {
        var c = new TableDesignConstraint { Name = $"CK_{TableName}_{Constraints.Count + 1}" };
        Constraints.Add(c);
        HasChanges = true;
        StatusMessage = "已新增约束，请填写检查表达式。";
    }

    /// <summary>删除指定约束。</summary>
    [RelayCommand]
    private void RemoveConstraint(TableDesignConstraint? c)
    {
        if (c is null || !Constraints.Remove(c))
            return;

        HasChanges = true;
        StatusMessage = $"已删除约束 {c.Name}。";
    }

    /// <summary>预览生成脚本（不执行）。</summary>
    [RelayCommand]
    private async Task GeneratePreviewAsync()
    {
        if (!Validate())
            return;

        IsBusy = true;
        StatusMessage = "正在生成脚本...";

        try
        {
            var result = await _designService.GenerateScriptsAsync(
                ConnectionName,
                DatabaseName,
                BuildDesign());

            if (!result.IsSuccess)
            {
                StatusMessage = $"生成失败：{result.ErrorMessage}";
                return;
            }

            PreviewScript = result.Script;
            StatusMessage = result.HasScripts
                ? "已生成脚本，可点击「保存」执行。"
                : "结构无变化，无需生成脚本。";
        }
        catch (Exception ex)
        {
            StatusMessage = $"生成失败：{ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>保存（生成并执行脚本）。</summary>
    [RelayCommand]
    private async Task SaveAsync()
    {
        if (!Validate())
            return;

        IsBusy = true;
        StatusMessage = "正在保存表结构...";

        try
        {
            var result = await _designService.SaveAsync(
                ConnectionName,
                DatabaseName,
                BuildDesign());

            if (!result.IsSuccess)
            {
                StatusMessage = $"保存失败：{result.ErrorMessage}";
                return;
            }

            HasChanges = false;
            StatusMessage = result.ScriptCount > 0
                ? $"保存成功，执行了 {result.ScriptCount} 条脚本。"
                : "结构无变化，无需保存。";

            // 保存成功后重载最新结构（获取真实定义）。
            await LoadCoreAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"保存失败：{ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>基本校验：表名与列名非空。</summary>
    private bool Validate()
    {
        if (string.IsNullOrWhiteSpace(TableName))
        {
            StatusMessage = "表名不能为空。";
            return false;
        }

        foreach (var col in Columns)
        {
            if (string.IsNullOrWhiteSpace(col.Name))
            {
                StatusMessage = "存在列名为空的列，请修正。";
                return false;
            }

            if (string.IsNullOrWhiteSpace(col.DataType))
            {
                StatusMessage = $"列 {col.Name} 未指定数据类型。";
                return false;
            }
        }

        return true;
    }

    /// <summary>构建 <see cref="TableDesignInfo"/> 用于生成脚本。</summary>
    private TableDesignInfo BuildDesign()
    {
        return new TableDesignInfo
        {
            DatabaseName = DatabaseName,
            Schema = string.IsNullOrEmpty(Schema) ? null : Schema,
            Name = TableName,
            IsNew = IsNew,
            Comment = Comment,
            Columns = new(Columns),
            PrimaryKey = PrimaryKey,
            Indexes = new(Indexes),
            ForeignKeys = new(ForeignKeys),
            Constraints = new(Constraints),
        };
    }

    /// <summary>清空设计状态。</summary>
    public void Clear()
    {
        Columns.Clear();
        Indexes.Clear();
        ForeignKeys.Clear();
        Constraints.Clear();
        PrimaryKey = null;
        TableName = string.Empty;
        Schema = string.Empty;
        Comment = string.Empty;
        IsLoaded = false;
        IsNew = false;
        HasChanges = false;
        PreviewScript = string.Empty;
        StatusMessage = string.Empty;
    }
}
