# WinForms → AvaloniaUI 迁移详细实施计划（含开源项目调研）

> 本文档在 [PR #3 `avalonia-migration-plan.md`](https://cnb.cool/sean-nj/DatabaseManager/-/pulls/3) 基础上深化，重点补充：
> 1. **Avalonia 生态可复用开源项目调研与选型**（替代 WinForms 控件依赖的最佳方案）
> 2. **更详细的阶段任务分解**（精确到具体文件/控件/ViewModel）
> 3. **布局不强制对齐策略**：用户允许"布局不完全对齐"，因此采用**功能等价 + 现代布局重设计**而非逐像素复刻
>
> 原则：**不在原项目修改，新建独立项目**，原 WinForms 版（`DatabaseManager.CoreApp`）保持可用。

---

## 〇、核心结论（TL;DR）

| 维度 | 结论 |
|------|------|
| 复用比例 | 核心引擎（`DatabaseInterpreter.*` / `DatabaseConverter.*` / `DatabaseManager.Core/FileUtility/Profile`）**约 2/3 代码零改动复用** |
| 需重写 | 仅 UI 层 `DatabaseManager.CoreApp`（194 个 .cs / 82 Designer / 86 resx / 约 4.9 万行） |
| 关键选型 | **主题**：`AtomUI`（Ant Design 风格，与本项目现有 AntdUI 视觉一致）⭐823 |
| | **Dock**：`wieslawsoltes/Dock` ⭐1459 |
| | **表格**：`Avalonia.Controls.TreeDataGrid` + `ProDataGrid` |
| | **编辑器**：`AvaloniaEdit` + TextMate ⭐1122 |
| | **MVVM**：`CommunityToolkit.Mvvm` |
| 新架构 | 增加 `DatabaseManager.AppCore`（UI 无关 ViewModel/Service 层）+ `DatabaseManager.Avalonia`（纯 UI 层） |
| 工作量 | 约 **30-45 人日**（单人 6-9 周），布局重设计可进一步优化 |
| 策略 | **阶段 0+1 先做最小可行原型（MVP）验证选型，再全面铺开** |

---

## 一、Avalonia 生态开源项目调研（核心选型）

> 调研自 GitHub 公开仓库，选取**成熟度（star）、维护活跃度、与需求契合度**三维度最优者。

### 1.1 主题与控件库（替代 AntdUI）

| 开源项目 | Star | 说明 | 选用建议 |
|---------|:---:|------|:---:|
| **AtomUI** (`AtomUI/AtomUI`) | 823 | 将 **Ant Design 设计语言**带到 Avalonia，含现代控件、主题、原生集成。**与当前项目 AntdUI 视觉语言高度一致**，迁移后外观几乎无缝。 | ✅ **首选** |
| **Semi.Avalonia** (`irihitech/Semi.Avalonia`) | 1928 | Semi Design 风格，企业级控件多（DataGrid、Tree、分页等），文档完善。 | 备选（外观为 Semi 而非 Antd，需接受风格变化） |
| **Ursa.Avalonia** (`irihitech/Ursa.Avalonia`) | 1550 | 企业级控件补充库（含大量高级控件），可叠加在 Semi/AtomUI 之上。 | ✅ 作为 AtomUI 的**补充增强** |
| **FluentAvalonia** (`amwx/FluentAvalonia`) | 1588 | WinUI/Windows 11 风格，适合追求微软原生观感。 | 备选 |
| **Aura.UI** (`PieroCastillo/Aura.UI`) | 723 | 通用控件扩展集合。 | 按需引入个别控件 |

> **决策**：主题基底选 **`AtomUI`**（视觉对齐原 AntdUI，用户"布局不强制对齐"的前提下保持品牌一致），控件缺口用 **`Ursa.Avalonia`** 补齐（如高级表格、徽标、标签、进度等）。

### 1.2 布局 / 停靠系统（替代 DockPanelSuite）

| 开源项目 | Star | 说明 | 选用 |
|---------|:---:|------|:---:|
| **Dock** (`wieslawsoltes/Dock`) | 1459 | 功能完整、与 Avalonia 深度集成的停靠布局系统，支持拖拽、浮动、标签页分组，是 Avalonia 生态最成熟的 Dock 方案。 | ✅ **首选** |
| **NP.Avalonia.UniDock** (`npolyak/NP.Ava.UniDock`) | 218 | 另一个停靠方案，功能强大但较复杂。 | 备选 |

> 原 WinForms 的 `DockPanelSuite.ThemeVS2015` 停靠布局（对象树 / 内容区 / 结果区）用 **Dock** 重新实现，支持与 VS 类似的拖拽停靠体验。

### 1.3 表格与树（替代 DataGridView / TreeView / ObjectListView）

| 开源项目 | Star | 说明 | 选用 |
|---------|:---:|------|:---:|
| **Avalonia.Controls.TreeDataGrid**（官方） | 347 | TreeView + DataGrid 结合的官方控件，虚拟滚动、层级展示，适合**对象树 + 结果网格**。 | ✅ 对象树/结果网格 |
| **ProDataGrid** (`wieslawsoltes/ProDataGrid`) | 271 | 高性能 DataGrid，大数据量虚拟化表现优，适合**查询结果大表**。 | ✅ 大数据网格 |
| **Avalonia.Controls.DataGrid**（官方） | - | 基础 DataGrid，随 Avalonia 分发，成熟稳定。 | 兜底/轻量场景 |

> 原项目 `DataGridView` 出现 450+ 处、`TreeView` 13 处、`ObjectListView` 2 处。迁移策略：查询结果与数据编辑用 **DataGrid**（虚拟滚动），对象浏览器用 **TreeDataGrid**。

### 1.4 代码编辑器（替代 SqlCodeEditor）

| 开源项目 | Star | 说明 | 选用 |
|---------|:---:|------|:---:|
| **AvaloniaEdit** (`AvaloniaUI/AvaloniaEdit`) | 1122 | AvalonEdit 的 Avalonia 移植，成熟稳定，支持语法高亮、行号、折叠（配合 TextMate/文法）。 | ✅ **首选** |
| TextMate Grammars | - | 配合 `TextMateSharp` 加载 SQL 语法高亮（可通过 `TextMateSharp.Grammars` 或自带 SQL 高亮）。 | ✅ 配套 |

> 原 `SqlCodeEditor`（SQL 高亮编辑器）用 **AvaloniaEdit + SQL 语法** 替换，可支持高亮与基础智能提示。

### 1.5 图表（统计）

| 开源项目 | Star | 说明 | 选用 |
|---------|:---:|------|:---:|
| **OxyPlot.Avalonia** (`oxyplot/oxyplot-avalonia`) | 359 | 成熟稳定的绘图库，适合**统计图表**（记录数、占用等）。 | ✅ 统计图表 |
| **Avalonia.Microcharts** | 192 | 轻量图表，适合简单仪表。 | 备选 |

> 统计类图表（柱状/饼图）用 **OxyPlot** 覆盖；~~`frmDatabaseDiagram` ER 关系图~~ 已移除，不作为 Avalonia 迁移主线待办。

### 1.6 其它通用组件

| 需求 | 选型 | 说明 |
|------|------|------|
| 消息框 | `AvaloniaCommunity/MessageBox.Avalonia`（⭐594） | 替代 `MessageBox.Show`（129+ 处） |
| 图标 | `Icons.Avalonia.FontAwesome` / `AvaloniaFontIcons` | 替代 `FontAwesome.Sharp` |
| 属性网格 | `Avalonia.PropertyGrid`（社区）或自研 `ItemsControl` | 替代 `PropertyGrid`（27 处），可用 Ursa/自研实现 |
| 对话框 | `StorageProvider`（官方 `TopLevel.StorageProvider`） | 替代 OpenFile/SaveFile/FolderBrowser |
| 剪贴板/屏幕 | `TopLevel.Clipboard` / `TopLevel.Screens` | 替代 Clipboard/Screen |
| MVVM | `CommunityToolkit.Mvvm`（⭐3k+，微软官方） | SourceGenerator 减少样板代码 |
| DI | `Microsoft.Extensions.DependencyInjection` | 服务注册/解析 |
| 主题切换/暗色 | `AtomUI` + 资源字典 | 亮/暗色切换 |
| 文本差异 | `DiffPlex`（**可原样复用**，无 UI 依赖） | 结构/数据对比 |

### 1.7 可参考的完整开源数据库客户端（架构借鉴）

| 项目 | 说明 | 借鉴点 |
|------|------|------|
| `timothydodd/dbclient` | 基于 Avalonia + .NET 的跨平台 SQL 客户端（SQL Server/MySQL/SQLite） | MVVM 分层、连接管理、查询编辑器的 Avalonia 落地范式 |
| `Antares SQL`（Java） | 经典多库管理工具 | 功能组织、对象树与编辑器交互模型 |
| `DBeaver`（Java/RCP） | 主流多库客户端 | 对象树 / 查询 / 数据编辑 / 转换的功能解耦思路 |

> 注：以上仅作**架构与交互参考**，本项目核心引擎自行维护（复用现有 `DatabaseInterpreter.*`），不直接引入其代码。

---

## 二、目标架构（新项目，与原项目并存）

```
DatabaseManager.Avalonia.sln                    # 【新】独立解决方案
├─ DatabaseManager.AppCore/                     # 【新】UI 无关业务层（net8.0）
│   ├─ ViewModels/                              # 各页面/控件的 VM（可独立单测）
│   │   ├─ MainWindowViewModel.cs
│   │   ├─ Connection/ConnectionManagerViewModel.cs
│   │   ├─ Explorer/ObjectsExplorerViewModel.cs
│   │   ├─ Query/QueryEditorViewModel.cs
│   │   ├─ Data/DataViewerViewModel.cs
│   │   ├─ TableDesigner/TableDesignerViewModel.cs
│   │   ├─ Convert/ConvertViewModel.cs
│   │   ├─ Compare/CompareViewModel.cs
│   │   ├─ Diagnose/DiagnoseViewModel.cs
│   │   ├─ Statistic/StatisticViewModel.cs
│   │   └─ Backup/BackupViewModel.cs
│   ├─ Services/                                # 数据库交互、转换、导出服务封装
│   │   ├─ IDbConnectionService.cs
│   │   ├─ IDbSchemaService.cs
│   │   ├─ IQueryService.cs
│   │   ├─ IConvertService.cs
│   │   ├─ IExportImportService.cs
│   │   └─ ...（实现均复用 DatabaseInterpreter.* / DatabaseConverter.*）
│   ├─ Models/
│   └─ Common/                                  # 通用工具、枚举、资源
├─ DatabaseManager.Avalonia/                    # 【新】Avalonia UI 项目（net8.0）
│   ├─ Views/                                   # 主窗口、各页面、对话框（.axaml）
│   ├─ Controls/                                # 自定义控件（DataGrid 扩展、分页、图标等）
│   ├─ Converters/                              # 值转换器
│   ├─ Themes/                                  # AtomUI 主题资源、亮暗色
│   ├─ App.axaml / Program.cs
│   └─ app.manifest
└─ DatabaseManager.Avalonia.Tests/              # 【新】单元测试（AppCore 业务层）
```

> 关键解耦：**UI 层不直接引用数据库驱动**，所有数据库操作通过 `AppCore` 的 Service 接口进行。`AppCore` 引用核心引擎库（零 WinForms 依赖），可独立单测。

---

## 三、分阶段详细实施计划

> 每阶段含：目标、任务分解（精确到文件/VM/控件）、验收标准、风险。布局以**功能等价 + 现代布局**为准，不逐像素对齐。

### 阶段 0：环境与骨架（1-2 天） — 里程碑 M0：跨平台空窗口

> ✅ **本 PR 已完成**（`DatabaseManager.Avalonia` 目录，详见 [docs/migration-progress.md](../DatabaseManager.Avalonia/docs/migration-progress.md)）

**任务**
- [x] 安装模板：`dotnet new install Avalonia.Templates`
- [x] 新建 `DatabaseManager.Avalonia.sln`，创建 `DatabaseManager.AppCore`（`net8.0`）与 `DatabaseManager.Avalonia`（`net8.0`）项目
- [x] 添加核心库 `ProjectReference`（Interpreter / Converter / Core / FileUtility / Profile）
- [x] 引入 NuGet：`CommunityToolkit.Mvvm`、`Microsoft.Extensions.DependencyInjection`、`AtomUI`、`Dock`、`MessageBox.Avalonia`（`Ursa.Avalonia`/`AvaloniaEdit`/`OxyPlot`/`Icons` 按阶段计划后续接入）
- [x] 配置 `Program.cs`（Avalonia AppBuilder + DI 容器 + 主题）
- [x] 建立 MVVM 基类（`ViewModelBase`）、`IDbConnectionService` 等空接口、服务注册
- [x] 基础 `MainWindow`（现代三栏布局骨架；Dock 停靠完整接入在阶段 1）
- [ ] **验收**：三平台（Win/Linux/macOS）可启动空窗口；`AppCore` 能调用核心库完成一次 `DatabaseType` 枚举

> 验收说明：`AppCore` 已在 Linux 环境验证可枚举 `DatabaseType`（SqlServer/MySql/Oracle/Postgres/Sqlite）；三平台 GUI 启动需在对应 OS 上验证。

**风险与应对**：AtomUI 与 Ursa 版本兼容性 → 已在 MVP 阶段锁定版本组合并记录到 [`docs/package-versions.md`](../DatabaseManager.Avalonia/docs/package-versions.md)。

---

### 阶段 1：连接管理 + 主框架（3-5 天） — 里程碑 M1：连接可用、布局停靠

**任务**
- [x] `ConnectionManagerViewModel`：连接增删改查（复用 `DatabaseManager.Profile`）
- [x] 视图：`frmDbConnect`→`ConnectWindow`、`frmDbConnectionManage`→`ConnectionManagerWindow`（`UC_DbAccountInfo` 等账号信息收敛进 `ConnectWindow`）
- [ ] 主框架：`frmMain` 用 **Dock** 重建布局（左：对象树；中：内容区 Tab；下：输出/结果），保留原停靠体验（当前以三栏 `Grid`+`TabControl` 实现等价布局；完整拖拽/浮动 Dock 待实机接入）
- [x] 菜单/工具栏：`MenuStrip`/`ToolStrip`→`Menu`/`ToolBar`（用 AtomUI 样式）
- [ ] `frmSetting`→`SettingsWindow`（含数据源类型映射、可见性等设置项）
- [ ] **验收**：可新建/管理多库连接（连接串/账号/SSH），主界面可拖拽停靠、切换主题

**布局说明**：主窗口采用现代化三栏（左对象树 + 中内容 + 下结果），**不与 WinForms 逐像素对齐**，但保证同等功能入口。

---

### 阶段 2：对象浏览 + 查询（5-8 天） — 里程碑 M2：浏览/查询/脚本

**任务**
- [ ] `ObjectsExplorerViewModel`：对象树数据源（Schema → 表/视图/存储过程/函数/触发器/序列）
- [ ] 视图：`UC_DbObjectsExplorer`/`UC_DbObjectsComplexTree`/`UC_DbObjectsSimpleTree` → `ObjectsExplorerControl`（用 `TreeDataGrid`，替代 WinForms `TreeView`）
- [ ] 对象右键菜单：查询数据/设计表/生成脚本/查看依赖/导出（`ContextMenu`）
- [ ] `QueryEditorViewModel` + 视图：`frmSqlQuery`/`UC_QueryEditor` → `QueryEditorControl`（用 **AvaloniaEdit** + SQL 高亮）
- [ ] `QueryResultGrid`：`UC_QueryResultGrid`/`UC_DataViewer`/`UC_Pagination` → `QueryResultControl`（用 **DataGrid/ProDataGrid** + 自定义分页）
- [ ] 脚本：`frmGenerateScripts`→`GenerateScriptsWindow`、`frmTranslateScript`→`TranslateScriptWindow`（复用 `ScriptGenerator`/Converter）
- [ ] `frmScriptsViewer`→`ScriptsViewerWindow`
- [ ] **验收**：可浏览对象树、执行 SQL 查询并分页展示结果、可生成/翻译脚本

**布局说明**：查询编辑器采用"上编辑器 + 下结果网格"的现代分栏，不做 WinForms 原样复刻。

---

### 阶段 3：数据编辑 + 表设计器（5-8 天） — 里程碑 M3：数据编辑/表设计

**任务**
- [ ] `DataViewerViewModel` + 视图：`UC_DataViewer`/`UC_DataEditor` → `DataEditorControl`（增删改、提交，用 DataGrid 编辑列）
- [ ] `TableDesignerViewModel` + 视图：`frmTableDesigner` → `TableDesignerWindow`（Tab 页组织）
  - `UC_TableColumns`→`ColumnsTab`：列定义（类型/默认值/可空）
  - `UC_TablePrimaryKey`→`PrimaryKeyTab`：主键
  - `UC_TableIndexes`→`IndexesTab`：索引
  - `UC_TableForeignKeys`→`ForeignKeysTab`：外键
  - `UC_TableConstraints`→`ConstraintsTab`：约束
  - `UC_TableComment`→`CommentTab`：表注释
- [ ] `frmTableCopy`→`TableCopyWindow`
- [ ] 大对象查看：`frmImageViewer`/`frmJsonViewer`/`frmWktViewer` → 统一 `ObjectViewerWindow`
- [ ] `UC_TablePartition_*`（MySql/Oracle/Postgres/SqlServer）→ `TablePartitionControl`
- [ ] **验收**：可查看/编辑表数据，完整设计表结构（列/主键/索引/外键/约束），分区管理可用

---

### 阶段 4：转换、对比、诊断、优化（5-7 天） — 里程碑 M4：转换对比诊断

**任务**
- [x] 转换：`frmConvert`/`UC_ConvertSetting`/`UC_ConvertResult`/`frmSchemaMapping`/`frmSchemaPreviewer` → `ConvertWindow`（复用 `DatabaseConverter.*`）
- [x] 列映射：`frmColumnMapping`→`ColumnMappingWindow`（对齐导入列映射与 Schema 预览列编辑）
- [x] 对比：`frmSchemaCompare`→`SchemaCompareWindow`、`frmDataCompare`/`frmDataCompareResult`→`DataCompareWindow`（复用 `DiffPlex`）
- [x] 诊断：`frmDiagnose`/`frmTableDiagnoseResult`/`frmScriptDiagnoseResult` → `DiagnoseWindow`
- [x] 优化：`frmOpitimizeResult`→`OptimizeResultWindow`
- [x] 依赖分析：`frmDbObjectDependency`/`frmTableDependency`→`DependencyWindow`
- [x] **验收**：跨库结构/数据转换、Schema/数据对比、诊断、优化流程可跑通

---

### 阶段 5：统计、备份、图表、代码生成（5-7 天） — 里程碑 M5：长尾功能

**任务**
- [x] 统计：`frmStatistic`→`StatisticWindow`、`frmTableRecordCount`→`RecordCountWindow`、`frmTableColumnContentMaxLength`→`ColumnLengthWindow`（统计图表用 **OxyPlot**）
- [x] 索引碎片：`Analysis/frmIndexFragmentation`→`IndexFragmentationWindow`
- [x] 备份：`frmBackupSetting`/`frmBackupSettingRedefine`→`BackupWindow`
- [x] ~~**数据库关系图**~~ `frmDatabaseDiagram`→`DatabaseDiagramWindow`（已移除，不作为 Avalonia 迁移主线待办）
- [x] 代码生成：`frmCodeGenerator`→`CodeGeneratorWindow`
- [x] 文档生成：`Documentation/frmGenerateColumnDocumentation`→`ColumnDocumentationWindow`
- [x] **验收**：统计、备份、依赖、代码/文档生成可用（关系图待办已移除）

---

### 阶段 6：导入导出 + 收尾（3-5 天） — 里程碑 M6：全功能对齐

**任务**
- [x] 导入/导出：`Import/frmImportData`→`ImportWindow`、`Export/frmExportData`→`ExportWindow`（复用 `FileUtility`/`DataExporter`/`DataImporter`）
- [ ] 通用对话框：`frmColumnSelect`/`frmDataFilter`/`frmDataFilterCondition`/`frmItemsSelector`/`frmItemsSimpleSelector`/`frmNumberSelector`/`frmInput`/`frmFindBox`/`frmTextContent`/`frmColumnMapping` → 统一 `CommonDialog` 集
- [ ] 其余：`frmAccountInfo`、`frmFileConnection`、`frmDbObjectDependency`、`frmLockApp`、`frmObjectsExplorer`、`frmTableColumnDetails`/`frmTableColumnRelation`、`frmDatabaseVisibility`
- [ ] 资源迁移：resx 图片/图标 → Avalonia 资源字典（`.axaml` + 嵌入资源）
- [ ] 全局异常处理、日志、国际化文案梳理
- [ ] 主题定制与暗色模式（AtomUI 亮/暗切换）
- [ ] **验收**：全部功能迁移完毕，可打包发布

---

### 阶段 7：跨平台验证 + 发布（2-3 天） — 里程碑 M7：三平台发布包

**任务**
- [ ] Win/Linux/macOS 三平台构建与运行验证
- [ ] 打包：Win 单文件发布；macOS `.dmg`；Linux `AppImage`/`deb`
- [ ] 性能优化：大数据量 DataGrid 虚拟滚动、查询异步化（`Task.Run` + `Progress<T>`）
- [ ] 为 `AppCore` 业务层补单元测试（转换、查询服务、连接管理）
- [ ] **验收**：三平台发布包可用，核心流程回归通过

---

## 四、工作量与里程碑总览

| 阶段 | 内容 | 预估人日 | 里程碑 | 关键开源选型 |
|:---:|------|:---:|:---:|------|
| 0 | 环境与骨架 | 1-2 | M0 跨平台空窗口 | AtomUI/Ursa/Dock/MVVM |
| 1 | 连接管理 + 主框架 | 3-5 | M1 连接可用、布局停靠 | Dock |
| 2 | 对象浏览 + 查询 | 5-8 | M2 浏览/查询/脚本 | TreeDataGrid/AvaloniaEdit |
| 3 | 数据编辑 + 表设计 | 5-8 | M3 数据编辑/表设计 | DataGrid/ProDataGrid |
| 4 | 转换/对比/诊断 | 5-7 | M4 转换对比诊断 | DiffPlex（复用） |
| 5 | 统计/备份/图表/生成 | 5-7 | M5 长尾功能 | OxyPlot（关系图待办已移除） |
| 6 | 导入导出 + 收尾 | 3-5 | M6 全功能对齐 | FileUtility（复用） |
| 7 | 跨平台 + 发布 | 2-3 | M7 三平台发布包 | - |

**合计约 30-45 人日**（单人 6-9 周）。核心引擎复用节省约 40% 成本。

---

## 五、替代方案清单（"布局不强制对齐"策略下可替换项）

> 用户明确允许"有更好方案可直接替换、不强制逐像素对齐"。以下为**主动简化的替换清单**：

| 原 WinForms 控件 | Avalonia 替换 | 说明（为何可替换） |
|------|------|------|
| `DataGridView`（450+ 处） | `DataGrid` / `ProDataGrid` | 用数据绑定 + 虚拟滚动替代手工列操作，布局更现代 |
| `ObjectListView`（2 处） | `DataGrid` | 直接合并到 DataGrid 能力 |
| `DockPanelSuite`（5 处） | `Dock` | 全新停靠体验，VS 风格 |
| `Microsoft.Msagl`（关系图） | ~~已移除~~ | 不作为 Avalonia 迁移主线待办 |
| `PropertyGrid`（27 处） | `Avalonia.PropertyGrid` / 自研 ItemsControl | 属性编辑可改为表单/分组展示 |
| `SqlCodeEditor` | `AvaloniaEdit` | 高亮对齐，自动完成后续迭代 |
| 各 `Form` 对话框 | 统一 `CommonDialog` 集 | 合并重复对话框，减少界面数量 |
| 主题 `AntdUI` | `AtomUI` | 同为 Ant Design 风格，外观几乎无缝 |
| `TabControl` 多页表设计器 | 侧边导航 + 内容 Tab | 更现代的编辑布局 |

---

## 六、风险清单与应对

| 风险 | 等级 | 应对 |
|------|:---:|------|
| DataGridView 深度使用（编辑/合并/右键） | **高** | 抽象数据模型，用 DataGrid 重构；大数据量启用虚拟化 |
| 关系图 `frmDatabaseDiagram` 迁移 | ~~已移除~~ | 不作为 Avalonia 迁移主线待办（用户要求移除） |
| AtomUI/Ursa 版本兼容 | 中 | 阶段 0 锁定版本组合，写入 `docs/package-versions.md` |
| 核心库隐式依赖 WinForms（如 `Bitmap`/`Image`，147 个文件涉及） | 中 | 排查 `System.Drawing`，替换为 `SkiaSharp` 或抽象 `IImageSource` |
| 异步线程模型差异（`Invoke`→`Dispatcher`） | 中 | 统一 `Dispatcher.UIThread` |
| SQL 编辑器自动完成/折叠 | 中 | AvaloniaEdit 基础高亮，自动完成二次开发 |
| 控件数量庞大（82 Designer/86 resx） | 中 | 分阶段分批迁移，每阶段可编译可回归 |
| 布局体验差异（用户接受不逐像素对齐） | 低 | 以功能等价 + 现代布局为准，提前与用户确认 |

---

## 七、建议的下一步（MVP 先行）

1. **先做阶段 0 + 阶段 1 的最小可行原型**（MVVM + 连接管理 + Dock 主框架），验证：
   - `AtomUI` 主题与本项目视觉契合度
   - `Dock` 停靠布局可用性
   - `DataGrid`/`TreeDataGrid` 大数据量性能
   - `AvaloniaEdit` SQL 高亮效果
2. MVP 通过后再全面铺开阶段 2-7。
3. 建立 `AppCore` Service 抽象，先让"连接管理 + 对象树 + 查询"跑通全链路。
4. 每阶段结束回归验证，确保核心引擎复用稳定。

---

## 附录 A：核心 WinForms → Avalonia 映射表（简版）

| WinForms | Avalonia |
|----------|----------|
| `Form`/`UserControl` | `Window`/`UserControl`(.axaml) |
| `MessageBox` | `MessageBox.Avalonia` |
| `OpenFileDialog`/`SaveFileDialog`/`FolderBrowser` | `StorageProvider` |
| `Control.Invoke` | `Dispatcher.UIThread.Post` |
| `ShowDialog`+`DialogResult` | `ShowDialog<TResult>` |
| `Clipboard.SetText` | `TopLevel.Clipboard.SetTextAsync` |
| `Timer` | `DispatcherTimer` |
| `BackgroundWorker` | `Task.Run`+`Progress<T>` |
| `Anchor`/`Dock` 布局 | `Grid`+`GridSplitter`+`DockPanel` |
| `resx` 图片/资源 | `.axaml` `<Image>`+资源字典 |
| `DataGridView` | `DataGrid`/`ProDataGrid` |
| `TreeView` | `TreeView`/`TreeDataGrid` |
| `MenuStrip`/`ContextMenuStrip` | `Menu`/`ContextMenu` |
| `ToolStrip`/`StatusStrip` | `ToolBar`/`StatusBar` |
| `SplitContainer` | `Grid`+`GridSplitter` |
| `PropertyGrid` | `Avalonia.PropertyGrid`/自研 |
| `AntdUI` | `AtomUI`（同 Ant Design） |
| `DockPanelSuite` | `Dock` |
| `Msagl` | `OxyPlot`/自绘 |
| `SqlCodeEditor` | `AvaloniaEdit` |

---

*本文档为 `avalonia-migration-plan.md` 的详细深化版，两者可对照使用。若后续选型/范围变化，可在此文档基础上迭代。*
