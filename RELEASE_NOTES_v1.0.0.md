# DatabaseManager v1.0.0

发布日期：2026-09-01

DatabaseManager v1.0.0 是首个面向日常数据库开发、管理与运维场景的正式版本。本版本以跨平台的 **Avalonia UI 客户端** 为主线，支持 Windows、Linux 和 macOS。

> `DatabaseManager.CoreApp`（WinForms）已进入历史兼容状态，不再作为主线维护或新功能交付目标。请使用 `DatabaseManager.Avalonia` 客户端。

## 核心能力

- **连接与对象管理**：连接配置、分组和颜色标签；连接测试、重连与断开；多层级对象树、元数据搜索与对象 SQL 生成。
- **SQL 开发**：多标签编辑器、语法高亮、对象/字段提示、格式化、参数化执行、事务控制、执行计划、执行历史、脚本库与危险语句确认。
- **数据处理**：查询结果分页、筛选、排序和内联编辑；CSV、Excel、SQL、JSON、XML 的导入导出；图片和 JSON 单元格查看。
- **结构与迁移**：表设计器、DDL 预览、跨库结构与数据迁移、Schema 映射、结构/数据对比及同步脚本预览。
- **运维辅助**：诊断与优化建议、对象依赖、统计、索引碎片分析、备份恢复、任务中心、定时调度、查询性能剖析、会话与锁监控、用户与权限管理。
- **工作区体验**：亮色、深色和高对比主题；字体缩放；窗口、面板与未保存 SQL 草稿恢复；结果区浮动与停靠。
- **可视化**：查询结果柱状图、折线图和饼图，以及可保存、可刷新的仪表盘图表。

## 支持的数据库

- Microsoft SQL Server
- MySQL
- Oracle
- PostgreSQL
- SQLite
- 人大金仓 KingbaseES

KingbaseES 当前以 **PG 兼容模式** 为支持边界，已覆盖独立连接、对象浏览、查询与数据工具，并支持会话/锁监控及用户确认后的会话终止。

## 重要改进

- 元数据搜索覆盖表、视图、过程、函数、序列及列名，并可定位到对象树。
- 会话监控支持查看活动会话、阻塞链和当前 SQL；KingbaseES PG 兼容模式使用其 `sys_stat_activity`、`sys_locks` 等系统视图路径。
- 主窗口恢复最大化状态改为在首帧布局绘制后执行，减少启动过程中的黑色过渡区域。
- 连接配置窗口优化了可用高度和表单间距。

## 已知限制

- KingbaseES 尚未完成真实 V8 实例的完整集成验证；Oracle、MySQL、SQL Server 等兼容模式不会静默复用 PG 路径。跨库转换仍处于保护性禁用状态，待真实实例回放验证后开放。
- SSH 隧道、ER 图、测试数据生成、数据脱敏、AI 助手、NoSQL 支持等能力尚未纳入 v1.0.0。
- 备份与恢复依赖相应数据库的本机客户端工具；请在使用前确认工具已安装并位于可执行路径中。

## 环境与启动

- .NET 8 SDK
- Windows、Linux 或 macOS 图形界面环境

```powershell
dotnet build DatabaseManager.Avalonia\DatabaseManager.Avalonia.sln
dotnet run --project DatabaseManager.Avalonia\DatabaseManager.Avalonia\DatabaseManager.Avalonia.csproj
```

## 验证

发布前已执行 Avalonia 解决方案构建与 `DatabaseManager.AppCore.RegressionTests` 回归测试，构建无错误，回归测试通过。

## 反馈

提交问题时，请附上操作系统、数据库类型与版本、数据库兼容模式、连接方式、可复现步骤及必要的脱敏日志，以便定位问题。
