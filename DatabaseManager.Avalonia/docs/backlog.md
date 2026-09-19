# 未完成事项与待验收清单

> 本文只记录**尚未完成 / 尚未验收**的工作，是路线图（[Roadmap.md](../../Roadmap.md)）与已交付能力（[README.md](../README.md)）之间的执行跟踪文档。
> 三者各有侧重：README 记录**已经交付**的能力；Roadmap 记录**功能取舍决策**（做 / 不做 / 分期）；本文记录**尚未闭环**的事项。
> 最后更新：2026-09-19。

## 一、待验收（已实现，未经真实环境验证）

> 原则：数据库通用代码不等于全数据库实例验收。SQLite / 本地 PostgreSQL 通过不代表其他方言通过。

### 1.1 数据库实例回归矩阵

| 数据库 | 已实现范围 | 待验收内容 |
| --- | --- | --- |
| SQL Server / MySQL / Oracle | 代码接入，部分计划格式解析 | 真实实例的连接、对象树、查询、数据编辑、备份恢复、迁移、权限逐项验收 |
| KingbaseES V8（PG 兼容） | 连接 / 对象树 / 查询 / 编辑 / DDL / 脚本生成 / 导入导出（阶段 C–D 代码级）；会话与锁（阶段 E 代码级） | 见 §2.1；`RETURNING`、事务、锁、类型 round-trip、五种格式导入导出实际回放 |
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

### 1.4 外部服务联调

- SSH 隧道：真实 SSH 服务端的认证、断线重连联调（当前仅编译与窗口校验，认证建立 15 秒超时）。
- SMTP 通知：真实邮件投递（当前仅配置持久化与失败留日志）。
- AI SQL 助手：真实模型效果验收（当前仅模拟 HTTP 接口契约测试）。

## 二、待启动 / 待推进

### 2.1 KingbaseES 阶段 E / F

> 详见 [kingbasees-support-plan.md](./kingbasees-support-plan.md) 与 [testing/](./testing/)。

- **阶段 E（运维与高级功能）**：
  - 会话 / 锁：已代码级接入 `sys_stat_activity` / `sys_locks` / `sys_blocking_pids()` / `sys_terminate_backend(pid)`（缺失时回退 `pg_*`）；待真实 V8 实例验证普通账号 / 监控账号 / 超级用户可见范围、阻塞链展示、终止会话与权限失败提示。
  - 用户 / 权限：待接入（写操作需二次确认）。
  - 查询剖析：只读 `EXPLAIN ANALYZE` 输入点已接入，计划与统计展示待实例验证。
  - 备份恢复：待接入（依赖金仓客户端工具与参数验证）。
  - 全库搜索与定时任务：待接入（标识符 / 文本匹配规则）。
- **阶段 F（质量门禁与发布）**：
  - 补齐会话 / 权限 SQL 与能力矩阵单元测试。
  - 真实数据库集成测试（基础用户 + 管理员用户两套，覆盖断连、权限不足、取消、超时、Unicode）。
  - Windows / Linux 目标版本 UI 冒烟并记录版本、驱动版本、实例参数、权限、截图与回滚步骤。
  - 发布时标注「已验证兼容模式 / 版本」，其他模式显示实验性或不可用。

### 2.2 模板外部化 T2 / T3

- **T2（渲染、管理与导入导出）**：`ITemplateEngine` 变量上下文（连接 / 库 / schema / 表 / 列 / 时间 / 方言）、模板管理窗口（列表 / 编辑 / 示例预览）、按 `ConnectionTransferService` 信封模式导入导出（`{ Version = 1, Payload(Base64 JSON) }`）。
- **T3（分批迁移内置模板）**：数据字典对象/列模板 → `DefaultDdlService.GetCreateTemplate` → 文档导出 HTML → `CodeGenerator`（C#/Java，需拆分骨架与循环片段）。PDF / Word 模板改造面大，另行评估。

### 2.3 表设计器遗留

- 分区管理（`UC_TablePartition_*`）。
- 列选择器内联编辑。

### 2.4 跨平台发布（M7）

- Windows / Linux / macOS 三平台发布包与实机 GUI 启动验证。
- DuckDB 等含 native 依赖的包需声明平台 RID 与版本兼容矩阵。

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

- 多查询标签共用同一连接事务的隔离与生命周期设计（含压力验证）。
- 构建现存的可空性、未使用成员与 Avalonia 资源加载告警逐项处理，不机械抑制。
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
