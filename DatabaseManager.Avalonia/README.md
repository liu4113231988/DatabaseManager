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

- **连接与工作区**：连接的新建、编辑、删除、刷新、测试与 Profile 持久化；主窗口提供菜单、工具栏、对象浏览器、查询区、结果区和状态栏。
- **对象浏览与 SQL 开发**：对象树支持数据库、Schema、表、视图、过程、函数、序列及列/索引/键/约束等多级加载；SQL 编辑器支持语法高亮、关键字和数据库对象提示、表/视图字段提示、Tab 接受提示、执行与结果展示。
- **事务、数据与表设计**：自动提交、Commit、Rollback；表/视图数据分页查看和增删改保存；表、列、主键、索引、外键和约束的设计、DDL 预览与保存。
- **转换、对比与分析**：数据库转换、Schema 映射、结构对比、数据对比、依赖分析、诊断、优化、统计和索引碎片分析。
- **备份与交付辅助**：备份、CSV/Excel 导入导出、代码生成、列文档生成，以及图片/JSON 内容查看。

当前版本已具备日常数据库浏览、查询、编辑、建表和跨库处理的主链路。后续完善事项按优先级记录在仓库根目录的 [todo-202608.md](../todo-202608.md)；完整迁移证据见 [docs/migration-progress.md](./docs/migration-progress.md)。

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
