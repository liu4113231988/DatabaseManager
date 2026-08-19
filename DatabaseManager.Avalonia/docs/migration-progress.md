# Avalonia 迁移进度记录

> 对应 `avalonia-migration-detailed-plan.md`，记录各阶段的实际完成进度。
> 新项目位于 `DatabaseManager.Avalonia/`，与原 WinForms 版（`DatabaseManager.CoreApp`）并存。

## 总体状态

| 阶段 | 内容 | 状态 | 里程碑 |
|:---:|------|:---:|------|
| 0 | 环境与骨架 | ✅ 已完成 | M0 跨平台空窗口 |
| 1 | 连接管理 + 主框架 | ⬜ 待执行 | M1 连接可用、布局停靠 |
| 2 | 对象浏览 + 查询 | ⬜ 待执行 | M2 浏览/查询/脚本 |
| 3 | 数据编辑 + 表设计 | ⬜ 待执行 | M3 数据编辑/表设计 |
| 4 | 转换/对比/诊断 | ⬜ 待执行 | M4 转换对比诊断 |
| 5 | 统计/备份/图表/生成 | ⬜ 待执行 | M5 长尾功能 |
| 6 | 导入导出 + 收尾 | ⬜ 待执行 | M6 全功能对齐 |
| 7 | 跨平台 + 发布 | ⬜ 待执行 | M7 三平台发布包 |

---

## 阶段 0：环境与骨架 ✅

**已完成内容**

1. **独立解决方案**：`DatabaseManager.Avalonia.sln`，含两个项目：
   - `DatabaseManager.AppCore`（net8.0，UI 无关业务层）
   - `DatabaseManager.Avalonia`（net8.0，Avalonia UI 层）
2. **核心库复用**：AppCore 通过 `ProjectReference` 引用 7 个原核心库：
   - `DatabaseInterpreter.Core` / `.Model` / `.Utility`
   - `DatabaseConverter.Core`
   - `DatabaseManager.Core` / `.FileUtility` / `.Profile`
3. **NuGet 依赖**（版本见 [package-versions.md](./package-versions.md)）：
   - 框架：Avalonia 11.3.20 + 配套
   - MVVM：CommunityToolkit.Mvvm 8.4.2
   - DI：Microsoft.Extensions.DependencyInjection 8.0.1
   - 主题：AtomUI 5.0.2（Ant Design 风格）
   - 停靠：Dock.Avalonia 11.3.12.1
   - 消息框：MessageBox.Avalonia 3.3.1.1
4. **架构骨架**：
   - AppCore：`ViewModelBase`、5 个 Service 接口 + 默认实现（连接/Schema/查询/转换/导入导出）、`ServiceCollectionExtensions.AddAppCore()`
   - UI：`Program.cs`（AppBuilder + ReactiveUI）、`App.axaml.cs`（AtomUI 主题 + DI 容器）、`ViewLocator`
   - `MainWindow`：现代三栏布局骨架（左对象树 / 中内容区 / 下结果区），为阶段 1 Dock 停靠布局预留结构
5. **验收验证**（Linux 环境）：
   - `dotnet build DatabaseManager.Avalonia.sln` ✅ 0 警告 0 错误
   - AppCore 运行验证：成功枚举 `DatabaseType`（SqlServer, MySql, Oracle, Postgres, Sqlite），连接服务 / 转换器 / 导出格式均可调用

**遗留 / 说明**
- 三平台 GUI 实际启动需在对应 OS 上验证（当前环境无 GUI）。
- Dock 完整停靠布局（拖拽/浮动）留待阶段 1 接入。
- `Ursa.Avalonia`、`AvaloniaEdit`、`OxyPlot.Avalonia`、`Icons.Avalonia.FontAwesome` 待对应阶段接入（注意：`AvaloniaEdit` 当前仅有 0.10.x 版本，需确认 Avalonia 11 兼容方案）。

---

*最后更新：阶段 0 完成时*
