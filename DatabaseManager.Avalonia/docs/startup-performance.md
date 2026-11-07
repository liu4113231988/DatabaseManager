# 发布版启动性能

> 当前发布策略（v1.1.3，2026-11-07）：Windows/Linux x64 改为自包含，无需安装 .NET；继续关闭 ReadyToRun 和裁剪。最新体积与裁剪实验见 [发布体积评估](release-size.md)。下文保留 2026-11-01 的历史测量，其中不携带运行时的命令和大小仅适用于当时版本。

2026-11-01，针对直接启动 Windows EXE 的等待时间优化：

- 最初试验启用 `PublishReadyToRun`，提前编译托管程序集以减少 JIT 工作。下表保留该次历史对照；目前已关闭 ReadyToRun，改用下文的 UI 启动链路优化，不启用裁剪。参见 [Microsoft ReadyToRun 文档](https://learn.microsoft.com/en-us/dotnet/core/deploying/ready-to-run)。
- 本地 Profile 库直接创建 SQLite 连接，复用已有连接串构造器，避免经过包含其他数据库驱动的通用工厂。配置升级、凭据保护和数据访问逻辑不变。
- 查询标签的数据库列表复用 `MainWindowViewModel.Connections`，切换标签或打开下拉框不再重新读取全部配置；显式刷新连接时仍重新加载。

## 测量

环境：Windows x64、.NET 10；本地空连接配置，恢复一个查询标签，无数据库网络连接。
临时控制台宿主使用与正式入口相同的 `AppBuilder.Configure<App>().UseAtomUIPlatformDetect().WithAtomUIDefaultOptions()`；
在 `AfterSetup` 中向 UI 线程投递 `DispatcherPriority.Background` 回调，确认主窗口及查询标签已初始化后结束计时并退出。
计时从托管入口开始，到主窗口首轮 UI 空闲为止，不包含进程创建和运行时加载。

同一份优化后源码分别发布 IL 和 ReadyToRun 包，停止构建后交替启动，各预热一次，再测三次：

| 发布方式 | 第 1 次 | 第 2 次 | 第 3 次 | 中位数 |
| --- | ---: | ---: | ---: | ---: |
| IL | 3475.9 ms | 3460.3 ms | 3645.7 ms | 3475.9 ms |
| ReadyToRun | 1898.8 ms | 1544.2 ms | 1842.9 ms | 1842.9 ms |

该场景中位数缩短约 47%。测试发布目录约由 218 MB 增至 350 MB；预编译也增加发布耗时。
这些结果是文件缓存预热后的比较，不代表重启系统后的冷启动或有大量会话草稿时的耗时。

单独测量配置读取路径，Profile 初始化由约 210 ms 降至约 56 ms；连续刷新数据库列表 30 次由约 105 ms 降至不足 1 ms。

## 发布与验证

从仓库根目录发布 Windows 测试包（默认关闭 ReadyToRun，不携带 .NET 运行时；运行前需安装 .NET 10 x64 运行时）：

```powershell
dotnet publish DatabaseManager.Avalonia/DatabaseManager.Avalonia/DatabaseManager.Avalonia.csproj -c Release -r win-x64 --self-contained false -p:UsedAvaloniaProducts= -o .artifacts/publish-fdd-no-r2r
```

需要重新对照 ReadyToRun 时，在同一命令增加 `-p:PublishReadyToRun=true` 并使用不同输出目录。Linux 发布同样关闭预编译，本轮未运行 Linux 包。

验证通过：Release 解决方案构建；单独构建并运行回归项目，输出 `All regression checks passed.`；直接运行预编译 `DatabaseManager.exe --smoke --p0`，退出码为 0。
新增确定性检查覆盖：数据库列表不重复读取配置、对象树新增数据库仍可刷新、显式刷新连接仍重新加载，以及 SQLite 配置连接对含分号/中文路径的读写。
PostgreSQL 集成检查因未设置 `DBSMOKE_CONNSTR` 跳过。

## 不携带运行时与裁剪检查

项目默认设置 `SelfContained=false`，CI 的 Windows/Linux 发布命令同步显式指定 `--self-contained false`，并检查输出中没有 `coreclr.dll` / `libcoreclr.so`。上表的 218/350 MB 是此前自包含发布的测量，不能作为当前发布包大小。

此前 Windows x64 不携带运行时的发布成功，保留 ReadyToRun 时目录约 273.2 MB（相比此前约 350 MB 减少约 22%）。检查确认没有 `coreclr.dll`、`hostfxr.dll`、`hostpolicy.dll` 或 `System.Private.CoreLib.dll`，`DatabaseManager.runtimeconfig.json` 引用共享 `Microsoft.NETCore.App 10.0.0`。直接运行该包的 `--smoke --p0` 与 `--smoke --p2` 均以退出码 0 完成。Linux 发布命令已同步，但未在本机生成或运行 Linux 包。

裁剪试验仅在主程序临时设置 `PublishTrimmed=true`，保留 `--self-contained false` 并关闭 ReadyToRun，以独立验证裁剪。当前 .NET SDK 10.0.402 / ILLink 10.0.13 拒绝该组合，报错 `NETSDK1102`（必须发布为自包含应用）；最终配置未启用裁剪。参见 [Microsoft 裁剪部署文档](https://learn.microsoft.com/en-us/dotnet/core/deploying/trimming/trim-self-contained)。

试验构建还产生 10 条 IL2026/IL2087 警告：包括工作台/结果表格的反射绑定、冒烟窗口的动态构造，以及 SQL 编辑器的反射 JSON 序列化。`ViewLocator` 也明确标记 `RequiresUnreferencedCode`，按类型名称反射创建视图。因此即使未来改回自包含发布，也需要先处理并验证这些路径，不能仅打开裁剪开关或忽略警告。

裁剪试验日志保存在本地忽略目录 `.artifacts/trim-check-fdd.log` 和 `.artifacts/trim-check-fdd-result.log`；未发布裁剪试验产物。

## 关闭 ReadyToRun 后的分段优化

2026-11-01，先关闭 ReadyToRun，在不携带运行时的 Windows x64 包上增加 `startup|...` Trace 检查点。只记录阶段名，不写入 SQL、连接配置或凭据。基线发现最大的可优化开销是 AtomUI 在 `App.Initialize()` 中的主题注册：最初三次预热后测量约 1.4～1.9 秒；服务解析与会话恢复各约 0.1～0.3 秒。

采用 AtomUI 6.2.7 的编译主题、按需资源装配与启动准备流程，将注册从 `Application.UseAtomUI` 迁到正式入口的 `AppBuilder.UseAtomUI`。Avalonia 基础包按该版本要求升到 12.1.4，DataGrid 保持可用的 12.1.2。没有把应用的连接读取、会话恢复或调度初始化推迟到主窗口就绪之后。参见 [AtomUI 启动迁移说明](https://github.com/AtomUI/AtomUI/blob/v6.2.7/docs/releases/6.2.6-api-changes.md) 与 [6.2.7 修正说明](https://github.com/AtomUI/AtomUI/blob/v6.2.7/docs/releases/6.2.7-api-changes.md)。

停止构建后，用同一配置（空连接、一个查询标签、1200×750 窗口），交替启动旧/新版本各三次。两个版本均关闭 ReadyToRun，丢弃此前的预热运行：

| 托管入口 → 主窗口首轮 UI 空闲 | 第 1 次 | 第 2 次 | 第 3 次 | 中位数 |
| --- | ---: | ---: | ---: | ---: |
| 基线：AtomUI 6.1.9 / Avalonia 12.1.2 | 4255.84 ms | 3258.91 ms | 3588.74 ms | 3588.74 ms |
| 优化：AtomUI 6.2.7 / Avalonia 12.1.4 | 2386.11 ms | 2395.54 ms | 2403.18 ms | 2395.54 ms |

窗口就绪中位数缩短约 33%。主窗口 `OnOpened` 完成时间中位数也由 3429.91 ms 降至 2241.67 ms。

| 阶段区间中位数 | 基线 | 优化 |
| --- | ---: | ---: |
| 框架、资源与主题准备（入口到 `App.Initialize` 结束） | 2121.3 ms | 879.7 ms |
| 主窗口 XAML 构建 | 288.2 ms | 350.6 ms |
| 窗口构建后服务解析及图标加载 | 127.1 ms | 125.4 ms |
| 连接列表读取 | 63.4 ms | 61.3 ms |
| 会话恢复 | 245.9 ms | 248.7 ms |

阶段表不包含全部窗口显示、布局和渲染区间，不能直接相加得到总时间。新版主题准备包含启动期间的并行工作和按需装配，所以不把旧的 `Application.UseAtomUI` 区间消失单独算作收益，最终以窗口已完整初始化后的 UI 空闲时间比较。以上均为缓存预热后的本机结果，未测系统重启后的冷启动。

可重复测量工具（执行过程中自动关闭自己的窗口，配置位于其输出目录的 `Profiles`，不会自动执行 SQL）：

```powershell
dotnet build DatabaseManager.Avalonia/tools/StartupProbe/StartupProbe.csproj -c Release
dotnet DatabaseManager.Avalonia/tools/StartupProbe/bin/Release/net10.0/StartupProbe.dll
```

工具复用正式 `BuildAvaloniaApp()`，输出累计毫秒 JSON，并自检所有可恢复草稿的数量、SQL、连接名和数据库名。比较时先构建完成，再启动多个独立进程并舍弃首轮；不要用包含编译耗时的 `dotnet run` 做启动计时。基线与优化后的原始检查点保存在本地 `.artifacts/startup-no-r2r-comparison.json`。

验证通过：升级后的回归套件输出 `All regression checks passed.`；不带 ReadyToRun 的发布包 P0/P2 UI 冒烟均退出 0；诊断工具自检一个默认标签，以及深色主题下 8 个 SQL 草稿和各自执行上下文的完整恢复。PostgreSQL 与 Linux 仍未实测。

最终 Windows 包位于本地 `.artifacts/publish-fdd-no-r2r`，目录约 144.8 MB；检查主程序集 PE 的 `ManagedNativeHeaderDirectory.Size=0`，确认没有发布预编译代码，且未携带 `coreclr.dll`。在该最终目录复用正式入口的启动自检通过，诊断宿主文件随后移除，不随包分发。
