# 冒烟测试截图索引

## 2026-11-08 对象树、补全与模板管理

- 对象树按连接隔离请求集合，同一元数据请求由预取与展开共享；刷新、断开和连接删除使旧请求失效。回归覆盖在途去重和旧请求晚于失效完成。
- SQL 补全识别 CTE 名称，含 WITH 的语句仍可解析物理表；覆盖多 CTE、可选列名、RECURSIVE、Schema 限定及内外作用域。CTE 输出列仍不推断。
- 菜单「SQL 片段模板...」提供列表、内容编辑、示例预览、保存覆盖与恢复默认；脚本库读取覆盖内容。回归覆盖示例展开、无效变量拒绝保存、存储失败、恢复默认、目录越界拦截和脚本库消费链路。模板导入导出及变量契约编辑未纳入此最小入口。
- 本轮 Release 解决方案构建（0 错误，保留现有告警）、完整回归（`All regression checks passed.`）及 Windows `--smoke --p0` 均通过。完整回归需在沙箱外运行，沙箱内的 Excel 临时文件移动检查触发权限错误。
- 建库 / 删库 / 数据库属性与备份恢复目标的既有回归、隔离 UI 检查本轮复跑通过。未设置 `DBSMOKE_CONNSTR`，未执行真实服务器建删库、权限和活动会话检查；Linux/macOS 未运行。

![SQL 片段模板管理窗口](sql-template-manager.png)


## 数据库右键删除（2026-11-07）

数据库节点新增「删除数据库... / Delete」，支持 PostgreSQL、MySQL、SQL Server 用户库。系统库及尚未支持的数据库类型禁用此项。确认框展示目标连接、库名和 SQL，要求输入完全一致的库名；关闭或取消不会执行。

执行前拦截运行中查询、未保存的数据编辑和活动事务；通过维护库自动提交执行，不强制终止其他会话。成功后移除树节点并更新数据库列表，保留相关标签中的 SQL/结果并解除执行绑定。如果删除的是连接默认库，本次会话的浏览器默认目标切至维护库，并提示手动更新已保存连接配置。

验证：Release 构建、完整回归可执行程序、Windows `--smoke --p0` 均通过。回归覆盖方言转义、系统库保护、不支持类型、取消令牌、忙碌/未保存修改拦截、失败状态保留、重复点击/过期节点拦截和成功后的上下文清理。界面检查覆盖入口可用性、名称不匹配和关闭取消。未配置 `DBSMOKE_CONNSTR`，未执行真实服务器删库。

| 截图 | 内容 |
| --- | --- |
| ![数据库右键菜单](database-context-menu.png) | 删除入口及 Delete 快捷键提示 |
| ![数据库删除确认](database-delete-confirm.png) | 目标信息、SQL 和库名输入确认 |

## 新建数据库、备份/恢复和属性（2026-11-07）

连接和数据库右键菜单新增「新建数据库...」，支持 PostgreSQL、MySQL、SQL Server。输入库名后展示目标连接和 SQL，确认后使用维护库自动提交执行；编码、排序规则等采用服务器默认值。校验名称长度、控制字符和系统库名称，并按方言转义标识符。创建成功后刷新对象树和查询标签的数据库列表，保留当前查询目标及 SQL，不重连或回滚现有事务；刷新失败会明确提示数据库已创建。

数据库右键「备份数据库... / 恢复数据库...」复用已有备份窗口和任务中心，始终定位右键选中的数据库，窗口内刷新也不会退回已保存连接的默认库。备份支持 PostgreSQL、MySQL、SQL Server、Oracle；恢复额外支持 SQLite，其他类型禁用相应入口。恢复模式突出恢复按钮；执行前再次展示目标库并确认覆盖风险。从右键打开恢复时拦截活动事务、运行中查询和未保存的数据编辑。

「数据库属性...」以只读表格展示连接与目标信息。PostgreSQL、MySQL、SQL Server 从系统目录读取编码、排序规则、所有者等可用字段；大小分别为 PostgreSQL 数据库大小、MySQL 可见表与索引的估算大小、SQL Server 可见数据库文件大小，单位均为字节。文件数据库显示数据源及实际文件大小；其他类型仅显示基本连接信息，Oracle/DM 标明用户 / Schema 范围。查询使用参数，不展示密码；读取失败保留基本信息并显示错误。

