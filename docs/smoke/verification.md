# 验证与冒烟说明

以下命令从仓库根目录运行，需要 .NET 10 SDK；UI 冒烟还需要桌面 GUI 环境。先构建 Release 客户端：

```powershell
dotnet build DatabaseManager.Avalonia/DatabaseManager.Avalonia.sln -c Release
```

## 控制台回归

回归项目未包含在 Avalonia 解决方案中，需要单独运行；`dotnet test` 不会执行其控制台断言：

```powershell
dotnet run --project DatabaseManager.Avalonia/DatabaseManager.AppCore.RegressionTests -c Release
```

成功输出 `All regression checks passed.`。其中 `QueryConnectionContextChecks` 验证标签连接、手动模式、提交/回滚、异步等待与分组断开；`SqlCompletionChecks` 验证 SQL 补全作用域及语句边界。

`SqlFormattingChecks` 验证编辑器与转换引擎共用 Hogimn 后的方言、注释/字面量保真、幂等性、原文回退与 MySQL 视图头及 JOIN 路径，并通过 SQLite 比较排版前后的实际结果，见 [格式化迁移说明](../../DatabaseManager.Avalonia/docs/sql-formatting.md)。

`SqliteEncryptionChecks` 使用临时 SQLCipher 4 格式文件验证密码、只读测试、事务、元数据和路径处理，以及错误密码/缺失文件的实际连接拒绝和禁止普通查询自动建库；说明与兼容边界见 [SQLite 加密访问](../../DatabaseManager.Avalonia/docs/sqlite-encryption.md)。P0 同时验证 SQLite 密码表单，截图为 `artifacts/p0-ui/sqlite-encryption.png`。

`PackageCompatibilityChecks` 验证升级后的 CSV/Excel 往返读写、表头/无表头、畸形 CSV 拒绝、Excel 批注、Word、ZIP、NCalc 及 DuckDB 原生库；包版本与框架兼容性见 [依赖升级说明](../../DatabaseManager.Avalonia/docs/dependencies.md)。

`ExcelMigrationChecks` 验证 MiniExcel 的 `.xlsx` 数据保真、导入预览、多工作表、全 NULL 记录、批注/高亮兼容及失败时保护原文件。设置 `DBM_TEST_LARGE_EXCEL=1` 额外检查 1,048,576 条数据按表头上限拆为两张工作表且无重复；2026-11-07 已执行并通过。边界见 [Excel 读写说明](../../DatabaseManager.Avalonia/docs/excel-io.md)。

2026-11-07 完全移除 NPOI 后，`ExcelMigrationChecks` 改用 Open XML SDK 构造独立输入并验证标准批注/高亮、VML 关系和最右列锚点；旧 `.xls` 在读取与预览入口均明确拒绝。`WordExportChecks` 校验 DOCX schema、标题/说明、动态与空表格、表头颜色/对齐、特殊字符/换行，以及非法数据不会破坏原文件。Word 候选评估见 [Word 导出说明](../../DatabaseManager.Avalonia/docs/word-export.md)。

测试会创建临时 SQLite 和测试配置。PostgreSQL 集成检查从环境变量 `DBSMOKE_CONNSTR` 读取测试连接串；未设置时跳过。数据库作业的临时库检查还需显式设置 `DBM_TEST_TEMP_DATABASES=1`。真实数据库检查使用专用测试实例与具备建表/建 Schema/建库等相应权限的测试账号，按启用的检查准备权限。

## 隔离 UI 冒烟

P0/P2 使用临时 SQLite 或合成元数据，无需真实服务器，不执行已保存连接的查询或调度任务：

```powershell
dotnet run --project DatabaseManager.Avalonia/DatabaseManager.Avalonia -c Release --no-build -- --smoke --p0
dotnet run --project DatabaseManager.Avalonia/DatabaseManager.Avalonia -c Release --no-build -- --smoke --p2
```

- P0：覆盖智能提示的实际键盘输入、异步字段补全、编辑器格式化的方言/选区/撤销、SQLite 密码表单、数据库下拉框、自动提交复选框绑定，以及结果/执行计划等窗口。输出到 `artifacts/p0-ui/`。
- P0 在临时目录读取旧格式的禁用计划，通过调度窗口改名保存，再重新读取文件，核对计划 ID、启用状态、步骤、字典对象和上次运行结果。
- P2：覆盖查询构建、测试数据、质量剖析、数据字典、脱敏和仪表盘，也在普通 SQLite 与 SQLCipher 文件中验证新建表、加列及 CSV/Excel/JSON/XML 往返和 SQL 脚本回放。输出到 `artifacts/p2-ui/`。
- P2 还按发布包内的函数映射转换 SQL Server 的 `DATEADD`、`DATEDIFF` 和 `DATEPART`，在临时 SQLite 库中执行并核对闰日加法、跨月天数和年份。
- 成功退出码为 0，输出目录包含当次 `passed.txt`；每次运行开始会清除旧标记。失败时检查应用输出目录的 `smoke.log`。截图不代表真实服务器或其他平台验收。

## 真实数据库截图入口

仅传 `--smoke` 会进入历史 PostgreSQL 截图流程，使用 `DBSMOKE_CONNSTR` 或代码中的本地默认连接，并可能保存测试连接配置。运行前准备专用测试环境。需要指定截图目录时使用 `--smoke-out <目录>`；已有截图索引见 [README.md](README.md)。该索引由程序生成，通用验证说明维护在本文件。

## 当前证据与测量

2026-11-06，SQLite 加密迁移、格式化迁移与保真/连接校验修复通过 Release 构建和完整回归。随后依赖统一升级的验收覆盖全仓库 19 个项目的编译、完整控制台回归、认证检查与 Windows P0/P2 隔离 UI 冒烟；主解决方案和旧客户端的 NuGet 已知漏洞审计（含传递依赖）为零。2026-11-07 移除办公文档旧依赖后，已不再产生原有的 NPOI 许可提示。

2026-11-13，模板 T2/T3、方言 DDL、KingbaseES E/F、表设计器分区/列内联编辑、SQLite BulkCopy 的 Release 构建（Any CPU 与 x64 均为 0 告警）、完整控制台回归、Windows P0/P2 隔离 UI 冒烟均通过。P0 已覆盖模板管理窗口的 SQL 片段与外置代码模板示例预览；P2 已覆盖数据字典在最小 DI 容器下回退内置模板。表设计器分区 Tab 与列选择器的完整人工交互、真实数据库分区元数据回放、KingbaseES 真实 V8 实例及 Linux/macOS 桌面仍未验收；`DBSMOKE_CONNSTR`、`DBSMOKE_KINGBASE_CONNSTR` 未设置时相关检查明确跳过。

智能提示的可选合成性能测量命令和适用范围见 [sql-completion.md](../../DatabaseManager.Avalonia/docs/sql-completion.md)；标签与事务规则见 [query-database-selection.md](../../DatabaseManager.Avalonia/docs/query-database-selection.md)。
