# 对象浏览器右键菜单完善计划（Avalonia 版，按优先级）

> 参考 `resources/dbeaver-screenshot/` 下各节点右键菜单截图（connection / tables / table / columns / column / constraints / index / trigger）。
> 目标：不追求与 DBeaver 1:1 对齐，优先补齐**常用高频功能**。
> ~~当前右键菜单在 `DatabaseManager.Avalonia/DatabaseManager.Avalonia/Views/MainWindow.axaml.cs` 的 `ObjectsTree_ContextRequested` 中硬编码~~
> ✅ 已重构：右键菜单已抽出为独立构建类 `DatabaseManager.AppCore/Common/ObjectTreeContextMenuBuilder.cs`，入口仍在 `MainWindow.axaml.cs:740` 的 `ObjectsTree_ContextRequested`。

## P0（核心常用，优先实现）

- [x] **连接节点右键菜单完善**（ObjectTreeContextMenuBuilder.cs:107-172）
  - 已有：连接 / 重连 F5 / 断开 / SQL Editor Ctrl+N
  - 已新增：刷新连接、编辑连接、重命名连接 F2（经 `SaveAsync` 实现）、删除连接 Delete
- [x] **表 / 视图节点右键菜单完善**（ObjectTreeContextMenuBuilder.cs:351-458）
  - 已有：查看数据（SELECT）F4、编辑数据、设计表
  - 已新增：Generate SQL 子菜单（SELECT / SELECT TOP N / INSERT / UPDATE / DELETE / CREATE）
    - ✅ 已修复：全部经 `IDdlService.GenerateObjectScriptAsync` 基于真实列结构与方言生成（2026-08-25）
  - 已新增：复制对象名、复制完整路径、高级复制
  - 已新增：删除对象 Delete、重命名对象 F2
  - 已新增：刷新父节点
- [x] **类型文件夹（Tables / Views / Procedures / Functions / Triggers 等）右键菜单**（266-332 行）
  - 新建表（打开表设计器）/ 新建视图 / 新建存储过程 / 新建函数（经 `IDdlService.GetCreateTemplate`）
  - 刷新
- [x] **通用菜单项（所有节点）**
  - 复制名称 / 复制完整路径（高级复制子菜单）
  - 刷新（F5）

## P1（高频补齐）

- [x] **Database / Schema 节点右键菜单**（178-234 行）
  - 设为当前数据库 / Schema、新建查询、刷新、比较与迁移子菜单
- [x] **视图节点右键**
  - 查看数据、查看视图定义、Generate SQL、刷新、删除、重命名
- [x] **列节点 / Columns 文件夹右键**（530-620 行左右）
  - 查看列信息、ALTER COLUMN / DROP COLUMN 模板（使用真实列名/类型）、删除列、重命名列
  - ✅ 已补「新建列...」入口：Columns 文件夹右键生成 ALTER TABLE ADD 方言模板（2026-08-25）
- [x] **索引 / 约束 / 触发器子对象右键**（约 620-745 行）
  - 查看信息、删除、重命名、刷新父节点
- [x] **表节点右键 导出 / 导入数据**（389-400 行）
  - 已挂接 ExportWindow / ImportWindow，并预填连接与表名（MainWindow.axaml.cs:830-879）

## P2（体验增强）

- [ ] **菜单分组与图标**
  - Separator 分组已有；快捷键提示已有（F4/F5/F2/Del/Ctrl+N）
  - ❌ 菜单项小图标未实现（全项目无 Icon 绑定）
- [x] **Generate SQL 子菜单扩展**
  - 表：SELECT *、SELECT TOP N、INSERT 模板、UPDATE 模板、DELETE、CREATE TABLE
  - 视图：CREATE VIEW
  - 列：ALTER COLUMN / DROP COLUMN 模板
  - ✅ 质量问题已修复：表级脚本均基于真实元数据与方言生成（2026-08-25）
- [x] **Filter / Browse from here**
  - 表/视图节点右键「过滤数据...」已实现
