# DatabaseManager.Avalonia

DatabaseManager 的主线跨平台客户端，基于 **Avalonia UI** 构建，运行于 Windows、Linux 与 macOS。`DatabaseManager.CoreApp` 的 WinForms 实现仅保留为历史兼容代码，已过时；所有新功能与修复均以本项目为准。

## 目标架构

```
DatabaseManager.Avalonia.sln
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
| 框架 | Avalonia 12.1.1（.NET 8） |
| 主题 | **AtomUI 6.1.5**（Ant Design 风格） |
| 停靠布局 | **Dock.Avalonia 12.1.0.4** |
| MVVM | CommunityToolkit.Mvvm |
| DI | Microsoft.Extensions.DependencyInjection |
| 消息框 | MessageBox.Avalonia |

## 已完成能力

> 以下为已交付能力汇总。**未完成事项与待验收清单**见 [docs/backlog.md](./docs/backlog.md)；功能取舍与优先级决策见根目录 [Roadmap.md](../Roadmap.md)。

- **连接与工作区**：连接的新建、编辑、重命名、删除、测试、连接/重连/断开/断开全部与 Profile 持久化（支持 SSL、集成认证）；主窗口提供菜单、工具栏、对象浏览器、查询区、结果区和状态栏，布局与未保存 SQL 草稿随会话恢复；连接配置多选导入导出（无密码 / 加密携密，PBKDF2 + AES-GCM）。
- **SSH 隧道**：跳板机密码 / 私钥认证、固定 SHA256 主机指纹、仅回环地址端口转发；断线在下次操作时重连，不重放 SQL，随断开 / 进程退出释放（真实 SSH 服务端联调待办）。
- **对象浏览**：对象树支持数据库、Schema、表、视图、过程、函数、序列及列/索引/键/约束/触发器多级懒加载；树内就地过滤（输入即时隐藏不匹配节点，清空恢复）与深度搜索定位、右键菜单（Generate SQL、新建/删除/重命名对象、过滤模板、比较迁移入口，菜单图标按动作统一）、元数据搜索窗口与菜单扩展点；`dbm://` 应用内 URI 直达对象。
- **SQL 开发**：多标签编辑、语法高亮、关键字/对象/字段智能提示、SQL 格式化、参数化执行、可视化执行计划（树形 + 节点详情 + 成本/耗时热点）、选区执行、超时与取消、危险 SQL 二次确认、错误行号定位、事务（自动提交 / Commit / Rollback）、查询历史（SQLite 存储）、脚本库与收藏查询、专注模式、结果分页与导出。
- **查询结果**：独立结果标签与左右对照窗口、最多固定 3 份快照（单次最多 64 结果集 / 100000 行 / 约 32 MiB 文本预算）、结果网格右键「复制为格式」（CSV / 制表符 / JSON / Markdown / INSERT）。
- **数据与表设计**：查询结果内联增删改保存（事务 + 乐观锁）、表/视图数据分页查看、图片/JSON 单元格查看器；外键候选、单记录表单、批量查找替换、文本 / 十六进制 / 文件二进制编辑；表、列、主键、索引、外键和约束的设计、DDL 预览与保存。
- **转换、对比与分析**：数据库转换（含 Schema 预览、Schema 映射、列映射）、结构对比、数据对比与同步 / 回滚脚本、依赖分析、诊断、优化、统计和索引碎片分析。
- **备份与交付**：备份与恢复、CSV/Excel/SQL/JSON/XML 导入导出、代码生成（C#/Java 实体）、Word 列文档生成；扩展导入来源（ODBC / Access / dBASE III）。
- **数据工作台（效率与数据治理）**：查询/视图构建器、测试数据生成、数据质量剖析、数据字典与 PDF/HTML 文档、AI SQL 助手（生成 / 解释 / 纠错 / 优化，仅预览不自动执行）、仪表盘（图表 / 计算字段 / 联动 / HTML 快照）、数据脱敏。
- **任务与外观**：任务中心（后台任务运行 / 取消 / 历史 / 通知）、任务定时调度（每天定时 / 每 N 分钟，覆盖 SQL 脚本 / 备份 / 导出 / 导入 / 迁移 / 结构·数据同步、顺序批处理、失败停止或继续、持久化日志、TLS SMTP 通知）、亮暗高对比主题、字体缩放、结果区浮动 / 停靠。
- **可视化与运维监控**：查询结果图表（柱 / 折 / 饼）与仪表盘、全库数据搜索、数据网格内筛选 / 排序、会话与锁监控、用户与权限管理、查询性能剖析。
- **性能优化（2026-09-18）**：对象树类型文件夹一次全量加载并并行预取同层其余文件夹、连接时多库 Schema 枚举并发 8、`GetDbVersion()` 解释器实例级缓存、PostgreSQL / KingbaseES 对象树路径改 `pg_class` 直查。
- **新数据库类型**：DuckDB（文件 / 内存 / 只读）、达梦 DM8（Oracle 兼容方言）已接入；两者均**未经真实实例回归验收**，跨库转换已拦截。
- **KingbaseES**：以 PG 兼容模式为当前边界，支持独立连接类型、对象浏览、查询与数据工具；会话 / 锁监控和终止会话已接入。真实 KingbaseES 实例验证及其他兼容模式仍按支持计划推进。
- **模板外部化（T1）**：统一 `{name}` 占位符契约（兼容 `$TOKEN$`）、`ITemplateEngine`/`ITemplateStore`、`Profiles/Templates/` 用户覆盖目录，SqlSnippets 已桥接为首批内置模板。

当前版本已具备日常数据库浏览、查询、编辑、建表、跨库处理和运维监控的主链路。功能决策、优先级与明确不做清单见根目录 [Roadmap.md](../Roadmap.md)，**未完成事项与待验收清单**见 [docs/backlog.md](./docs/backlog.md)。KingbaseES 的支持边界与验证状态见 [docs/kingbasees-support-plan.md](./docs/kingbasees-support-plan.md)。

## 构建与运行

```bash
# 构建 Avalonia 解决方案
dotnet build DatabaseManager.Avalonia.sln

# 运行客户端（需要 GUI 环境）
dotnet run --project DatabaseManager.Avalonia/DatabaseManager.Avalonia.csproj
```

> 核心引擎（`DatabaseInterpreter.*` / `DatabaseConverter.*` / `DatabaseManager.Core` 等）直接复用原仓库，通过 `ProjectReference` 引用，无需拷贝代码。

## 架构与维护文档

- [docs/backlog.md](./docs/backlog.md)：**未完成事项与待验收清单**（执行跟踪）。
- [docs/extensibility.md](./docs/extensibility.md)：方言、对象树菜单、导出器与代码模板的扩展方式。
- [docs/kingbasees-support-plan.md](./docs/kingbasees-support-plan.md)：KingbaseES 支持计划、能力矩阵与兼容性边界。
- [docs/testing/](./docs/testing/)：KingbaseES 各阶段测试证据（TDD）。
- 根目录 [Roadmap.md](../Roadmap.md)：产品功能取舍与优先级决策。
