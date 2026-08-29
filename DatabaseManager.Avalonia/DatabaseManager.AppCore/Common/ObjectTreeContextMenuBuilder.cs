using System;
using System.Text;
using Avalonia;                // <- 新增
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;  // <- 新增：IClassicDesktopStyleApplicationLifetime
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;          // <- 新增：TextWrapping
using DatabaseInterpreter.Model;
using DatabaseManager.AppCore.Models;
using DatabaseManager.AppCore.Services;
using DatabaseManager.AppCore.ViewModels;

namespace DatabaseManager.AppCore.Common;

/// <summary>
/// 对象树右键菜单构建器。
/// 按节点类型分发，为不同类型的节点（连接/数据库/Schema/文件夹/对象/子对象）构建对应的右键菜单。
/// P2 增强：菜单分组、快捷键提示、Generate SQL 扩展、Filter、Compare/Migrate、高级复制。
/// </summary>
public class ObjectTreeContextMenuBuilder
{
    private readonly MainWindowViewModel _viewModel;
    private readonly TreeView _treeView;
    private readonly Action<Func<Task>> _asyncAction;
    private readonly Action? _openConnectionManager;
    private readonly Action<DbObjectTreeNode, bool>? _openTableDesigner;
    private readonly Action<DbObjectTreeNode>? _openExportWindow;
    private readonly Action<DbObjectTreeNode>? _openImportWindow;
    private readonly Action<DbObjectTreeNode>? _openSchemaCompare;
    private readonly Action<DbObjectTreeNode>? _openDataCompare;
    private readonly Action<DbObjectTreeNode>? _openConvert;

    private readonly IDbConnectionService _connectionService;
    private readonly IDdlService? _ddlService;

    /// <summary>创建右键菜单构建器。</summary>
    public ObjectTreeContextMenuBuilder(
        MainWindowViewModel viewModel,
        TreeView treeView,
        Action<Func<Task>> asyncAction,
        IDbConnectionService connectionService,
        IDdlService? ddlService = null,
        Action? openConnectionManager = null,
        Action<DbObjectTreeNode, bool>? openTableDesigner = null,
        Action<DbObjectTreeNode>? openExportWindow = null,
        Action<DbObjectTreeNode>? openImportWindow = null,
        Action<DbObjectTreeNode>? openSchemaCompare = null,
        Action<DbObjectTreeNode>? openDataCompare = null,
        Action<DbObjectTreeNode>? openConvert = null)
    {
        _viewModel = viewModel;
        _treeView = treeView;
        _asyncAction = asyncAction;
        _connectionService = connectionService;
        _ddlService = ddlService;
        _openConnectionManager = openConnectionManager;
        _openTableDesigner = openTableDesigner;
        _openExportWindow = openExportWindow;
        _openImportWindow = openImportWindow;
        _openSchemaCompare = openSchemaCompare;
        _openDataCompare = openDataCompare;
        _openConvert = openConvert;
    }

    /// <summary>根据节点类型构建并显示右键菜单。</summary>
    public void BuildAndShow(DbObjectTreeNode node, ContextRequestedEventArgs e)
    {
        var menu = new ContextMenu();

        switch (node.NodeType)
        {
            case DbObjectTreeNodeType.Connection:
                BuildConnectionMenu(menu, node);
                break;
            case DbObjectTreeNodeType.Database:
                BuildDatabaseMenu(menu, node);
                break;
            case DbObjectTreeNodeType.Schema:
                BuildSchemaMenu(menu, node);
                break;
            case DbObjectTreeNodeType.Folder:
                BuildFolderMenu(menu, node);
                break;
            case DbObjectTreeNodeType.DbObject:
                BuildDbObjectMenu(menu, node);
                break;
            case DbObjectTreeNodeType.ChildFolder:
            case DbObjectTreeNodeType.ChildObject:
                BuildChildObjectMenu(menu, node);
                break;
            default:
                BuildDefaultMenu(menu, node);
                break;
        }

        // 外部菜单贡献者（IObjectTreeMenuContributor 注册）：在内置菜单之后追加。
        foreach (var contributor in ObjectTreeMenuRegistry.GetContributors(node))
        {
            contributor.Contribute(new ObjectTreeMenuContext(menu.Items, node, _viewModel, _asyncAction));
        }

        menu.Open(_treeView);
        e.Handled = true;
    }

    #region 连接节点右键菜单

    private void BuildConnectionMenu(ContextMenu menu, DbObjectTreeNode node)
    {
        // ==== Open/View 组 ====
        var connect = CreateMenuItem("连接\tEnter", "打开连接");
        connect.Click += (_, _) => _asyncAction(async () =>
        {
            await _viewModel.ConnectConnectionNodeAsync(node);
            ExpandNode(node);
        });
        connect.IsEnabled = !node.IsConnectionActive;
        menu.Items.Add(connect);

        var reconnect = CreateMenuItem("重新连接\tF5", "重新建立连接");
        reconnect.Click += (_, _) => _asyncAction(async () => await _viewModel.ReconnectConnectionNodeAsync(node));
        reconnect.IsEnabled = node.IsConnectionActive;
        menu.Items.Add(reconnect);

        var disconnect = CreateMenuItem("断开", "断开当前连接");
        disconnect.Click += (_, _) => _viewModel.DisconnectConnectionNode(node);
        disconnect.IsEnabled = node.IsConnectionActive;
        menu.Items.Add(disconnect);

        menu.Items.Add(new Separator());

        // ==== 查询组 ====
        var newQuery = CreateMenuItem("SQL Editor\tCtrl+N", "新建 SQL 查询");
        newQuery.Click += (_, _) => _viewModel.NewQuery();
        menu.Items.Add(newQuery);

        menu.Items.Add(new Separator());

        // ==== 管理组 ====
        var refreshConn = CreateMenuItem("刷新连接", "重新加载对象树");
        refreshConn.Click += async (_, _) =>
        {
            if (node.IsConnectionActive)
            {
                _viewModel.DisconnectConnectionNode(node);
            }
            await _viewModel.ConnectConnectionNodeAsync(node);
            ExpandNode(node);
        };
        menu.Items.Add(refreshConn);

        var editConn = CreateMenuItem("编辑连接...", "打开连接管理并定位");
        editConn.Click += (_, _) => _openConnectionManager?.Invoke();
        menu.Items.Add(editConn);

        var renameConn = CreateMenuItem("重命名连接...\tF2", "修改连接名称");
        renameConn.Click += async (_, _) => await RenameConnectionAsync(node);
        menu.Items.Add(renameConn);

        var deleteConn = CreateMenuItem("删除连接\tDelete", "删除此连接配置");
        deleteConn.Click += async (_, _) => await DeleteConnectionAsync(node);
        menu.Items.Add(deleteConn);

        menu.Items.Add(new Separator());

        // ==== Compare/Migrate 组 ====
        AddCompareMigrateMenuItems(menu, node);

        menu.Items.Add(new Separator());

        // ==== 剪贴板组 ====
        AddCopyMenuItems(menu, node, advanced: true);
    }

    #endregion

    #region 数据库节点右键菜单

