# 产品路线图

本文汇总当前 Avalonia 主线与 Navicat Premium 的功能对比结果，作为后续功能取舍清单。

状态说明：`[ ]` 待决定，`[x]` 已实施，`[-]` 暂不实施，`[~]` 已评估并给出分期方案。2026-09-12 按本轮确认将原 P1 提升为 P0；ER 图与多数据库完整实例验收暂不实施。2026-09-17 明确不做数据库建模、存储过程调试、团队协作、企业认证、达梦支持、完整可拖拽停靠布局六类；新增 DuckDB；查询结果增强（收藏查询、专注模式、应用内 URI）与模板外部化已完成评估。2026-09-18 确认达梦官方包 `DM.DmProvider` 已上架 NuGet（发布者 dameng），达梦（DM8）按 Oracle 兼容方言接入（未经真实实例验收）。2026-09-19 将各批次已完成交付记录合并入 [README 已完成能力](DatabaseManager.Avalonia/README.md)，**未完成与待验收事项**集中登记到 [docs/backlog.md](DatabaseManager.Avalonia/docs/backlog.md)，各历史交付/评估记录文档移除。

> 文档分工：本文只记录**功能取舍与优先级决策**；已经交付的能力见 [README.md](DatabaseManager.Avalonia/README.md)「已完成能力」；尚未闭环的事项见 [docs/backlog.md](DatabaseManager.Avalonia/docs/backlog.md)。

优先级依据：日常使用频率、数据安全与运维价值、现有架构基础，以及开发成本。

## P0：高频使用与可靠性（本轮交付）

| 决策 | 功能 | 当前情况 | 建议交付范围 |
| --- | --- | --- | --- |
| [x] | SSH 隧道连接 | 已接入公共连接入口和连接配置窗口。 | 密码/私钥认证、固定 SHA256 主机指纹、仅回环地址端口转发；下次操作重连，不重放 SQL；随断开/进程退出释放。真实 SSH 服务端验收尚未执行。 |
| [x] | 查询结果固定与多结果集 | 已实现结果标签、固定快照与左右对照窗口。 | 单次最多 64 个结果集、100000 行、约 32 MiB 文本预算；每个查询标签最多固定 3 份快照；截断提示及后续语句处理。 |
| [x] | 数据编辑增强 | 已实现外键候选、单记录表单、批量查找替换、文本/十六进制/文件二进制编辑。 | 保留网格保存和回滚流程；外键每组最多 200 个候选；文件最多 1 MiB，Oracle 二进制字面量最多 2000 字节。 |
| [-] | ER 图 / 数据库关系图 | 按本轮要求暂不实施。 | 后续再决定范围。 |
| [x] | 可视化执行计划 | 已实现树形展示、节点详情、成本/耗时和热点标记，保留原始计划。 | PostgreSQL JSON 计划已实测；SQL Server/SQLite 父子节点和 Oracle/MySQL 文本计划解析；按对象名定位 SQL，无法提供精确源码范围时明确提示。 |
| [x] | 自动化作业完善 | 已增加导入、迁移、表结构/数据同步、顺序批处理、停止/继续失败策略及持久化日志、TLS SMTP 通知。 | PostgreSQL 导入导出、迁移、结构/数据同步已实测；客户端仍需运行；独立调度服务未纳入本轮。真实邮件投递尚未执行。 |
| [-] | 多数据库真实实例验收 | 按本轮要求跳过完整跨数据库矩阵；仅使用 SQLite 和本地 PostgreSQL 完成本轮功能回归。 | SQL Server、MySQL、Oracle、KingbaseES 的真实实例矩阵留待具备环境后验收。 |

P0 各项的实现要点与限制见 [README 已完成能力](DatabaseManager.Avalonia/README.md)；对应功能与数据库实例的验收边界见 [docs/backlog.md](DatabaseManager.Avalonia/docs/backlog.md)。

## P2：效率与数据治理（2026-09-13 交付）

