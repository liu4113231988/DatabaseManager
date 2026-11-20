# 未完成事项与待验收清单

> 本文只记录**尚未完成 / 尚未验收**的工作，是路线图（[Roadmap.md](../../Roadmap.md)）与已交付能力（[README.md](../README.md)）之间的执行跟踪文档。
> 三者各有侧重：README 记录**已经交付**的能力；Roadmap 记录**功能取舍决策**（做 / 不做 / 分期）；本文记录**尚未闭环**的事项。
> 最后更新：2026-11-20。

## 一、待验收（已实现，未经真实环境验证）

> 原则：数据库通用代码不等于全数据库实例验收。SQLite / 本地 PostgreSQL 通过不代表其他方言通过。

### 1.1 数据库实例回归矩阵

| 数据库 | 已实现范围 | 待验收内容 |
| --- | --- | --- |
| SQL Server / MySQL / Oracle | 代码接入，部分计划格式解析 | 真实实例的连接、对象树、查询、数据编辑、备份恢复、迁移、权限逐项验收 |
| KingbaseES V8（PG 兼容） | 连接 / 对象树 / 查询 / 编辑 / DDL / 脚本生成 / 导入导出（阶段 C–D 代码级）；会话/锁、用户权限、查询剖析、备份恢复、全库搜索与定时任务（阶段 E 代码级）；阶段 F 确定性回归与发布状态标注 | 见 §2.1；真实 V8 实例的 `RETURNING`、事务、锁、类型 round-trip、五种格式导入导出、运维 SQL/客户端工具、两套权限账号与 UI 冒烟 |
| DuckDB | `DuckDB = 7`；`DuckDbInterpreter : PostgresInterpreter`；文件 / 内存 / 只读连接面板；对象类型收窄（Table/View/Sequence/Function） | 真实实例的连接、对象树、查询、数据编辑、脚本生成主链路；跨库转换已被 `UnverifiedConversionTypes` 拦截 |
| 达梦 DM8 | 官方包 `DM.DmProvider 8.3.1.47463`；`DM = 8`；`DmInterpreter : OracleInterpreter`（Schema 即用户、默认端口 5236） | 同上；诊断入口已禁用，跨库转换已拦截 |

### 1.2 PostgreSQL / KingbaseES 性能改写回归

- 对象树表/视图 Simple 模式已由 `information_schema` 包装视图改为 `pg_class` 直查（`relkind r/p/f/v`，Detail 模式不变）。需在真实实例核对表/视图清单与旧版一致，覆盖分区表、外部表与扩展对象排除开关。

### 1.3 UI 手工回归

- 专注模式：F11 / 视图菜单切换，折叠与恢复列宽，状态持久化（`app-settings.json`）。
- 收藏查询：脚本库/历史「仅收藏」筛选、收藏置顶；查询历史 500 条裁剪跳过收藏项。
- 应用内 URI 直达：`dbm://<连接>/<库>/[schema/]<类型>/<对象名>[?rows=N]` 各段缺失时的提示；`?rows` 为保留字段（未接数据预览）。
- 查询历史 SQLite 存储（`Profiles/query-history.db3`）：新增 / 更新 / 最近 / 清理与裁剪规则。
- 对象树就地过滤、结果网格「复制为格式」（CSV / 制表符 / JSON / Markdown / INSERT 各格式转义与空值处理）。
- SQL 编辑器辅助：右键菜单各菜单项、查找/替换面板样式与 Esc 关闭、`Ctrl+Enter` 执行选区、`Ctrl+Shift+Enter` 新标签执行、`Ctrl+L` 执行计划、注释/大小写/缩进/行操作对多行选区的行为，以及多标签场景下命令是否作用于当前标签。
- 查询连接与事务：标签连接隔离、手动提交模式、同连接异步串行、排队取消和分组断开已通过 SQLite 确定性回归；自动提交控件及智能提示已通过 Windows P0 隔离 UI 冒烟。仍需真实服务器验证跨库事务目标、慢查询期间提交/断开、网络中断与提交失败；PostgreSQL 可选检查本轮因未设置 `DBSMOKE_CONNSTR` 而跳过。Linux/macOS 桌面交互仍待验收。
- 智能提示：真实服务器的元数据权限、网络延迟与对象刷新行为仍待验收；CTE/派生表输出列及关联子查询的外层别名尚不推断。已有合成基线只覆盖多语句脚本与已加载对象树，不代表单条超长 SQL 或服务器端性能，见 [sql-completion.md](./sql-completion.md)。
- 新增 UI 流程（2026-11-13，代码级已完成）：模板管理窗口的类别筛选、变量/方言编辑与导入导出；表设计器「分区」Tab、列选择器排序与主键/索引/外键列内联编辑；对象树新建视图/过程/函数按方言生成模板。Windows P0/P2 隔离冒烟已覆盖模板管理示例预览与数据字典最小 DI 回退；仍需表设计器分区/列选择器的人工交互、真实数据库分区元数据回放，以及 Linux 桌面冒烟与截图，见 [验证与冒烟说明](../../docs/smoke/verification.md)。