- [x] **Compare / Migrate**
  - 连接 / 数据库 / Schema / 表视图节点均已挂接 SchemaCompareWindow / DataCompareWindow / ConvertWindow
  - ✅ 已修复：打开窗口时预填节点所属连接为源连接（2026-08-25）
- [x] **Copy Advanced Info**
  - 连接：复制连接字符串 ✓；对象：复制名称 / schema.table ✓

## 依赖与阻塞（实现前需确认/补齐）

- [x] 统一对话框工具：✅ 已抽公共 `DialogHelper`（AppCore/Common/DialogHelper.cs，2026-08-25）；各 Window 内的轻量提示继续使用 MsBox.Avalonia
- [x] DDL 能力：未在 `IDbSchemaService` 上加 Generate*Script，而是落在独立的 `IDdlService`（PreviewDrop / DropAsync / RenameTableAsync / RenameTableColumnAsync / GetCreateTemplate / GetObjectDefinitionAsync，见 DefaultDdlService.cs）——设计上更清晰，视为已解决
- [x] 对象删除 / 重命名后端接口：经 `IDdlService.DropAsync / RenameTableAsync / RenameTableColumnAsync` 提供
- [x] 连接重命名 / 编辑：`IDbConnectionService.SaveAsync` 支持新增或更新，重命名经修改 Name 后 SaveAsync 实现

---

# 顶部菜单完善计划 · 2026-08-22

> 参考 `resources/dbeaver-screenshot/` 下 `database-top-menu.png` / `navigate-top-menu.png` / `search-top-menu.png` 三张顶部菜单截图。
> 与当前 Avalonia 版主窗口菜单（文件/连接/数据库/搜索/视图/工具/帮助）逐项对比，只补**常用高频**功能，不常用项明确不做。

## 已实现（对比后确认无需重复建设）

- [x] New Database Connection → 已有「新建连接」（文件/连接菜单、工具栏）
- [x] Connect / Invalidate+Reconnect / Disconnect → 已有「连接 / 重连 / 断开」
- [x] Commit / Rollback / Transaction mode → 已有「提交 / 回滚 / 自动提交」开关与事务命令
- [x] Tools 类功能（Convert / Compare / Diagnose / Optimize / Statistic / Backup / Import / Export / CodeGen / Documentation / IndexFragmentation 等）→ 工具菜单均已实现

## P0（核心常用，优先实现）

- [x] **元数据搜索（对应 Search > DB Metadata + Navigate > Open Database Object）**
  - 服务端：`IDbSchemaService.SearchMetadataAsync`（DefaultDbSchemaService.cs:255-400，模糊匹配表/视图/过程/函数/序列名，含列匹配）
  - UI：SearchWindow（搜索菜单或快捷键进入）；结果支持「定位树节点」（MainWindow.axaml.cs `LocateNodeInTreeAsync` 逐级懒加载展开）与「生成 SELECT 打开查询标签」
  - 备注：搜索框为独立对话框而非主窗口常驻全局搜索框（可用，暂不改）
- [x] **断开全部连接（对应 Database > Disconnect All）**
  - 「连接」菜单已有「断开全部」，命令 `MainWindowViewModel.DisconnectAllCommand`（MainWindowViewModel.cs:230-240）

## P1（高频补齐）

- [ ] **只读模式开关（对应 Database > Read-only connection）——未实现**
  - 「数据库」菜单增加 ToggleSwitch；全局状态存于 MainWindowViewModel
  - `QueryTabViewModel.ExecuteAsync`（QueryTabViewModel.cs:126-162）目前执行前仅有空值校验，**无任何只读拦截**，需补：非 SELECT / WITH / SHOW / EXPLAIN 开头的语句直接拒绝
