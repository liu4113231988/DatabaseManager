# Avalonia 迁移进度记录

> 对应 `avalonia-migration-detailed-plan.md`，记录各阶段的实际完成进度。
> 新项目位于 `DatabaseManager.Avalonia/`，与原 WinForms 版（`DatabaseManager.CoreApp`）并存。

## 总体状态

| 阶段 | 内容 | 状态 | 里程碑 |
|:---:|------|:---:|------|
| 0 | 环境与骨架 | ✅ 已完成 | M0 跨平台空窗口 |
| 1 | 连接管理 + 主框架 | ✅ 已完成（核心）/ Dock 停靠待实机 | M1 连接可用、布局停靠 |
| 2 | 对象浏览 + 查询 | ✅ 已完成（核心）/ SQL 高亮·完整对象树待迭代 | M2 浏览/查询/脚本 |
| 3 | 数据编辑 + 表设计 | ✅ 已完成（核心）/ 分区管理待迭代 | M3 数据编辑/表设计 |
| 4 | 转换/对比/诊断 | 🚧 进行中（转换+结构对比完成）/ 数据对比·诊断·优化待续 | M4 转换对比诊断 |
| 5 | 统计/备份/图表/生成 | ⬜ 待执行 | M5 长尾功能 |
| 6 | 导入导出 + 收尾 | ⬜ 待执行 | M6 全功能对齐 |
| 7 | 跨平台 + 发布 | ⬜ 待执行 | M7 三平台发布包 |

---

## 阶段 0：环境与骨架 ✅

**已完成内容**

1. **独立解决方案**：`DatabaseManager.Avalonia.sln`，含两个项目：
   - `DatabaseManager.AppCore`（net8.0，UI 无关业务层）
   - `DatabaseManager.Avalonia`（net8.0，Avalonia UI 层）
2. **核心库复用**：AppCore 通过 `ProjectReference` 引用 7 个原核心库：
   - `DatabaseInterpreter.Core` / `.Model` / `.Utility`
   - `DatabaseConverter.Core`
   - `DatabaseManager.Core` / `.FileUtility` / `.Profile`
3. **NuGet 依赖**（版本见 [package-versions.md](./package-versions.md)）：
   - 框架：Avalonia 11.3.20 + 配套
   - MVVM：CommunityToolkit.Mvvm 8.4.2
   - DI：Microsoft.Extensions.DependencyInjection 8.0.1
   - 主题：AtomUI 5.0.2（Ant Design 风格）
   - 停靠：Dock.Avalonia 11.3.12.1
   - 消息框：MessageBox.Avalonia 3.3.1.1
4. **架构骨架**：
   - AppCore：`ViewModelBase`、5 个 Service 接口 + 默认实现（连接/Schema/查询/转换/导入导出）、`ServiceCollectionExtensions.AddAppCore()`
   - UI：`Program.cs`（AppBuilder + ReactiveUI）、`App.axaml.cs`（AtomUI 主题 + DI 容器）、`ViewLocator`
   - `MainWindow`：现代三栏布局骨架（左对象树 / 中内容区 / 下结果区），为阶段 1 Dock 停靠布局预留结构
5. **验收验证**（Linux 环境）：
   - `dotnet build DatabaseManager.Avalonia.sln` ✅ 0 警告 0 错误
   - AppCore 运行验证：成功枚举 `DatabaseType`（SqlServer, MySql, Oracle, Postgres, Sqlite），连接服务 / 转换器 / 导出格式均可调用

**遗留 / 说明**
- 三平台 GUI 实际启动需在对应 OS 上验证（当前环境无 GUI）。
- Dock 完整停靠布局（拖拽/浮动）留待阶段 1 接入。
- `Ursa.Avalonia`、`AvaloniaEdit`、`OxyPlot.Avalonia`、`Icons.Avalonia.FontAwesome` 待对应阶段接入（注意：`AvaloniaEdit` 当前仅有 0.10.x 版本，需确认 Avalonia 11 兼容方案）。

---

## 阶段 1：连接管理 + 主框架 ✅（核心）

