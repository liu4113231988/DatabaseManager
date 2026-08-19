# DatabaseManager.Avalonia（WinForms → AvaloniaUI 迁移项目）

> ⚠️ **本目录为独立的 Avalonia 迁移项目**，与原 WinForms 版 `DatabaseManager.CoreApp` **并存**。
> 原版保持可用，迁移过程不修改原项目。

## 目标架构

```
DatabaseManager.Avalonia.sln
├─ DatabaseManager.AppCore/      # UI 无关业务层（复用原核心引擎，零 WinForms 依赖）
│   ├─ ViewModels/               # MainWindowViewModel 等
│   ├─ Services/                 # IDbConnectionService / IDbSchemaService 等 + 默认实现
│   └─ Common/                   # ViewModelBase、DI 注册
├─ DatabaseManager.Avalonia/     # Avalonia UI 层（AtomUI 主题 + Dock 停靠布局）
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