    private void BuildDatabaseMenu(ContextMenu menu, DbObjectTreeNode node)
    {
        // ==== Open/View 组 ====
        // Oracle 的"数据库"节点实为当前用户/Schema，文案按方言调整以免误解。
        bool isOracle = string.Equals(GetConnectionDatabaseType(node), "Oracle", StringComparison.OrdinalIgnoreCase);
        var setCurrent = CreateMenuItem(isOracle ? "设为当前 Schema" : "设为当前数据库",
            isOracle ? "将此用户/Schema 设为默认查询目标" : "将此数据库设为默认查询目标");
        setCurrent.Click += (_, _) => SetCurrentDatabase(node);
        menu.Items.Add(setCurrent);

        var newQuery = CreateMenuItem("新建查询\tCtrl+N", "打开 SQL 编辑器");
        newQuery.Click += (_, _) => _viewModel.NewQuery();
        menu.Items.Add(newQuery);

        menu.Items.Add(new Separator());

        // ==== Compare/Migrate 组 ====
        AddCompareMigrateMenuItems(menu, node);

        menu.Items.Add(new Separator());

        // ==== 剪贴板组 ====
        AddCopyMenuItems(menu, node);

        menu.Items.Add(new Separator());

        // ==== 刷新组 ====
        AddRefreshMenuItem(menu, node);
    }

    #endregion

    #region Schema 节点右键菜单

    private void BuildSchemaMenu(ContextMenu menu, DbObjectTreeNode node)
    {
        // ==== Open/View 组 ====
        var setCurrent = CreateMenuItem("设为当前 Schema", "将此 Schema 设为默认查询目标");
        setCurrent.Click += (_, _) => SetCurrentSchema(node);
        menu.Items.Add(setCurrent);

        var newQuery = CreateMenuItem("新建查询\tCtrl+N", "打开 SQL 编辑器");
        newQuery.Click += (_, _) => _viewModel.NewQuery();
        menu.Items.Add(newQuery);

        menu.Items.Add(new Separator());

        // ==== Compare/Migrate 组 ====
        AddCompareMigrateMenuItems(menu, node);

        menu.Items.Add(new Separator());

        // ==== 剪贴板组 ====
        AddCopyMenuItems(menu, node);

        menu.Items.Add(new Separator());

        // ==== 刷新组 ====
        AddRefreshMenuItem(menu, node);
    }

    #endregion

    #region 辅助方法：设为当前数据库/Schema

    /// <summary>向上查找连接节点并返回其数据库类型（用于方言相关文案/行为）。</summary>
    private static string? GetConnectionDatabaseType(DbObjectTreeNode node)
    {
        var current = node;
        while (current is not null)
        {
            if (current.NodeType == DbObjectTreeNodeType.Connection)
                return current.Connection?.DatabaseType;
            current = current.Parent;
        }
        return null;
    }

    private void SetCurrentDatabase(DbObjectTreeNode node)
    {
        if (node.NodeType == DbObjectTreeNodeType.Database)
        {
            _viewModel.CurrentDatabase = node.Name;
            _viewModel.CurrentSchema = string.Empty;
            _viewModel.SchemaSelectorVisible = false;
            _viewModel.QueryEditor.StatusMessage = $"已设为当前数据库：{node.Name}";
        }
    }

    private void SetCurrentSchema(DbObjectTreeNode node)
    {
        if (node.NodeType == DbObjectTreeNodeType.Schema)
        {
            _viewModel.CurrentDatabase = node.DatabaseName ?? _viewModel.CurrentDatabase;
            _viewModel.CurrentSchema = node.Name;
            _viewModel.SchemaSelectorVisible = true;
            _viewModel.QueryEditor.StatusMessage = $"已设为当前 Schema：{node.Name}";
        }
    }

    #endregion

    #region 类型文件夹右键菜单

    private void BuildFolderMenu(ContextMenu menu, DbObjectTreeNode node)
    {
        // ==== DDL 组（新建对象）====
        switch (node.DatabaseObjectType)
        {
            case DatabaseObjectType.Table:
                var newTable = CreateMenuItem("新建表...", "在表设计器中创建新表");
                newTable.Click += (_, _) => _openTableDesigner?.Invoke(node, true);
                menu.Items.Add(newTable);
                break;
            case DatabaseObjectType.View:
                var newView = CreateMenuItem("新建视图...", "创建新视图");
                newView.Click += (_, _) =>
                {
                    var ddl = GetDdlService();
                    if (ddl is null) return;
                    var result = ddl.GetCreateTemplate(DatabaseObjectType.View, node.Schema);
                    if (result.IsSuccess)
                        _viewModel.NewObjectDefinitionQuery(result.Script!, _viewModel.FindNodeConnectionName(node), node.DatabaseName);
                    else
                        _viewModel.QueryEditor.StatusMessage = result.ErrorMessage;
                };
                menu.Items.Add(newView);
                break;
            case DatabaseObjectType.Procedure:
                var newProc = CreateMenuItem("新建存储过程...", "创建新存储过程");
                newProc.Click += (_, _) =>
                {
                    var ddl = GetDdlService();
                    if (ddl is null) return;
                    var result = ddl.GetCreateTemplate(DatabaseObjectType.Procedure, node.Schema);
                    if (result.IsSuccess)
                        _viewModel.NewObjectDefinitionQuery(result.Script!, _viewModel.FindNodeConnectionName(node), node.DatabaseName);
                    else
                        _viewModel.QueryEditor.StatusMessage = result.ErrorMessage;
                };
                menu.Items.Add(newProc);
                break;
            case DatabaseObjectType.Function:
                var newFunc = CreateMenuItem("新建函数...", "创建新函数");
                newFunc.Click += (_, _) =>
                {
                    var ddl = GetDdlService();
                    if (ddl is null) return;
                    var result = ddl.GetCreateTemplate(DatabaseObjectType.Function, node.Schema);
                    if (result.IsSuccess)
                        _viewModel.NewObjectDefinitionQuery(result.Script!, _viewModel.FindNodeConnectionName(node), node.DatabaseName);
                    else
                        _viewModel.QueryEditor.StatusMessage = result.ErrorMessage;
                };
                menu.Items.Add(newFunc);
                break;
        }

        if (menu.Items.Count > 0)
        {
            menu.Items.Add(new Separator());
        }

        // ==== 剪贴板组 ====
        AddCopyMenuItems(menu, node);

        menu.Items.Add(new Separator());

        // ==== 刷新组 ====
        AddRefreshMenuItem(menu, node);
    }

    #endregion

    #region 数据库对象右键菜单（表 / 视图）

    private void BuildDbObjectMenu(ContextMenu menu, DbObjectTreeNode node)
    {
        if (node.DbObject is Table or View)
        {
            BuildTableOrViewMenu(menu, node);
        }
        else
        {
            BuildOtherDbObjectMenu(menu, node);
        }
    }

