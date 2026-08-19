# Avalonia 迁移进度记录

> 对应 `avalonia-migration-detailed-plan.md`，记录各阶段的实际完成进度。
> 新项目位于 `DatabaseManager.Avalonia/`，与原 WinForms 版（`DatabaseManager.CoreApp`）并存。

## 总体状态

| 阶段 | 内容 | 状态 | 里程碑 |
|:---:|------|:---:|------|
| 0 | 环境与骨架 | ✅ 已完成 | M0 跨平台空窗口 |
| 1 | 连接管理 + 主框架 | ✅ 已完成（核心）/ Dock 停靠待实机 | M1 连接可用、布局停靠 |
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

## 阶段 1：连接管理 + 主框架 ✅（核心）

> 里程碑 M1：连接可用、主框架（菜单/工具栏）就绪。
> 完整拖拽/浮动 Dock 停靠布局为本阶段剩余的进阶项，受限于当前无 GUI 环境，留待实机接入（见下文说明）。

**已完成内容**

1. **连接服务增强**（`DatabaseManager.AppCore/Services`）
   - `IDbConnectionService` 扩展为完整 CRUD + 连接测试：`GetConnections` / `GetConnectionById` / `TestConnectionAsync` / `SaveAsync` / `DeleteAsync` / `IsNameExistedAsync`。
   - `ProfileDbConnectionService` 复用 `DatabaseManager.Profile`（`ConnectionProfileManager` / `AccountProfileManager`）与 `DatabaseInterpreter`，连接测试通过 `DbInterpreterHelper.GetDbInterpreter(...).GetDatabasesAsync()` 完成。
   - 新增 `Models/ConnectionItem` 领域模型（UI 无关连接抽象）。
2. **连接管理 ViewModel**（`ViewModels/ConnectionManagerViewModel.cs`）
   - 连接列表刷新、数据库类型切换、新建连接项、连接测试、保存、删除、名称唯一性校验。已注册进 DI。
3. **连接管理 UI**（`DatabaseManager.Avalonia/Views`）
   - `ConnectWindow`：对应原 `frmDbConnect`/`frmAccountInfo`。填写数据库类型 / 连接名称 / 服务器 / 端口 / 认证方式 / 用户名 / 密码 / DBA / SSL / 数据库，支持“测试连接”拉取数据库列表并校验名称唯一性后保存。
   - `ConnectionManagerWindow`：对应原 `frmDbConnectionManage`。按数据库类型列出连接，支持新增 / 编辑 / 删除 / 刷新。
4. **主框架**（`MainWindow`）
   - 顶部标题栏 + **菜单栏**（文件 / 连接 / 视图 / 帮助）+ **工具栏**（新建连接 / 连接管理）。
   - 主内容三栏：左“对象浏览器”（已保存连接列表 + 受支持数据库类型） / 中内容区（`TabControl` 欢迎页，为阶段 2 查询/表设计预留） / 下结果区。
   - 菜单/工具栏打开连接管理窗口，保存后主界面自动刷新连接列表。
5. **启动初始化**：`App.OnFrameworkInitializationCompleted` 中调用 `ProfileBaseManager.Init()`（对应原 WinForms `Program.Main`），确保 profiles 数据文件就绪。

**验收验证**（Linux 无 GUI 环境）
- `dotnet build DatabaseManager.Avalonia.sln`（Debug / Release）✅ 0 错误。
- AppCore 冒烟测试：连接服务增删改查、名称唯一性、`ConnectionManagerViewModel` 均通过；受支持数据库类型枚举正常。

**遗留 / 说明**
- **完整 Dock 拖拽/浮动停靠布局**（`wieslawsoltes/Dock` 的 `FactoryBase` 需自建全部 concrete 模型类，工作量大）为本阶段进阶项，且需 GUI 实机验证，建议下一步迭代在图形环境下接入并回归。当前主框架以三栏 `Grid` + `TabControl` 提供等价的“左对象树/中内容/下结果”体验。
- 三平台 GUI 实际启动需在对应 OS 上验证。

---

*最后更新：阶段 1 完成时（核心）*
