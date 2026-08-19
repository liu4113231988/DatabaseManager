# 依赖版本锁定（Package Versions）

> 本文档记录 Avalonia 迁移项目各阶段锁定的 NuGet 依赖版本组合，确保可复现构建。
> 对应 `avalonia-migration-detailed-plan.md` 阶段 0 的"版本锁定"风险应对。

## 阶段 0 — 骨架版本组合（已验证可编译）

| 包 | 版本 | 说明 |
|------|------|------|
| Avalonia | 11.3.20 | 生态兼容基座（AtomUI/Dock/MessageBox 均基于 Avalonia 11） |
| Avalonia.Desktop | 11.3.20 | |
| Avalonia.Themes.Fluent | 11.3.20 | 基础主题 |
| Avalonia.Fonts.Inter | 11.3.20 | 默认字体 |
| CommunityToolkit.Mvvm | 8.4.2 | MVVM 源生成器 |
| Microsoft.Extensions.DependencyInjection | 8.0.1 | DI 容器 |
| **AtomUI** | **5.0.2** | Ant Design 风格主题（对齐原 AntdUI 视觉）；依赖 Avalonia 11.3.8 |
| **Dock.Avalonia** | **11.3.12.1** | 停靠布局系统；依赖 Avalonia 11.3.x |
| **MessageBox.Avalonia** | **3.3.1.1** | 消息框 |
| ReactiveUI.Avalonia | 11.3.8 | 由 AtomUI 传递引入；提供 `UseReactiveUI()` |

## 版本组合决策记录

- **为何固定 Avalonia 11.3.x**：计划所选的核心生态库（AtomUI 5.0.2、Dock.Avalonia 11.3.x、MessageBox.Avalonia 3.3.x）均基于 Avalonia 11 构建；Avalonia 12 需 AtomUI 6.x / Dock 12.x 才支持，属于后续升级项。
- **AtomUI 5.0.2 与 6.x 差异**：6.x（基于 Avalonia 12）提供 `UseDesktopControls` 等新 API；本阶段沿用 5.0.2 的 `UseOSSControls`。
- **尚未引入**：`Ursa.Avalonia`、`AvaloniaEdit`、`OxyPlot.Avalonia`、`Icons.Avalonia.FontAwesome` 等按阶段计划在对应阶段接入（部分包名/版本需在接入时复核）。

## 升级路径（后续可选）

当 Avalonia 12 生态成熟后，可将基座升级至 Avalonia 12.1.x + AtomUI 6.x + Dock 12.x，届时同步更新本表。