    /// <summary>表/视图节点的完整右键菜单（P2增强版）。</summary>
    private void BuildTableOrViewMenu(ContextMenu menu, DbObjectTreeNode node)
    {
        bool isTable = node.DbObject is Table;

        // ==== Open/View 组 ====
        var select = CreateMenuItem("查看数据 (SELECT)\tF4", "生成 SELECT 查询并查看数据");
        select.Icon = CreateIcon("avares://DatabaseManager.Avalonia/Assets/tree_Table.png");
        select.Click += (_, _) => _viewModel.GenerateSelectScript(node);
        menu.Items.Add(select);

        if (isTable)
        {
            var editData = CreateMenuItem("编辑数据", "在查询结果中编辑（生成 SELECT 后可直接增删改）");
            editData.Icon = CreateIcon("avares://DatabaseManager.Avalonia/Assets/Edit.png");
            editData.Click += (_, _) => _viewModel.GenerateSelectScript(node);
            menu.Items.Add(editData);
        }

        if (isTable)
        {
            var design = CreateMenuItem("设计表...", "打开表设计器修改表结构");
            design.Icon = CreateIcon("avares://DatabaseManager.Avalonia/Assets/Tool16.png");
            design.Click += (_, _) => _openTableDesigner?.Invoke(node, false);
            menu.Items.Add(design);
        }
        else
        {
            var viewDef = CreateMenuItem("查看视图定义", "在新查询标签页显示 CREATE VIEW 脚本");
            viewDef.Icon = CreateIcon("avares://DatabaseManager.Avalonia/Assets/tree_View.png");
            viewDef.Click += (_, _) => _asyncAction(async () => await ViewObjectDefinitionAsync(node));
            menu.Items.Add(viewDef);
        }

        // P2: Filter 功能
        var filter = CreateMenuItem("过滤数据...", "生成带 WHERE 的 SELECT 模板");
        filter.Icon = CreateIcon("avares://DatabaseManager.Avalonia/Assets/Tool16.png");
        filter.Click += (_, _) => GenerateFilterTemplate(node);
        menu.Items.Add(filter);

        menu.Items.Add(new Separator());

        // ==== 数据导出/导入组（仅表）====
        if (isTable)
        {
            var exportData = CreateMenuItem("导出数据...", "导出表数据到文件");
            exportData.Icon = CreateIcon("avares://DatabaseManager.Avalonia/Assets/DbBackup.png");
            exportData.Click += (_, _) => _openExportWindow?.Invoke(node);
            menu.Items.Add(exportData);

            var importData = CreateMenuItem("导入数据...", "从文件导入数据到表");
            importData.Icon = CreateIcon("avares://DatabaseManager.Avalonia/Assets/DbConvert.png");
            importData.Click += (_, _) => _openImportWindow?.Invoke(node);
            menu.Items.Add(importData);

            menu.Items.Add(new Separator());
        }

        // ==== DDL 组（Generate SQL）====
        var generateSql = CreateMenuItem("Generate SQL", "生成 SQL 脚本模板");
        
        // P2 扩展：SELECT * / SELECT TOP N
        var genSelectAll = CreateMenuItem("SELECT *", "生成 SELECT * 查询");
        genSelectAll.Click += (_, _) => _viewModel.GenerateSelectScript(node);
        generateSql.Items.Add(genSelectAll);

        var genSelectTopN = CreateMenuItem("SELECT TOP N", "按方言生成 SELECT TOP N 查询");
        genSelectTopN.Click += (_, _) => _asyncAction(async () => await GenerateSqlTemplateAsync(node, SqlTemplateType.SelectTopN));
        generateSql.Items.Add(genSelectTopN);

        var genInsert = CreateMenuItem("INSERT 模板", "基于真实列结构生成 INSERT 语句模板");
        genInsert.Click += (_, _) => _asyncAction(async () => await GenerateSqlTemplateAsync(node, SqlTemplateType.Insert));
        generateSql.Items.Add(genInsert);

        var genUpdate = CreateMenuItem("UPDATE 模板", "基于真实列结构与主键生成 UPDATE 语句模板");
        genUpdate.Click += (_, _) => _asyncAction(async () => await GenerateSqlTemplateAsync(node, SqlTemplateType.Update));
        generateSql.Items.Add(genUpdate);

        var genDelete = CreateMenuItem("DELETE 模板", "基于主键生成 DELETE 语句模板");
        genDelete.Click += (_, _) => _asyncAction(async () => await GenerateSqlTemplateAsync(node, SqlTemplateType.Delete));
        generateSql.Items.Add(genDelete);

        var genCreate = CreateMenuItem(isTable ? "CREATE TABLE" : "CREATE VIEW", 
            isTable ? "生成建表脚本（方言生成器）" : "生成建视图脚本");
        genCreate.Click += (_, _) => _asyncAction(async () => await GenerateSqlTemplateAsync(node, SqlTemplateType.Create));
        generateSql.Items.Add(genCreate);

        menu.Items.Add(generateSql);

        // ==== 表维护组（仅表）====
        if (isTable)
        {
            var truncate = CreateMenuItem("截断表 (TRUNCATE)...", "生成 TRUNCATE TABLE 模板（清空数据，不可回滚）");
            truncate.Icon = CreateIcon("avares://DatabaseManager.Avalonia/Assets/Translate.png");
            truncate.Click += (_, _) => SetQueryText($"TRUNCATE TABLE {GetQualifiedObjectName(node)};", $"已生成 {node.Name} 的 TRUNCATE 模板。");
            menu.Items.Add(truncate);

            var count = CreateMenuItem("查看行数 (COUNT)...", "生成 SELECT COUNT(*) 查询");
            count.Icon = CreateIcon("avares://DatabaseManager.Avalonia/Assets/Database16.png");
            count.Click += (_, _) => SetQueryText($"SELECT COUNT(*) AS RowCount FROM {GetQualifiedObjectName(node)};", $"已生成 {node.Name} 的行数统计查询。");
            menu.Items.Add(count);

            menu.Items.Add(new Separator());
        }

        // ==== Compare/Migrate 组 ====
        AddCompareMigrateMenuItems(menu, node);

        menu.Items.Add(new Separator());

        // ==== 管理组 ====
        var renameObj = CreateMenuItem("重命名对象...\tF2", "修改对象名称");
        renameObj.Click += async (_, _) => await RenameDbObjectAsync(node);
        menu.Items.Add(renameObj);

        var deleteObj = CreateMenuItem("删除对象\tDelete", "删除此数据库对象");
        deleteObj.Click += async (_, _) => await DeleteDbObjectAsync(node);
        menu.Items.Add(deleteObj);

        menu.Items.Add(new Separator());

        // ==== 剪贴板组 ====
        AddCopyMenuItems(menu, node, advanced: true);

        menu.Items.Add(new Separator());

        // ==== 刷新组 ====
        AddRefreshParentMenuItem(menu, node);
    }

    /// <summary>其他数据库对象的右键菜单。</summary>
    private void BuildOtherDbObjectMenu(ContextMenu menu, DbObjectTreeNode node)
    {
        // ==== 查看定义组（仅 ScriptDbObject：视图/函数/存储过程）====
        if (node.DbObject is View or Function or Procedure)
        {
            var viewDef = CreateMenuItem("查看对象定义", "在新查询标签页显示 CREATE 定义脚本");
            viewDef.Click += (_, _) => _asyncAction(async () => await ViewObjectDefinitionAsync(node));
            menu.Items.Add(viewDef);

            menu.Items.Add(new Separator());
        }

        // ==== 管理组（删除/重命名）====
        if (node.DbObject is View or Function or Procedure or UserDefinedType or Sequence)
        {
            var renameObj = CreateMenuItem("重命名对象...", "修改对象名称");
            renameObj.Click += async (_, _) => await RenameDbObjectAsync(node);
            menu.Items.Add(renameObj);

            var deleteObj = CreateMenuItem("删除对象", "删除此数据库对象");
            deleteObj.Click += async (_, _) => await DeleteDbObjectAsync(node);
            menu.Items.Add(deleteObj);

            menu.Items.Add(new Separator());
        }

        // ==== 剪贴板组 ====
        AddCopyMenuItems(menu, node);

        menu.Items.Add(new Separator());

        // ==== 刷新组 ====
        AddRefreshParentMenuItem(menu, node);
    }

