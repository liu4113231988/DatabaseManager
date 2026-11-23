# DatabaseManager.Avalonia

DatabaseManager 的主线跨平台客户端，基于 **Avalonia UI** 构建，运行于 Windows、Linux 与 macOS。`DatabaseManager.CoreApp` 的 WinForms 实现仅保留为历史兼容代码，已过时；所有新功能与修复均以本项目为准。

## 目标架构

```
DatabaseManager.Avalonia/
├─ DatabaseManager.Avalonia.sln # 客户端及核心依赖；回归可执行项目需单独运行
├─ DatabaseManager.AppCore/      # UI 无关业务层（复用原核心引擎，零 WinForms 依赖）
│   ├─ ViewModels/               # MainWindowViewModel / ConnectionManagerViewModel 等
│   ├─ Services/                 # IDbConnectionService / IDbSchemaService 等 + 默认实现
│   ├─ Models/                   # ConnectionItem 等 UI 无关领域模型
│   └─ Common/                   # ViewModelBase、DI 注册
├─ DatabaseManager.Avalonia/     # Avalonia UI 层（窗口、控件、主题、菜单与三栏工作区）
├─ DatabaseManager.AppCore.RegressionTests/ # 无 UI 回归测试
└─ docs/                         # 架构、兼容性、测试证据与实施记录
```

## 技术选型（实际引用版本）

| 用途 | 选型 |
|------|------|
| 框架 | Avalonia 12.1.5（.NET 10；DataGrid 保持 12.1.2） |
| 主题 | **AtomUI 6.2.7**（Ant Design 风格） |
| 停靠布局 | **Dock.Avalonia 12.1.0.4** |
| MVVM | CommunityToolkit.Mvvm |
| DI | Microsoft.Extensions.DependencyInjection |
| 消息框 | MessageBox.Avalonia |

## 在对象树查看用户与角色

展开连接后，可按数据库的安全体系查看以下目录。每个目录在首次展开时读取，可通过右键或 `F5` 刷新，加载时双击目录可取消。目录支持复制名称；右键可新建用户/角色、查看权限、在管理窗口中定位或删除主体。账户与权限变更统一在「用户与权限管理」窗口中生成方言 SQL，二次确认后才执行。

| 数据库 | 对象树位置 |
| --- | --- |
| SQL Server | 连接下有登录名、服务器角色。各数据库下有数据库用户、数据库角色和应用角色，与 Schema 同级。 |
| PostgreSQL、KingbaseES（PG 兼容） | 连接下按是否可登录分组，包含当前连接可见的内置和自定义角色。KingbaseES 目录随常规客户端提供。 |
| MySQL | 连接下有「账户与角色」，按 `用户@主机` 区分同名条目。 |
| Oracle | 连接下有「用户」和「角色」，范围是当前连接的服务或容器。角色目录需要读取 `DBA_ROLES` 的权限。 |

目录只返回连接账号可见的名称。读取失败时，可检查连接或目录权限后重试，同级的表和视图仍可浏览。SQLite、DuckDB 和达梦当前仅显示已有元数据目录。

## 管理型只读目录

除安全主体外，对象树还提供以下只读目录，首次展开时加载，支持 `F5` 刷新与「查看详细信息」：

| 数据库 | 目录位置 | 内容 |
| --- | --- | --- |
| PostgreSQL、KingbaseES（PG 兼容） | 连接下「扩展」 | 扩展名称、版本与安装 Schema（`pg_extension`） |
| Oracle、DM | 数据库下「表空间」 | 当前用户可见的表空间；Oracle 在 `USER_TABLESPACES` 读取失败时回退 `DBA_TABLESPACES`（需目录权限） |
| SQL Server | 数据库下「分区方案」「分区函数」 | 方案名称、绑定函数、分区数；函数类型与边界数 |
| Oracle、SQL Server | 数据库下「同义词」 | 目标对象与数据库链接 / 基对象名；需 `USER_SYNONYMS` 或 `sys.synonyms` 权限 |
| Oracle | 数据库下「包与包体」 | 包说明与包体名称（`USER_SOURCE`）；仅列出名称与类型，不展示源码 |
| Oracle、PostgreSQL | 数据库下「物化视图」 | 刷新模式、方法与上次刷新时间 / 填充状态与定义摘要 |
| Oracle、SQL Server | Oracle 数据库下、SQL Server 连接下「数据库作业」 | 作业状态、启用标志与调度摘要；SQL Server 读取 `msdb.dbo.sysjobs`，需 `SQLAgentReaderRole` |