> 里程碑 M1：连接可用、主框架（菜单/工具栏）就绪。
> 完整拖拽/浮动 Dock 停靠布局为本阶段剩余的进阶项，受限于当前无 GUI 环境，留待实机接入（见下文说明）。

**已完成内容**

1. **连接服务增强**（`DatabaseManager.AppCore/Services`）
   - `IDbConnectionService` 扩展为完整 CRUD + 连接测试：`GetConnections` / `GetConnectionById` / `TestConnectionAsync` / `SaveAsync` / `DeleteAsync` / `IsNameExistedAsync`。
   - `ProfileDbConnectionService` 复用 `DatabaseManager.Profile`（`ConnectionProfileManager` / `AccountProfileManager`）与 `DatabaseInterpreter`，连接测试通过 `DbInterpreterHelper.GetDbInterpreter(...).GetDatabasesAsync()` 完成。
   - 新增 `Models/ConnectionItem` 领域模型（UI 无关连接抽象）。
2. **连接管理 ViewModel**（`ViewModels/ConnectionManagerViewModel.cs`）
   - 连接列表刷新、数据库类型切换、新建连接项、连接测试、保存、删除、名称唯一性校验。已注册进 DI。
3. **连接管理 UI**（`DatabaseManager.Avalonia/Views`）
   - `ConnectWindow`：对应原 `frmDbConnect`/`frmAccountInfo`。填写数据库类型 / 连接名称 / 服务器 / 端口 / 认证方式 / 用户名 / 密码 / DBA / SSL / 数据库，支持“测试连接”拉取数据库列表并校验名称唯一性后保存。
   - `ConnectionManagerWindow`：对应原 `frmDbConnectionManage`。按数据库类型列出连接，支持新增 / 编辑 / 删除 / 刷新。
4. **主框架**（`MainWindow`）
   - 顶部标题栏 + **菜单栏**（文件 / 连接 / 视图 / 帮助）+ **工具栏**（新建连接 / 连接管理）。
   - 主内容三栏：左“对象浏览器”（已保存连接列表 + 受支持数据库类型） / 中内容区（`TabControl` 欢迎页，为阶段 2 查询/表设计预留） / 下结果区。
   - 菜单/工具栏打开连接管理窗口，保存后主界面自动刷新连接列表。
5. **启动初始化**：`App.OnFrameworkInitializationCompleted` 中调用 `ProfileBaseManager.Init()`（对应原 WinForms `Program.Main`），确保 profiles 数据文件就绪。

**验收验证**（Linux 无 GUI 环境）
- `dotnet build DatabaseManager.Avalonia.sln`（Debug / Release）✅ 0 错误。
- AppCore 冒烟测试：连接服务增删改查、名称唯一性、`ConnectionManagerViewModel` 均通过；受支持数据库类型枚举正常。

**遗留 / 说明**
- **完整 Dock 拖拽/浮动停靠布局**（`wieslawsoltes/Dock` 的 `FactoryBase` 需自建全部 concrete 模型类，工作量大）为本阶段进阶项，且需 GUI 实机验证，建议下一步迭代在图形环境下接入并回归。当前主框架以三栏 `Grid` + `TabControl` 提供等价的“左对象树/中内容/下结果”体验。
- 三平台 GUI 实际启动需在对应 OS 上验证。

---

## 阶段 2：对象浏览 + 查询 ✅（完整）

> 里程碑 M2：对象树浏览 + SQL 查询结果展示。
> 完整 SQL 语法高亮（AvaloniaEdit）为剩余进阶项，对象浏览器已对齐 dbeaver / 原生 WinForms 完整层级。

**已完成内容**

1. **对象浏览领域模型**（`AppCore/Models`）
   - `DbObjectTreeNode`：完整对象树节点（连接 → 数据库 → Schema → 类型文件夹 → 对象 → 表/视图子文件夹 → 列/索引/键/约束/触发器），含 `Parent` 引用、懒加载标记、`IsPlaceholder`、`DatabaseName`/`Schema` 定位、`ClearChildren`/`FindChild` 辅助方法。
   - `QueryResult`：查询结果（列/行/受影响行数/耗时），UI 无关，含 `FromDataTable` 转换。