- [ ] **全库数据搜索（对应 Search > DB Full-Text）——未实现**
  - 输入关键字，在指定数据库全部表的所有文本列中 `LIKE '%kw%'` 查找
  - 结果展示 库.表.列 + 样例值；点击生成 `SELECT * FROM t WHERE col LIKE ...`
  - 需控制成本：逐表顺序扫描 + LIMIT 保护 + 可取消；文本列仅限字符类型

## P2（体验增强，可选）

- [ ] **编辑器光标位置前进/后退（对应 Navigate > Previous/Next Edit Location, Alt+←/→）——未实现**
  - SqlEditor 内维护光标位置历史栈（跳转阈值 >N 行才入栈），快捷键导航

## 明确不实现（低频/平台特有）

- JDBC URL 直连、Driver Manager（驱动由各 ADO.NET 包内置）
- Transaction log / Pending transactions（依赖驱动级事务日志）
- Open Dashboard（监控仪表盘）、Tasks / Context tools（任务调度）
- Disconnect Others、Open Resource、File/Text Search、Quick Search、Data 导航子菜单

## 依赖项（实现前需补齐）

- [x] `IDbSchemaService.SearchMetadataAsync(connectionName, keyword)`：已实现（INFORMATION_SCHEMA 等名称模糊匹配，含列名）
- [x] TreeView 定位辅助：MainWindow 中按 DbObjectTreeNode 路径逐级展开并选中目标节点的方法已实现（`LocateNodeInTreeAsync`）
- [ ] 只读拦截需要 QueryTabViewModel 能读到全局只读标志（构造注入回调或静态 App 状态，注意 DI 注册方式）——随只读模式一起实现

---

# 功能完善度检查结论 · 2026-08-25

对 Avalonia 版数据库管理功能整体检查结果：**架构合理、核心链路完整**（AppCore 分层清晰，17 个服务接口覆盖连接/Schema/查询/编辑/导入导出/转换/对比/诊断/优化/统计/备份/代码生成等）。已完成功能总体质量良好。

## 遗留问题修复记录 · 2026-08-25（全部完成）

1. [x] **Generate SQL 占位模板已替换为真实脚本生成**
   - `IDdlService` 新增 `GenerateObjectScriptAsync`：基于真实列结构/主键/方言生成 SELECT TOP N / INSERT / UPDATE / DELETE / CREATE TABLE（DefaultDdlService.cs）
   - INSERT 排除自增与计算列；UPDATE/DELETE 基于主键生成 WHERE；SELECT TOP N 按方言输出 TOP/LIMIT/FETCH FIRST；CREATE TABLE 走各库 ScriptGenerator
   - `ObjectTreeContextMenuBuilder.GenerateSqlTemplateAsync` 已改为调用服务，并填充到当前查询标签页
2. [x] **比较与迁移窗口预填源连接**
   - 右键「比较与迁移」打开 SchemaCompare / DataCompare / Convert 窗口时，先 `RefreshConnections()` 再按节点所属连接预填 `SourceConnection`（MainWindow.axaml.cs `PrefillSourceConnection`）
3. [x] **Columns 文件夹右键新增「新建列...」入口**
   - `IDdlService.GetAddColumnTemplate` 按数据库类型生成 ALTER TABLE ADD 方言模板（SQL Server 无 COLUMN 关键字、Oracle 需括号），填充到查询编辑器
4. [x] **对话框辅助已抽公共 `DialogHelper`**（AppCore/Common/DialogHelper.cs）
   - ContentDialog / InputDialog 从 ObjectTreeContextMenuBuilder 移出，统一经 `DialogHelper.ShowConfirmAsync / ShowInputAsync` 调用
   - 备注：各 Window 内的轻量提示继续使用 MsBox.Avalonia 一行式调用，不强制迁移

## 待实现清单（按优先级）