### 1.4 外部服务联调

- SSH 隧道：真实 SSH 服务端的认证、断线重连联调（当前仅编译与窗口校验，认证建立 15 秒超时）。
- SMTP 通知：真实邮件投递（当前仅配置持久化与失败留日志）。
- AI SQL 助手：真实模型效果验收（当前仅模拟 HTTP 接口契约测试）。

## 二、待启动 / 待推进

### 2.1 KingbaseES 阶段 E / F

> 详见 [kingbasees-support-plan.md](./kingbasees-support-plan.md) 与 [testing/](./testing/)。

> 2026-11-13：**阶段 E/F 的代码级工作已完成并通过确定性回归**；真实 V8 实例、权限与跨平台 UI 验收仍为未完成部分，不能视为已验证发布。

- **阶段 E（已完成的代码级接入）**：
  - 会话 / 锁：`sys_stat_activity` / `sys_locks` / `sys_blocking_pids()` / `sys_terminate_backend(pid)`，缺失时回退 `pg_*`。
  - 用户 / 权限：`sys_catalog.sys_roles` 优先、`pg_catalog.pg_roles` 回退；`information_schema` 读取表/例程授权与角色成员；CREATE/DROP USER、GRANT 复用 PG 兼容语法，写操作仍走现有二次确认。
  - 查询剖析：只读 `EXPLAIN ANALYZE` 输入点、能力门控与错误提示。
  - 备份恢复：新增 `KingbaseBackup`（`sys_dump`）与 `ksql` / `sys_restore` 恢复路径，密码经 `KBPASSWORD` / `PGPASSWORD` 环境变量传递，不写入命令行。
  - 全库搜索与定时任务：显式接入 KingbaseES 双引号标识符、LIKE 转义与 LIMIT；备份白名单放开后定时任务可调用。
  - 非 PG 兼容模式的运维能力仍由 `KingbaseCompatibilityModes.GetAdminFeatureBlockReason` 明确拦截。
- **阶段 F（已完成的代码级门禁）**：
  - `KingbaseStageEFChecks`：能力矩阵、用户/权限 SQL、备份工具识别、模式门控、全库搜索方言与发布状态标注。
  - `DBSMOKE_KINGBASE_CONNSTR` / `DBSMOKE_KINGBASE_ADMIN_CONNSTR` 可选真实实例探针：连接、Unicode、角色/授权、超时；未配置时明确跳过。
  - `KingbaseSupportStatus`：锁定驱动包、默认端口、代码级支持模式与实验性模式；`IsInstanceVerified = false`，发布说明不得声称已验证。
- **仍待验收（不可在本机闭环）**：
  - 真实 V8 实例的普通账号 / 管理员账号两套权限、断连、取消、超时、Unicode、阻塞链、终止会话、计划统计、`RETURNING`、事务、锁与五种格式导入导出。
  - 金仓客户端工具 `sys_dump` / `sys_restore` / `ksql` 的实际参数、环境变量、编码、取消与覆盖确认。
  - Windows / Linux UI 冒烟，记录版本、实例参数、权限、日志、截图与回滚步骤；验收通过后才能把发布标注改为「已验证」。

### 2.2 模板外部化 T2 / T3

> 2026-11-13：**T2/T3 与按方言的 DDL 新建模板已完成代码级交付并通过确定性回归**；剩余是 UI 手工验收与 PDF/Word 模板改造评估。

- **T2 已完成**：`TemplateContext` 提供连接 / 库 / schema / 表 / 列 / 时间 / 方言内置变量；模板管理窗口支持多类别筛选、名称/变量/方言编辑、示例预览、恢复默认；`TemplateTransferService` 按 `{ Version = 1, Payload(Base64 JSON) }` 信封导入导出，含版本、大小、契约、变量与方言校验。
- **T3 已完成**：内置模板清单覆盖 DDL（按方言）、数据字典对象/列、HTML 导出、C#/Java 骨架与循环片段；`DefaultDdlService.GetCreateTemplate` 从模板仓库读取并按 `DatabaseType` 选择 SQL Server / MySQL / Oracle / DM / PostgreSQL / KingbaseES / DuckDB / SQLite 视图模板，SQLite/DuckDB 的例程模板明确拒绝；数据字典与代码生成读取用户覆盖模板。
- **仍待验收**：Windows/Linux 手工验证模板导入导出文件选择器、用户覆盖生效、脚本库重新打开与代码生成结果；PDF / Word 模板改造面大，另行评估。

### 2.3 表设计器遗留

> 2026-11-13：代码级已完成；分区功能定位为**只读元数据 + DDL 回放**，未实现分区 ALTER 编辑。