这些目录来自只读 catalog 查询，不提供新建/修改入口，也不与客户端任务中心混用：此处展示的是数据库服务器端持久化的对象。KingbaseES 与 DM 的目录在 Native AOT 构建中不提供；Oracle 相关目录为代码级接入，真实实例与权限边界仍待验收。

## 已完成能力

> 以下为已交付能力汇总。**未完成事项与待验收清单**见 [docs/backlog.md](./docs/backlog.md)；功能取舍与优先级决策见根目录 [Roadmap.md](../Roadmap.md)。

本节以当前 `master` 源码为准；发布包行为以对应版本的发布说明为准。

- **连接与工作区**：连接的新建、编辑、重命名、删除、测试、连接/重连/断开/断开全部与 Profile 持久化（支持 SSL、集成认证）；主窗口提供菜单、工具栏、对象浏览器、查询区、结果区和状态栏，布局与未保存 SQL 草稿随会话恢复；连接配置多选导入导出（无密码 / 加密携密，PBKDF2 + AES-GCM）。
- **SQLite 加密访问**：使用 SQLite3MC 原生实现，支持普通文件与 SQLCipher 4 默认参数加密文件；连接窗口可填写并记住数据库密码，测试连接采用只读方式。兼容范围及验证见 [SQLite 加密说明](./docs/sqlite-encryption.md)。
- **SSH 隧道**：跳板机密码 / 私钥认证、固定 SHA256 主机指纹、仅回环地址端口转发；断线在下次操作时重连，不重放 SQL，随断开 / 进程退出释放（真实 SSH 服务端联调待办）。
- **对象浏览**：对象树支持数据库、Schema、表、视图、过程、函数、序列、类型及全局触发器目录，并提供扩展、表空间、分区方案/函数、同义词、包、物化视图与数据库作业只读目录；表/视图下支持列/索引/键/约束/触发器多级懒加载；树内就地过滤（输入即时隐藏不匹配节点，清空恢复）与深度搜索定位、右键菜单（Generate SQL、新建/删除/重命名对象、过滤模板、比较迁移入口，菜单图标按动作统一）、元数据搜索窗口与菜单扩展点；`dbm://` 应用内 URI 直达对象。
- **SQL 开发**：多标签编辑、语法高亮、关键字/对象/字段智能提示、SQL 格式化、参数化执行、可视化执行计划（树形 + 节点详情 + 成本/耗时热点）、选区执行、超时与取消、危险 SQL 二次确认、错误行号定位、事务（自动提交 / Commit / Rollback）、查询历史（SQLite 存储）、脚本库与收藏查询、专注模式、结果分页与导出。
- **查询上下文与事务**：已有标签保留独立连接/数据库；事务工具栏和执行计划跟随当前标签。关闭自动提交后由查询服务开启事务，提交/回滚后保留手动模式；启用自动提交异步提交已有事务。同一连接的标签共享事务并串行执行查询和事务操作，不同连接可并行；分组连接也包含在“断开全部”中。详见 [查询连接与事务说明](./docs/query-database-selection.md)。
- **上下文智能提示**：匹配表、字段、别名、Schema 与 SQL 上下文，按标签连接/数据库过滤；字段异步加载，支持 Ctrl+Space、Tab/Enter 接受，注释和字符串内不弹出。候选使用已加载元数据，暂不推断 CTE/派生表输出列。大脚本只对当前语句创建解析标记，测量及限制见 [智能提示说明](./docs/sql-completion.md)。
- **SQL 编辑器辅助**：选中文本右键菜单与快捷键——执行选中 / 新标签执行选中（`Ctrl+Enter`、`Ctrl+Shift+Enter`）、显示执行计划（`Ctrl+L`）、保存选中到脚本库（`Ctrl+Shift+S`）、美化选区 SQL（`Ctrl+Shift+F`）、查找与替换（`Ctrl+F` / `Ctrl+R`）、注释与取消注释行（`Ctrl+Shift+C`）、选中内容大小写转换（`Ctrl+Shift+U` / `Ctrl+Shift+L`）、增减缩进、复制当前行或选区（`Ctrl+D`）、删除行（`Ctrl+Shift+K`）、上下移动行（`Alt+↑` / `Alt+↓`）、复制选中 SQL 为一行 / JSON 字符串 / C# 字面量。
- **查询结果**：独立结果标签与左右对照窗口、最多固定 3 份快照（单次最多 64 结果集 / 100000 行 / 约 32 MiB 文本预算）、结果网格右键「复制为格式」（CSV / 制表符 / JSON / Markdown / INSERT）。
- **数据与表设计**：查询结果内联增删改保存（事务 + 乐观锁）、表/视图数据分页查看、图片/JSON 单元格查看器；外键候选、单记录表单、批量查找替换、文本 / 十六进制 / 文件二进制编辑；表、列、主键、索引、外键和约束的设计、DDL 预览与保存；分区只读元数据/DDL 回放 Tab、主键/索引/外键列选择器与内联编辑。
- **转换、对比与分析**：数据库转换（含 Schema 预览、Schema 映射、列映射）、结构对比、数据对比与同步 / 回滚脚本、依赖分析、诊断、优化、统计和索引碎片分析。
- **备份与交付**：备份与恢复、CSV/Excel/SQL/JSON/XML 导入导出（SQLite 支持参数化 BulkCopy 批量导入）、代码生成（C#/Java 实体）、Word 列文档生成；扩展导入来源（ODBC / Access / dBASE III）。
- **数据工作台（效率与数据治理）**：查询/视图构建器、测试数据生成、数据质量剖析、数据字典与 PDF/HTML 文档、AI SQL 助手（生成 / 解释 / 纠错 / 优化，仅预览不自动执行）、仪表盘（图表 / 计算字段 / 联动 / HTML 快照）、数据脱敏。
- **任务与外观**：任务中心（后台任务运行 / 取消 / 历史 / 通知）、任务定时调度（每天定时 / 每 N 分钟，覆盖 SQL 脚本 / 备份 / 导出 / 导入 / 迁移 / 结构·数据同步、顺序批处理、失败停止或继续、持久化日志、TLS SMTP 通知）、亮暗高对比主题、字体缩放、结果区浮动 / 停靠。
- **可视化与运维监控**：查询结果图表（柱 / 折 / 饼）与仪表盘、全库数据搜索、数据网格内筛选 / 排序、会话与锁监控、用户与权限管理（用户 / 角色创建与删除、授权与撤销、角色成员关系、改密码，均生成方言 SQL 二次确认后执行）、查询性能剖析。
- **性能优化（2026-09-18）**：对象树类型文件夹一次全量加载并并行预取同层其余文件夹、连接时多库 Schema 枚举并发 8、`GetDbVersion()` 解释器实例级缓存、PostgreSQL / KingbaseES 对象树路径改 `pg_class` 直查。
- **新数据库类型**：DuckDB（文件 / 内存 / 只读）、达梦 DM8（Oracle 兼容方言）已接入；两者均**未经真实实例回归验收**，跨库转换已拦截。
- **KingbaseES**：以 PG 兼容模式为当前边界，支持独立连接类型、对象浏览、查询与数据工具；会话/锁、用户与权限、查询剖析、`sys_dump` 备份与 `ksql`/`sys_restore` 恢复、全库搜索与定时任务已代码级接入，非 PG 兼容模式明确拦截。真实 V8 实例、权限与跨平台 UI 验收仍按支持计划推进，**当前不得声称已验证发布**。
- **模板外部化（T1–T3）**：统一 `{name}` 占位符契约（兼容 `$TOKEN$`）、`ITemplateEngine`/`ITemplateStore`、`Profiles/Templates/` 用户覆盖目录；模板上下文（连接/库/schema/表/列/时间/方言）、多类别管理、变量/方言编辑、示例预览、恢复默认与 `{ Version = 1, Payload(Base64 JSON) }` 信封导入导出；内置清单覆盖按方言 DDL、数据字典、HTML 导出与 C#/Java 代码生成片段，用户可同名覆盖。PDF/Word 模板改造另行评估。
- **按方言 DDL 模板**：对象树新建视图/过程/函数按数据库类型生成可编辑 SQL；SQLite / DuckDB 的例程模板明确提示不支持。