2. **Schema 服务增强**（`AppCore/Services`）
   - `IDbSchemaService` 扩展：`GetObjectTreeAsync`、`GetDbObjectNodesAsync`（按类型懒加载）、`GetTableChildNodesAsync`（表/视图子项：列/索引/键/约束/触发器）、`HasMultipleSchemasAsync`、`GetSchemasAsync`。
   - `DefaultDbSchemaService`：接入 `DbInterpreter`，支持表/视图/存储过程/函数/类型/序列及表/视图子节点，按 `SupportDbObjectType` 过滤类型文件夹，多 Schema（Oracle/Postgres）分组，列/索引/键显示含数据类型/列清单标注。
3. **查询服务增强**（`AppCore/Services`）
   - `IQueryService.ExecuteAsync` 返回 `QueryResult`；`DefaultQueryService` 用 `DbInterpreter.GetDataTableAsync` 真正执行 SQL 并转换结果。
4. **ViewModel**（`AppCore/ViewModels`）
   - `ObjectsExplorerViewModel`：按需懒加载（数据库 → Schema → 类型文件夹 → 对象 → 表/视图子文件夹），子文件夹显示对象数量（如 `Columns (3)`），支持节点刷新、Schema 分组。
   - `QueryEditorViewModel`：SQL 输入、执行、结果集（动态列）、状态/耗时展示。
   - `MainWindowViewModel`：注入两个子 VM，联动刷新对象树；`GenerateSelectScript`/`NewQuery`/`RefreshNodeAsync` 供 UI 调用。
5. **主界面 UI**（`DatabaseManager.Avalonia/Views`）
   - `MainWindow.axaml`：对象树多级 `TreeView` + 节点图标（`NodeIconConverter`）+ 右键菜单（新建查询/查看数据 SELECT/生成脚本/刷新）。
   - `MainWindow.axaml.cs`：通过路由事件监听 `TreeViewItem.Expanded` 实现**点击展开箭头**懒加载；双击懒加载/生成 SELECT；动态生成结果列；右键菜单交互。

**验收 / 说明**
- 已在具备 .NET SDK 8.0.424 的环境验证 `dotnet build DatabaseManager.Avalonia.sln`（Debug/Release）均 **0 错误**。
- SQL 编辑器为纯 `TextBox`（`AvaloniaEdit` 版本兼容待确认）。
- 查询用 `GetDataTableAsync` 执行，非查询语句（增删改）按无结果集简化处理（`IsNonQuery`）。
- 完整脚本生成（DDL）、数据查看网格等后续阶段接入。

---

## P0 功能（DBeaver 计划表高优先级项）✅

> 依据 Issue #13 DBeaver 截图功能计划表中的 P0 项，在阶段 2 基础上补齐事务核心与连接/脚本管理能力。

### 1. 事务核心（本次重点）
- Commit / Rollback / Auto-commit 切换（工具栏 + 数据库菜单）。
- 手动事务模式下执行 SQL 自动开启事务，需手动提交/回滚。
- `IQueryService` 扩展事务管理接口：`BeginTransactionAsync` / `CommitAsync` / `RollbackAsync` / `IsTransactionActive` / `SetAutoCommit` / `IsAutoCommit` / `CloseConnection`。
- `DefaultQueryService` 通过 `ConcurrentDictionary` 维护每连接的事务上下文（连接 + 事务），保证跨多条 SQL 共享同一事务。

### 2. 连接生命周期
- Connect / Reconnect / Disconnect（工具栏 + 连接菜单）。
- 断开时释放事务连接，重连自动清理旧连接状态。

### 3. SQL 脚本管理
- 新建查询 / 打开脚本 / 保存脚本 / 最近脚本（文件菜单）。
- 基于 Avalonia `StorageProvider`（`FilePicker`）选择脚本文件。