| 决策 | 功能 | 当前情况 | 建议交付范围 |
| --- | --- | --- | --- |
| [x] | 可视化查询和视图构建器 | 工作台支持加入/拖入表、字段、INNER/LEFT 关联、条件分组、聚合、排序与设计保存。 | SELECT/CREATE VIEW 先预览，可送入新查询标签；PostgreSQL 聚合视图已执行验证。 |
| [x] | 测试数据生成 | 类型推断、数值范围、枚举、有限正则、唯一性、空值比例、固定种子和外键组合已实现。 | 每批 1–10000 行；先预览后事务写入；父表先生成，引用现有候选；范围不足或数据库约束冲突明确失败。 |
| [x] | 数据质量剖析 | NULL 率、重复余数、前十值分布、最小/最大、IQR 异常与格式识别已实现。 | 可筛选问题明细并导出报告；最多 100000 行且明确标记采样，保留 NULL/空字符串区别。 |
| [x] | 数据字典与文档 | 对象目录、列说明、外键关系、文本模板、PDF/HTML 与定时任务已实现。 | 原 Word 文档入口保留；中文 PDF 已渲染复核，定时失败走任务中心日志。 |
| [x] | AI SQL 助手 | 兼容接口、本地模型地址、生成/解释/纠错/优化模式已实现。 | 用户明确选择元数据并确认发送；密钥来自环境变量；仅预览、不自动执行。HTTP 契约已测试，真实模型效果未验收。 |
| [x] | 连接配置导入导出 | 多选、导入预览、重名改名/跳过、无密码导出与加密携密导出已实现。 | PBKDF2 + AES-GCM；导入失败回滚本批新建连接，不覆盖原连接。 |
| [x] | 扩展导入来源 | ODBC/Access 快照和 dBASE III DBF 读取已实现。 | 稳定 CSV 快照进入原列映射、错误行和 skipRows 续导流程；本机 64 位 Access 与 dBASE ODBC 已实测。 |
| [x] | 仪表盘深化 | 分页、卡片尺寸/顺序、计算字段、同名列筛选联动、全屏展示与 HTML 图表快照已实现。 | 四则运算计算字段、维度值选择联动；图表使用独立连接及保存的数据库，限定单条 SELECT。 |
| [-] | 结果网格布局记忆 | 按本轮要求暂缓。 | 不实施跨结果的列宽、排序、显示/隐藏列和筛选记忆。 |
| [x] | 数据脱敏 | 查询结果副本/表样本的手机号、证件、银行卡、全部遮盖和正则规则已实现。 | 可保存规则，预览并导出脱敏 CSV；不更新源数据，采样/截断范围明确展示。 |

本轮同时移除主窗口工具菜单的图片工具入口。P2 各项的实现要点见 [README 已完成能力](DatabaseManager.Avalonia/README.md)；使用边界与待验收见 [docs/backlog.md](DatabaseManager.Avalonia/docs/backlog.md)。

## P3：专业与团队能力

