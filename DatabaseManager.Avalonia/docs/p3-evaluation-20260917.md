# P3 评估与实施方案（2026-09-17）

> **实施状态（2026-09-18 更新）**：DuckDB、收藏查询、专注模式、应用内 URI 直达、模板外部化 T1 均已实现；达梦（DM8）因官方包上架 NuGet 已一并接入；DuckDB/达梦均未经真实实例回归验收（跨库转换已拦截）。详见文末「五、2026-09-18 实施记录」。

本轮结论：**完整数据库建模、存储过程调试器、团队协作与同步、企业认证与 HTTP 隧道、完整可拖拽停靠布局明确不做**（已在 `Roadmap.md` P3 标记为 `[-]`；达梦原列为本轮不做，后因官方驱动上架 NuGet 而接入，见「四」节补记）。
本文只评估仍需推进的两条线：

1. 新增数据库类型：**DuckDB**（达梦暂不实施，相关兼容方案已整体移除）；
2. 两项待定功能的实施方案：**查询结果与工作区增强**（收藏查询、专注模式、应用内 URI 直达）、**代码与文档模板外部化**。

> 约束：本仓库坚持"按数据库方言标明已实现 / 已测试 / 受限 / 未支持"，不得把单方言能力泛化为全量支持；新增方言不得静默套用基类方言规则（参见 `docs/kingbasees-support-plan.md`）。

---

## 一、新增数据库类型：DuckDB（达梦暂不实施）

### 1.1 现状

- 现有方言：`DatabaseType`（`DatabaseInterpreter/DatabaseInterpreter.Model/Enum/DatabaseType.cs`）：SqlServer=1、MySql=2、Oracle=3、Postgres=4、Sqlite=5、KingbaseES=6。
- DuckDB（及已决定暂不实施的达梦）：**零代码、零配置、零图标**，仅出现在 `Roadmap.md` 与 `README.md` 的"未来计划"里。
- 新增方言的清单与"UI 层通常零改动"的结论已在 `docs/extensibility.md` §3.1 固化；KingbaseES 是最小落地样例（3 个新文件 + 4 处注册），`docs/kingbasees-support-plan.md` 第 59-120 行是可直接照搬的分阶段模板。

### 1.2 通用落地清单（两种数据库共用）

1. `DatabaseType` 追加枚举值（保持既有数值不变）：`DuckDB = 7`。
2. `DatabaseInterpreter.Core`：
   - `Interpreter/XxxInterpreter.cs`（继承最接近的既有 Interpreter）
   - `ScriptGenerator/XxxScriptGenerator.cs`
   - `Provider/XxxProvider.cs`（`ProviderName`）
   - `Builder/Connection/XxxConnectionBuilder.cs`（默认端口 / 连接串）
3. 注册点（漏一个就会运行期异常）：
   - `Helper/DbInterpreterHelper.GetDbInterpreter`、`GetDisplayDatabaseTypes`
   - `Helper/DbScriptGeneratorHelper`
   - `Connection/DbConnector.CreateConnection`（按 ProviderName 子串匹配创建 `DbConnection`）
   - `DatabaseInterpreter.Core.csproj`：驱动 PackageReference + `Config\**` 的 Content 输出
