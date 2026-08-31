# KingbaseES A–C 测试证据

## 来源与用户旅程

来源为 `docs/kingbasees-support-plan.md` 的 A、B、C 阶段。

- 作为数据库管理员，我可以把 KingbaseES 保存为独立连接类型，并使用供应商驱动连接 PG 兼容实例。
- 作为用户，我可以在连接时标记实例兼容模式；未验证的 Oracle、SQL Server 模式不会被误当作 PostgreSQL 执行。
- 作为用户，我可以在已验证的 PG 兼容路径上浏览 Schema，并查看只读 SQL 的执行计划/剖析结果。

## RED / GREEN 记录

| 行为 | RED 证据 | GREEN 证据 | 保证 |
|---|---|---|---|
| 兼容模式安全边界 | `dotnet build DatabaseManager.AppCore.RegressionTests... --no-restore`：`KingbaseCompatibilityModes` 不存在，4 个预期编译错误 | `dotnet run --project DatabaseManager.AppCore.RegressionTests... --no-restore`：`All regression checks passed.` | `postgres` 规范化为 `Postgres`，SQL Server 模式返回禁止连接提示。 |
| 剖析/计划 PG 路径 | `dotnet run --project DatabaseManager.AppCore.RegressionTests... --no-restore`：`KingbaseES PG 路径应提供 EXPLAIN ANALYZE` 失败 | 同一命令输出 `All regression checks passed.` | KingbaseES 生成 `EXPLAIN ANALYZE`，执行计划服务走 PG 兼容语法。 |
| Avalonia 连接 UI | 不适用：当前轻量回归项目不承载 Avalonia 自动化 | `dotnet build DatabaseManager.Avalonia/DatabaseManager.Avalonia/DatabaseManager.Avalonia.csproj -nologo --no-restore`：0 errors | 兼容模式控件、持久化和连接前安全校验可编译接入客户端。 |

## 覆盖与已知缺口

项目当前没有可用于该桌面 UI 的覆盖率命令，因此未报告覆盖率比例。已有回归程序覆盖无数据库的驱动注册、连接串、兼容模式和 SQL 生成。

真实 KingbaseES V8（PG 兼容）实例尚未提供，故连接、Kdbndp 异步行为及 catalog 查询未做集成验证；Oracle 和 SQL Server 兼容模式被有意禁用，待独立验证后再开放。
