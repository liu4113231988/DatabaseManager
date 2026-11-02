# SQL 查询数据库选择：调研、计划与实施

## 调研结论

SSMS 的 SQL 编辑器工具栏提供 Available databases，用于切换同一服务器中的数据库。执行按钮与 F5 执行选中的 SQL；没有选区时执行全文。[微软官方文档](https://learn.microsoft.com/en-us/ssms/f1-help/database-engine-query-editor-sql-server-management-studio)。本功能沿用这些交互，并保留项目现有的多查询标签。

PostgreSQL 一个连接只能访问一个数据库，因此不能通过添加通用 USE 语句实现切库；应当用目标数据库建立连接。[PostgreSQL 官方说明](https://www.postgresql.org/docs/16/manage-ag-overview.html)。MySQL 的 USE 只改变当前会话的默认数据库，[MySQL 官方说明](https://dev.mysql.com/doc/refman/8.4/en/use.html)。项目自动提交查询原本就为每次执行创建连接，因此使用连接配置中的数据库覆盖参数即可同时满足这些引擎，无需新增连接池或长期会话。

代码链路为 MainWindow 的执行按钮/编辑器快捷键 → QueryTabViewModel.ExecuteWithSqlAsync → IQueryService.ExecuteAsync → DefaultQueryService.CreateInterpreter。查询服务已经接收 databaseName；查询历史、结果元数据和数据保存也已使用标签的 DatabaseName。原界面在两个执行入口把对象浏览器的 CurrentDatabase 写回标签，导致跨标签浏览对象后可能执行到别的数据库。会话恢复原本就保存 DatabaseName，无需新增配置格式。

手动事务按连接名共享持久连接，之前执行时虽然创建了目标数据库的 interpreter，实际却直接使用事务连接。数据库不同会被悄悄忽略，所以必须在共享查询服务中拒绝目标库与事务连接不一致的执行。

## 实施计划及结果

1. 在每个 SQL 标签状态条增加数据库下拉框，按该标签的连接读取对象树已有的数据库节点；连接加载完成、标签激活和下拉框打开时同步列表，去重排序并保留当前选择。对象树刷新后重新打开下拉框即可看到新列表。
2. 查询标签独立保存连接和数据库。浏览对象树只更新浏览上下文；执行和“在新标签执行”使用当前查询上下文。连接/重连只为尚未绑定连接的查询标签初始化上下文。
3. 执行、保存、未保存结果修改和活动事务期间拒绝切库，并给出中文提示。切库保留旧结果供查看，但撤销其编辑能力，重新查询后再按目标库加载可编辑元数据。
4. 服务层校验活动事务连接的真实数据库，避免其他执行调用绕过界面保护。
5. 在现有回归与隔离 UI 冒烟框架增加检查，不增加依赖。

## 数据库范围与边界

### 多连接标签的目标连接

打开连接只更新对象浏览器；已经绑定的查询标签保留自己的连接和数据库。仅在连接成功后，为发起连接时尚未绑定的标签初始化上下文；新建查询继承对象浏览器的连接和数据库。标签头悬停可查看连接名。

事务工具栏随当前查询标签更新状态，刷新显示不会触发自动提交。执行计划也使用当前标签的连接与数据库快照，避免浏览 B 时误用 B 分析 A 的 SQL。显式打开 A 的查询时，缺省数据库取 A 的上下文，不从 B 的浏览上下文继承。断开连接保留已有标签的绑定，需要重连原连接才能继续执行。

`QueryConnectionContextChecks` 使用两个临时 SQLite 文件验证标签执行目标、工具上下文、事务切换不提交、独立回滚及断开连接的隔离行为。同一连接下多个标签仍共享事务，尚未改成每标签独立会话。

提交模式由查询服务按连接保存。关闭自动提交只进入手动模式，下一次交互查询按标签数据库开启事务；显式“开始事务”也使用标签数据库。提交或回滚结束当前事务后仍保持手动模式，下次查询重新开启事务。新标签可以初始化数据库；已有标签在活动事务期间仍禁止切库。

同一连接的交互查询、事务操作与断开通过异步锁串行执行，不同连接可并行。启用自动提交会异步提交已有事务，等待期间禁用事务控件，失败显示状态且不启用自动提交。断开立即拒绝新查询，等待当前操作结束后回滚；“断开全部”包含分组内连接。后台独立查询仍不加入交互事务。

回归增加：正常标签执行路径下的手动提交/回滚、兄弟标签开启事务后提交、异步模式切换期间切换连接、排队查询取消、断开等待与回滚、分组断开以及断开后的模式切换失败。PostgreSQL 可选检查将配置默认库改成不存在的库，验证工具栏仍按标签指定库开启事务。

上述修复的最终 Release 解决方案构建通过（0 错误，91 个已有警告），完整回归通过，P0 界面冒烟通过并实际切换自动提交复选框。未配置 `DBSMOKE_CONNSTR`，PostgreSQL 实库检查跳过。

2026-11-02：Release 构建通过；完整回归输出 `All regression checks passed.`；隔离 P0 UI 冒烟退出码为 0。未设置 `DBSMOKE_CONNSTR`，真实 PostgreSQL 检查按约定跳过。

| 数据库 | 行为 |
| --- | --- |
| SQL Server、MySQL | 下拉选择数据库，后续自动提交查询用目标库连接执行 |
| PostgreSQL、KingbaseES | 下拉选择数据库，后续查询建立目标库连接，不使用 USE |
| Oracle、DM | 数据库节点涉及用户 Schema，暂不提供数据库切换 |
| SQLite、DuckDB | 文件/catalog 与服务器数据库语义不同，暂不提供数据库切换 |

下拉框选择默认执行上下文，不改写用户 SQL；SQL 内显式写出的跨库对象名或 USE 仍由数据库执行。自动提交模式每次执行独立连接，SQL 内 USE 不会自动更新下拉框或改变下一次执行的选择。目标数据库权限或可用性由实际执行反馈错误，不自动退回其他数据库。

手动事务仍按连接名共享；本次增加防错检查，没有引入每标签长期会话。活动事务期间新库执行会被拒绝，需要先提交或回滚。数据库列表复用对象树，不额外发起全服务器元数据查询。

## 验证

2026-10-02 验证结果：Release 构建通过（0 错误，76 警告）；回归运行输出 `All regression checks passed.`；隔离 UI 冒烟通过，实际验证下拉框初始显示、选择写回标签，以及拒绝切换后恢复正确显示。未配置 `DBSMOKE_CONNSTR`，PostgreSQL 集成检查跳过。

![SQL 标签数据库选择](smoke/query-database-selection.png)

- Release 解决方案构建与回归可使用已还原依赖运行：`dotnet build DatabaseManager.Avalonia/DatabaseManager.Avalonia.sln -c Release --no-restore`；`dotnet run --project DatabaseManager.Avalonia/DatabaseManager.AppCore.RegressionTests -c Release --no-build --no-restore`。
- 确定性检查覆盖：未保存数据保护、执行中保护、活动事务保护、标签独立上下文、切库后旧结果只读。
- PostgreSQL 集成检查增加不同数据库的事务执行拒绝断言；需设置 `DBSMOKE_CONNSTR`，未配置时按原框架跳过。
- 隔离 UI 冒烟：`dotnet run --project DatabaseManager.Avalonia/DatabaseManager.Avalonia -c Release --no-build --no-restore -- --smoke --p0`。数据库下拉框使用合成服务器节点，不连接服务器。截图输出 `artifacts/p0-ui/query-database.png`。

服务器实际跨库查询和权限差异需要另行在已配置的 SQL Server/MySQL/PostgreSQL/KingbaseES 上验收，不能用 SQLite 保护检查代替。