4. **必须提供**的方言配置（文件名 = `DatabaseType.ToString()`，从程序目录 `Config\` 加载）：
   - `Config/DataTypeSpecification/Xxx.xml`（缺失 → 类型映射为空）
   - `Config/FunctionSpecification/Xxx.xml`（**缺失会 `XDocument.Load` 抛异常**，硬要求）
   - `Config/Keyword/Xxx.txt`、`Config/Option/CreateTableOption/Xxx.xml`（缺失仅为空，可选）
5. **三个硬异常点必须显式处理**，否则新增类型在相关入口会直接抛出：
   - `DatabaseManager/DatabaseManager.Core/Diagnosis/DbDiagnosis.GetInstance`（未覆盖 → `NotImplementedException`）
   - `DatabaseConverter/DatabaseConverter.Core/Helper/TranslateHelper.GetSqlAnalyser`（未覆盖 → `NotSupportedException`）
   - `DatabaseConverter/SqlAnalyser.Core/ScriptBuildFactory/ScriptBuildFactory.GetStatementBuilder`（未覆盖 → `NotSupportedException`）
   - 首期建议：照 KingbaseES 的做法加入"未验证转换类型"，给出明确拦截原因，而不是静默套用基类规则。位置：`DatabaseManager.AppCore/Services/DefaultConvertService.cs:21`（`UnverifiedConversionTypes`）。
6. 功能门控逐项确认是否纳入（现状是分散式能力判断，无统一 capability 类）：
   - `DefaultExecutionPlanService`（仅 MySql/Postgres/KingbaseES 走 analyze）
   - `IDbUserService.IsSupported`（用户/权限白名单）
   - `DefaultDbSchemaService`（`GetSupportedTypes` 与 `TryGetSchemasAsync:542` 的多 Schema 字符串分支）
   - `DbAdminGuidance`、`DbSessionSql`、`QueryProfilerSql`、`DefaultBackupService`、`DefaultFullDataSearchService`、`P2DataTools`、`Optimizer`
   - `DatabaseInterpreter.Model/Account/DatabaseAuthentication.cs`（集成认证白名单）
   - `MainWindow.axaml.cs:1340` 帮助文案同步（当前文案还漏了 KingbaseES，一并修）

### 1.3 DuckDB 方案（本轮纳入，✅ 2026-09-18 已按本方案实施）

| 维度 | 结论 |
| --- | --- |
| 驱动 | `DuckDB.NET.Data.Full`（NuGet，含 native，当前 1.5.5）优先，部署简单；`DuckDB.NET.Data` + 自带 native 作为体积敏感时的备选。注意 native 包体积与平台 RID（win-x64 / linux-x64 / osx-arm64），发布矩阵需声明。 |
| 连接模型 | 与现有"服务器 + 账号"模型**不匹配**：只有文件 / 内存（`DataSource=xxx.db` 或 `:memory:`，支持 `ACCESS_MODE=read_only`）。需在 `ConnectWindow` 增加"文件路径 / 内存 / 只读"面板（复用 `PanelKingbaseMode` 的加面板方式），`ConnectionInfo` 中 Server/Port/User/Password 对 DuckDB 置空并不校验。 |
| 基线选择 | DuckDB SQL 更接近 PostgreSQL → `DuckDbInterpreter : PostgresInterpreter`，override 对象类型集合、分页、类型映射、版本查询。 |
| 元数据 | 走 `information_schema` / `duckdb_*`；支持 `CREATE SCHEMA`。 |
| 对象类型收窄 | 只有 Table / View / Sequence / 函数（macro），**无存储过程、触发器、包** → `SupportDbObjectType` 必须收窄，并处理对象树空文件夹的显示。 |
| 能力开关 | `SupportBulkCopy = false`；无用户/权限管理（`IDbUserService` 不纳入）；执行计划首期不纳入（`EXPLAIN` 文本/JSON 可后续评估，但必须显式提示"暂不支持该数据库类型的执行计划分析"）。 |
| 版本风险 | DuckDB 迭代极快 → 在 `docs/package-versions.md` 固定 native 版本并声明兼容矩阵；升级 native 时回归元数据查询。 |

工作量估计：**2 天**（驱动现成、方言简单）+ 验收 0.5 天。

### 1.4 排期建议

1. 本轮只做 **DuckDB**：驱动现成、方言简单，无需外部实例即可完整自测；（2026-09-18 更新：因达梦官方包 `DM.DmProvider` 上架 NuGet，达梦也已按 Oracle 兼容方言接入，见"四"节补记）
2. 直接复用 KingbaseES 的清单与验收模板（`docs/kingbasees-support-plan.md` + `docs/testing/kingbasees-*.tdd.md`，复制改名即可）；
3. `DatabaseType` 追加时直接取 `DuckDB = 7`（已移除达梦，不再预留 `Dameng`）；
4. MariaDB / ClickHouse 仍按"有需求再评估"，MongoDB / Redis / Snowflake 维持"独立产品线评估"。

---

## 二、查询结果与工作区增强（已评估）

Roadmap 原文目标为**收藏查询、URI 直达对象、专注模式、完整可拖拽停靠布局**；本轮确认：**完整可拖拽停靠布局暂不实施**，URI 直达**只做应用内**，保留收藏查询与专注模式。

现状基础：

| 能力 | 现状 |
| --- | --- |
| 查询标签 / 结果展示 | `MainWindow.axaml` 主内容区为手写 `Grid` + `DockPanel` + `GridSplitter` + `TabControl`；`QueryTabViewModel`（约 60 KB）承载结果集、`ResultSnapshots`、`IsResultFloating`。 |
| 结果浮动 | 已实现：`FloatingResultWindow` 复用同一 `QueryTabViewModel`，关闭时回调恢复停靠区。 |
| 持久化 | `Profiles/*.json`（`query-history.json` 最近 500 条、`script-library.json`、`app-settings.json`），Newtonsoft + `lock` + 静默容错。 |
| 停靠框架 | `Dock.Avalonia 12.1.0.4` **已在 csproj 引用但实际未使用**（`MainWindow` 用的是 Avalonia 内置 `DockPanel`）。 |
| 深层链接 | **无** activation / URL scheme / 命令行解析机制。 |
| 对象树标识 | `DbObjectTreeNode` 已有 `Name` / `Schema` / `DatabaseName` / `DbObject`，可作为定位键。 |

### 2.1 收藏查询 —— ✅ 2026-09-18 已实施

不新建存储文件，直接扩展既有模型（Newtonsoft 缺省即默认值，向后兼容）：

- `ScriptLibraryItem`（`IScriptLibraryService.cs`）增加 `IsFavorite`、`LastUsedAt`、`ConnectionName`；`ScriptLibraryWindow` 增加"收藏"筛选与置顶。
- `QueryHistoryEntry` 增加 `IsFavorite`：历史有 500 条上限，收藏项需排除在裁剪之外（`DefaultQueryHistoryService.Add` 的裁剪逻辑按 `!IsFavorite` 过滤）。
- 收藏项与脚本库共用条目时，以脚本库为准，避免两套收藏来源。

成本：约 0.5 天。

### 2.2 URI 直达对象 —— ✅ 2026-09-18 已实施（仅应用内）

- 语法：`dbm://<连接名>/<数据库>/<schema>/<对象类型>/<对象名>`（含空格或特殊字符时需转义）。
- 交付范围：**仅应用内解析**。在对象浏览器已有搜索框上支持该语法，回车后按 `DbObjectTreeNode.Name/Schema/DatabaseName` 逐级展开并选中节点；可选 `?rows=1000` 直接打开数据预览；复用现有懒加载（`ObjectsTree_Item_Expanded`）。成本约 1-2 天。
- 明确不做：操作系统级的 `dbm:` scheme 注册、单实例管道转发、冷启动命令行参数解析，以及跨进程唤起。此类能力需要单实例设计与安装/卸载期写注册表，平台差异与权限风险较大，本轮不纳入。
- 解析失败（连接不存在、对象不存在、连接未打开）时给出明确提示，不静默降级为普通文本搜索。

### 2.3 专注模式 —— ✅ 2026-09-18 已实施

- `MainContentGrid` 是三列 `Grid`（对象浏览器 400px / 分割条 / 内容区），菜单与工具栏为 `DockPanel.Dock=Top`。
- 方案：`MainWindowViewModel` 增加 `FocusMode`：对象浏览器列宽置 0 并隐藏分割条、折叠菜单/工具栏与状态栏；快捷键进入/退出；状态写入 `app-settings.json`（`DefaultAppSettingsService` 已有 JSON 持久化）。
- 纯显隐切换，不涉及布局框架改造。成本约 0.5 天。

### 2.4 完整可拖拽停靠布局 —— 暂不实施

结论：本轮不做，也不做前置的布局记忆改造。保留现有的结果区浮动/停靠（`FloatingResultWindow`）。

不做的原因与成本依据：

1. `MainWindow.axaml` 700+ 行布局与 `MainWindow.axaml.cs`（约 1400 行 code-behind，含大量按名字查找控件与事件回调）需要整体迁移到 Dock 的 Document/Dockable 模型；
2. 布局持久化需从零新增（当前只保存窗口位置与对象浏览器宽度），并要考虑旧布局数据兼容；
3. `FloatingResultWindow` 与 Dock 自带浮动窗口语义冲突，需要统一成一套再切换；
4. AtomUI 主题与 Dock 控件样式适配，回归面覆盖 P0/P2 全部功能（`Smoke/SmokeHarness` 需同步扩展）。

> 备注：`Dock.Avalonia 12.1.0.4` 当前已在 csproj 引用但实际未被使用（`MainWindow` 用的是 Avalonia 内置 `DockPanel`）。若后续重启该项，可评估是否移除该未使用的引用；`README.md` P3 第 13 项"完整 Dock 拖拽布局"需同步标记为暂不实施。

---

## 三、代码与文档模板外部化（已评估）

### 3.1 现状盘点

| 模板 | 位置 | 存放方式 | 占位符语法 |
| --- | --- | --- | --- |
| SQL 对象脚本（**唯一已外部化**） | `DatabaseManager.Core/Config/Template/{Function,Procedure,TableTrigger,View}/*.txt` | 程序目录 txt（`ScriptTemplate.cs` 渲染） | `$ACTION$` / `$NAME$` / `$TABLE_NAME$` |
| 数据字典对象/列模板 | `AppCore/Services/DataDictionaryService.cs` | C# 默认属性值 | `{schema}` `{name}` `{type}` `{nullable}` `{default}` `{comment}`，正则白名单校验 |
| SQL 片段 | `AppCore/Services/SqlSnippets.cs` | C# 静态数组（14 条） | 无 |
| DDL 新建对象 | `AppCore/Services/DefaultDdlService.cs:GetCreateTemplate` | C# 插值 + switch | 无 |
| 代码生成 | `DatabaseManager.Core/Generator/CodeGenerator.cs` | StringBuilder 硬编码 | `{0}`（`string.Format`） |
| 文档导出 | `DataDictionaryService`（PDF/HTML 内联）、`FileUtility/Writer/WordWriter.cs` | C# 内联字符串 / DOM 构建 | 无 |

关键结论：**存在 4 套互不兼容的占位符语法**（`$TOKEN$`、`{name}`、`{0}`、纯插值）。`docs/extensibility.md` §4.2 已记录"外部化未实施，需设计模板变量契约"。
**先统一契约，再谈外部化** —— 否则会把分裂的语法固化进用户文件，后续无法演进。

### 3.2 实施方案（三期）

**T1：契约与目录（约 1 天，✅ 2026-09-18 已实施；SqlSnippets 已桥接为首批内置模板）**

- 统一占位符语法：采用现有 `{name}` 白名单正则（`DataDictionaryService.Expand` 已实现未知变量报 `ArgumentException`、长度上限 2000）；`$TOKEN$` 旧语法保留为兼容别名，一期后移除。
- 模板清单文件 `TemplateManifest`：`Id / Name / Kind / Version / Engine / Variables[] / AppliesToDialects[] / Language`。
- 目录约定：
  - 新栈（Avalonia/AppCore）：`Profiles/Templates/<kind>/<id>.tpl`，与 `WorkbenchSupport.SettingsPath`（`Profiles/<name>`，tmp + `File.Move` 原子写）同构；
  - 旧栈（Core）：`Config/Custom/Template/`，沿用 `ConvertConfigManager.CustomConfigRootFolder` 的"自动建目录 + 用户覆盖"先例。
- 优先级：内置模板随程序发布，**用户目录同名覆盖**，不做就地改写用户文件。

**T2：渲染、管理与导入导出（约 1.5-2 天）**

- 新建 `ITemplateEngine`（`Expand(manifest, variables)`）+ 变量上下文对象（连接 / 库 / schema / 表 / 列 / 时间 / 方言）。
- "模板管理"窗口：列表、编辑、用示例数据预览（复用 `DictionaryWindow` 已有的"保存模板/加载模板"交互）。
- 导入/导出：照 `ConnectionTransferService` 的信封模式 —— `{ Version = 1, Payload(Base64 JSON) }`，版本不为 1 直接拒绝并提示；导入失败整体回滚。

**T3：分批迁移内置模板**

| 顺序 | 模板 | 说明 | 成本 |
| --- | --- | --- | --- |
| 1 | `SqlSnippets`（14 条） | 纯文本、无逻辑，最易 | 0.5 天 |
| 2 | 数据字典对象/列模板 | 已是 `{name}` 语法，直接搬 | 0.5 天 |
| 3 | `DefaultDdlService.GetCreateTemplate` | 需先抽出方言分支 | 0.5 天 |
| 4 | 文档导出 HTML | 内联字符串抽文件 | 0.5 天 |
| 5 | `CodeGenerator` C#/Java | 需把 StringBuilder 拆成"骨架 + 循环片段"，类型映射逻辑保留在代码内不进模板 | 2-3 天 |
| 6 | PDF（手工拼 PDF 流）/ Word | 样式与分页是常量，改造面大，建议暂缓或以 `WordGenerationOption` 序列化替代 | 另行评估 |

### 3.3 版本兼容与安全

- `Version` 必填；版本不匹配 → **提示并回退内置模板**，不静默改写用户文件（对齐 `ProfileBaseManager` 的备份思路，但首期只做"回退 + 提示"，不做自动迁移）。
- 渲染前校验变量完整性，缺失变量直接列出并阻止导出（沿用 `DataDictionaryService.Expand` 的严格校验）。
- 模板保持"纯文本占位符、不执行脚本"（P2 已确立的原则）；导入时展示变量清单与作者字段，避免替换逻辑把敏感信息带出。

---

## 四、本期不做（已在 Roadmap 标记 `[-]`）

| 功能 | 不做原因（记录用） |
| --- | --- |
| 完整数据库建模（概念/逻辑/物理、正逆向工程、模型同步与差异） | 与 P0 已标记为暂不实施的 ER 图同源，工程量集中在图元与同步引擎，投入产出比低 |
| 存储过程调试器 | 需数据库侧调试协议（如 PostgreSQL pldbgapi / Oracle DBMS_DEBUG），依赖实例权限与部署环境 |
| 团队协作与同步 | 需服务端、账号体系与审计存储，超出单机桌面产品范围 |
| 企业认证与 HTTP 隧道 | 依赖客户目录服务（LDAP/Kerberos/MFA/SSO）与部署形态，无法在无环境情况下验证 |
| 达梦（DM8）支持 | 原结论"驱动不在 NuGet"已失效：官方账号 **dameng** 已发布 `DM.DmProvider`（8.3.1.47463，支持 net8.0）。**2026-09-18 已按金仓模板接入**：`DmInterpreter` 继承 Oracle 兼容方言（Schema 即用户、默认端口 5236、关闭批量导入），方言配置 `DM.xml/DM.txt` 随包发布；跨库转换列入未验证集合拦截，待真实 DM8 实例完成回归验收后再放开诊断、转换等能力 |
| 完整可拖拽停靠布局 | 本轮明确不做：主窗口布局与 code-behind 需整体迁移到 Dock 模型，回归面覆盖 P0/P2 全部功能；结果区浮动/停靠已能满足当前诉求 |
| URI 直达的 OS 协议注册 | 只保留应用内 `dbm://` 解析；单实例、注册表写入与冷启动参数解析平台差异大，暂不纳入 |

---

## 五、2026-09-18 实施记录

本节汇总 2026-09-18 批次的实施内容与验收边界（整解决方案编译 0 错误）。

### 5.1 功能实施（对应本文 §一/§二/§三）

| 项 | 实现要点 | 验收边界 |
| --- | --- | --- |
| DuckDB | `DuckDB = 7`；`DuckDbInterpreter : PostgresInterpreter`（元数据走 `information_schema` + `duckdb_*`，对象类型收窄 Table/View/Sequence/Function，无批量复制）；四注册点（InterpreterHelper/ScriptGeneratorHelper/DbConnector/csproj）；`DataTypeSpecification/FunctionSpecification/Keyword/CreateTableOption` 四个方言配置；ConnectWindow 文件/内存（`:memory:`）/只读（`ACCESS_MODE=READ_ONLY`）面板 | 未经真实实例回归验收 |
| 达梦 DM8 | 官方包 `DM.DmProvider 8.3.1.47463`（NuGet 发布者 dameng）；`DM = 8`；`DmInterpreter : OracleInterpreter`（Schema 即用户，默认端口 5236，关闭批量导入）；方言配置四个；`DmConnection` 直构造（官方文档连接串 `Server=ip:port; UserId; PWD`） | 未经真实实例回归验收；跨库转换列入 `UnverifiedConversionTypes` 拦截，诊断入口明确禁用 |
| 收藏查询 | `ScriptLibraryItem` 增加 `IsFavorite/LastUsedAt/ConnectionName`；`QueryHistoryEntry.IsFavorite` + `IQueryHistoryService.Update`；历史 500 条裁剪跳过收藏项；脚本库/历史窗口支持「仅收藏」筛选、收藏置顶与收藏切换 | JSON 字段向后兼容（缺省即默认值） |
| 专注模式 | `MainWindowViewModel.FocusMode`；F11 / 视图菜单切换；折叠菜单/工具栏/状态栏/对象浏览器与分割条，退出恢复原列宽；持久化到 `app-settings.json`（`Workspace.FocusMode`） | — |
| URI 直达 | `DbObjectUri` 解析 `dbm://<连接>/<库>/[schema/]<类型>/<对象名>[?rows=N]`（URL 转义、类型白名单、失败明确提示）；对象浏览器搜索框回车触发，复用 `LocateNodeInTreeAsync` 逐级展开定位 | `?rows` 参数为保留字段（未接数据预览） |
| 模板外部化 T1 | `TemplateManifest`（`{name}` 契约 + `$TOKEN$` 兼容别名 + 版本校验 + 必填变量校验）；`ITemplateEngine`/`ITemplateStore`；用户目录 `Profiles/Templates/<kind>/<id>.tpl`（tmp+Move 原子写），内置同名覆盖、内置不可删；SqlSnippets 已桥接为首批内置模板 | T2（管理窗口/导入导出）、T3（其余内置模板迁移）待推进 |

### 5.2 追加性能优化（连接后对象树加载慢，对齐 SSMS 体验）

| 层 | 改动 |
| --- | --- |
| 对象树 VM | 类型文件夹子对象**一次全量加载**（移除 500 条分页与「加载更多」占位及 `DbObjectTreeNode` 分页字段）；展开后**并行预取同层其余类型文件夹**（并发 4，`ConcurrentDictionary` 缓存按连接失效）；命中缓存零等待填充 |
| Schema 服务 | 连接时多库 Schema 枚举并发 4 → 8 |
| 解释器层 | `GetDbVersion()` 按解释器实例缓存（PG 序列、MySQL 计算列等路径省去重复连接往返）；PostgreSQL/KingbaseES 表/视图 Simple 模式（对象树路径）改 `pg_class` 直查（`relkind r/p/f`、`v`），替代 `information_schema` 包装视图；Detail 模式不变 |
| 审查未改 | SQL Server 已全 `sys.*`（`IDENT_SEED` 承载脚本生成语义保留）；MySQL/Oracle/DM/SQLite 目录查询已是平台推荐形式；PG 列查询的 `pg_depend/element_types` join 承载类型解析语义不可裁剪 |

### 5.3 待验收清单

1. DuckDB / 达梦：真实实例回归（连接、对象树、查询、数据编辑、脚本生成主链路）；
2. PostgreSQL / KingbaseES：`pg_class` 改写后核对对象树表/视图清单与旧版一致（含分区表、外部表、扩展对象排除开启时）；
3. 专注模式 / 收藏 / URI 直达：UI 手工回归（状态持久化、历史裁剪、URI 各段缺失提示）。