当前版本已具备日常数据库浏览、查询、编辑、建表、跨库处理和运维监控的主链路。功能决策、优先级与明确不做清单见根目录 [Roadmap.md](../Roadmap.md)，**未完成事项与待验收清单**见 [docs/backlog.md](./docs/backlog.md)。KingbaseES 的支持边界与验证状态见 [docs/kingbasees-support-plan.md](./docs/kingbasees-support-plan.md)。

## 构建与运行

源码构建需要 .NET 10 SDK。从 v1.1.3 起，Windows/Linux x64 发布包自带 .NET 运行时，无需另行安装 .NET；Linux 仍需桌面 GUI 环境及系统原生依赖。发布及裁剪评估见 [docs/release-size.md](./docs/release-size.md)，历史启动测量见 [docs/startup-performance.md](./docs/startup-performance.md)。

```bash
# 构建 Avalonia 解决方案
dotnet build DatabaseManager.Avalonia.sln

# 运行客户端（需要 GUI 环境）
dotnet run --project DatabaseManager.Avalonia/DatabaseManager.Avalonia.csproj
```

> 核心引擎（`DatabaseInterpreter.*` / `DatabaseConverter.*` / `DatabaseManager.Core` 等）直接复用原仓库，通过 `ProjectReference` 引用，无需拷贝代码。

