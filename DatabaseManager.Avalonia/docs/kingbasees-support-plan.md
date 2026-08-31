# 人大金仓 KingbaseES 支持计划

> 状态：A–C 开发中（2026-08-31）。A 的本地驱动核验、B 的连接骨架及 C 的 PG 兼容对象树入口已完成；真实 KingbaseES V8 实例验证仍是上线前置条件。
> 目标：在 Avalonia 数据库管理工具中把 KingbaseES 作为独立数据库类型接入，覆盖连接、对象浏览、查询、编辑、导入导出、DDL、诊断与管理功能，并允许按实例兼容模式进行能力降级。

## 1. 结论与建议

**结论：可行，推荐独立方言实现。**

KingbaseES 官方提供 ADO.NET 数据提供程序 `Kdbndp`，包含 `KdbndpConnection`、`KdbndpCommand`、`KdbndpDataReader`、参数和事务对象；官方文档还说明提供覆盖 .NET 8 适用范围的 Kdbndp V9 NuGet 包。因此，本项目现有基于 `DbConnection`/`DbCommand` 的抽象可以复用。

不建议仅把 KingbaseES 标记为 `Postgres` 后直接上线。项目中的 PostgreSQL 解释器、Npgsql 驱动、系统目录查询、备份工具名、会话管理 SQL、EXPLAIN 输出和权限视图都含有 PostgreSQL 特定假设；而 KingbaseES 还存在 PG、Oracle、MySQL、SQL Server 等兼容模式。正确做法是新增 `KingbaseES` 方言，并在首期以 **PG 兼容模式** 为支持边界。

### 当前实现记录（A–C）

- 已引入并实际编译 `SqlSugarCore.Kdbndp 9.3.8.413` 中的供应商驱动，新增 `KingbaseES`、`KingbaseProvider`、`KingbaseConnectionBuilder` 和独立解释器；默认端口为 `54321`，仍允许用户按实例配置覆盖。
- 连接窗口已提供 `Auto / Postgres / Oracle / SqlServer` 模式标记，并随连接侧车信息持久化。该值只描述目标服务端，**不会**试图在客户端切换兼容模式。
- `Auto` 与 `Postgres` 当前复用已接入的 PG catalog 路径；Oracle、SQL Server 模式在保存、测试和加载数据库前被拦截，且连接服务也会执行相同拦截，避免其他调用路径错误执行 PG 系统目录 SQL。
- 对象树 Schema 枚举、执行计划与只读查询剖析已接入 PG 兼容路径。表、视图、列、索引、约束、触发器、序列、函数和过程仍须用真实 V8 基准库逐项验证，未验证前不可标记为已完全支持。

官方资料：

