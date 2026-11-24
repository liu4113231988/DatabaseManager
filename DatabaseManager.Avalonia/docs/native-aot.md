# Windows Native AOT 发布

Native AOT 的发布目标是 Avalonia 客户端的 Windows x64。正式的 Windows 和 Linux x64 自包含 CoreCLR 包继续使用现有发布流程。Linux AOT 不在本项目的支持范围内。

## 首阶段验收范围

从 Windows x64 的普通 SQLite 和 SQLCipher 文件开始，覆盖连接、对象树、表与列元数据、表设计、查询、配置读取以及导入导出。每项都使用发布后的 EXE 验证。P0、P2 隔离冒烟和控制台回归是这一阶段的基本检查。其他数据库按驱动和认证方式逐项验证后纳入支持范围。

初始试验生成了 Windows x64 原生 EXE。P0 通过，P2 的查询构建器未能取得字段。独立探针确认，同一条 SQLite 查询使用 ADO.NET 可以返回结果，Dapper `QueryAsync<T>` 的对象映射在 AOT 下会因运行时代码生成抛出 `PlatformNotSupportedException`。

## 本地实验发布

在安装了 .NET 10 SDK 10.0.404 或更新的维护版本和 Visual Studio C++ x64 工具链的 Windows 上，从仓库根目录运行下面的命令。

```powershell
dotnet publish DatabaseManager.Avalonia/DatabaseManager.Avalonia/DatabaseManager.Avalonia.csproj `
  -c Release -r win-x64 -p:DatabaseManagerNativeAot=true -p:UsedAvaloniaProducts= `
  -warnaserror:NU1903,NU1904
```

本地输出默认位于仓库根目录的 `bin\win-x64-aot`。更新前关闭该目录中正在运行的客户端。首次启动会在 EXE 旁创建 `Profiles`，同目录后续发布可继续使用原连接配置。

`.github/scripts/Test-NativeAot.ps1` 接收干净的发布目录。脚本从独立工作目录启动发布后的 EXE，检查 P0/P2 的退出码与成功标记，并输出截图目录。验证已使用的本地包时，可把程序文件和 `Config` 复制到临时目录运行脚本。CI 在变更检查中验证发布目录，在正式发布前验证解压后的 ZIP。