### 回归与 UI 冒烟

以下命令从仓库根目录运行。回归项目虽与解决方案同目录，但未包含在解决方案中，需要直接运行；`dotnet test` 不会运行其控制台断言。

```powershell
dotnet build DatabaseManager.Avalonia/DatabaseManager.Avalonia.sln -c Release
dotnet run --project DatabaseManager.Avalonia/DatabaseManager.AppCore.RegressionTests -c Release
dotnet run --project DatabaseManager.Avalonia/DatabaseManager.Avalonia -c Release --no-build -- --smoke --p0
```

完整回归成功输出 `All regression checks passed.`。P0 使用合成元数据和临时 SQLite，无需真实服务器；截图与结果标记输出到 `artifacts/p0-ui/`。数据库集成检查和其他冒烟模式见 [验证与冒烟说明](../docs/smoke/verification.md)。

## 架构与维护文档

- [docs/backlog.md](./docs/backlog.md)：**未完成事项与待验收清单**（执行跟踪）。
- [docs/extensibility.md](./docs/extensibility.md)：方言、对象树菜单、导出器与代码模板的扩展方式。
- [docs/query-database-selection.md](./docs/query-database-selection.md)：标签连接、数据库选择、事务模式与并发规则。
- [docs/sql-completion.md](./docs/sql-completion.md)：智能提示范围、限制、回归与性能测量。
- [docs/sql-formatting.md](./docs/sql-formatting.md)：转换引擎格式化包迁移、方言与 JOIN/ON 保真规则。
- [docs/sqlite-encryption.md](./docs/sqlite-encryption.md)：SQLite 原生包选择、密码访问与 SQLCipher 兼容边界。
- [docs/dependencies.md](./docs/dependencies.md)：中央包版本、升级兼容性检查与办公文档依赖。
- [docs/word-export.md](./docs/word-export.md)：Word 开源库评估、Open XML 导出实现与验证边界。
- [docs/excel-io.md](./docs/excel-io.md)：MiniExcel 数据读写、格式边界与批注样式、数据保真和分表检查。
- [docs/startup-performance.md](./docs/startup-performance.md)：启动基线、诊断与发布体积取舍。
- [docs/native-aot.md](./docs/native-aot.md)：Windows x64 Native AOT 改造与验收范围。
- [docs/kingbasees-support-plan.md](./docs/kingbasees-support-plan.md)：KingbaseES 支持计划、能力矩阵与兼容性边界。
- [docs/testing/](./docs/testing/)：KingbaseES 各阶段测试证据（TDD）。
- 根目录 [Roadmap.md](../Roadmap.md)：产品功能取舍与优先级决策。