| 决策 | 功能 | 当前情况 | 建议交付范围 |
| --- | --- | --- | --- |
| [-] | 完整数据库建模 | 仅计划 ER 图，且 ER 图已在 P0 列为暂不实施。 | 本轮明确不做：概念、逻辑、物理模型，正逆向工程，模型同步与差异对比一并搁置。 |
| [-] | 存储过程调试器 | 未实现。 | 本轮明确不做：依赖数据库侧调试协议与实例权限，无环境可验证。 |
| [-] | 团队协作与同步 | 未实现。 | 本轮明确不做：需要服务端、账号体系与审计存储，超出单机桌面产品范围。 |
| [~] | 更多数据库类型 | 目前支持 SQL Server、MySQL、Oracle、PostgreSQL、SQLite、KingbaseES；DuckDB 已实现（解释器/脚本生成/连接构建/方言配置/文件·内存·只读连接面板落地，**尚未用真实实例回归验收**，跨库转换已拦截）。 | 本轮新增 DuckDB（`DuckDB.NET.Data.Full`，文件/内存连接模型，对象类型收窄、无存储过程与触发器）；`DatabaseType` 追加 `DuckDB = 7`。MariaDB、ClickHouse 按需求评估；MongoDB、Redis、Snowflake 仍作为独立产品线评估。 |
| [~] | 达梦（DM8）支持 | 已实现（官方包 `DM.DmProvider 8.3.1.47463`，`DmInterpreter` 继承 Oracle 兼容方言，默认端口 5236；**尚未用真实实例回归验收**，跨库转换已拦截）。 | 原阻塞"驱动不在 NuGet"已解除（官方发布者 dameng）。待真实 DM8 实例完成验收后，再逐步放开转换、诊断等能力。 |
| [-] | 企业认证与 HTTP 隧道 | 仅有部分集成认证。 | 本轮明确不做：LDAP、Kerberos、MFA/SSO 与 HTTP 隧道均依赖客户目录服务与部署形态，保留现有集成认证。 |
| [~] | 查询结果与工作区增强 | 已有脚本库、历史和结果浮动；**收藏查询、专注模式（F11/视图菜单，状态持久化）、应用内 URI 直达（搜索框解析 `dbm://`）已实现**。 | 收藏查询（扩展脚本库与历史模型，收藏项不参与历史裁剪）、专注模式（折叠对象浏览器/菜单/工具栏，状态持久化）、应用内 URI 直达（对象浏览器搜索框解析 `dbm://<连接>/<库>/<schema>/<类型>/<名>`，解析失败明确提示）。OS 协议注册、单实例与冷启动参数解析不做。 |
| [-] | 完整可拖拽停靠布局 | 已有结果区浮动/停靠；`Dock.Avalonia` 已引用但未使用，主窗口为手写 Grid + DockPanel。 | 暂不实施：主窗口布局与 code-behind 需整体迁移到 Dock 模型，回归面覆盖 P0/P2 全部功能；保留现有结果浮动即可满足当前诉求。 |
| [~] | 代码与文档模板外部化 | 模板主要内置，且存在 4 套互不兼容的占位符语法；**T1 已落地**：`TemplateManifest` 契约（`{name}` 统一语法 + `$TOKEN$` 兼容别名）、`ITemplateEngine`/`ITemplateStore`、`Profiles/Templates/` 目录（内置同名可覆盖），SqlSnippets 已桥接为首批内置模板。 | 先统一模板变量契约与目录（`Profiles/Templates/`），再配渲染、预览与带版本信封的导入导出（T2），最后按 SQL 片段、数据字典、DDL、文档导出、代码生成的顺序分批迁移内置模板（T3）。 |

DuckDB / 达梦的待验收边界、查询结果增强与模板外部化的后续分期方案见 [docs/backlog.md](DatabaseManager.Avalonia/docs/backlog.md)。

## 已识别的基础改进

以下不是新增产品模块，但会影响后续功能质量，建议在启动相关大型功能前纳入对应项目验收（细化跟踪见 [docs/backlog.md](DatabaseManager.Avalonia/docs/backlog.md) 第四节）：

- 多查询标签共用同一连接事务的隔离与生命周期设计。
- 构建现有的可空性、未使用成员和 Avalonia 资源加载告警逐项处理。
- 对高风险操作统一补充预览、确认、取消、日志和可恢复策略。
- 保持每项功能按数据库方言标明“已实现、已测试、受限、未支持”，避免将单方言能力泛化为全量支持。

### 已落地的性能改进（2026-09-18）

- 对象树：类型文件夹子对象一次全量加载（移除 500 条分页与「加载更多」）；展开后并行预取同层其余类型文件夹（并发 4，带按连接的缓存失效）；连接时多库 Schema 枚举并发 4 → 8。
- 解释器层：`GetDbVersion()` 按解释器实例缓存（PG 序列、MySQL 计算列等路径省去重复连接往返）；PostgreSQL/KingbaseES 的表/视图 Simple 模式（对象树路径）改为 `pg_class` 直查，替代 `information_schema` 包装视图（Detail 模式保持不变）。PG 改写需真实实例回归验收。

## 对比依据

Navicat Premium 官方功能矩阵列出了 SSH/HTTP 隧道、数据编辑辅助、查询构建与固定结果、可视化计划、数据生成、建模、自动化、协作等能力：

- [Navicat Premium 功能矩阵](https://www.navicat.com/en/products/navicat-premium-feature-matrix.html)
- [Navicat Premium 产品说明](https://www.navicat.com/en/products/navicat-premium.html)
- [Navicat 数据字典说明](https://www.navicat.com/en/company/aboutus/blog/2426-create-a-data-dictionary-in-navicat-17)

本项目的事务与任务中心能力见 [README 已完成能力](DatabaseManager.Avalonia/README.md)；仍需真实实例与并发压力验收的部分见 [docs/backlog.md](DatabaseManager.Avalonia/docs/backlog.md)。