    /// <summary>异步：读取已有对象（View/Function/Procedure/Trigger）定义并在新查询标签页显示。</summary>
    private async Task ViewObjectDefinitionAsync(DbObjectTreeNode node)
    {
        if (node?.DbObject is null) return;

        var ddl = GetDdlService();
        if (ddl is null)
        {
            _viewModel.QueryEditor.StatusMessage = "DDL 服务未初始化。";
            return;
        }

        var connectionName = _viewModel.FindNodeConnectionName(node);
        if (string.IsNullOrEmpty(connectionName))
        {
            _viewModel.QueryEditor.StatusMessage = "请先连接对应连接。";
            return;
        }

        var result = await ddl.GetObjectDefinitionAsync(connectionName, node.DatabaseName ?? string.Empty, node.DbObject);
        if (!result.IsSuccess)
        {
            _viewModel.QueryEditor.StatusMessage = result.ErrorMessage;
            return;
        }

        _viewModel.NewObjectDefinitionQuery(result.Script!, connectionName, node.DatabaseName);
        _viewModel.QueryEditor.StatusMessage = $"已显示 {node.DbObject.GetType().Name}「{node.DbObject.Name}」的定义。";
    }

    #endregion

    #region 子对象右键菜单（列 / 索引 / 键 / 约束 / 触发器）

    private void BuildChildObjectMenu(ContextMenu menu, DbObjectTreeNode node)
    {
        // 子文件夹节点（Columns / Indexes / Keys / Constraints / Triggers）单独处理。
        if (node.NodeType == DbObjectTreeNodeType.ChildFolder)
        {
            BuildChildFolderMenu(menu, node);
            return;
        }

        var childType = GetChildObjectType(node);

        switch (childType)
        {
            case DbObjectChildType.Column:
                BuildColumnMenu(menu, node);
                break;
            case DbObjectChildType.Index:
                BuildIndexMenu(menu, node);
                break;
            case DbObjectChildType.PrimaryKey:
            case DbObjectChildType.ForeignKey:
                BuildKeyMenu(menu, node, childType);
                break;
            case DbObjectChildType.Constraint:
                BuildConstraintMenu(menu, node);
                break;
            case DbObjectChildType.Trigger:
                BuildTriggerMenu(menu, node);
                break;
            default:
                BuildGenericChildObjectMenu(menu, node);
                break;
        }
    }

    /// <summary>子文件夹节点（Columns / Indexes / Keys / Constraints / Triggers）右键菜单。</summary>
    private void BuildChildFolderMenu(ContextMenu menu, DbObjectTreeNode node)
    {
        // Columns 文件夹：提供「新建列」入口（生成 ALTER TABLE ADD 方言模板）。
        if (node.DatabaseObjectType == DatabaseObjectType.Column)
        {
            var addColumn = CreateMenuItem("新建列...", "生成 ALTER TABLE ADD COLUMN 模板");
            addColumn.Click += (_, _) => GenerateAddColumnTemplate(node);
            menu.Items.Add(addColumn);

            menu.Items.Add(new Separator());
        }

        AddCopyMenuItems(menu, node);

        menu.Items.Add(new Separator());

        AddRefreshMenuItem(menu, node);
    }

    /// <summary>基于所属表生成「新建列」的 ALTER TABLE ADD 模板。</summary>
    private void GenerateAddColumnTemplate(DbObjectTreeNode node)
    {
        var ddl = GetDdlService();
        if (ddl is null)
        {
            _viewModel.QueryEditor.StatusMessage = "DDL 服务未初始化。";
            return;
        }

        // 向上定位所属表（Columns 文件夹 → 表）。
        var tableNode = node.Parent;
        if (tableNode?.DbObject is not Table table)
        {
            _viewModel.QueryEditor.StatusMessage = "无法定位所属表。";
            return;
        }

        var connectionNode = _viewModel.FindNodeConnectionName(node);
        if (string.IsNullOrEmpty(connectionNode))
        {
            _viewModel.QueryEditor.StatusMessage = "请先连接对应连接。";
            return;
        }

        // 从连接配置读取数据库类型（GetAddColumnTemplate 不访问数据库）。
        var connectionItem = _connectionService.GetConnections()
            .FirstOrDefault(c => string.Equals(c.Name, connectionNode, StringComparison.OrdinalIgnoreCase));

        var result = ddl.GetAddColumnTemplate(connectionItem?.DatabaseType ?? string.Empty, table);
        if (!result.IsSuccess)
        {
            _viewModel.QueryEditor.StatusMessage = result.ErrorMessage;
            return;
        }

        SetQueryText(result.Script!, $"已为表 {table.Name} 生成新建列模板，请编辑后执行。");
    }

    /// <summary>列节点右键菜单（P2增强版）。</summary>
    private void BuildColumnMenu(ContextMenu menu, DbObjectTreeNode node)
    {
        // ==== View 组 ====
        var viewColumn = CreateMenuItem("查看列信息", "查看列的详细信息");
        viewColumn.Click += (_, _) => ViewColumnInfo(node);
        menu.Items.Add(viewColumn);

        menu.Items.Add(new Separator());

        // ==== DDL 组（列专用 SQL）====
        var columnSql = CreateMenuItem("Generate SQL", "生成列相关 SQL");

        var alterCol = CreateMenuItem("ALTER COLUMN 模板", "生成修改列语句模板");
        alterCol.Click += (_, _) => GenerateColumnSqlTemplate(node, ColumnSqlTemplateType.Alter);
        columnSql.Items.Add(alterCol);

        var dropCol = CreateMenuItem("DROP COLUMN", "生成删除列语句");
        dropCol.Click += (_, _) => GenerateColumnSqlTemplate(node, ColumnSqlTemplateType.Drop);
        columnSql.Items.Add(dropCol);

        menu.Items.Add(columnSql);

        menu.Items.Add(new Separator());

        // ==== 管理组 ====
        var renameCol = CreateMenuItem("重命名列...\tF2", "修改列名称");
        renameCol.Click += async (_, _) => await RenameChildObjectAsync(node, "列");
        menu.Items.Add(renameCol);

        var deleteCol = CreateMenuItem("删除列\tDelete", "删除此列");
        deleteCol.Click += async (_, _) => await DeleteChildObjectAsync(node, "列");
        menu.Items.Add(deleteCol);

        menu.Items.Add(new Separator());

        // ==== 剪贴板组 ====
        AddCopyMenuItems(menu, node, advanced: true);

        menu.Items.Add(new Separator());

        // ==== 刷新组 ====
        AddRefreshParentMenuItem(menu, node);
    }

    /// <summary>索引节点右键菜单（P2增强版）。</summary>
    private void BuildIndexMenu(ContextMenu menu, DbObjectTreeNode node)
    {
        // ==== View 组 ====
        var viewIndex = CreateMenuItem("查看索引信息", "查看索引详细信息");
        viewIndex.Click += (_, _) => ViewIndexInfo(node);
        menu.Items.Add(viewIndex);

        menu.Items.Add(new Separator());

        // ==== 管理组 ====
        var renameIdx = CreateMenuItem("重命名索引...\tF2", "修改索引名称");
        renameIdx.Click += async (_, _) => await RenameChildObjectAsync(node, "索引");
        menu.Items.Add(renameIdx);

        var deleteIdx = CreateMenuItem("删除索引\tDelete", "删除此索引");
        deleteIdx.Click += async (_, _) => await DeleteChildObjectAsync(node, "索引");
        menu.Items.Add(deleteIdx);

        menu.Items.Add(new Separator());

        // ==== 剪贴板组 ====
        AddCopyMenuItems(menu, node);

        menu.Items.Add(new Separator());

        // ==== 刷新组 ====
        AddRefreshParentMenuItem(menu, node);
    }

