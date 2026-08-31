# DatabaseManager.Avalonia（WinForms → AvaloniaUI 迁移项目）

> ⚠️ **本目录为独立的 Avalonia 迁移项目**，与原 WinForms 版 `DatabaseManager.CoreApp` **并存**。
> 原版保持可用，迁移过程不修改原项目。

## 目标架构

```
DatabaseManager.Avalonia.sln
├─ DatabaseManager.AppCore/      # UI 无关业务层（复用原核心引擎，零 WinForms 依赖）
│   ├─ ViewModels/               # MainWindowViewModel / ConnectionManagerViewModel 等
│   ├─ Services/                 # IDbConnectionService / IDbSchemaService 等 + 默认实现
│   ├─ Models/                   # ConnectionItem 等 UI 无关领域模型
│   └─ Common/                   # ViewModelBase、DI 注册
├─ DatabaseManager.Avalonia/     # Avalonia UI 层（AtomUI 主题 + 菜单/工具栏 + 三栏主框架）
└─ docs/                         # 迁移进度 / 版本锁定记录
```

## 技术选型（详见 [docs/package-versions.md](./docs/package-versions.md)）

| 用途 | 选型 |
|------|------|
| 框架 | Avalonia 12.1.1（.NET 8） |
| 主题 | **AtomUI**（Ant Design 风格，对齐原 AntdUI） |
| 停靠布局 | **Dock.Avalonia** |
| MVVM | CommunityToolkit.Mvvm |
| DI | Microsoft.Extensions.DependencyInjection |
| 消息框 | MessageBox.Avalonia |

## 已完成能力

- **连接与工作区**：连接的新建、编辑、重命名、删除、测试、连接/重连/断开/断开全部与 Profile 持久化（支持 SSL、集成认证）；主窗口提供菜单、工具栏、对象浏览器、查询区、结果区和状态栏，布局与未保存 SQL 草稿随会话恢复。
- **对象浏览**：对象树支持数据库、Schema、表、视图、过程、函数、序列及列/索引/键/约束/触发器多级懒加载；树内搜索定位、右键菜单（Generate SQL、新建/删除/重命名对象、过滤模板、比较迁移入口）、元数据搜索窗口与菜单扩展点。
- **SQL 开发**：多标签编辑、语法高亮、关键字/对象/字段智能提示、SQL 格式化、参数化执行、执行计划、选区执行、超时与取消、危险 SQL 二次确认、错误行号定位、事务（自动提交/Commit/Rollback）、查询历史、脚本库与代码片段、结果分页与导出。
- **数据与表设计**：查询结果内联增删改保存（事务 + 乐观锁）、表/视图数据分页查看、图片/JSON 单元格查看器；表、列、主键、索引、外键和约束的设计、DDL 预览与保存。
- **转换、对比与分析**：数据库转换、Schema 映射、结构对比、数据对比与同步/回滚脚本、依赖分析、诊断、优化、统计和索引碎片分析。
- **备份与交付**：备份与恢复、CSV/Excel/SQL/JSON/XML 导入导出、代码生成（C#/Java 实体）、Word 列文档生成。
- **任务与外观**：任务中心（后台任务运行/取消/历史/通知）、任务定时调度（每天定时/每 N 分钟，SQL 脚本/备份/导出）、亮暗高对比主题、字体缩放、结果区浮动/停靠。
- **可视化与运维监控（2026-09）**：查询结果图表（柱/折/饼）与仪表盘、全库数据搜索、数据网格内筛选/排序、会话与锁监控、用户与权限管理、查询性能剖析。

当前版本已具备日常数据库浏览、查询、编辑、建表和跨库处理的主链路。后续完善事项按优先级记录在仓库根目录的 [todo.md](../todo.md)；与主流数据库管理平台的功能差距与 Roadmap 见根目录 [README.md](../README.md) 的「待实现功能」章节。完整迁移证据见 [docs/migration-progress.md](./docs/migration-progress.md)。

## 构建与运行

```bash
# 构建整个解决方案
dotnet build DatabaseManager.Avalonia.sln

# 运行 Avalonia 客户端（需要 GUI 环境）
dotnet run --project DatabaseManager.Avalonia/DatabaseManager.Avalonia.csproj
```

> 核心引擎（`DatabaseInterpreter.*` / `DatabaseConverter.*` / `DatabaseManager.Core` 等）直接复用原仓库，通过 `ProjectReference` 引用，无需拷贝代码。

## 迁移进度

参见 [docs/migration-progress.md](./docs/migration-progress.md)。