- [ ] P1：只读模式开关 + QueryTabViewModel 执行拦截（防误改生产库）
- [ ] P1：全库数据搜索（DB Full-Text）
- [x] ~~P1：视图不应挂载 Indexes/Keys/Constraints 子文件夹~~（2026-08-25 已修复：`AddTableChildFolders` 增加 isView 参数，视图仅保留 Columns）
- [ ] P2：右键菜单图标
- [ ] P2：编辑器光标位置前进/后退导航
- [x] ~~P2：Oracle 数据库节点语义 / 连接阶段 N+1 schema 查询优化~~（2026-08-25 已修复，见下方树结构检查 #2、#3）
- [ ] P2（可选增强）：INSERT/UPDATE 模板可进一步带表注释/列默认值；「新建列」可考虑直接挂接表设计器编辑后提交

---

# 数据编辑功能重构 · 方案 C 实施 · 2026-08-25

> 背景：数据编辑器「新增行」点击无反应（新行追加在网格底部不可见）且体验不佳。经确认采用方案 C：
> ①查询结果内联编辑为主入口（方案 A）；②数据编辑器 Tab 保留为整表编辑入口并修缺陷（方案 B）。
> **2026-08-25 补充**：经检查查询 Tab 已具备分页与增删改能力，**数据编辑 Tab 已删除**（见下方），查询结果内联编辑成为唯一数据维护入口。

## 已实现

### 方案 A：查询结果内联编辑

- **可编辑判定**：执行 SELECT 成功后自动解析（新增 `Common/SimpleSelectParser.cs`）
  - 仅单表简单 SELECT 可编辑；含 JOIN/GROUP BY/DISTINCT/UNION/子查询/多语句等一律只读，并在状态栏显示原因
  - 经 `IDataEditService.GetTableMetadataAsync`（新增接口方法）读取目标表元数据
  - 校验：表必须有主键、SELECT 结果必须包含全部主键列；不满足则只读并说明
- **可编辑模式 UI**：结果区右上角出现「＋新增 / －删除 / 💾保存 / ↺还原」工具栏（MainWindow.axaml）
  - 非只读列（非自增/计算/二进制列）开放单元格双向编辑；自增/计算列保持只读
  - 新增行插入当前页末尾并**滚动定位选中**（不会"点了没反应"）
- **保存管线**：完全复用 `DefaultDataEditService.SaveChangesAsync`（事务内先删后改后插 + 乐观锁冲突检测）
  - 新增 `QueryResultRow` 行模型（Models/QueryResult.cs）：原始值快照/脏列/行状态，UPDATE 以原始主键值生成 WHERE
  - 删除行为标记删除，保存时统一 DELETE；「还原」恢复原值/放回删除行/丢弃新增行
- **翻页兼容**：改动跨页保留（行对象驻留在全量集合中）

### 方案 B：数据编辑器缺陷修复 → 已删除

- 原缺陷（新增行不可见）已随 Tab 删除而消除。
- **可行性结论**：查询 Tab 的分页（客户端分页）+ 内联增删改已可覆盖数据编辑 Tab 的核心能力
  - 差异：数据编辑器为**服务端分页**（`GetPagedDataTableAsync`，适合大表）；查询结果为**客户端分页**（全量加载后分页，大表 `SELECT *` 会全量拉取）
  - 结论：中小表完全可行；大表建议在查询中使用 `WHERE`/`LIMIT` 分页查询，或后续为查询结果增加服务端分页优化
- **删除内容**（2026-08-25）：
  - `MainWindow.axaml`：移除外层 `ContentModeTabs` 与 `数据编辑` TabItem，查询 Tab 成为唯一内容区
  - `MainWindow.axaml.cs`：移除 `_dataEditor`、列重建、新增/删除、切换等 5 处方法及 `openDataEditorTab` 回调
  - `ObjectTreeContextMenuBuilder.cs`：`编辑数据` 菜单由 `OpenDataEditor` 重定向为 `GenerateSelectScript`（在查询结果中编辑）
  - `MainWindowViewModel.cs`：移除 `DataEditor` 属性及 `OpenDataEditor/SwitchToDataEditor`，保留 `IDataEditService` 供查询内联编辑复用
  - `DataEditorViewModel.cs` 保留类文件与 DI 注册（暂不删除，便于回滚）