- 分区 Tab：按 MySQL / Oracle / PostgreSQL / KingbaseES / SQL Server 读取分区摘要、明细与可回放 DDL（含继承分区、分区方案与函数）。真实数据库的分区元数据与生成脚本仍待实例回放验证。
- 列选择器：主键、索引、外键支持候选列选择、添加/移除/上移/下移；`ColumnsDisplay` 为可双向绑定的内联编辑文本，编辑自动标记未保存状态。
- 分区编辑/保存（新增、合并、拆分分区）因共享引擎缺少统一的 ALTER PARTITION 生成接口，仍未纳入本期范围。

### 2.4 跨平台发布（M7）

- Windows / Linux / macOS 三平台发布包与实机 GUI 启动验证。
- DuckDB 等含 native 依赖的包需声明平台 RID 与版本兼容矩阵。

### 2.5 Windows x64 Native AOT 预览

> 范围与已闭环项见 [native-aot.md](./native-aot.md)。当前为「可发布的受限预览」，不是无告警、全数据库支持。

- 已用 SQLite/SQLCipher 与模拟 SQL Server catalog 完成 P0/P2 原生冒烟；`Newtonsoft.Json`、Avalonia 反射绑定、`dynamic`、DataSet/ODBC、各数据库驱动仍有约 272 条 IL 告警，按实际调用路径逐项处理，不机械抑制。
- CI 的 `windows-aot` job 目前只检查 EXE、原生依赖与打包，尚未在 AOT 二进制上运行控制台回归和 P0/P2 冒烟。
- 尚未建立 Oracle / Kdbndp / DM / DuckDB / SqlClient / ODBC 的 AOT 支持矩阵；Oracle 若干认证/钱包路径因可选程序集缺失在 AOT 分析中判定必然抛异常。
- 其他表结构变更、文件格式边界、其他数据库及认证方式待真实实例验收；Linux AOT 不在范围内，正式 CoreCLR 包继续保留。

## 三、待评估 / 待决定

- **更多数据库类型**：MariaDB、ClickHouse 按需求评估；MongoDB / Redis / Snowflake 作为独立产品线评估。
- **KingbaseES 其他兼容模式**（Oracle / MySQL / SQL Server）：按独立里程碑与验证矩阵评估，不与 PG 兼容模式合并交付。
- **ER 图 / 数据库关系图**：Roadmap P0 暂不实施，后续再决定范围。
- **结果网格布局记忆**：Roadmap P2 暂缓（跨结果的列宽 / 排序 / 显示列 / 筛选记忆）。
- **扩展机制后续**（详见 [extensibility.md](./extensibility.md) §4）：
  - 插件宿主（扫描 `Plugins\` 目录加载程序集）。
  - 导出 Writer 注册表（格式数超过约 8 种时重构）。
  - 方言分支收敛：`DefaultDbSchemaService.TryGetSchemasAsync` 字符串分支下沉为 `DbInterpreter` 虚属性。

## 四、已识别的基础改进

以下不是新增产品模块，但会影响后续功能质量，建议在启动相关大型功能前纳入对应验收：

- 多查询标签仍按连接共享事务；模式持久到断开、目标数据库传递、异步串行与断开回滚已实现并通过本地回归。剩余工作是实库并发/故障压力验证，以及按需求评估每标签独立会话，详见 [query-database-selection.md](./query-database-selection.md)。
- 构建告警：MSB3270 已通过统一根解决方案 x64 平台映射消除；`Release` 全量重新生成（Any CPU 与 x64）当前均为 0 告警。可空性、未使用成员与 Avalonia 资源加载告警若在其他配置/IDE 重现，仍逐项处理，不机械抑制。
- 数据库管理：建库 / 删库（PostgreSQL、MySQL、SQL Server）、只读数据库属性、备份恢复目标隔离已接入并有确定性回归及隔离 UI 冒烟；真实服务器的权限不足、活动会话、执行失败及事务状态仍待验收，不能视为全数据库验证通过。
- 对象树预取：共享并去重在途请求，刷新 / 断开丢弃旧请求集合；慢服务器交互仍需实机检查。
- 高风险操作统一补充预览、确认、取消、日志与可恢复策略。
- 每项功能按数据库方言标明「已实现 / 已测试 / 受限 / 未支持」，避免单方言能力泛化。

## 五、明确不做（决策留档）

以下已在 [Roadmap.md](../../Roadmap.md) 标记为 `[-]`，此处仅登记，不再重复评估：

- 完整数据库建模（概念 / 逻辑 / 物理、正逆向工程、模型同步与差异）。
- 存储过程调试器。
- 团队协作与同步。
- 企业认证（LDAP / Kerberos / MFA / SSO）与 HTTP 隧道。
- 完整可拖拽停靠布局（保留结果区浮动 / 停靠）。
- `dbm://` 的操作系统级协议注册与单实例冷启动唤起。