    /// <summary>主键/外键节点右键菜单（P2增强版）。</summary>
    private void BuildKeyMenu(ContextMenu menu, DbObjectTreeNode node, DbObjectChildType keyType)
    {
        string keyName = keyType == DbObjectChildType.PrimaryKey ? "主键" : "外键";

        // ==== View 组 ====
        var viewKey = CreateMenuItem($"查看{keyName}信息", $"查看{keyName}详细信息");
        viewKey.Click += (_, _) => ViewKeyInfo(node, keyType);
        menu.Items.Add(viewKey);

        menu.Items.Add(new Separator());

        // ==== 管理组 ====
        var deleteKey = CreateMenuItem($"删除{keyName}\tDelete", $"删除此{keyName}");
        deleteKey.Click += async (_, _) => await DeleteChildObjectAsync(node, keyName);
        menu.Items.Add(deleteKey);

        menu.Items.Add(new Separator());

        // ==== 剪贴板组 ====
        AddCopyMenuItems(menu, node);

        menu.Items.Add(new Separator());

        // ==== 刷新组 ====
        AddRefreshParentMenuItem(menu, node);
    }

    /// <summary>约束节点右键菜单（P2增强版）。</summary>
    private void BuildConstraintMenu(ContextMenu menu, DbObjectTreeNode node)
    {
        // ==== View 组 ====
        var viewConstraint = CreateMenuItem("查看约束信息", "查看约束详细信息");
        viewConstraint.Click += (_, _) => ViewConstraintInfo(node);
        menu.Items.Add(viewConstraint);

        menu.Items.Add(new Separator());

        // ==== 管理组 ====
        var renameCon = CreateMenuItem("重命名约束...\tF2", "修改约束名称");
        renameCon.Click += async (_, _) => await RenameChildObjectAsync(node, "约束");
        menu.Items.Add(renameCon);

        var deleteCon = CreateMenuItem("删除约束\tDelete", "删除此约束");
        deleteCon.Click += async (_, _) => await DeleteChildObjectAsync(node, "约束");
        menu.Items.Add(deleteCon);

        menu.Items.Add(new Separator());

        // ==== 剪贴板组 ====
        AddCopyMenuItems(menu, node);

        menu.Items.Add(new Separator());

        // ==== 刷新组 ====
        AddRefreshParentMenuItem(menu, node);
    }

    /// <summary>触发器节点右键菜单（P2增强版）。</summary>
    private void BuildTriggerMenu(ContextMenu menu, DbObjectTreeNode node)
    {
        // ==== View 组 ====
        var viewTrigger = CreateMenuItem("查看触发器脚本", "查看触发器定义");
        viewTrigger.Click += (_, _) => ViewTriggerScript(node);
        menu.Items.Add(viewTrigger);

        menu.Items.Add(new Separator());

        // ==== 管理组 ====
        var renameTrg = CreateMenuItem("重命名触发器...\tF2", "修改触发器名称");
        renameTrg.Click += async (_, _) => await RenameChildObjectAsync(node, "触发器");
        menu.Items.Add(renameTrg);

        var deleteTrg = CreateMenuItem("删除触发器\tDelete", "删除此触发器");
        deleteTrg.Click += async (_, _) => await DeleteChildObjectAsync(node, "触发器");
        menu.Items.Add(deleteTrg);

        menu.Items.Add(new Separator());

        // ==== 剪贴板组 ====
        AddCopyMenuItems(menu, node);

        menu.Items.Add(new Separator());

        // ==== 刷新组 ====
        AddRefreshParentMenuItem(menu, node);
    }

    /// <summary>通用子对象右键菜单。</summary>
    private void BuildGenericChildObjectMenu(ContextMenu menu, DbObjectTreeNode node)
    {
        // ==== View 组 ====
        var viewObj = CreateMenuItem("查看详情", "查看对象详细信息");
        viewObj.Click += (_, _) => ViewChildObjectInfo(node);
        menu.Items.Add(viewObj);

        menu.Items.Add(new Separator());

        // ==== 管理组 ====
        var deleteObj = CreateMenuItem("删除\tDelete", "删除此对象");
        deleteObj.Click += async (_, _) => await DeleteChildObjectAsync(node, "对象");
        menu.Items.Add(deleteObj);

        menu.Items.Add(new Separator());

        // ==== 剪贴板组 ====
        AddCopyMenuItems(menu, node);

        menu.Items.Add(new Separator());

        // ==== 刷新组 ====
        AddRefreshParentMenuItem(menu, node);
    }

    #endregion

    #region 默认右键菜单

    private void BuildDefaultMenu(ContextMenu menu, DbObjectTreeNode node)
    {
        AddCopyMenuItems(menu, node);
        AddRefreshMenuItem(menu, node);
    }

    #endregion

    #region P2: Compare / Migrate 菜单项

    /// <summary>添加 Compare/Migrate 相关菜单项（连接/数据库/表节点）。</summary>
    private void AddCompareMigrateMenuItems(ContextMenu menu, DbObjectTreeNode node)
    {
        var hasCompare = _openSchemaCompare is not null;
        var hasDataCompare = _openDataCompare is not null;
        var hasConvert = _openConvert is not null;

        if (!hasCompare && !hasDataCompare && !hasConvert)
            return;

        var compareMenu = CreateMenuItem("比较与迁移", "结构对比、数据对比、数据库转换");

        if (hasCompare)
        {
            var schemaCmp = CreateMenuItem("结构对比...", "打开 Schema Compare 窗口");
            schemaCmp.Click += (_, _) => _openSchemaCompare?.Invoke(node);
            compareMenu.Items.Add(schemaCmp);
        }

        if (hasDataCompare)
        {
            var dataCmp = CreateMenuItem("数据对比...", "打开 Data Compare 窗口");
            dataCmp.Click += (_, _) => _openDataCompare?.Invoke(node);
            compareMenu.Items.Add(dataCmp);
        }

        if (hasConvert)
        {
            var convert = CreateMenuItem("数据库转换...", "打开 Convert 窗口");
            convert.Click += (_, _) => _openConvert?.Invoke(node);
            compareMenu.Items.Add(convert);
        }

        menu.Items.Add(compareMenu);
    }

    #endregion

    #region P2: Copy Advanced Info（高级复制）