### 渲染缺陷修复（2026-08-25）

- 现象：查询/数据编辑均返回 1 行但表格空白
- 原因：`DataGridTextColumn` 绑定使用了 WPF 风格 `Item[列名]`，在 Avalonia 中应为 `[列名]`；`QueryResultRow` 的 `this[int]` 缺少 setter 导致双向绑定失效
- 修复：两处列重建均改为 `Binding($"[{columnName}]")`（Avalonia 索引器语法），补上 `QueryResultRow[int].set`，并补充 `PropertyChanged` 对 `[key]`/`Item[key]`/`Item[]` 的通知

## 后续优化候选（待使用反馈）

- [ ] 内联保存成功后自动重执行查询以刷新自增列值
- [ ] 主键列在表头加标记（如 🔑），提升辨识度
- [ ] 关闭有未保存内联改动的标签页时提示确认
- [ ] SimpleSelectParser 升级为更完整的 SQL 解析（当前保守判定，误判时退化为只读，安全）

---

# 对象浏览器树结构层级检查 · 2026-08-25

> 检查各数据库类型在对象浏览器中渲染的树层级是否合理完善（核心实现：`DefaultDbSchemaService.cs`）。

## 检查结论：整体架构合理 ✓

统一层级模型：`连接 → 数据库 → [Schema] → 类型文件夹 → 对象 → 子文件夹 → 子对象`，全部懒加载（占位节点 + 展开时加载，ObjectsExplorerViewModel.LoadFolderChildrenAsync / LoadTableChildFolderAsync）。

| 数据库 | 实际渲染层级 | 结论 |
|---|---|---|
| SQL Server | 连接→库→Schema→文件夹→表 | ✓ 系统 schema 已过滤（guest/sys/INFORMATION_SCHEMA/db_*）；系统库已过滤（master/model/msdb/tempdb） |
| Postgres | 连接→库→Schema→文件夹→表 | ✓ 过滤 pg_catalog/information_schema/pg_toast/template%/postgres；schema 逐库独立枚举 |
| Oracle | 连接→当前用户(单节点)→文件夹→表 | ✓ 单 schema 路径且作为过滤条件传入，不会混入其他用户对象 |
| MySQL | 连接→库→文件夹→表 | ✓ 无 schema 层（database 即 schema）；已过滤 sys/mysql/information_schema/performance_schema |
| SQLite | 连接→文件库→Tables/Views | ✓ 按 SupportDbObjectType 裁剪（仅 Tables/Views），无多余类型文件夹 |

设计亮点：
- 类型文件夹按 `interpreter.SupportDbObjectType` 方言能力动态裁剪（如 MySQL 无 Types/Sequences 文件夹）
- Schema 层仅在 `schemas.Count > 1` 时出现；单 schema 时作为查询过滤条件避免混入其他 schema 的对象
- 视图列经 `ColumnType.ViewColumn + IsForView` 单独获取；子对象显示文本带类型/可空/自增/外键引用等元信息

## 发现的问题（2026-08-25 已全部修复）

1. [x] **P1：视图挂载了不适用的子文件夹**（DefaultDbSchemaService.cs `ToNode` → `AddTableChildFolders`）
   - 修复：`AddTableChildFolders` 增加 `isView` 参数，视图仅保留 Columns 子文件夹，表才挂 Triggers/Indexes/Keys/Constraints
2. [x] **P2：Oracle「数据库」节点语义不准**
   - 修复：`ObjectTreeContextMenuBuilder.BuildDatabaseMenu` 经 `GetConnectionDatabaseType` 判断 Oracle 时文案改为「设为当前 Schema」
3. [x] **P2：连接阶段的 N+1 schema 查询**
   - 修复：`GetObjectTreeAsync` 改为 `Task.WhenAll` 并行枚举各库 schema（SQL Server/Postgres 各用目标库自己的解释器；Oracle 复用默认解释器且仅单库无并发冲突）