旧 SDK 10.0.402 默认使用存在 CVE-2026-111497 的 Native AOT 编译器 10.0.13。更新 SDK 后需重新发布原生 EXE。[Microsoft 安全公告](https://github.com/advisories/GHSA-p3jp-9v83-76w9)

## 当前兼容性

| 路径 | Windows x64 Native AOT 验收状态 |
| --- | --- |
| SQLite / SQLCipher | P0/P2 使用临时数据库验证配置、查询、对象元数据、表设计和文件往返。SQLite 另验证相同表结构与索引列差异。 |
| SQL Server | 使用 SQLite 模拟目录验证登录与分区元数据映射。本机 LocalDB 测试实例启动失败，真实服务器、Windows 集成认证和云认证仍待验收。 |
| PostgreSQL 18 | 独立原生探针在本机临时实例通过 P0/P2 数据库回归，覆盖多结果集、事务、执行计划、元数据、二进制编辑和导入导出。使用本机信任认证，其他认证方式待验收。 |
| MySQL / Oracle | 驱动已打包。真实实例及各类认证待验收。Oracle 的可选云配置、令牌和 Kerberos 路径在原生编译中被报告为缺少相应程序集。 |
| DuckDB / 达梦 / KingbaseES / Access 和 ODBC | 当前原生包未包含这些驱动。相应操作可使用 CoreCLR 包。 |

## 改造进度

SQLite 表、视图、列、触发器和键的元数据，以及本地连接和账号配置，已改用显式 ADO.NET 映射。工作台预览改用有类型的单元格模板，工作台和仪表盘的设置逐步改用源生成 JSON。控制台回归覆盖 SQLite 元数据、连接配置往返和旧版仪表盘 JSON。

2026-11-19，Windows x64 Release 构建和控制台回归通过。普通构建和 Native AOT 发布产物均通过 P0、P2 隔离冒烟，覆盖加密 SQLite 连接表单及 SQLite 查询构建、数据生成、质量剖析、数据字典、脱敏和仪表盘。

扩展后的 P2 在普通 SQLite 和 SQLCipher 临时数据库中验证新建表、预览和执行加列 DDL、保留原有数据，以及 CSV、Excel、JSON、XML 的导出、预览和重新导入。CSV 覆盖视图导出，SQL 覆盖导出的 INSERT 脚本回放。验证从独立工作目录运行原生 EXE，并检查加密数据库文件头。表设计和文件操作使用发布目录的配置资源，Excel 行读取采用静态类型映射。

SQL 转换器按映射文件中的名称直接构造三种内置日期函数翻译器。Windows x64 原生 P2 已将 SQL Server 的 `DATEADD`、`DATEDIFF` 和 `DATEPART` 转为 SQLite 表达式，并在临时 SQLite 库中核对了闰日加法、跨月天数和年份。源端转换只读取方言配置，无需连接 SQL Server。

2026-11-20，调度计划文件读写、窗口编辑副本和提交执行时的快照改用源生成 JSON。Windows x64 原生 P0 已验证旧格式计划的编辑、保存和重新加载，保留嵌套步骤、字典对象、禁用状态和上次运行结果。普通控制台回归另行验证执行中的批处理使用提交时的步骤快照。

SQL 编辑器的“复制为 JSON 字符串”使用 `JsonEncodedText` 转义选区。Windows x64 原生 P0 验证了引号、反斜杠、换行、制表符、中文和表情字符的往返，以及空选区的处理；测试在剪贴板写入前核对结果。

已移除按类名反射创建视图的全局 `ViewLocator`。查询页签继续使用主窗口声明的模板，原生 P0 验证切换页签后可见编辑器绑定到所选页签并显示对应 SQL。从独立工作目录运行原生 P0/P2 时，`smoke.log` 写入 EXE 所在目录。

2026-11-20，SQL Server 的登录元数据（数据库、Schema）和分区元数据（分区表、方案、函数、文件组、边界值）查询改为显式 ADO.NET 行映射与 `DbParameter` 列表参数，移除 `Dapper` 依赖；前者供对象树切换数据库使用，后者供表设计器分区 Tab 使用。原生 P0 在 SQLite 模拟的 `sys` 目录上验证两类查询的行映射、参数展开与排序，覆盖 `schema_name()` 兼容函数、分区方案的文件组顺序和分区函数边界值。发布日志中的 IL 告警从 296 条降至 272 条。

启动排查：`StartupProbe` 显示全部启动阶段正常（约 7.9 秒进入 UI idle），原生 EXE 的主窗口可显示并响应。历史错误日志中的 `profiles.db3 already exists` 来自 `File.Exists` 与 `File.Copy` 之间的并发竞态，已改为捕获该特定 `IOException` 并在目标文件已存在时继续，避免冒烟与正式入口同时初始化时卡在启动错误窗口。

SSH 隧道配置改用源生成 JSON（`SshProfileJsonContext`），去除启动路径的 Newtonsoft 反射反序列化并缓存重复读取；`Secret` 使用 `System.Text.Json.Serialization.JsonIgnore` 保证明文密码不落盘。全量 AOT 发布的 IL 告警从 272 条降至 251 条，原生 P0/P2 与独立工作目录的主窗口启动验证通过。

继续排查剩余告警：应用设置与连接视觉标注分别迁移到 `AppSettingsJsonContext` 和 `ConnectionVisualJsonContext`，保留旧文件的大小写不敏感、注释与尾逗号兼容；JSON 数据导入导出改用 `JsonDocument` / `Utf8JsonWriter`，维持对象数组、`{"rows":[...]}` 包装、null、数值/布尔与 Base64 二进制兼容。新增 `StartupConfigJsonChecks` 与 `SshProfileStoreChecks` 覆盖旧格式、字段往返和 Secret 隔离。执行计划解析与 JSON 查看器改用 `JsonDocument`/`Utf8JsonWriter`，P0 在原生 EXE 中验证 PostgreSQL 计划节点解析和 JSON 树构建。AOT 配置固化 `AvaloniaUseCompiledBindingsByDefault=true`，避免 XAML 运行时反射绑定。发布时仍建议显式传入 `-p:UsedAvaloniaProducts=` 以跳过未使用的产品程序集分析；项目文件内空值属性不能覆盖 SDK 默认值，命令行显式空参数才有效。脚本库、任务历史、模板存储与模板导入导出信封改用源生成 JSON（`ScriptLibraryJsonContext`、`TaskHistoryJsonContext`、`TemplateJsonContext`），保留大小写不敏感、注释与尾逗号兼容；连接配置导出/预览/导入的深拷贝与加密信封改用 `ConnectionTransferPayloadContext` / `ConnectionTransferEnvelopeContext`，既有回归继续验证口令、SSH Secret、篡改检测与回滚；AI SQL 助手的请求体与响应解析改用 `Utf8JsonWriter` / `JsonDocument`。数据编辑路径的 `Dictionary<..., dynamic>` 与 `newValue` 改为 `object`（SQL Server / MySQL / Postgres / Oracle 及共享 `DataTableHelper`），PostgreBulkCopy 与 ANTLR 规则分析改用具体类型，脚本 token/语句提取器改用 `IEnumerable` 显式遍历；数据库属性窗口恢复编译绑定并补充 `x:DataType`，主窗口两处相对源绑定因编译期上下文无法静态解析而保留 `ReflectionBinding`。主窗口两处相对源绑定通过 `#RootWindow.ObjectsExplorer` 强类型包装属性和 `Style x:DataType` 改为编译绑定；结果快照、数据对照、执行计划和导入预览的只读动态列改用 `FuncDataTemplate` 直接读取索引值；冒烟工具窗口工厂改为显式无参构造，去掉 `Activator`/`GetConstructors`；定时任务连接克隆、外键元数据克隆和复制为 JSON 改用无反射实现。主查询结果网格改用 `DataGridTemplateColumn + FuncDataTemplate<QueryResultRow>`，`TextBox.LostFocus`/Enter 显式写回 `QueryResultRow.SetValue`，保留脏列与行状态标记，P0 断言结果列均为模板列。以统一口径（含 IL2104/IL3000 等）统计：全量 AOT 告警从 96 条降至 58 条、32 条，再降至 19 条，原生 P0/P2 均通过。后续批次将 `SchemaInfoHelper`/`ObjectHelper` 大对象深拷贝、`SettingManager`/`ScriptRunner` 配置与分析器读取、`SqlServerInterpreter` 数组写值、表设计器属性复制改为源生成 JSON 或无反射显式映射；查询结果也改为按 `IDataReader` 显式建列填行，移除 `DataTable.Load` 的动态代码告警。AOT 构建通过 `DATABASE_MANAGER_AOT` 条件化移除尚未验收的 DuckDB、达梦、KingbaseES/Kdbndp、ODBC 驱动以及 Avalonia 侧未启用的 MiniProfiler，相关路径在原生预览中给出明确不支持提示；普通构建仍保留这些依赖。剩余告警均为第三方程序集级汇总（Oracle / SqlClient / Microsoft.SqlServer.Types / Dapper / CompareNETObjects / MiniExcel / Avalonia DataGrid / System.Configuration.ConfigurationManager）及 Oracle 的 `Assembly.Location` 使用，按真实数据库路径另行评估，不机械抑制。

共享查询映射已移除 Dapper，按当前结果集的列名和列序号构建映射。诊断结果表格使用显式列和编译绑定。原生发布仍报告 Oracle、SqlClient、CompareNETObjects、MiniExcel、Avalonia DataGrid 等依赖中的裁剪和动态代码诊断。其他表结构变更、文件格式边界和认证方式仍待单独验收。

2026-11-22，使用 .NET SDK 10.0.404 和 ILCompiler 10.0.15 发布 Windows x64 原生 ZIP，从解压目录运行的 P0/P2 均通过。发布日志保留 17 条第三方 IL 告警。常规 Release 构建和控制台回归通过，并复验了 Windows x64 独立还原、错误 RID 拒绝和模板缺失时的冒烟失败退出。

## 实施顺序

1. 将 AOT 设为 Avalonia 入口的独立构建选项，仅还原和发布 `win-x64`。先改造 SQLite 的元数据及本地连接配置路径。
2. 迁移运行时反射绑定、视图和 SQL 转换器工厂。将持久化的数据结构迁移到源生成 JSON，并验证既有配置和导入文件。
3. 逐项验证数据库驱动、认证方式和导入导出依赖。确认功能覆盖并处理涉及实际调用路径的裁剪和动态代码警告后，再考虑正式发布。

参考 [Avalonia Native AOT 指南](https://docs.avaloniaui.net/docs/deployment/native-aot) 和 [.NET Native AOT 部署](https://learn.microsoft.com/dotnet/core/deploying/native-aot/)。