本轮 Release 构建、完整回归和 Windows `--smoke --p0` 均通过。回归检查覆盖建库 SQL、名称边界、重复提交、失败及刷新失败、列表同步、备份恢复目标隔离、文件与 Schema 目标保留、能力禁用和属性参数化。回归项目未包含在解决方案中，须单独构建或运行。隔离界面冒烟检查菜单回调、建库 SQL 确认与取消、备份恢复目标及刷新、恢复确认与取消，以及真实临时 SQLite 文件属性。

```powershell
dotnet build DatabaseManager.Avalonia/DatabaseManager.Avalonia.sln -c Release
dotnet run --project DatabaseManager.Avalonia/DatabaseManager.AppCore.RegressionTests -c Release
dotnet run --project DatabaseManager.Avalonia/DatabaseManager.Avalonia -c Release -- --smoke --p0
```

| 截图 | 内容 |
| --- | --- |
| ![新建数据库](database-create-name.png) | 名称输入；随后预览对应方言的 SQL |
| ![备份数据库](database-backup.png) | 备份定位右键选中的 report_db |
| ![恢复数据库](database-restore.png) | 恢复模式及目标库 |
| ![恢复确认](database-restore-confirm.png) | 再次确认目标和覆盖风险 |
| ![数据库属性](database-properties.png) | 临时 SQLite 文件的只读属性 |

未配置 `DBSMOKE_CONNSTR`，未执行真实服务器建库、删库、备份、恢复或属性查询。重命名、克隆和权限管理按实际需求另行实现。

## 查询工具栏布局（2026-11-07）

连接名和数据库选择移至主工具栏，绑定当前查询标签；长名称提供悬停提示，窄窗口按功能组换行。SQL 状态条保留超时、消息和参数入口，长消息省略显示。

以下截图由隔离的 `--smoke --p0` 生成，无需真实服务器连接。检查覆盖标签切换、切库绑定、执行期间拒绝切库后恢复显示，以及 760px 窄窗口可见性。

| 截图 | 布局 |
| --- | --- |
| ![查询工具栏](query-toolbar.png) | 1280px，工具栏单行排列 |
| ![窄窗口查询工具栏](query-toolbar-narrow.png) | 760px，按功能组换行 |

## 完整功能冒烟

由 `dotnet run -- --smoke` 自动产出。运行前请确认本地 PostgreSQL `Member` 库可访问。

| 截图 | 界面 |
| --- | --- |
| ![主窗口（连接前）](01_main.png) | 主窗口（连接前） |
| ![连接管理](02_connection_manager.png) | 连接管理 |
| ![连接向导](03_connect_wizard.png) | 连接向导 |
| ![数据库转换](04_convert.png) | 数据库转换 |
| ![结构对比](05_schema_compare.png) | 结构对比 |
| ![数据对比](06_data_compare.png) | 数据对比 |
| ![数据库备份](07_backup.png) | 数据库备份 |
| ![统计](08_statistic.png) | 统计 |
| ![依赖分析](09_dependency.png) | 依赖分析 |
| ![数据库诊断](10_diagnose.png) | 数据库诊断 |
| ![数据库优化](11_optimize.png) | 数据库优化 |
| ![索引碎片](12_index_fragmentation.png) | 索引碎片 |
| ![代码生成](13_code_generate.png) | 代码生成 |
| ![列结构文档](14_column_documentation.png) | 列结构文档 |
| ![数据导出](15_export.png) | 数据导出 |
| ![数据导入](16_import.png) | 数据导入 |
| ![执行计划](17_execution_plan.png) | 执行计划 |
| ![查询历史](18_query_history.png) | 查询历史 |
| ![元数据搜索](19_search.png) | 元数据搜索 |
| ![全库数据搜索](20_full_data_search.png) | 全库数据搜索 |
| ![表设计器](21_table_designer.png) | 表设计器 |
| ![脚本库](22_script_library.png) | 脚本库 |
| ![仪表盘](23_dashboard.png) | 仪表盘 |
| ![图表窗口](24_chart.png) | 图表窗口 |
| ![主窗口（已连接）](25_main_connected.png) | 主窗口（已连接） |
| ![主窗口（对象树展开）](26_main_tree_expanded.png) | 主窗口（对象树展开） |