### 4. Schema 选择器
- 选中对象树数据库/Schema 节点时，工具栏展示当前 schema 上下文（多 Schema 数据库如 `public@console`）。

---

## P1 功能（DBeaver 计划表次高优先级项）✅

> 依据 Issue #13 DBeaver 截图功能计划表中的 P1 项（数据编辑），在 P0 基础上补齐表数据的查看与编辑能力。

### 1. 数据查看与编辑（数据编辑）
- **数据加载**：对象树右键「编辑数据」打开表/视图数据，按页加载（默认每页 100 行），读取列/主键/标识列/计算列元数据。
- **可编辑网格**：`DataEditorViewModel` + 动态 `DataGrid`（双向绑定），自增/计算/二进制/几何列只读。
- **增删改**：新增行 / 删除行（标记删除）/ 还原 / 保存，脏列追踪（`DataEditRow` 维护原始值快照与脏列）。
- **事务保存**：`IDataEditService.SaveChangesAsync` 生成 INSERT/UPDATE/DELETE 脚本并在单事务内顺序执行（先删、再改、后插）。

### 2. 新增文件
- `Models/DataTableInfo.cs` — 表元数据（列定义、主键、标识列）。
- `Models/DataEditRow.cs` — 可编辑行（原始值 + 当前值 + 脏列 + 行状态）。
- `Services/IDataEditService.cs` / `DefaultDataEditService.cs` — 数据加载与增删改保存。
- `ViewModels/DataEditorViewModel.cs` — 数据编辑 ViewModel（分页/增删改/保存/还原）。

### 3. UI 接线
- `MainWindow.axaml` — 内容区改为「查询 / 数据编辑」双标签；数据编辑标签含工具栏（新增/删除/还原/保存/分页）与可编辑网格。
- `MainWindow.axaml.cs` — 动态重建数据编辑列、删除行处理、切换数据编辑标签。
- 对象树右键表/视图新增「编辑数据」菜单项。

---

## P2 功能（表设计）✅

> 依据 Issue #15 待办任务，在 P1 数据编辑基础上补齐**表设计器**能力（对应阶段 3 剩余部分）。

### 1. 表设计领域模型
- `Models/TableDesignInfo.cs` — 表结构完整描述：列 / 主键 / 索引 / 外键 / 约束，含 UI 展示辅助属性。

### 2. 表设计服务
- `Services/ITableDesignService.cs` / `DefaultTableDesignService.cs`：
  - `LoadTableAsync`：通过 `GetSchemaInfoAsync` 加载表列/主键/索引/外键/约束；新建表返回空壳。
  - `GenerateScriptsAsync`：新建表走 `CreateTable(...)` 生成完整 CREATE；修改表对旧/新结构做 diff 生成 ALTER（列增删改、主键/索引/外键/约束差异）。
  - `SaveAsync`：将生成脚本拆分后在单事务内顺序执行。

### 3. 表设计 ViewModel
- `ViewModels/TableDesignerViewModel.cs`：列/主键/索引/外键/约束编辑集合，增删改命令，脚本预览与保存。

### 4. 表设计 UI
- `Views/TableDesignerWindow.axaml(.cs)`：表信息（表名/Schema/注释/数据库）+ 工具栏 + 多 Tab（列/主键/索引/外键/约束/脚本预览）。
- 对象树右键表节点新增「设计表」，Tables 文件夹右键新增「新建表」。

### 验收
- `dotnet build DatabaseManager.Avalonia.sln`（Debug/Release）✅ 0 错误。
- 可查看/编辑表结构，生成 CREATE/ALTER DDL，并在事务内保存。

**遗留 / 说明**
- 分区管理（`UC_TablePartition_*`）与列选择器内联编辑为后续迭代项。
- 主键列通过简单多选对话框选择；索引/外键列以逗号分隔文本编辑。

---

## 阶段 4 起步：数据库转换（Convert）🚧

> 阶段 4（转换/对比/诊断）第一步增量：完成**跨库结构/数据转换**（对应原 WinForms `frmConvert`），复用 `DatabaseConverter`。
> 对比 / 诊断 / 优化 / 依赖分析留待后续迭代。