- [KingbaseES ADO.NET 概述与版本适用范围](https://bbs.kingbase.com.cn/kingbase-doc/v9.4.12/development/application-develop-guide/application_development/client-interfaces/ado-net/ado-net-1.html)
- [Kdbndp 连接示例](https://help.kingbase.com.cn/v9.4.12/development/application-develop-guide/application_development/client-interfaces/ado-net/ado-net-13.html)
- [KingbaseES SQL 标准与词法约定](https://help.kingbase.com.cn/v9.3.11/admin/general/specification/data-access.html)

## 2. 支持边界

### 首期（MVP）

- KingbaseES V9、PG 兼容模式。
- 使用供应商提供、与目标运行环境匹配的 `Kdbndp` 驱动包。
- Windows 与 Linux 各验证一套实例；连接 TLS 由驱动连接串参数透传。
- 支持：连接测试、库/Schema/表/视图/列/索引/约束/触发器浏览、SQL 执行、结果编辑、导入导出、基础 DDL、查询历史、对象搜索。

### 第二期

- 会话和锁监控、用户与权限、执行计划、备份/恢复、结构/数据比较。
- 类型、函数、序列、分区、注释、权限及大对象等高级元数据。

### 非目标（首期明确不承诺）

- 同时完整支持 PG/Oracle/MySQL/SQL Server 全部兼容模式。
- 利用 PostgreSQL 驱动 `Npgsql` 直连替代金仓官方驱动。
- 未获得测试实例和相应权限时，对系统视图或管理命令做“猜测性兼容”。

## 3. 现有架构影响面

| 层级 | 现状 | KingbaseES 改动 |
|---|---|---|
| 模型 | `DatabaseType` 仅含 SQL Server/MySQL/Oracle/Postgres/SQLite | 新增 `KingbaseES` 枚举值及显示名、默认端口和连接表单行为 |
| 驱动 | `DatabaseInterpreter.Core` 依赖 `Npgsql` 实现 PostgreSQL | 引入审核后的 `Kdbndp` 包，新增 provider、connection builder、interpreter |
| 元数据 | `PostgresInterpreter` 查询 PostgreSQL catalog | 为 KingbaseES 编写或提炼独立 catalog 查询；不得直接复制后不验证 |
| SQL 生成 | Postgres script generator、关键字/类型配置 | 新增 KingbaseES 数据类型、函数、关键字、建表选项与脚本生成配置 |
| Avalonia 服务 | 多处用 `DatabaseType` 分支生成 SQL | 补齐全库搜索、会话、用户权限、剖析、DDL、备份和导出分支 |
| UI | 连接窗口、树结构、帮助说明列出五种数据库 | 加入金仓选项、端口/连接串提示、能力不足时的明确提示 |
| 测试 | 轻量回归项目 + 解决方案构建 | 增加无数据库单元测试、容器/测试实例集成测试及人工验收矩阵 |

## 4. 实施阶段

### 阶段 A：技术预研与决策（2–4 人日）

1. 获取目标客户实际使用的 KingbaseES 版本、兼容模式、部署 OS、字符集、TLS、认证方式和管理员权限模型。
2. 从官方渠道取得与 .NET 8 匹配的 `Kdbndp` NuGet 包或离线包；核验许可证、发布源、签名、依赖及 Linux 运行兼容性。
3. 建立两套隔离实例：普通业务账号与 DBA/监控账号；准备含 Schema、表、视图、函数、触发器、序列、注释、中文标识符和大字段的基准库。
4. 执行最小驱动探针：打开连接、参数化 SELECT、事务提交回滚、异步 reader、多结果集、取消、超时、TLS。
5. 输出“PG 兼容模式首期”ADR；若关键 catalog 或驱动异步行为不满足，暂停后续开发。

**通过标准**：Kdbndp 在目标 .NET 8 与目标 OS 上可稳定完成连接、异步查询、参数、事务、取消和超时验证。

### 阶段 B：核心方言与连接（3–5 人日）

1. 在 `DatabaseInterpreter.Model/Enum/DatabaseType.cs` 新增 `KingbaseES`。
2. 新增 `KingbaseProvider`、`KingbaseConnectionBuilder`、`KingbaseInterpreter`，在 `DbInterpreterHelper` 注册。
3. 在核心项目添加 Kdbndp 包引用；禁止把驱动 DLL 直接散落到 UI 输出目录。
4. 在连接窗口/管理窗口加入 KingbaseES，默认端口以**实际部署配置**为准；连接表单提示 `Server`、`Port`、`Database`、`User Id`、`Password` 与可选 SSL 参数。
5. 在连接配置 JSON 版本兼容、导入导出、显示说明、连接测试和重连链路中补齐新类型。

**自动化测试**：数据库类型解析、连接串构建、驱动工厂选择、未知类型拒绝。

### 阶段 C：元数据与对象树（5–8 人日）

1. 以金仓 PG 兼容模式的官方系统目录为准实现数据库、Schema、表、视图、列、主键、外键、索引、约束、触发器、序列、函数和存储过程读取。
2. 处理系统 Schema/扩展对象过滤、大小写和双引号标识符，保留用户可配置的系统对象显示开关。
3. 为对象树懒加载、搜索、列智能提示、对象定义读取与对象依赖关系接入 KingbaseES。
4. 对每个 catalog 查询设置短超时和权限错误解释，不因一个对象类别不可读而阻断整棵树。

**验收数据**：至少 3 个 Schema、50 张表、跨 Schema 外键、中文/引号标识符、视图/函数/触发器各不少于 2 个。

### 阶段 D：查询、编辑、DDL 与数据工具（进行中）

当前已完成：KingbaseES 复用 PG 方言的标识符、分页、服务端排序、查询剖析与执行计划路径；脚本生成器及同步脚本的默认 Schema 已接入；代码生成的数据类型→C#/Java 类型映射已与 PG 一致接入；`DbConverter` 的 `CreateSchemaIfNotExists` 已放行 KingbaseES（PG 语义）。由于 PostgreSQL 的二进制 COPY 实现绑定 `NpgsqlConnection`，金仓当前明确禁用该快捷路径并退回参数化批量插入，避免在 Kdbndp 连接上发生类型转换或运行时失败。阶段 D 任务 5 的“数据库转换能力标记”已落地：KingbaseES 在未用真实实例验证前被列入未验证转换集合，转换/预览/加载 Schema 映射三个入口均明确拦截并返回提示，不再静默套用 PostgreSQL 翻译规则。

> 代码级核查小结（2026 阶段 D 收尾）：逐项核查了所有 `DatabaseType.Postgres` 分支与金仓入口。MVP 范围内的连接、对象树/搜索、查询、同步脚本、执行计划/剖析、脚本生成、`CodeGenerator` 类型映射与批量导入回退均已纳入 KingbaseES。核查中发现并修复一处遗漏：`Optimizer.cs` 的注释已声明“Postgres / Kingbase：执行 VACUUM”，但分支条件漏加 KingbaseES，现已补上并更新提示消息。会话/锁、用户/权限、备份恢复、诊断/碎片分析（`DbSessionSql`、`IDbUserService`、`DefaultBackupService`、`DbDiagnosis`、`Analysiser`）属于阶段 E，强依赖真实实例目录与权限，按“未验证不得静默套用 PG 规则”原则刻意未补，待 E 阶段按环境验收后接入。

1. 验证参数符号、分页（`LIMIT/OFFSET`）、列序号排序、标识符引用、`RETURNING`、事务与锁行为；实现专属 SQL 方言帮助器。
2. 适配 `SimpleSelectParser`、数据编辑主键定位、插入/更新/删除模板及默认值/序列行为。
3. 新增 KingbaseES 关键字、数据类型、函数和 CREATE TABLE 选项配置；实现脚本生成器并确保输出可回放。
4. 验证 CSV/Excel/JSON/XML/SQL 导入导出、二进制/JSON/时间类型、批量写入和错误行报告。
5. 结构/数据比较、数据库转换按能力标记显示；未验证类型转换必须禁用而非静默使用 PostgreSQL 规则。

**验收标准**：CRUD、分页、服务端排序、事务回滚、DDL 回放和五种导入导出在基准库通过。

### 阶段 E：运维与高级功能（5–10 人日）

> 当前状态：尚未接入。代码中已有 PG 方言路径（会话/锁、用户/权限、备份、诊断等），但金仓在这些功能的 `DatabaseType` 分支上**刻意未纳入 KingbaseES**——因为这些 SQL 与工具路径强依赖版本、兼容模式和权限，未用真实实例验证前不能静默套用 PG 规则。接入前须按下面每项完成实例验收。

1. **会话/锁**：依据金仓系统视图实现会话、阻塞链和终止会话；在权限不足时展示所需角色/权限。
2. **用户/权限**：实现用户、角色、对象授权与成员关系读取；写操作始终经二次确认。
3. **查询剖析**：确认 KingbaseES 支持的 `EXPLAIN`/`EXPLAIN ANALYZE` 格式，接入只读检查后展示计划与实际统计。
4. **备份恢复**：确认客户端工具、可执行路径、参数、编码和远程/本地运行模型；完成备份、恢复、取消、日志和覆盖确认。
5. **全库搜索与定时任务**：使用 KingbaseES 方言的标识符/文本匹配规则；复用现有“应用运行期间”调度约束。

**重点风险**：运维 SQL 与工具路径强依赖版本、兼容模式和权限，必须按具体环境验收，不与核心 CRUD 一起作为上线阻塞。

### 阶段 F：质量门禁与发布（3–5 人日）

1. 增加单元测试：SQL 生成、标识符引用、分页、会话/权限 SQL、能力矩阵和错误提示。
2. 增加真实数据库集成测试：基础用户与管理员用户各一套；覆盖连接中断、权限不足、取消、超时与 Unicode。
3. 在 Windows/Linux 的目标版本上完成 UI 冒烟，记录版本、驱动包版本、实例参数、权限、日志、截图与回滚步骤。
4. 发布时标注“已验证兼容模式/版本”；其他兼容模式显示实验性或不可用。

## 5. 能力矩阵（首期目标）

| 能力 | 首期状态 | 说明 |
|---|---|---|
| 连接、查询、事务、取消、超时 | 必须 | Kdbndp 驱动探针先行 |
| 对象树与元数据 | 必须 | 独立 catalog 验证 |
| 查询结果编辑 | 必须 | 单表简单 SELECT + 主键 |
| DDL / 脚本 / 导入导出 | 必须 | 按实际方言回放验证 |
| 执行计划 | 建议 | 仅只读 SELECT |
| 会话、锁、用户、权限 | 第二期 | 权限/版本差异大 |
| 备份恢复 | 第二期 | 客户端工具依赖强 |
| 其他兼容模式 | 后续 | 单独计划与矩阵 |

## 6. 风险与缓解

- **驱动版本/授权风险**：Kdbndp 包按 .NET 与数据库版本选择；锁定版本、校验来源和许可证，离线部署纳入发布清单。
- **兼容模式风险**：连接时探测 `version()`、兼容模式和关键 catalog；将模式写入连接能力缓存，非 PG 模式不复用 PG 元数据 SQL。
- **系统视图差异**：每条管理 SQL 在普通账号和 DBA 账号都测试；失败返回可操作提示。
- **类型差异**：以 round-trip 测试覆盖 numeric、timestamp with time zone、json/jsonb、数组、bytea、UUID、几何和中文标识符。
- **备份恢复风险**：不假设 `pg_dump`/`pg_restore` 可用；仅使用已验证的金仓客户端工具和参数。
- **供应商升级风险**：将驱动/实例版本写入诊断信息和 CI 测试矩阵，避免升级后无感回归。

## 7. 资源与排期

在已具备可用测试实例、驱动包和 DBA 配合的前提下：

- MVP（阶段 A–D）：约 **15–25 人日**。
- 运维与高级能力（阶段 E）：约 **5–10 人日**。
- 跨平台验收与发布（阶段 F）：约 **3–5 人日**。

总计约 **23–40 人日**；若要覆盖四种兼容模式，应按每种模式额外建立独立验证与适配工作包，不能简单按比例缩减。

## 8. 启动前需要确认

1. 目标 KingbaseES 的完整版本、补丁级别和兼容模式。
2. 目标客户端 OS/.NET 版本及 Kdbndp 的获取与许可方式。
3. 测试实例连接信息、TLS/证书要求、普通账号与 DBA 账号。
4. 必须上线的运维功能范围（会话、用户权限、备份恢复是否进入首期）。
5. 是否需要 Oracle/MySQL/SQL Server 兼容模式；若需要，按独立里程碑估算。
