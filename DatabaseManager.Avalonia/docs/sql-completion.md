# SQL 智能提示的上下文匹配

本轮优先提高候选准确性，不新增依赖或提示设置。

| 输入位置 | 提示行为 |
| --- | --- |
| `FROM` / `JOIN` / `UPDATE` / `INTO` 后 | 当前查询数据库中已加载的表、视图，不混入 SQL 关键字 |
| `FROM public.us` | 按 Schema 筛选表；接受后保留原有 `public.` 前缀 |
| `SELECT u.na FROM public.users AS u` | 将 `u` 解析到对应物理表，提示其字段 |
| `SELECT na FROM public.users`、`WHERE`、`ON`、`GROUP BY`、`ORDER BY` | 提示当前查询引用表的字段，并保留关键字候选 |
| 多表字段 | 显示并插入别名或完整表限定名，如 `u.name`、`sales.users.id`，区分同名字段 |
| 注释、单引号字符串、PostgreSQL dollar 字符串 | 不弹出补全，不将其中的 FROM/JOIN 误认作表引用 |

候选范围跟随查询标签的连接和数据库，支持对象浏览器的分组连接。不同语句、UNION 分支和嵌套 SELECT 分开解析，避免复用别处的同名别名。MySQL 的 `database.table` 与 SQL Server 的 `database.schema.table` 按各自语义定位；PostgreSQL 不为其他数据库的三段引用加载字段。PostgreSQL/Oracle 的标识符大小写与引号区别参与解析。

字段直接复用对象树中已加载的列；尚未展开列时，异步读取目标表的元数据并缓存。请求按连接、数据库、Schema、表名去重，类型化缓存键避免标识符含分隔符时发生冲突。切换数据库、连接或刷新对象树会使旧请求失效；按 Esc 后不会因旧请求完成而重新弹窗，之后仍可用 Ctrl+Space 调出缓存结果。

输入标识符或点号自动触发；Ctrl+Space 手动触发。普通输入交给 AvaloniaEdit 原生过滤，保留完整候选集，取消原有前 80 项限制。输入点号会切换到新的上下文，不会误接受旧关键字。Tab/Enter 接受时替换完整标识符，包含光标后已存在的后缀；保留已输入的引号，并为特殊字符、保留字或区分大小写的对象添加必要引号。

候选描述区分关键字、表、视图和字段，不再将所有项目标成“SQL 关键字”。

## 边界

- 表和视图候选来自对象浏览器已加载的元数据，不额外遍历所有服务器数据库。
- 这是针对编辑中不完整 SQL 的轻量分析，不是完整 SQL 语言服务器。CTE、派生表的输出列，以及关联子查询的外层别名，暂不推断。含 `WITH` 的语句仍可提示其中物理表（包括 Schema 限定表）的字段；只排除匹配的 CTE 引用。
- 未指定 Schema 且存在多个同名对象时，不猜测搜索路径；指定 Schema 后再提示字段。
- 元数据请求失败不影响 SQL 编辑或执行，不会自动执行编辑器中的 SQL。

## 验证

`SqlCompletionChecks` 在 AppCore 回归执行器中覆盖作用域、别名、分组连接、数据库隔离、跨库语义、引号、注释/字符串和替换区间。

P0 隔离 UI 冒烟使用合成元数据，验证真实 Ctrl+Space/输入事件、超过 80 项的列表过滤、点号切换、Tab 接受、JOIN 同名字段限定、异步字段刷新、缓存复用、切库时丢弃旧结果及 Esc 取消。截图输出为 `artifacts/p0-ui/sql-completion.png`。

```powershell
dotnet build DatabaseManager.Avalonia/DatabaseManager.Avalonia.sln -c Release
dotnet run --project DatabaseManager.Avalonia/DatabaseManager.AppCore.RegressionTests -c Release
dotnet run --project DatabaseManager.Avalonia/DatabaseManager.Avalonia -c Release --no-build -- --smoke --p0
```

UI 冒烟不连接真实服务器；服务器元数据权限、网络延迟以及 Linux/macOS 的实际桌面交互仍需在对应环境验收。

## 大脚本解析测量

分析前用线性扫描定位光标所在语句，仅对该语句创建正则标记。语句边界跳过引号、行注释、嵌套块注释及 dollar 字符串中的分号，替换区间保持文档绝对偏移。对象树遍历不再为叶节点创建空递归迭代器。

2026-11-02 本机 Release 合成测量（15 次取中位数，不含 UI 绘制/网络）：光标位于末尾 395 字符查询，前面分别放置 1000/10000 条语句。49395 字符脚本解析从 49.01 ms 降到 0.30 ms；490395 字符从 476.32 ms 降到 1.28 ms。490395 字符场景的单次分配由约 137 MB 降到 138 KB。5000 个对象中解析 10 个表引用的分配由约 3.6 MB 降到 14 KB，最终中位数约 4.6–4.9 ms。计时仅用于同机比较，不作为通过阈值；单条很长的 SQL 仍完整解析。

可选测量入口（只运行测量，不运行完整回归）：

```powershell
$env:DBM_TEST_SQL_COMPLETION_PERF = '1'
dotnet run --project DatabaseManager.Avalonia/DatabaseManager.AppCore.RegressionTests -c Release --no-build --no-restore
Remove-Item Env:DBM_TEST_SQL_COMPLETION_PERF
```