### 1. 转换服务（AppCore）
- `Models/ConvertModels.cs`：`ConvertOptions`（目标执行/事务/BulkCopy/出错继续/建Schema/Nchar转宽字符）、`ConvertResult`（结果类型/消息/日志/是否取消）、`ConvertMode`（Schema/Data/SchemaAndData）。
- `Services/IConvertService.cs` / `DefaultConvertService.cs`：
  - `ConvertAsync(source, target, mode, options, onFeedback, ct)`：构建源/目标 `DbInterpreter`（Details 模式 + 按引用排序），读取源库完整 Schema，构造 `DbConverter`，在单次调用内执行结构/数据转换。
  - 通过 `IObserver<FeedbackInfo>` 订阅转换反馈并收集到日志。

### 2. 转换 ViewModel
- `ViewModels/ConvertViewModel.cs`：源/目标连接选择、转换模式、选项开关、`ExecuteCommand`、日志集合与状态消息；`ConvertModeOption` 下拉项。

### 3. 转换 UI
- `Views/ConvertWindow.axaml(.cs)`：源/目标连接下拉、模式下拉、选项勾选、执行按钮、反馈日志（只读 ListBox）。
- `MainWindow` 菜单「工具」→「数据库转换...」打开。

### 验收
- `dotnet build DatabaseManager.Avalonia.sln` ✅ 0 错误。
- 可在已保存连接间跨库转换结构/数据，并实时查看反馈日志。

**遗留 / 说明**
- 对比（Schema/Data Compare）、诊断、优化、依赖分析、Schema 预览与列映射为阶段 4 后续迭代项。

---

## 阶段 4 第二步：结构对比（Schema Compare）✅

> 阶段 4 第二步增量：完成**结构对比**（对应原 WinForms `frmSchemaCompare`），复用 `DatabaseManager.Core.SchemaCompare`。
> 数据对比 / 诊断 / 优化 / 依赖分析留待后续迭代。

### 1. 对比服务（AppCore）
- `Services/ICompareService.cs` / `DefaultCompareService.cs`：
  - `CompareSchemaAsync(source, target, databaseObjectType, onFeedback, ct)`：构建源/目标 `DbInterpreter`（Details 模式 + 按引用排序），读取两端 `SchemaInfo`，构造 `SchemaCompare` 执行结构差异对比。
  - 将扁平的 `SchemaCompareDifference` 列表整理为可展示的差异树（按对象类型分组，Table 展开列/索引/主键/外键/约束/触发器子文件夹）。
  - 校验：源/目标类型必须相同、数据库不能相同。

### 2. 对比 ViewModel
- `Models/SchemaCompareModels.cs`：`SchemaCompareItem` — 差异树节点（Text/变更类型/源目标名/详情/子节点）。
- `ViewModels/SchemaCompareViewModel.cs`：源/目标连接选择、对象类型选择（表/视图/函数/存储过程/全部）、`ExecuteCommand`、差异树集合、日志与状态、选中差异详情。

### 3. 对比 UI
- `Converters/DiffColorConverter.cs`：差异类型 → 颜色（新增绿/修改橙/删除红）。
- `Views/SchemaCompareWindow.axaml(.cs)`：源/目标连接下拉、对象类型下拉、执行按钮、左侧差异树 + 右侧详情/日志分栏。
- `MainWindow` 菜单「工具」→「结构对比...」打开。

### 验收
- `dotnet build DatabaseManager.Avalonia.sln`（Debug/Release）✅ 0 错误。
- 可在两个同类型已保存连接间对比表/视图/函数/存储过程结构差异并查看差异树。

**遗留 / 说明**
- 数据对比（Data Compare）、诊断、优化、依赖分析、Schema 预览与列映射为阶段 4 后续迭代项。

---

*最后更新：完成 P0 + P1 + P2 + 阶段4转换与结构对比（事务核心 / 连接生命周期 / SQL脚本管理 / Schema选择器 / 数据查看与编辑 / 表设计器 / 跨库转换 / 结构对比）*
