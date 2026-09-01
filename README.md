# DatabaseManager

一个基于 **.NET 8** 的多数据库管理与迁移工具，提供对象浏览、SQL 开发、数据编辑、表设计、结构/数据转换、差异对比与同步、导入导出、诊断优化、备份恢复等一站式数据库运维能力。

当前产品客户端为 **DatabaseManager.Avalonia**：基于 Avalonia UI + AtomUI，支持 Windows / Linux / macOS。它通过 `DatabaseManager.AppCore` 承载 UI 无关的业务服务和 ViewModel，并复用 `DatabaseInterpreter`、`DatabaseConverter` 与 `DatabaseManager.Core` 等核心引擎。

> ⚠️ `DatabaseManager.CoreApp` 是历史 WinForms 实现，仅为兼容既有代码和功能参考而保留，**已过时且不再作为主线维护或新功能交付目标**。新功能、缺陷修复和跨平台使用请统一使用 Avalonia 客户端。

---

## 解决方案结构

推荐使用 `DatabaseManager.Avalonia/DatabaseManager.Avalonia.sln`，其中包含 Avalonia 客户端及其所需核心项目。根目录 `DatabaseManager.sln` 保留完整历史解决方案，包含已过时的 WinForms 客户端。

| 项目 | 说明 |
| --- | --- |
| **DatabaseInterpreter.Core** | 核心数据访问与 Schema 解析引擎，内含各数据库方言实现（连接构建、批量复制、脚本生成、数据类型/函数规格配置等） |
| **DatabaseInterpreter.Model / .Utility** | 引擎模型与工具库 |
| **DatabaseInterpreter.Geometry** | 空间/几何类型支持 |
| **DatabaseConverter.Core** | 数据库结构与数据迁移（转换）核心引擎 |
| **SqlAnalyser.Core** | SQL 解析/分析库（多方言语法解析） |
| **DatabaseManager.Core** | 客户端共享的核心模型、配置、通用逻辑 |
| **DatabaseManager.CoreApp** | 已过时的 WinForms 客户端，仅供兼容和历史功能参考 |
| **DatabaseManager.AppCore** | Avalonia 客户端的 UI 无关业务层（服务、ViewModel、配置与 DI） |
| **DatabaseManager.Avalonia** | 推荐使用的 Avalonia 跨平台 UI 客户端 |
| **DatabaseManager.FileUtility** | 文件/导出辅助工具库 |
| **DatabaseManager.Profile** | 连接配置（Profile）持久化与管理 |

## 支持的数据库

- Microsoft SQL Server
- MySQL
- Oracle
- PostgreSQL
- SQLite
- 人大金仓 KingbaseES（当前以 PG 兼容模式为支持边界；会话、锁监控和终止会话已接入）

---

## 功能总览（Avalonia 版）

### 1. 连接与配置管理
- 连接的新建、编辑、重命名、删除、测试、连接/重连/断开/断开全部；Profile 持久化。
- 支持 SSL、集成认证、记住密码、优先级；按数据库类型过滤连接列表。
- 连接分组（对象树按「📁 分组」归档）与颜色标签（连接色点标识，便于区分生产/测试环境）。

### 2. 对象浏览
- 多级懒加载对象树：连接 → 数据库 → Schema → 表/视图/存储过程/函数/序列 → 列/索引/键/约束/触发器；类型文件夹按方言能力（`SupportDbObjectType`）动态裁剪，系统对象自动过滤。
- 树内搜索：元数据模糊匹配并定位到树节点；大目录懒分页（500/页）、加载指示与取消。
- 右键菜单：查看详情、Generate SQL（SELECT / TOP N / INSERT / UPDATE / DELETE / CREATE / ALTER / DROP，基于真实元数据与方言生成）、新建对象、删除/重命名、过滤数据（生成 WHERE 模板）、比较与迁移入口、复制名称/完整路径/连接串。
- 元数据搜索窗口：跨表/视图/过程/函数/序列（含列名）搜索，支持定位树节点或生成 SELECT。
- 全库数据搜索：跨表/视图搜索**数据内容**（按方言 LIKE 匹配文本列、每表限量、单表错误不中断），结果可生成带条件的 SELECT 打开到新查询标签。
- 对象树菜单扩展点（`IObjectTreeMenuContributor`），支持第三方菜单贡献。