    /// <summary>添加复制菜单项（基础 + 高级）。</summary>
    private void AddCopyMenuItems(ContextMenu menu, DbObjectTreeNode node, bool advanced = false)
    {
        var copyName = CreateMenuItem("复制名称", "复制对象名称到剪贴板");
        copyName.Click += (_, _) => CopyToClipboard(node.Name);
        menu.Items.Add(copyName);

        var copyFullPath = CreateMenuItem("复制完整路径", "复制 schema.name 格式路径");
        copyFullPath.Click += (_, _) => CopyToClipboard(GetFullPath(node));
        menu.Items.Add(copyFullPath);

        // P2 高级复制
        if (!advanced) return;

        switch (node.NodeType)
        {
            case DbObjectTreeNodeType.Connection:
                // 复制连接字符串
                var copyConnStr = CreateMenuItem("复制连接字符串", "复制完整连接字符串");
                copyConnStr.Click += (_, _) => CopyConnectionString(node);
                menu.Items.Add(copyConnStr);
                break;

            case DbObjectTreeNodeType.DbObject when node.DbObject is Table or View:
                // 复制 schema.table 格式
                var copySchemaTable = CreateMenuItem("复制 Schema.Table", "复制 schema.table 格式名称");
                copySchemaTable.Click += (_, _) => CopyToClipboard(GetQualifiedObjectName(node));
                menu.Items.Add(copySchemaTable);
                break;

            case DbObjectTreeNodeType.ChildObject when node.DbObject is TableColumn:
                // 列的高级复制已在 BuildColumnMenu 中通过 AddCopyColumnDefinition 处理
                break;
        }
    }

    /// <summary>复制连接字符串到剪贴板。</summary>
    private void CopyConnectionString(DbObjectTreeNode node)
    {
        if (node.Connection is null) return;

        // 构建连接字符串
        var sb = new StringBuilder();
        sb.Append("Server=").Append(node.Connection.Server);
        if (!string.IsNullOrEmpty(node.Connection.Port))
        {
            sb.Append(',').Append(node.Connection.Port);
        }
        sb.Append(";Database=").Append(node.Connection.Database);
        if (!string.IsNullOrEmpty(node.Connection.UserId))
        {
            sb.Append(";User Id=").Append(node.Connection.UserId);
        }
        // 注意：不复制密码到剪贴板出于安全考虑
        
        CopyToClipboard(sb.ToString());
        _viewModel.QueryEditor.StatusMessage = "已复制连接字符串（不含密码）";
    }

    #endregion

    #region P2: Generate SQL 扩展

    private enum SqlTemplateType { Select, SelectTopN, Insert, Update, Delete, Create }

    private static ObjectScriptType ToObjectScriptType(SqlTemplateType templateType) => templateType switch
    {
        SqlTemplateType.Select => ObjectScriptType.Select,
        SqlTemplateType.SelectTopN => ObjectScriptType.SelectTopN,
        SqlTemplateType.Insert => ObjectScriptType.Insert,
        SqlTemplateType.Update => ObjectScriptType.Update,
        SqlTemplateType.Delete => ObjectScriptType.Delete,
        SqlTemplateType.Create => ObjectScriptType.CreateTable,
        _ => throw new ArgumentOutOfRangeException(nameof(templateType)),
    };

    /// <summary>基于真实元数据生成脚本（经 IDdlService，按方言产出），并填充到查询编辑器。</summary>
    private async Task GenerateSqlTemplateAsync(DbObjectTreeNode node, SqlTemplateType templateType)
    {
        if (node.DbObject is not (Table or View))
            return;

        var ddl = GetDdlService();
        if (ddl is null)
        {
            _viewModel.QueryEditor.StatusMessage = "DDL 服务未初始化。";
            return;
        }

        var connectionName = _viewModel.FindNodeConnectionName(node);
        if (string.IsNullOrEmpty(connectionName))
        {
            _viewModel.QueryEditor.StatusMessage = "请先连接对应连接。";
            return;
        }

        var result = await ddl.GenerateObjectScriptAsync(
            connectionName,
            node.DatabaseName ?? string.Empty,
            node.DbObject,
            ToObjectScriptType(templateType));

        if (!result.IsSuccess)
        {
            _viewModel.QueryEditor.StatusMessage = result.ErrorMessage;
            return;
        }

        SetQueryText(result.Script!, $"已生成 {node.DbObject.Name} 的 {templateType} 脚本（基于真实结构）。");
    }

    /// <summary>P2: Filter 模板 - 生成带 WHERE 的 SELECT。</summary>
    private void GenerateFilterTemplate(DbObjectTreeNode node)
    {
        if (node.DbObject is not (Table or View))
            return;

        string objectName = GetQualifiedObjectName(node);
        string sql = $"SELECT * FROM {objectName}\nWHERE /* 过滤条件 */\nORDER BY 1;";

        SetQueryText(sql, $"已生成 {node.DbObject.Name} 的过滤查询模板，请编辑 WHERE 条件。");
    }

    /// <summary>将 SQL 填充到当前查询标签页并更新状态。</summary>
    private void SetQueryText(string sql, string statusMessage)
    {
        if (_viewModel.SelectedQueryTab is not null)
        {
            _viewModel.SelectedQueryTab.SqlText = sql;
            _viewModel.SelectedQueryTab.StatusMessage = statusMessage;
        }

        // 向后兼容（无标签页时仍填充全局编辑器）
        _viewModel.QueryEditor.SqlText = sql;
        _viewModel.QueryEditor.StatusMessage = statusMessage;
    }

    #endregion

    #region P2: 列 SQL 模板

    private enum ColumnSqlTemplateType { Alter, Drop }

    private void GenerateColumnSqlTemplate(DbObjectTreeNode node, ColumnSqlTemplateType templateType)
    {
        if (node.DbObject is not TableColumn column || node.Parent?.Parent?.DbObject is null)
            return;

        string tableName = GetQualifiedObjectName(node.Parent.Parent);
        string sql = templateType switch
        {
            ColumnSqlTemplateType.Alter => $"ALTER TABLE {tableName}\nALTER COLUMN {column.Name} {column.DataType};",
            ColumnSqlTemplateType.Drop => $"ALTER TABLE {tableName}\nDROP COLUMN {column.Name};",
            _ => string.Empty,
        };

        _viewModel.QueryEditor.SqlText = sql;
        _viewModel.QueryEditor.StatusMessage = $"已生成列 {column.Name} 的{templateType}模板脚本。";
    }

    #endregion

    #region 通用菜单项：刷新

    private void AddRefreshMenuItem(ContextMenu menu, DbObjectTreeNode node)
    {
        var refresh = CreateMenuItem("刷新\tF5", "刷新此节点");
        refresh.Click += (_, _) => _asyncAction(async () => await _viewModel.RefreshNodeAsync(node));
        menu.Items.Add(refresh);
    }

    private void AddRefreshParentMenuItem(ContextMenu menu, DbObjectTreeNode node)
    {
        var parent = node.Parent;
        if (parent is null) return;

        var refresh = CreateMenuItem("刷新\tF5", "刷新父节点");
        refresh.Click += (_, _) => _asyncAction(async () => await _viewModel.RefreshNodeAsync(parent));
        menu.Items.Add(refresh);
    }

    #endregion

    #region 辅助方法：获取完整路径

    internal static string GetFullPath(DbObjectTreeNode node)
    {
        var parts = new List<string>();
        var current = node;

        if (current.NodeType == DbObjectTreeNodeType.Connection)
            return current.Name;

        while (current is not null && current.NodeType != DbObjectTreeNodeType.Connection)
        {
            if (current.NodeType is DbObjectTreeNodeType.DbObject or DbObjectTreeNodeType.ChildObject
                or DbObjectTreeNodeType.Schema)
            {
                parts.Insert(0, current.Name);
            }
            else if (current.NodeType == DbObjectTreeNodeType.Database && parts.Count > 0)
            {
                parts.Insert(0, current.Name);
            }
            current = current.Parent;
        }

        return string.Join(".", parts);
    }

    #endregion

    #region 辅助方法：获取限定对象名

