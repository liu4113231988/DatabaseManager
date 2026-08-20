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
| 框架 | Avalonia 11.3.20（.NET 8） |
| 主题 | **AtomUI**（Ant Design 风格，对齐原 AntdUI） |
| 停靠布局 | **Dock.Avalonia** |
| MVVM | CommunityToolkit.Mvvm |
| DI | Microsoft.Extensions.DependencyInjection |
| 消息框 | MessageBox.Avalonia |

## 功能（阶段 0-3）

- **连接管理**：`ConnectionManagerWindow` + `ConnectWindow` 支持新建 / 编辑 / 删除 / 刷新多数据库连接，支持“测试连接”拉取数据库列表，校验连接名称唯一性后保存到 Profile。
- **主框架**：顶部标题栏 + 菜单栏 + 工具栏；主内容三栏（左对象浏览器 / 中内容区 / 下结果区）。
- **对象浏览 + 查询**：对象树多级懒加载（数据库 → Schema → 类型文件夹 → 对象 → 列/索引/键/约束/触发器），右键菜单生成查询/脚本，SQL 编辑器执行并展示结果。
- **数据查看与编辑**：对象树右键「编辑数据」打开表/视图数据，分页加载，可编辑网格（增删改/还原/保存），自增/计算/二进制列只读，事务内保存。
- **事务核心**：Commit / Rollback / Auto-commit 切换，手动事务下执行 SQL 纳入同一事务。
- **表设计器**：对象树右键表「设计表」或 Tables 文件夹「新建表」，编辑列/主键/索引/外键/约束，预览并保存生成的 CREATE/ALTER DDL。
- **数据库转换**：菜单「工具 → 数据库转换」打开，选择源/目标连接与模式（结构/数据/结构+数据），执行跨库转换并实时展示反馈日志。
- 完整拖拽 / 浮动 **Dock 停靠布局**为进阶项，待图形环境实机接入（详见迁移进度）。

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