### 3. SQL 开发
- 多标签查询编辑器：语法高亮、关键字/数据库对象/字段智能提示（支持表别名、Schema 限定名、`[]`/`"`/`` ` `` 标识符）、Tab 接受提示。
- 执行：全部执行、选区执行（F5）、单次超时、随时取消；常见驱动错误行号解析并自动定位光标。
- 安全：危险 DDL/DML 二次确认（可按标签开关）、关闭含未保存修改标签时的确认提示。
- 事务：自动提交切换、开始事务 / Commit / Rollback。
- 工作台：查询历史（近 200 条）、脚本库收藏 + 内置代码片段、最近脚本、参数化执行（占位符替换）、SQL 格式化、执行计划（EXPLAIN / SHOWPLAN）、Schema 快速切换。
- 结果区：分页浏览、消息输出、结果导出、内联编辑（见下）。

### 4. 数据查看与编辑
- 查询结果内联编辑：单表简单 SELECT 自动判定可编辑性（JOIN/GROUP BY/DISTINCT/UNION/子查询等自动只读并说明原因），网格内新增/删除/修改，保存走事务 + 乐观锁冲突检测，改动跨页保留；自增/计算/二进制列只读。
- 数据网格内交互式筛选与排序：按列/运算符（包含、等于、比较、为空等）筛选当前结果集，数值优先的列排序，与分页/内联编辑兼容（未保存新增行始终可见）。
- 分页浏览（50–1000 行/页）、单元格图片与 JSON 内容查看器。
- 结果区浮动：结果区可浮动为独立窗口（与主窗口实时同步），停靠回主窗口一键恢复。

### 5. 表设计器
- 表、列、主键、索引、外键、约束的可视化设计与保存，DDL 预览；新建表/视图/过程/函数模板（方言感知）；对象 DDL 生成、删除与重命名（预览 + 执行）。

### 6. 数据库转换（结构与数据迁移）
- 跨库结构转换（表、索引、约束等）与数据迁移（按表/分页批量），Schema 映射，目标结构预览与列编辑，任务可取消。

### 7. 结构与数据对比 + 同步发布
- 对象结构对比（表/列/索引差异）与数据对比（值级差异）。
- 在对比结果上生成可审阅的同步脚本：选择性应用、执行前预览、执行日志、可选回滚脚本。

### 8. 诊断、优化与统计
- 连接/对象/脚本诊断；优化建议；索引碎片分析与重建；表记录数统计、列内容最大长度分析；表/对象外键与引用依赖分析。均支持取消。

### 9. 备份与恢复
- 按数据库类型适配的备份与恢复：SQL Server 原生 RESTORE，MySQL/PostgreSQL/Oracle 使用客户端工具，SQLite 替换前自动创建安全副本；支持压缩、文件校验、取消与日志。

### 10. 导入 / 导出
- 导出 CSV / Excel / SQL / JSON / XML：可选表/列、是否含列名、文本编码、起始页续传、进度与取消。
- 导入：文件预览、列映射、错误行报告、可恢复的批处理进度；查询结果网格亦可导出。

### 11. 交付辅助
- 代码生成：C# / Java 实体类；文档生成：Word 列结构文档。

### 12. 任务中心与定时调度
- 后台任务运行/取消（转换、导出、导入、备份恢复、统计、脚本执行已接入）、状态分级、任务日志、跨会话历史（task-history.json）、完成 Toast 通知、主窗口关闭保护。
- 任务定时调度：计划（每天 HH:mm / 每 N 分钟）驱动 SQL 脚本执行、备份、数据导出，到期检查经任务中心执行（可见/可取消/有历史），支持立即运行与启用开关。

### 13. 工作区与外观
- 亮 / 暗 / 高对比三套主题（颜色令牌层）、跟随系统切换、状态栏字体缩放（90–125%）。
- 布局持久化：窗口大小/位置/最大化、对象树栏宽、未保存 SQL 草稿随会话恢复；多显示器位置钳制。

### 14. 数据可视化与监控运维（2026-09 新增）
- 图表与仪表盘：查询结果一键绘制柱状/折线/饼图（X/Y 列选择、计数/求和/平均分组聚合），可保存为仪表盘图表（SQL 定义持久化、卡片网格展示、重新执行刷新）。
- 会话与锁监控：按数据库类型查看活动会话（来源、状态、等待、当前 SQL）与阻塞链，支持自动刷新与终止会话（二次确认）。
- 用户与权限管理：用户列表、权限查看、模板化创建/授权/删除用户（生成方言 SQL、确认后执行）。
- 查询性能剖析：重复执行 SQL 并分阶段计时（执行/取数/合计、平均/最快/最慢），MySQL/PostgreSQL 支持 EXPLAIN ANALYZE 输出。

> WinForms 原版额外提供：**数据库关系图（frmDatabaseDiagram）**，该功能已列入 Avalonia 版待实现清单。

---

## 与主流数据库管理平台对比

参考平台：[DBeaver](https://dbeaver.com/docs/dbeaver/)、[Navicat Premium](https://www.navicat.com/en/products/navicat-premium)、[JetBrains DataGrip](https://www.jetbrains.com/datagrip/)、[TablePlus](https://tableplus.com/)、HeidiSQL、Beekeeper Studio、Azure Data Studio。

图例：✅ 支持　⚠️ 部分支持　❌ 不支持

| 功能 | 本工具 | DBeaver | Navicat | DataGrip | TablePlus |
| --- | :-: | :-: | :-: | :-: | :-: |
| 多数据库支持（≥5 种关系库） | ✅ | ✅ | ✅ | ✅ | ⚠️ |
| 连接 Profile / 测试 / SSL | ✅ | ✅ | ✅ | ✅ | ✅ |
| SSH 隧道连接 | ❌ | ✅ | ✅ | ✅ | ✅ |
| 连接分组 / 颜色标签 | ✅ | ✅ | ✅ | ✅ | ✅ |
| 对象浏览 + 元数据搜索 | ✅ | ✅ | ✅ | ✅ | ⚠️ |
| SQL 编辑（高亮/补全/格式化） | ✅ | ✅ | ✅ | ✅ | ✅ |
| 执行计划 | ✅ | ✅ | ✅ | ✅ | ⚠️ |
| 查询历史 / 脚本库 / 片段 | ✅ | ✅ | ✅ | ✅ | ⚠️ |
| 数据网格分页与内联编辑 | ✅ | ✅ | ✅ | ✅ | ✅ |
| 网格内交互式筛选/排序 | ✅ | ✅ | ✅ | ✅ | ✅ |
| 全库数据搜索（跨表全文） | ✅ | ✅ | ⚠️ | ✅ | ❌ |
| 导入导出 CSV/Excel/SQL/JSON/XML | ✅ | ✅ | ✅ | ✅ | ✅ |
| 结构/数据对比 + 同步脚本 | ✅ | ⚠️ | ✅ | ⚠️ | ❌ |
| 跨库结构 + 数据迁移 | ✅ | ✅ | ✅ | ⚠️ | ❌ |
| 表设计器 | ✅ | ✅ | ✅ | ✅ | ✅ |
| 备份 / 恢复 | ✅ | ✅ | ✅ | ⚠️ | ⚠️ |
| 诊断 / 优化建议 / 碎片分析 | ✅ | ⚠️ | ✅ | ⚠️ | ❌ |
| 代码生成 / 文档生成 | ✅ | ⚠️ | ✅ | ⚠️ | ❌ |
| 任务中心（运行/取消/历史/定时调度） | ✅ | ⚠️ PRO | ✅ | ⚠️ | ❌ |
| 查询性能剖析（分阶段计时） | ✅ | ⚠️ | ✅ | ⚠️ | ❌ |
| ER 图 / 数据库关系图 | ❌ | ✅ | ✅ | ✅ | ❌ |
| 图表 / 仪表盘（数据可视化） | ✅ | ✅ | ✅ | ⚠️ 绘图 | ❌ |
| 用户 / 角色 / 权限管理 | ✅ | ✅ | ✅ | ✅ | ⚠️ |
| 会话 / 锁监控 | ✅ | ⚠️ PRO | ✅ | ⚠️ | ❌ |
| Mock 数据生成 / 数据脱敏 | ❌ | ⚠️ PRO | ✅ | ❌ | ❌ |
| AI 助手（自然语言 → SQL） | ❌ | ⚠️ PRO | ✅ v17 | ✅ | ⚠️ |

结论：**核心数据库管理链路（连接、浏览、SQL 开发、编辑、设计、迁移、对比、备份、导入导出、任务）已基本对齐主流工具**；差距主要集中在可视化（ER 图、图表）、数据库安全管理（用户/权限、会话/锁、脱敏）与智能化（AI 助手）三大类。

---

## 待实现功能（Roadmap）

> 依据与主流平台的差距分析整理。**2026-09 批次已实现**：全库数据搜索、数据网格内交互式筛选/排序、连接分组与颜色标签、图表/仪表盘、用户/权限管理 UI、会话与锁监控、任务定时调度、查询性能剖析、结果区浮动/停靠（实施记录、已知限制与后续优先级见 [todo.md](./todo.md)）。

### P1 · 高频刚需

| # | 功能 | 说明 | 参考 |
| --- | --- | --- | --- |
| 1 | **SSH 隧道连接** | 连接配置增加 SSH 主机/端口/认证（密码/密钥）与隧道选项，并接入各数据库连接构建器（所有主流工具的标配能力） | DBeaver / Navicat / TablePlus |
| 2 | **ER 图（数据库关系图）** | 库/Schema/表级右键生成 ER 图：表节点 + 外键关系连线，自动布局、缩放、导出图片；WinForms 版曾有 `frmDatabaseDiagram` | DBeaver / DataGrip / Navicat |

### P2 · 进阶能力

| # | 功能 | 说明 | 参考 |
| --- | --- | --- | --- |
| 3 | **测试数据生成（Mock Data）** | 按列类型/规则批量生成测试数据（随机、区间、枚举、正则、引用表） | DBeaver Mock Data / Navicat |
| 4 | **数据脱敏** | 导出与查询结果的敏感列脱敏（手机号/证件/银行卡等规则），规则可配置 | DBeaver PRO / dbForge |
| 5 | **网格排序记忆与列宽持久化** | 结果网格的列宽/排序/布局按标签页记忆（当前筛选排序随结果集重置） | DBeaver |

### P3 · 长期 / 差异化方向

| # | 功能 | 说明 | 参考 |
| --- | --- | --- | --- |
| 6 | **AI 助手** | 自然语言 → SQL、SQL 解释/纠错/优化建议；支持 OpenAI 兼容接口与本地模型（如 Ollama），结合 Schema 上下文 | DataGrip AI / DBeaver AI / Chat2DB |
| 7 | **可视化查询构建器** | 拖拽表/列、条件分组生成 SELECT，降低手写 SQL 门槛 | Navicat / DBeaver |
| 8 | **存储过程调试器** | 断点、单步、变量查看（优先 PostgreSQL PL/pgSQL，逐步扩展） | DBeaver PRO / DataGrip |
| 9 | **NoSQL 支持** | MongoDB / Redis 的连接与文档浏览、查询 | DBeaver / Navicat / DbGate |
| 10 | **数据 Notebook** | 交互式 SQL + Markdown + 结果混排的笔记本（类 Jupyter / Azure Data Studio） | Azure Data Studio |
| 11 | **团队协作与云同步** | 连接配置加密同步、脚本库共享、团队 SQL 审计 | Beekeeper / Navicat Cloud |
| 12 | **更多数据库类型** | ClickHouse、DuckDB、达梦等数据库（引擎层按方言适配器扩展）；KingbaseES 的其余兼容模式和真实实例验收另行推进 | DBeaver |
| 13 | **完整 Dock 拖拽布局** | 基于 Dock.Avalonia 的面板级停靠/浮动/布局持久化（当前已实现结果区浮动/停靠） | DBeaver / DataGrip |
| 14 | **对象树右键菜单图标补全** | 当前仅部分菜单项有图标，需统一图标集与主题适配（`todo.md` P2） | — |
| 15 | **代码模板外部化** | 代码生成/文档生成模板开放为可自定义（占位符模板文件） | — |

---

## 构建与运行

### 环境要求
- .NET 8 SDK
- Windows / Linux / macOS（需 GUI 环境）
- 对应数据库的客户端驱动（随 NuGet 包引入）；备份/恢复功能需对应数据库客户端工具（mysqldump、pg_dump 等）

### 构建
```powershell
# 构建推荐的 Avalonia 跨平台解决方案
dotnet build DatabaseManager.Avalonia\DatabaseManager.Avalonia.sln
```

### 运行
```powershell
# 运行 Avalonia 跨平台客户端
dotnet run --project DatabaseManager.Avalonia\DatabaseManager.Avalonia\DatabaseManager.Avalonia.csproj
```

---

## 目录说明

- `DatabaseInterpreter/`：核心数据访问与 Schema 解析引擎及各数据库适配器。
- `DatabaseConverter/`：结构与数据迁移引擎及各数据库适配器。
- `DatabaseManager/`：共享客户端核心（`Core`）、工具（`FileUtility`、`Profile`），以及已过时的 WinForms `CoreApp`。
- `DatabaseManager.Avalonia/`：主线 Avalonia 跨平台客户端（`AppCore` 业务层 + `Avalonia` UI 层 + 技术与测试文档）。

---

## 相关文档

- [todo.md](./todo.md)：Avalonia 版统一 TODO（当前待办、优先级与已完成事项）。
- [todo-202609.md](./todo-202609.md)：2026-09 功能批次归档入口。
- [todo-202608.md](./todo-202608.md)：2026-08 功能批次归档入口。
- [DatabaseManager.Avalonia/README.md](./DatabaseManager.Avalonia/README.md)：Avalonia 版架构、选型与迁移进度。
- [DatabaseManager.Avalonia/docs/migration-progress.md](./DatabaseManager.Avalonia/docs/migration-progress.md)：逐阶段迁移证据。