    private string GetQualifiedObjectName(DbObjectTreeNode node)
    {
        var sb = new StringBuilder();
        if (!string.IsNullOrEmpty(node.Schema))
        {
            sb.Append(node.Schema).Append('.');
        }
        sb.Append(node.DbObject!.Name);
        return sb.ToString();
    }

    #endregion

    #region 辅助方法：删除/重命名连接

    private async Task DeleteConnectionAsync(DbObjectTreeNode node)
    {
        if (node.Connection is null || string.IsNullOrEmpty(node.Connection.Id))
            return;

        var result = await ShowConfirmDialog($"确定要删除连接 \"{node.Name}\" 吗？", "删除连接");
        if (result != true)
            return;

        try
        {
            var connectionService = GetConnectionService();
            await connectionService.DeleteAsync(new[] { node.Connection.Id });
            _viewModel.QueryEditor.StatusMessage = $"已删除连接：{node.Name}";
            _viewModel.RefreshConnections();
        }
        catch (Exception ex)
        {
            _viewModel.QueryEditor.StatusMessage = $"删除连接失败：{ex.Message}";
        }
    }

    private async Task RenameConnectionAsync(DbObjectTreeNode node)
    {
        if (node.Connection is null)
            return;

        var newName = await ShowInputDialog("请输入新的连接名称：", "重命名连接", node.Name);
        if (string.IsNullOrWhiteSpace(newName) || newName == node.Name)
            return;

        try
        {
            node.Connection.Name = newName;
            var connectionService = GetConnectionService();
            await connectionService.SaveAsync(node.Connection);
            _viewModel.QueryEditor.StatusMessage = $"已重命名连接为：{newName}";
            _viewModel.RefreshConnections();
        }
        catch (Exception ex)
        {
            _viewModel.QueryEditor.StatusMessage = $"重命名连接失败：{ex.Message}";
        }
    }

    #endregion

    #region 辅助方法：删除/重命名数据库对象

    private async Task DeleteDbObjectAsync(DbObjectTreeNode node)
    {
        if (node.DbObject is null)
            return;

        var ddl = GetDdlService();
        if (ddl is null)
        {
            _viewModel.QueryEditor.StatusMessage = "DDL 服务未初始化。";
            return;
        }

        var connectionName = _viewModel.FindNodeConnectionName(node);
        if (string.IsNullOrEmpty(connectionName))
        {
            _viewModel.QueryEditor.StatusMessage = "请先连接对应连接。";
            return;
        }

        string objType = node.DbObject.GetType().Name;
        string confirmMsg = $"确定要删除{objType} \"{node.Name}\" 吗？\n此操作不可撤销！";
        var result = await ShowConfirmDialog(confirmMsg, $"删除{objType}");
        if (result != true)
            return;

        try
        {
            var exec = await ddl.DropAsync(connectionName, node.DatabaseName ?? string.Empty, node.DbObject);
            if (!exec.IsSuccess)
                throw new Exception(exec.ErrorMessage ?? "未知错误。");

            _viewModel.QueryEditor.StatusMessage = $"已删除{objType}：{node.Name}。";

            // 从对象树中移除该节点，并重建父文件夹节点列表
            if (node.Parent is DbObjectTreeNode parent)
            {
                parent.Children.Remove(node);
                if (parent.NodeType is DbObjectTreeNodeType.Folder or DbObjectTreeNodeType.ChildFolder)
                    await _viewModel.RefreshNodeAsync(parent);
            }
        }
        catch (Exception ex)
        {
            _viewModel.QueryEditor.StatusMessage = $"删除{objType}失败：{ex.Message}";
        }
    }

    private async Task RenameDbObjectAsync(DbObjectTreeNode node)
    {
        if (node.DbObject is null)
            return;

        var ddl = GetDdlService();
        if (ddl is null)
        {
            _viewModel.QueryEditor.StatusMessage = "DDL 服务未初始化。";
            return;
        }

        var connectionName = _viewModel.FindNodeConnectionName(node);
        if (string.IsNullOrEmpty(connectionName))
        {
            _viewModel.QueryEditor.StatusMessage = "请先连接对应连接。";
            return;
        }

        // 重命名仅通过 DbScriptGenerator 暴露了 Table / TableColumn 两类统一 API
        if (node.DbObject is not Table and not TableColumn)
        {
            _viewModel.QueryEditor.StatusMessage = $"当前暂不支持重命名 {node.DbObject.GetType().Name}，请改用生成脚本执行。";
            return;
        }

        var newName = await ShowInputDialog("请输入新的对象名称：", "重命名对象", node.Name);
        if (string.IsNullOrWhiteSpace(newName) || newName == node.Name)
            return;

        // 找到所属表（对列重命名时；对表重命名时即自身）
        Table? table = node.DbObject as Table ?? FindAncestorDbObject<Table>(node);
        if (table is null)
        {
            _viewModel.QueryEditor.StatusMessage = "无法定位所属表。";
            return;
        }

        try
        {
            DdlExecuteResult exec;
            if (node.DbObject is TableColumn col)
            {
                exec = await ddl.RenameTableColumnAsync(connectionName, node.DatabaseName ?? string.Empty, table, col, newName);
            }
            else
            {
                exec = await ddl.RenameTableAsync(connectionName, node.DatabaseName ?? string.Empty, table, newName);
            }

            if (!exec.IsSuccess)
                throw new Exception(exec.ErrorMessage ?? "未知错误。");

            _viewModel.QueryEditor.StatusMessage = $"已重命名为：{newName}。";

            // 刷新父文件夹
            if (node.Parent is DbObjectTreeNode parent
                && parent.NodeType is DbObjectTreeNodeType.Folder or DbObjectTreeNodeType.ChildFolder)
            {
                await _viewModel.RefreshNodeAsync(parent);
            }
        }
        catch (Exception ex)
        {
            _viewModel.QueryEditor.StatusMessage = $"重命名失败：{ex.Message}";
        }
    }

    /// <summary>向上查找第一个匹配类型的 DbObject。</summary>
    private static T? FindAncestorDbObject<T>(DbObjectTreeNode node) where T : DatabaseObject
    {
        var current = node.Parent;
        while (current is not null)
        {
            if (current.DbObject is T t) return t;
            current = current.Parent;
        }
        return null;
    }

    #endregion

    #region 辅助方法：展开节点

    private void ExpandNode(DbObjectTreeNode node)
    {
        var container = _treeView.ContainerFromItem(node);
        if (container is TreeViewItem tvi)
        {
            tvi.IsExpanded = true;
        }
    }

    #endregion

    #region 辅助方法：创建带快捷键的菜单项

    /// <summary>创建菜单项（带 Header 文本和 ToolTip）。</summary>
    private static MenuItem CreateMenuItem(string header, string? toolTip = null)
    {
        var item = new MenuItem { Header = header };
        if (toolTip is not null)
        {
            ToolTip.SetTip(item, toolTip);
        }
        return item;
    }

    private static Control? CreateIcon(string uri)
    {
        try
        {
            var bitmap = new Avalonia.Media.Imaging.Bitmap(Avalonia.Platform.AssetLoader.Open(new Uri(uri)));
            return new Image { Source = bitmap, Width = 14, Height = 14 };
        }
        catch { return null; }
    }

    #endregion

    #region 辅助方法：剪贴板操作

    private static void CopyToClipboard(string text)
    {
        var mainWindow = (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;
        mainWindow?.Clipboard?.SetTextAsync(text);
    }

    #endregion

    #region 子对象辅助方法

    private static DbObjectChildType GetChildObjectType(DbObjectTreeNode node)
    {
        if (node.Parent is null) return DbObjectChildType.None;

        return node.Parent.Name switch
        {
            "Columns" => DbObjectChildType.Column,
            "Triggers" => DbObjectChildType.Trigger,
            "Indexes" => DbObjectChildType.Index,
            "Keys" => node.DbObject switch
            {
                TablePrimaryKey => DbObjectChildType.PrimaryKey,
                TableForeignKey => DbObjectChildType.ForeignKey,
                _ => DbObjectChildType.PrimaryKey,
            },
            "Constraints" => DbObjectChildType.Constraint,
            _ => DbObjectChildType.None,
        };
    }

    #endregion

    #region 子对象辅助方法：查看操作

    private void ViewColumnInfo(DbObjectTreeNode node)
    {
        if (node.DbObject is not TableColumn column) return;

        var sb = new StringBuilder();
        sb.AppendLine($"列名: {column.Name}");
        sb.AppendLine($"数据类型: {column.DataType}");
        if (!string.IsNullOrEmpty(column.DataTypeSchema))
            sb.AppendLine($"类型 Schema: {column.DataTypeSchema}");
        sb.AppendLine($"可空: {(column.IsNullable ? "是" : "否")}");
        if (column.IsIdentity)
            sb.AppendLine($"自增列: 是");
        if (column.MaxLength.HasValue && column.MaxLength.Value > 0)
            sb.AppendLine($"长度: {column.MaxLength.Value}");
        if (!string.IsNullOrEmpty(column.DefaultValue))
            sb.AppendLine($"默认值: {column.DefaultValue}");

        _viewModel.QueryEditor.StatusMessage = $"列信息: {column.Name} ({column.DataType})";
        CopyToClipboard(sb.ToString());
    }

    private void ViewIndexInfo(DbObjectTreeNode node)
    {
        if (node.DbObject is not TableIndex index) return;

        var sb = new StringBuilder();
        sb.AppendLine($"索引名: {index.Name}");
        sb.AppendLine($"是否唯一: {(index.IsUnique ? "是" : "否")}");
        sb.AppendLine($"列: {string.Join(", ", index.Columns.OrderBy(c => c.Order).Select(c => c.ColumnName))}");

        _viewModel.QueryEditor.StatusMessage = $"索引信息: {index.Name}";
        CopyToClipboard(sb.ToString());
    }

    private void ViewKeyInfo(DbObjectTreeNode node, DbObjectChildType keyType)
    {
        var sb = new StringBuilder();
        string keyName = keyType == DbObjectChildType.PrimaryKey ? "主键" : "外键";

        switch (node.DbObject)
        {
            case TablePrimaryKey pk:
                sb.AppendLine($"{keyName}名: {pk.Name}");
                sb.AppendLine($"列: {string.Join(", ", pk.Columns.OrderBy(c => c.Order).Select(c => c.ColumnName))}");
                break;
            case TableForeignKey fk:
                sb.AppendLine($"{keyName}名: {fk.Name}");
                sb.AppendLine($"列: {string.Join(", ", fk.Columns.OrderBy(c => c.Order).Select(c => c.ColumnName))}");
                var refTable = string.IsNullOrEmpty(fk.ReferencedSchema) 
                    ? fk.ReferencedTableName 
                    : $"{fk.ReferencedSchema}.{fk.ReferencedTableName}";
                sb.AppendLine($"引用表: {refTable}");
                break;
        }

        _viewModel.QueryEditor.StatusMessage = $"{keyName}信息: {node.Name}";
        CopyToClipboard(sb.ToString());
    }

    private void ViewConstraintInfo(DbObjectTreeNode node)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"约束名: {node.Name}");
        if (node.DbObject is not null)
        {
            sb.AppendLine($"类型: {node.DbObject.GetType().Name}");
        }

        _viewModel.QueryEditor.StatusMessage = $"约束信息: {node.Name}";
        CopyToClipboard(sb.ToString());
    }

    private void ViewTriggerScript(DbObjectTreeNode node)
    {
        _asyncAction(async () => await ViewObjectDefinitionAsync(node));
    }

    private void ViewChildObjectInfo(DbObjectTreeNode node)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"对象名: {node.Name}");
        sb.AppendLine($"类型: {node.DatabaseObjectType}");
        if (node.DbObject is not null)
        {
            sb.AppendLine($"详细类型: {node.DbObject.GetType().Name}");
        }

        _viewModel.QueryEditor.StatusMessage = $"对象信息: {node.Name}";
        CopyToClipboard(sb.ToString());
    }

    #endregion

    #region 子对象辅助方法：删除/重命名

    private async Task DeleteChildObjectAsync(DbObjectTreeNode node, string objectTypeName)
    {
        if (node.DbObject is null)
        {
            _viewModel.QueryEditor.StatusMessage = $"无法识别 {objectTypeName}。";
            return;
        }
        // 子对象（列/索引/主键/外键/约束/触发器）逻辑完全复用 DbObject 版本（Drop 支持所有子对象）
        await DeleteDbObjectAsync(node);
    }

    private async Task RenameChildObjectAsync(DbObjectTreeNode node, string objectTypeName)
    {
        if (node.DbObject is TableColumn)
        {
            await RenameDbObjectAsync(node);
            return;
        }
        _viewModel.QueryEditor.StatusMessage = $"当前暂不支持重命名 {objectTypeName}，请改用生成脚本执行。";
    }

    #endregion

    #region 子对象辅助方法：复制列定义

    private void AddCopyColumnDefinition(ContextMenu menu, DbObjectTreeNode node)
    {
        if (node.DbObject is not TableColumn column) return;

        var copyDef = CreateMenuItem("复制列定义", "复制 name type nullable 格式定义");
        copyDef.Click += (_, _) =>
        {
            string def = FormatColumnDefinition(column);
            CopyToClipboard(def);
        };
        menu.Items.Add(copyDef);
    }

    private static string FormatColumnDefinition(TableColumn column)
    {
        var sb = new StringBuilder();
        sb.Append(column.Name);
        sb.Append(' ');

        if (!string.IsNullOrEmpty(column.DataTypeSchema))
        {
            sb.Append(column.DataTypeSchema).Append('.');
        }
        sb.Append(column.DataType);

        if (column.MaxLength.HasValue && column.MaxLength.Value > 0)
        {
            sb.Append('(').Append(column.MaxLength.Value);
            if (column.Precision.HasValue && column.Precision.Value > 0)
            {
                sb.Append(',').Append(column.Precision.Value);
            }
            sb.Append(')');
        }

        if (!column.IsNullable)
            sb.Append(" NOT NULL");

        if (column.IsIdentity)
            sb.Append(" IDENTITY");

        if (!string.IsNullOrEmpty(column.DefaultValue))
            sb.Append(" DEFAULT ").Append(column.DefaultValue);

        return sb.ToString();
    }

    #endregion

    #region 对话框辅助方法（统一走 DialogHelper）

    private static Task<bool?> ShowConfirmDialog(string message, string title)
        => DialogHelper.ShowConfirmAsync(title, message);

    private static Task<string?> ShowInputDialog(string message, string title, string defaultValue = "")
        => DialogHelper.ShowInputAsync(title, message, defaultValue);

    private IDbConnectionService GetConnectionService()
    {
        return _connectionService;
    }

    private IDdlService? GetDdlService() => _ddlService;

    #endregion
}
