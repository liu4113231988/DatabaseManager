# DatabaseManager 功能与逻辑复审 TODO

## 本轮审查：2026-11-17

- **基线**：`master` / `b7f34c4`，包含审查时工作区已有的未提交修改与新增文件，不是仅审查已提交版本。
- **范围**：活跃 Avalonia 客户端、AppCore 服务/视图模型、共享数据库解释器与转换/对比引擎；旧 WinForms UI 仅保留历史清单，不代表本轮逐项复验。
- **结论**：主要功能入口与模块已具备，但“功能存在”不等于“业务闭环正确”。查询结果编辑、同步目标定位、转换预览、DDL 变更和失败状态传递仍有缺陷；现有回归通过不足以支持这些流程全部验收通过。
- **变更边界**：本轮仅记录检查结果，未修复业务实现、未提交代码，保留用户已有修改。以下复现使用临时独立 SQLite 文件、纯 SQL 生成器或服务桩，不访问用户业务库。
- **优先级**：P0＝可能误写对象或造成结构/数据损失，应优先阻断；P1＝核心流程错误、静默漏操作或状态不可信；P2＝功能反馈、测试与文档完善。共 **24 项：P0 4 项、P1 16 项、P2 4 项**，均未修复。
- **证据分级**：“隔离复现”表示本轮执行样例得到实际结果；“静态确认”表示调用链和分支已核对，但没有进行相应外部数据库或 GUI 运行验证。下列源码行号对应本轮工作树，后续修改可能导致偏移。

### 本轮验证结果与边界

| 验证 | 结果 | 说明 |
|---|---|---|
| Avalonia 解决方案 Release 构建 | 通过，0 警告 / 0 错误 | `dotnet build DatabaseManager.Avalonia/DatabaseManager.Avalonia.sln -c Release --nologo` |
| 回归项目独立 Release 构建 | 通过，0 警告 / 0 错误 | `dotnet build DatabaseManager.Avalonia/DatabaseManager.AppCore.RegressionTests -c Release --nologo`，避免引用陈旧产物 |
| 新构建产物运行回归 | `All regression checks passed.` | 包括模板、SQL 格式化、类型转换、文件格式、SQLite 加密/批量写入、查询上下文及 P0/P2 等已有检查；不等于覆盖本轮新增反例 |
| 专项隔离样例 | 复现多项待办中的反例 | 删除、并发、NULL、默认值、表设计、对比分页/键值、SQL 目标竞态、转换预览及任务状态 |
| PostgreSQL / KingbaseES 等外部实例 | 未运行 | 回归显式关闭外部连接及可选实库探针；日志明确显示相关集成测试跳过 |
| Windows GUI、Linux/macOS、SSH/SMTP/AI 外部服务 | 未运行本轮端到端验收 | 静态 UI 调用链、服务桩及旧冒烟记录不替代本轮实机验收 |

回归启动前移除了 `DBSMOKE_CONNSTR`、`DBSMOKE_KINGBASE_CONNSTR`、`DBSMOKE_KINGBASE_ADMIN_CONNSTR`、`DBM_TEST_TEMP_DATABASES`、`DBM_TEST_ODBC`、`DBM_TEST_SQL_COMPLETION_PERF`，随后执行 `dotnet run --project DatabaseManager.Avalonia/DatabaseManager.AppCore.RegressionTests -c Release --no-build`。日志另有临时 SQLite 文件仍被 provider 持有的清理提示，未导致断言失败，但不应将其表述为临时资源已全部释放。

### P0：误写目标与结构完整性

- [ ] **AUDIT-01 — 查询完成后用当前编辑器 SQL 推断可编辑表，结果与保存目标可错配**【隔离复现】
  - 触发/影响：执行表 A 的查询，在等待结果期间把编辑器改为表 B；结果仍来自 A，但内联编辑元数据与保存目标变成 B，后续保存存在误写 B 的风险。
  - 证据：[QueryTabViewModel.cs:418–425](../DatabaseManager.Avalonia/DatabaseManager.AppCore/ViewModels/QueryTabViewModel.cs#L418) 使用执行时 SQL 获取结果；[同文件:879–930](../DatabaseManager.Avalonia/DatabaseManager.AppCore/ViewModels/QueryTabViewModel.cs#L879) 却解析实时 `SqlText` 并设置 `_editableTableName`。服务桩复现输出 `actual_query_table=original_table; edit_target=another_table; editable=True`。
  - 修复/验收：编辑元数据绑定不可变的本次执行上下文；覆盖执行期间修改 SQL、连续执行及元数据延迟返回，确保结果、连接、数据库、Schema、表名始终一致，不能由后续文本重新定位。

- [ ] **AUDIT-02 — 数据对比切换连接后保留旧结果，却使用新目标生成同步/回滚脚本**【隔离复现】
  - 触发/影响：对 A→B 完成对比，再把目标改为 C；旧差异仍可勾选，脚本生成和预览目标已变为 C。切换源连接也未使旧结果失效。
  - 证据：[DataCompareViewModel.cs:116–126](../DatabaseManager.Avalonia/DatabaseManager.AppCore/ViewModels/DataCompareViewModel.cs#L116) 仅更新连接；[同文件:378–406](../DatabaseManager.Avalonia/DatabaseManager.AppCore/ViewModels/DataCompareViewModel.cs#L378) 用当前连接消费 `_lastResults`。服务桩输出 `comparison_target=target-B; script_target=target-C; retained_results=1`。
  - 修复/验收：保存对比上下文快照；连接、数据库或比较条件变化时使结果失效并禁止生成，且拒绝过期异步结果。覆盖切换源/目标、对比中切换及生成中切换，不得误定位到新目标。

- [ ] **AUDIT-03 — 数据同步/回滚部分 SQL 丢失 Schema，更新列名未统一引用**【隔离 SQL 复现＋静态确认】
  - 触发/影响：非默认 Schema 与默认 Schema 存在同名表时，生成的 DELETE/UPDATE 可能命中错误表；保留字或特殊列名还可能导致 SQL 失败。
  - 证据：[DataCompare.cs:652](DatabaseManager.Core/Compare/DataCompare.cs#L652)、[同文件:727–733](DatabaseManager.Core/Compare/DataCompare.cs#L727) 仅引用表名且直接拼接 SET 列名；[DefaultSyncScriptService.cs:364–384](../DatabaseManager.Avalonia/DatabaseManager.AppCore/Services/DefaultSyncScriptService.cs#L364) 的回滚路径同样遗漏 Schema。给定 `audit_schema.orders`，生成 `DELETE FROM "orders" where "id" = 1;`。
  - 修复/验收：所有同步/回滚 DML 使用完整目标对象标识及方言列名引用；覆盖默认/非默认 Schema 同名表、带空格与保留字列名，断言完整 SQL 并在隔离库确认只影响预期对象。

- [ ] **AUDIT-04 — 转换预览编辑结果只重建表和列，主外键、索引及其他对象丢失**【服务桩复现＋静态确认】
  - 触发/影响：即使预览后不编辑，也会用不完整 `SchemaInfo` 覆盖完整翻译结果；创建的目标结构可能缺主键、外键、索引、约束、视图等，不能认为“预览通过”等于迁移结构完整。
  - 证据：[ConvertViewModel.cs:262–304](../DatabaseManager.Avalonia/DatabaseManager.AppCore/ViewModels/ConvertViewModel.cs#L262) 仅复制 Tables/TableColumns；[同文件:435–463](../DatabaseManager.Avalonia/DatabaseManager.AppCore/ViewModels/ConvertViewModel.cs#L435) 将其传入执行；[DbConverter.cs:296–320](../DatabaseConverter/DatabaseConverter.Core/DbConverter.cs#L296) 在已有目标结构时跳过重新翻译。桩返回含 1 个主键、1 个视图的预览结构，执行服务收到的两者均为 0。
  - 修复/验收：保留完整翻译后结构，仅在副本上应用允许的列编辑；无编辑预览路径与直接转换的对象清单、约束和生成脚本应等价，并覆盖关系对象及表级属性保真。

### P1：核心流程正确性与操作闭环

- [ ] **AUDIT-05 — 仅删除行时不生成 DELETE，却报告保存成功并清除待保存状态**【隔离 SQLite 复现】
  - 触发/影响：没有新增/修改，仅删除查询结果中的行并保存；数据库未删除，UI 却按成功处理。
  - 证据：[DefaultDataEditService.cs:261](../DatabaseManager.Avalonia/DatabaseManager.AppCore/Services/DefaultDataEditService.cs#L261) 由 inserts/updates 构造列元数据；[同文件:490–508](../DatabaseManager.Avalonia/DatabaseManager.AppCore/Services/DefaultDataEditService.cs#L490) 找不到主键列后跳过条件，空脚本被忽略；[QueryTabViewModel.cs:1174–1205](../DatabaseManager.Avalonia/DatabaseManager.AppCore/ViewModels/QueryTabViewModel.cs#L1174) 成功后清除编辑状态。实测 `success=True; affected=0; remaining=1`。
  - 修复/验收：DELETE 必须使用完整表元数据，不依赖其他操作；纯删除、复合主键、多行删除和混合增删改均验证影响行数，空操作计划不能冒充删除成功。

- [ ] **AUDIT-06 — 乐观并发条件忽略原始 NULL，覆盖他人更新**【隔离 SQLite 复现】
  - 触发/影响：读取值为 NULL 的行后，另一写入者把该值改为非 NULL；当前用户保存仍成功，未检测冲突。
  - 证据：[DefaultDataEditService.cs:490–533](../DatabaseManager.Avalonia/DatabaseManager.AppCore/Services/DefaultDataEditService.cs#L490)，尤其 525–527 跳过原始 NULL/DBNull。样例中另一写入者的值被覆盖为 `mine`，返回成功。
  - 修复/验收：区分“未加载的列”和“已加载且为 NULL 的列”，后者生成 `IS NULL` 并发条件；NULL→非 NULL、删除冲突及混合事务冲突应被报告且整体回滚，不能对未加载列臆造条件。

- [ ] **AUDIT-07 — 查询和编辑链路混淆 NULL 与空字符串，无法可靠往返**【隔离 SQLite 复现】
  - 触发/影响：查询结果把 NULL 显示为普通空字符串；编辑归一化又将空字符串当 NULL，用户不能可靠保存真正的空字符串，复制/导出也缺少区分依据。
  - 证据：[DefaultQueryService.cs:203–205](../DatabaseManager.Avalonia/DatabaseManager.AppCore/Services/DefaultQueryService.cs#L203)、[QueryResult.cs:40–53](../DatabaseManager.Avalonia/DatabaseManager.AppCore/Models/QueryResult.cs#L40)、[DataEditRow.cs:247–250](../DatabaseManager.Avalonia/DatabaseManager.AppCore/Models/DataEditRow.cs#L247)。实测查询 `NULL` 与 `''` 得到相同表示，保存空字符串后数据库 `IS NULL` 为真。
  - 修复/验收：保留原始值和 NULL 标记，展示文本不作为唯一数据来源；在支持区分的数据库覆盖 NULL、空字符串、空白字符串的查询/编辑/复制/导出往返，并显式适配 Oracle 的空字符串语义。

- [ ] **AUDIT-08 — 新增行对未填写列显式写 NULL，数据库默认值不生效**【隔离 SQLite 复现】
  - 触发/影响：表有 `NOT NULL DEFAULT ...` 列，用户未填写该列时仍生成 NULL，导致插入失败；当前模型缺少“未设置”与“显式 NULL”的区别。
  - 证据：[DefaultDataEditService.cs:409–435](../DatabaseManager.Avalonia/DatabaseManager.AppCore/Services/DefaultDataEditService.cs#L409) 纳入所有非标识/非计算列；[DataTableInfo.cs:35–63](../DatabaseManager.Avalonia/DatabaseManager.AppCore/Models/DataTableInfo.cs#L35) 未表达默认值/省略状态。样例 `TEXT NOT NULL DEFAULT 'server-default'` 插入报 NOT NULL 约束错误。
  - 修复/验收：未设置且可用默认值的列从 INSERT 中省略，显式 NULL 保持用户意图；覆盖常量/表达式默认值、标识列及仅依赖默认值的整行插入，各方言生成有效语法。

- [ ] **AUDIT-09 — 表设计器删除列时丢失所属表名和 Schema**【隔离 SQL 复现】
  - 触发/影响：删除已有列后生成不可执行的 ALTER，保存失败。
  - 证据：[DefaultTableDesignService.cs:506–512](../DatabaseManager.Avalonia/DatabaseManager.AppCore/Services/DefaultTableDesignService.cs#L506) 将 `ToTableColumn(oldCol)` 传给 Drop；[同文件:755–769](../DatabaseManager.Avalonia/DatabaseManager.AppCore/Services/DefaultTableDesignService.cs#L755) 转换未填表标识；[SqliteScriptGenerator.cs:78–80](../DatabaseInterpreter/DatabaseInterpreter.Core/ScriptGenerator/SqliteScriptGenerator.cs#L78) 依赖该标识。实测生成 `ALTER TABLE  DROP COLUMN value;`。
  - 修复/验收：构造删除列对象时保留原表完整身份；SQLite 与其他支持方言验证删除列 SQL 的目标限定、引用规则和实际执行结果。

- [ ] **AUDIT-10 — 表重命名使用新名称加载旧结构，无法生成正确差异**【隔离 SQL 复现】
  - 触发/影响：设计器修改表名后，服务按新名称读取“旧表”；新名不存在时把原列当新增列，新名已存在时还有针对其他表比较的风险。
  - 证据：[TableDesignerViewModel.cs:449–462](../DatabaseManager.Avalonia/DatabaseManager.AppCore/ViewModels/TableDesignerViewModel.cs#L449) 仅传当前名称；[DefaultTableDesignService.cs:323–338](../DatabaseManager.Avalonia/DatabaseManager.AppCore/Services/DefaultTableDesignService.cs#L323) 按该名称加载，导致 [470–475](../DatabaseManager.Avalonia/DatabaseManager.AppCore/Services/DefaultTableDesignService.cs#L470) 重命名判断失去原始身份。实测输出对 `renamed_rows` 的 ADD 列，而非 RENAME。
  - 修复/验收：保留原始名称/Schema 作为结构快照身份，新名称作为变更值；覆盖新名冲突、同时改列、依赖对象和数据保留，不允许落到另一张表。

- [ ] **AUDIT-11 — 新建表保存成功后仍处于 IsNew，列定义被清空**【隔离 SQLite 复现】
  - 触发/影响：第一次 CREATE 已成功，但回读继续走新表空模板，窗口仍要求新建；后续保存无法形成正常编辑闭环。
  - 证据：[TableDesignerViewModel.cs:382–409](../DatabaseManager.Avalonia/DatabaseManager.AppCore/ViewModels/TableDesignerViewModel.cs#L382) 保存成功未切换 IsNew；[DefaultTableDesignService.cs:42–53](../DatabaseManager.Avalonia/DatabaseManager.AppCore/Services/DefaultTableDesignService.cs#L42) 为新表返回空结构。实测 `exists=1; IsNew=True; columns=0`。
  - 修复/验收：确认创建成功后切换为已有表并重新读取元数据；连续执行“创建→修改→保存”，第二次应生成 ALTER 而非重复 CREATE，列/主键与数据库一致。

- [ ] **AUDIT-12 — PostgreSQL 同次列修改被覆盖，默认值变更遗漏且 ALTER 未限定 Schema**【纯生成器复现】
  - 触发/影响：同列同时改类型、可空性和默认值时，只保留最后赋给 `alter` 的片段；非空默认值改为另一非空值没有分支处理，可能静默漏改。
  - 证据：[PostgresScriptGenerator.cs:146–187](../DatabaseInterpreter/DatabaseInterpreter.Core/ScriptGenerator/PostgresScriptGenerator.cs#L146)。样例 integer→bigint、可空→非空、默认值 1→2，结果只有 `ALTER TABLE "orders" ALTER COLUMN "amount" SET NOT NULL;`，且缺少 Schema。
  - 修复/验收：独立累积有序的列变更，正确处理 SET/DROP DEFAULT，并引用完整表名；单项、两项、三项组合均断言 SQL，后续 PostgreSQL 隔离实例回读确认最终列属性。

- [ ] **AUDIT-13 — 数据对比主键过滤直接拼接字面量，普通含引号数据即可破坏 SQL**【纯 SQL 复现】
  - 触发/影响：字符串主键含单引号时，明细读取或脚本条件不合法；值与 SQL 结构直接拼接也存在不应有的注入面。
  - 证据：[DataCompare.cs:261–354](DatabaseManager.Core/Compare/DataCompare.cs#L261)，324/345 直接用单引号包住值。正常键值 `O'Brien` 生成 `where id = 'O'Brien'`。
  - 修复/验收：查询使用参数，导出脚本使用统一的方言字面量序列化；覆盖引号、Unicode、日期、GUID、小数、二进制和复合键，禁止靠 `ToString()` 猜测 SQL 类型。

- [ ] **AUDIT-14 — 复合主键用下划线拼接，不同键会碰撞**【纯逻辑复现】
  - 触发/影响：`("a_b", "c")` 与 `("a", "b_c")` 均变成 `a_b_c`，可把不同记录误判为同一行，导致差异分类和后续同步操作错误。
  - 证据：[DataCompare.cs:210–258](DatabaseManager.Core/Compare/DataCompare.cs#L210) 用拼接键匹配并移除首个匹配项；样例输出 `composite_key_collision=True`。
  - 修复/验收：按有类型的键元组进行结构比较，不依赖分隔符文本；覆盖分隔符、空值、二进制键和不同行顺序，结果应稳定且不碰撞。

- [ ] **AUDIT-15 — 数据对比明细服务重复分页，第二页可能为空**【隔离 SQLite 复现】
  - 触发/影响：OnlyInSource、OnlyInTarget、Identical 分支先筛选本页主键，再以相同页号查询这组键，产生第二次偏移；问题已在服务入口复现，未声称本轮操作过 GUI 翻页。
  - 证据：[DefaultCompareService.cs:432–453](../DatabaseManager.Avalonia/DatabaseManager.AppCore/Services/DefaultCompareService.cs#L432)。150 条记录、页大小 100，第二页预期 50 条，实测 0 条；Different 分支不能据此一概判为相同缺陷。
  - 修复/验收：仅在一个层面分页；三种分类分别覆盖首页、中间页、末页、越界页，确认记录集合不重不漏。

- [ ] **AUDIT-16 — 对比失败/不可比较与“无差异”共用结果形态**【静态确认】
  - 触发/影响：结构对比返回带错误信息的空上下文，VM 按空根节点显示“未发现差异”；数据对比异常返回空列表，按完成零张表处理。目标缺主键或主键不匹配时，底层还会留下未经比较的明细。
  - 证据：[SchemaCompareViewModel.cs:158–173](../DatabaseManager.Avalonia/DatabaseManager.AppCore/ViewModels/SchemaCompareViewModel.cs#L158)、[DefaultCompareService.cs:396–399](../DatabaseManager.Avalonia/DatabaseManager.AppCore/Services/DefaultCompareService.cs#L396)、[DataCompare.cs:97–105](DatabaseManager.Core/Compare/DataCompare.cs#L97) 及 [DataCompareResult.cs:30](DatabaseManager.Core/Model/Compare/DataCompareResult.cs#L30)；后者可将有源记录但未比较的明细判断为一致。
  - 修复/验收：显式区分成功无差异、失败、取消、跳过、不可比较；错误或不完整结果禁止生成执行脚本。覆盖连接失败、权限错误、目标主键缺失/不匹配，并让汇总数和明细状态一致。

- [ ] **AUDIT-17 — 转换预览未形成确认阶段，且“预览/不执行”仍可创建目标 Schema**【自动继续经服务桩复现；写入分支静态确认】
  - 触发/影响：勾选预览后首次执行，在预览完成时直接进入转换，没有让用户审阅/编辑的阶段边界；更改连接等条件也缺少完整的预览失效处理。另在自动创建 Schema 开启、源非 SQLite 且目标为 SQL Server/Postgres/KingbaseES 时，预览流程本身可能写目标库。
  - 证据：[ConvertViewModel.cs:405–418](../DatabaseManager.Avalonia/DatabaseManager.AppCore/ViewModels/ConvertViewModel.cs#L405)，桩验证首次 Execute 已调用 Convert 一次；[DefaultConvertService.cs:175–185](../DatabaseManager.Avalonia/DatabaseManager.AppCore/Services/DefaultConvertService.cs#L175) 将建 Schema 选项带入预览；[DbConverter.cs:247–270](../DatabaseConverter/DatabaseConverter.Core/DbConverter.cs#L247) 的执行早于 [320–322](../DatabaseConverter/DatabaseConverter.Core/DbConverter.cs#L320) 预览返回，且不受“不执行目标脚本”约束。
  - 修复/验收：预览和仅生成脚本路径必须零目标写入；预览后停止，等待用户明确执行；连接、映射和选项变化使预览失效。使用记录执行调用的桩断言预览无 DDL/DML，外部实例验证仍留待授权环境。

- [ ] **AUDIT-18 — 导入/导出/备份/转换返回失败结果时，任务中心仍记为 Completed**【转换服务桩复现；其他调用点静态确认】
  - 触发/影响：服务用 `IsSuccess=false`、`IsOK=false` 或错误结果返回而不抛异常，VM 只写失败摘要；任务状态却是完成，任务中心与窗口结果矛盾。
  - 证据：[DefaultTaskCenterService.cs:64–87](../DatabaseManager.Avalonia/DatabaseManager.AppCore/Services/DefaultTaskCenterService.cs#L64) 仅依据异常/取消判定；[ConvertViewModel.cs:470–480](../DatabaseManager.Avalonia/DatabaseManager.AppCore/ViewModels/ConvertViewModel.cs#L470)、[ExportViewModel.cs:235–237](../DatabaseManager.Avalonia/DatabaseManager.AppCore/ViewModels/ExportViewModel.cs#L235)、[ImportViewModel.cs:414–416](../DatabaseManager.Avalonia/DatabaseManager.AppCore/ViewModels/ImportViewModel.cs#L414)、[BackupViewModel.cs:134–138](../DatabaseManager.Avalonia/DatabaseManager.AppCore/ViewModels/BackupViewModel.cs#L134)。转换失败样例最终 `error_task_state=Completed`。
  - 修复/验收：任务层显式接收失败/取消结果或由调用者传播失败；覆盖返回失败、抛异常、取消和成功四类，任务状态、结果摘要、日志与历史一致。

- [ ] **AUDIT-19 — 存在后台任务时退出分支绕过未保存数据检查，三秒后即继续关闭**【静态确认】
  - 触发/影响：既有运行任务又有未保存内联修改时，选择取消任务并退出，会在任务分支直接关闭，不再进入后续未保存数据确认；等待最多三秒也不能保证任务已结束或事务已完成清理。
  - 证据：[MainWindow.axaml.cs:529–559](../DatabaseManager.Avalonia/DatabaseManager.Avalonia/Views/MainWindow.axaml.cs#L529) 提前设置 `_closingConfirmed` 并 Close/return；[同文件:561 起](../DatabaseManager.Avalonia/DatabaseManager.Avalonia/Views/MainWindow.axaml.cs#L561) 才处理未保存编辑。保存 SQL 会话草稿不能恢复这些行级待提交修改。
  - 修复/验收：统一关闭决策顺序，覆盖任务、未保存 SQL/行编辑及事务；超时仍在运行时必须明确提示，不能当作正常完成。Windows GUI 验证取消退出、放弃修改、慢取消和保存失败，不允许隐式丢弃。

- [ ] **AUDIT-20 — 调度覆盖数据库名时手写复制连接，丢失只读与 Kingbase 兼容模式**【静态确认】
  - 触发/影响：ScheduleDefinition 指定不同 DatabaseName 时，新的 ConnectionItem 未继承 `DuckDbReadOnly`、`KingbaseCompatibilityMode`，运行语义可能不同于用户保存的连接，尤其只读约束会退回默认值。
  - 证据：[IScheduleService.cs:340–371](../DatabaseManager.Avalonia/DatabaseManager.AppCore/Services/IScheduleService.cs#L340)、[ConnectionItem.cs:65–84](../DatabaseManager.Avalonia/DatabaseManager.AppCore/Models/ConnectionItem.cs#L65)、[ConnectionHelper.cs:87](../DatabaseManager.Avalonia/DatabaseManager.AppCore/Services/ConnectionHelper.cs#L87)。这指向该调度解析分支，不把其他已有完整复制的路径一概判错。
  - 修复/验收：基于完整 Clone 仅替换数据库名称；覆盖 DuckDB 只读、Kingbase 模式、SSL/SSH 等属性保留，验证调度与交互操作遵守同一连接约束。

### P2：可见反馈、测试与文档完善

- [ ] **AUDIT-21 — 表设计器允许编辑列注释，但注释变化未进入差异检测**【纯逻辑复现】
  - 触发/影响：只修改列 Comment，生成器判断该列未变化，用户编辑不能形成持久化闭环；ComputeExp 也未包含在同一检测逻辑中，应结合是否开放编辑统一处理。
  - 证据：[DefaultTableDesignService.cs:674–683](../DatabaseManager.Avalonia/DatabaseManager.AppCore/Services/DefaultTableDesignService.cs#L674)、[TableDesignerWindow.axaml:161](../DatabaseManager.Avalonia/DatabaseManager.Avalonia/Views/TableDesignerWindow.axaml#L161)。注释单独变更的样例 `comment_change_detected=False`。
  - 修复/验收：支持的数据库生成对应注释变更语句并回读；不支持的属性应只读或明确提示，不得显示已保存却静默忽略。逐一覆盖注释新增、修改、清空及不支持方言。

- [ ] **AUDIT-22 — 解决方案构建未包含回归项目，存在使用旧回归产物的验证缺口**【构建与文件核对】
  - 现状/影响：[DatabaseManager.Avalonia.sln:6–9](../DatabaseManager.Avalonia/DatabaseManager.Avalonia.sln#L6) 仅包含 AppCore 与 UI；先构建此解决方案再运行回归 `--no-build`，不能保证测试程序最新。该回归项目是控制台断言程序，不能用 `dotnet test` 成功替代执行它。
  - 本轮处理：已单独构建回归项目后重新运行并通过，最终结论不依赖最初可能陈旧的 `--no-build` 结果；但未修改构建配置。
  - 修复/验收：固定验证入口显式构建并运行回归，或将项目纳入受验证的解决方案构建链；把 AUDIT-01～21 的反例加入确定性回归。全新输出目录也应成功运行，并校验最终通过标记与退出码。

- [ ] **AUDIT-23 — 补齐核心操作端到端与外部实例验收，不用模型测试替代功能验收**【覆盖缺口】
  - 现状/影响：现有回归覆盖广，但本轮发现的缺陷仍能通过；例如表设计模型/解析器检查不能证明 CREATE→重载→ALTER，SQL 字符串断言也不能证明外部方言实际执行正确。
  - 验收矩阵：查询→编辑→保存→重查；创建表→改名/改列/删列→重查；对比→分页→同步预览→执行→回滚；预览→编辑映射→转换→结构核对；导入导出默认值/NULL 保真；运行任务期间关闭窗口。分别记录成功、失败、取消和并发操作。
  - 边界：Windows GUI 以及 Linux/macOS 发布包需分别验证；Postgres、SQL Server、MySQL、Oracle、KingbaseES 等真实实例仅由用户在授权隔离环境执行，保留跳过原因。沿用 [当前 backlog](../DatabaseManager.Avalonia/docs/backlog.md) 和 [冒烟记录](../docs/smoke/verification.md) 的范围，不把旧证据标成今天已验收。

- [ ] **AUDIT-24 — 历史功能清单与当前实现/产品范围脱节，需分离实现与验收状态**【文档核对】
  - 现状/影响：下方 2026-04～08 历史表仍将多个阶段一概记为完成，并列出过期的迁移待办；当前已有模板外部化、表设计扩展等代码，状态应按实际提交/工作树与验收证据更新，而非重复开发旧条目。
  - 修复/验收：将“已实现、回归通过、实机通过、外部实例待验、明确不做”分列；完整 Dock、过程调试、团队协作等当前不在范围的事项不误报为缺陷，分区按当前“只读元数据＋DDL 回放”边界验收，不擅自扩大为 ALTER 分区。本轮保留下文原始记录，仅将本审查置顶作为当前入口。

### 建议处理顺序

先阻断 AUDIT-01～04 的错误目标/结构丢失路径，再修复编辑保存与表设计闭环（05～12）；随后处理对比与预览（13～17）、任务及退出（18～20），最后完善反馈和验证链（21～24）。每项修复都应先固定反例，再运行现有回归；涉及外部实例的验收必须单独记录，不以未执行或跳过视为通过。

---

> **以下为原历史清单，保留原文供追溯。其日期、完成标记和范围不代表 2026-11-17 已重新验收；当前问题优先参考上方 AUDIT 待办。**

# DatabaseManager 功能清单（已完成 vs 未完成）

> 本文档汇总两条开发主线：
> - **WinForms 版**（`DatabaseManager.CoreApp`，原树结构功能缺失清单，见下文）
> - **Avalonia 迁移版**（`DatabaseManager.Avalonia/`，跨平台重写主线，已完成能力详见 `DatabaseManager.Avalonia/README.md`，未完成事项详见 `DatabaseManager.Avalonia/docs/backlog.md`）

## 📋 检查日期：2026-04-05  
## 🔄 最后更新：2026-08-20

---

## ✅ 已完成的功能（Avalonia 迁移主线）

> 依据 Issue #13 DBeaver 计划表与 Issue #15 待办，在 Avalonia 版持续推进，均已构建验证（Debug/Release 0 错误）并合入 master。

| 里程碑 | 内容 | 状态 |
|:---:|------|:---:|
| M0 | 环境与骨架（独立解决方案/核心库复用/三栏布局） | ✅ 已完成 |
| M1 | 连接管理 + 主框架（连接 CRUD/测试/菜单工具栏/三栏布局） | ✅ 已完成（核心） |
| M2 | 对象浏览 + 查询（对象树懒加载/SQL 查询结果） | ✅ 已完成（核心） |
| M3 | 数据编辑 + 表设计（数据查看编辑/事务保存/表设计器） | ✅ 已完成（核心） |
| P0 | 事务核心/连接生命周期/SQL 脚本管理/Schema 选择器 | ✅ 已完成 |
| P1 | 数据查看与编辑（分页/增删改/事务保存） | ✅ 已完成 |
| P2 | 表设计器（列/主键/索引/外键/约束 + CREATE/ALTER 脚本） | ✅ 已完成 |
| 阶段4·转换 | 跨库数据库转换（结构/数据） | ✅ 已完成 |
| 阶段4·结构对比 | Schema Compare（差异树/着色） | ✅ 已完成 |
| 阶段4·数据对比 | Data Compare（表级概览/明细分页/同步脚本） | ✅ 已完成 |
| 阶段4·诊断 | 数据库诊断（表/脚本诊断） | ✅ 已完成 |
| 阶段4·优化 | 数据库优化（SQLite VACUUM / MySQL 表整理） | ✅ 已完成 |
| 阶段4·依赖分析 | 依赖分析（表/视图/函数/存储过程的入/出依赖） | ✅ 已完成 |
| 阶段5·统计 | 统计（表记录数 / 列内容最大长度） | ✅ 已完成 |
| 阶段5·索引碎片 | 索引碎片分析 + 重建 | ✅ 已完成 |
| 阶段5·备份 | 数据库备份（SQL Server/Oracle/MySQL/Postgres） | ✅ 已完成 |
| 阶段5·代码生成 | 代码生成（C# / Java 实体类） | ✅ 已完成 |
| 阶段5·文档生成 | 列结构文档生成（Word） | ✅ 已完成 |
| 阶段4·Schema预览/列映射 | Schema 预览（目标结构预览/编辑）+ Schema/列映射 | ✅ 已完成 |
| M6·导入导出 | 数据导入/导出（CSV / Excel，复用 FileUtility/DataExporter/DataImporter） | ✅ 已完成 |

---

## ⏳ 未完成的功能（Avalonia 迁移主线）

| 里程碑 | 内容 | 状态 |
|:---:|------|:---:|
| M7 | 跨平台 + 发布（三平台发布包） | ⬜ 待执行 |
| 进阶项 | 完整 Dock 拖拽/浮动停靠、SQL 语法高亮（AvaloniaEdit 兼容待确认） | ⏳ 待实机 |
| 迭代项 | 分区管理、索引/外键列内联编辑、主键多选优化 | ⏳ 待迭代 |

---

# 数据库管理器树结构功能缺失清单（WinForms 版）

## 📋 检查日期：2026-04-05  
## 🔄 最后更新：2026-04-06

## ✅ 已实现的功能

### 基础功能
- [x] 树结构显示（数据库、表、视图、函数、存储过程、触发器、索引、列等）
- [x] 树节点图标显示（使用 AntdUI.Tree 的 Icon 属性）
- [x] 右键菜单显示
- [x] 节点展开/折叠
- [x] 断开连接功能

### 对象操作
- [x] 新建查询
- [x] 新建表/视图/函数/存储过程/触发器
- [x] 修改对象（Alter）
- [x] 设计表（Table Designer）
- [x] 新建列/修改列（已区分实现）✨ **新增**
- [x] 删除对象
- [x] 复制表

### 数据操作
- [x] 查看数据（View Data）
- [x] 编辑数据（Edit Data）
- [x] 导出数据（Export Data）
- [x] 导入数据（Import Data）

### 脚本生成
- [x] 创建脚本（Create Script）
- [x] 查询脚本（Select Script）
- [x] 插入脚本（Insert Script）
- [x] 更新脚本（Update Script）
- [x] 删除脚本（Delete Script）
- [x] 执行存储过程脚本
- [x] 生成增删改存储过程

### 高级功能
- [x] 数据库转换（Convert）
- [x] 结构比较（Compare Schema）
- [x] 数据比较（Compare Data）
- [x] 数据库图表（Diagram）
- [x] 查看依赖关系（View Dependencies）
- [x] 复制子节点名称
- [x] 诊断（Diagnose）
- [x] 统计信息（Statistic）
- [x] 索引碎片分析（Index Fragmentation）
- [x] 生成列文档

---

## 🎯 高优先级功能（全部完成 ✅）

#### 1. ~~刷新功能未实现~~ → **✅ 已实现**
- **状态**：✅ 完成
- **实现位置**：UC_DbObjectsComplexTree.cs - `tsmiRefresh_Click` 方法 (L716)
- **说明**：调用 RefreshFolderNode() 方法，支持手动刷新当前选中的节点

#### 2. ~~双击操作未定义~~ → **✅ 已实现**
- **状态**：✅ 完成
- **实现位置**：UC_DbObjectsComplexTree.cs - `tvDbObjects_NodeMouseDoubleClick` 方法
- **功能**：
  - 双击表/视图 → 打开查看数据界面
  - 双击函数/存储过程 → 显示创建脚本
  - 双击触发器 → 显示修改脚本
- **空值检查**：✅ 已添加 item 和 Tag 的 null 检查

#### 3. ~~备份功能未实现~~ → **✅ 已验证存在**
- **状态**：✅ 存在且完整
- **代码位置**：UC_DbObjectsComplexTree.cs - `tsmiBackup_Click` 方法 (L1481-L1505)
- **功能**：打开备份设置对话框，异步执行备份操作，显示成功/失败提示

#### 4. ~~清空数据库功能未实现~~ → **✅ 已验证存在**
- **状态**：✅ 存在且完整
- **代码位置**：UC_DbObjectsComplexTree.cs - `tsmiEmptyDatabase_Click` 方法 (L872)
- **功能**：显示确认对话框，调用 EmptyDatabaseAsync()，自动刷新节点

---

## 🎯 中等优先级功能（大部分完成 ✅）

#### 5. ~~清除数据功能未实现~~ → **✅ 已实现**
- **状态**：✅ 完成
- **实现位置**：UC_DbObjectsComplexTree.cs - `tsmiClearData_Click` 方法
- **功能**：
  - 右键点击表 → More → Clear Data
  - 显示确认对话框（警告不可恢复）
  - 调用 DeleteDataAsync() 删除表数据
  - 显示成功/失败提示

#### 6. ~~优化功能未实现~~ → **✅ 已实现**
- **状态**：✅ 完成
- **实现位置**：UC_DbObjectsComplexTree.cs - `tsmiOptimize_Click` 方法
- **功能**：
  - 右键点击数据库 → More → Optimize
  - 仅支持 MySQL 和 SQLite（其他数据库显示提示）
  - 调用 OptimizeDatabaseAsync() 优化数据库
  - 显示进度反馈和完成提示

#### 7. ~~转换功能不完整~~ → **✅ 已完善**
- **状态**：✅ 完善
- **实现位置**：UC_DbObjectsComplexTree.cs - `tsmiTranslate_MouseEnter` + `TranslateTo` 方法
- **改进内容**：
  - 完善了 MouseEnter 事件处理逻辑
  - 动态生成目标数据库类型子菜单
  - 根据当前数据库类型智能推荐可转换的目标
  - 新增 TranslateTo() 方法打开转换窗口并填充信息
- **支持的转换映射**：
  | 当前数据库 | 可转换到 |
  |-----------|---------|
  | SQL Server | MySQL, PostgreSQL, Oracle, SQLite |
  | MySQL | SQL Server, PostgreSQL, SQLite |
  | PostgreSQL | SQL Server, MySQL, Oracle |
  | Oracle | SQL Server, MySQL, PostgreSQL |
  | SQLite | SQL Server, MySQL |

#### 8. ~~缺少搜索/过滤功能~~ → **❌ 已回滚（暂不实现）**
- **状态**：❌ 回滚
- **原因**：
  - AntdUI.Tree 控件没有 Visible 属性控制节点显示/隐藏
  - 添加搜索框后导致树结构布局异常（出现滚动条和容器大小问题）
  - 克隆重建策略影响性能
- **建议**：未来可以考虑使用其他方案或第三方组件

#### 9. ~~缺少键盘快捷键~~ → **✅ 已实现**
- **状态**：✅ 完成
- **实现位置**：UC_DbObjectsComplexTree.cs - `TvDbObjects_KeyDown` 方法 (L945)
- **实现的快捷键**：

| 快捷键 | 功能 | 状态 |
|--------|------|------|
| **F5** | 刷新当前节点 | ✅ |
| **Delete** | 删除选中对象（需要权限） | ✅ |
| **Enter** | 双击操作（查看数据或打开脚本） | ✅ |
| **Ctrl+C** | 复制节点名称到剪贴板 | ✅ |
| **Ctrl+F** | 查找子节点 | ✅ |
| **F2** | 重命名（预留接口） | ⏳ TODO |

---

## 🔵 低优先级 - 待实现

#### 10. **缺少重命名功能** ⏳ 待实现
- **问题**：没有重命名对象的右键菜单项或快捷键
- **建议**：添加 Rename 菜单项和内联编辑功能
- **优先级**：🔵 低

#### 11. **缺少属性窗口** ⏳ 待实现
- **问题**：无法查看对象的详细属性信息
- **建议**：添加属性面板或详细信息对话框
- **优先级**：🔵 低

#### 12. **缺少拖拽操作** ⏳ 待实现
- **问题**：不支持拖拽排序或拖拽复制
- **建议**：评估是否需要，实现 DragDrop 事件
- **优先级**：🔵 低

#### 13. **缺少多选操作** ⏳ 待实现
- **问题**：不支持同时选择多个节点进行批量操作
- **建议**：启用 TreeView 的 CheckBoxes 或实现多选模式
- **优先级**：🔵 低

#### 14. **缺少收藏夹/书签功能** ⏳ 待实现
- **问题**：无法将常用的表或查询添加到收藏夹
- **建议**：添加收藏夹功能，提升常用对象访问速度
- **优先级**：🔵 低

#### 15. **缺少最近访问历史** ⏳ 待实现
- **问题**：没有最近访问的对象记录
- **建议**：实现访问历史记录功能
- **优先级**：🔵 低

#### 16. **图标显示验证** ⏳ 需要测试
- **状态**：⏳ 已实现，待用户测试验证
- **实现位置**：DbObjectsAntdTreeHelper.cs - GetIconFromResources() + CreateTreeItem()
- **注意事项**：
  - 使用 Resources.ResourceManager.GetObject() 加载图标
  - 支持的图标类型：Database, Table, TableColumn, View, Function, Procedure 等
  - 图标缓存机制提高性能
- **建议**：全面测试所有节点类型的图标显示

---

## 🆕 额外修复项

### BUG 修复记录

1. **Views -> Object Explorer 菜单无响应** ✅ 已修复
   - **文件**：frmMain.cs L596-L603
   - **原因**：只调用了 Show() 未调用 Activate()
   - **修复**：添加 DockState.DockLeft 参数 + Activate() 调用

2. **新建/修改列功能重复** ✅ 已修复
   - **文件**：UC_DbObjectsComplexTree.cs L1353-L1400
   - **改进**：区分 New Column（info.Content = "NEW_COLUMN"）和 Modify Column（info.Content = "MODIFY_COLUMN:{列名}"）

3. **类型转换缺少空值检查** ✅ 已修复
   - **文件**：UC_DbObjectsComplexTree.cs L1389-L1393
   - **改进**：采用 Early Return 模式，避免传入不完整的 info 对象

4. **双击事件处理缺少空值检查** ✅ 已修复
   - **文件**：UC_DbObjectsComplexTree.cs L1332
   - **改进**：添加 `item.Tag == null` 检查

5. **重复键盘事件处理方法** ✅ 已修复
   - **文件**：UC_DbObjectsComplexTree.cs
   - **删除**：新添加的 TvDbObjects_KeyDown 方法（L71-98）
   - **保留**：原有的完整版本（L945-967），包含所有快捷键

---

## 📊 功能完整性统计（更新）

| 类别 | 总数 | 已实现 | 完成率 | 状态 |
|------|------|--------|--------|------|
| 右键菜单项 | 35+ | 33 | **94%** | ✅ 几乎完整 |
| 高优先级功能 | 4 | 4 | **100%** | ✅ 全部完成 |
| 中优先级功能 | 5 | 4 | **80%** | ✅ 大部分完成（搜索回滚） |
| 键盘快捷键 | 6 | 5 | **83%** | ✅ 大部分完成（F2待实现） |
| 低优先级功能 | 7 | 0 | 0% | ⏳ 待实现 |
| BUG 修复 | 5 | 5 | **100%** | ✅ 全部修复 |
| **总计** | **62+** | **51** | **82%** | ✅ 显著提升 |

---

## 🎯 实施进度更新

### 第一阶段：修复关键缺陷 ✅ **全部完成**
1. ✅ 实现刷新功能（Refresh）
2. ✅ 实现双击操作
3. ✅ 验证备份功能（Backup）
4. ✅ 验证清空数据库功能（Empty Database）

### 第二阶段：完善核心功能 ✅ **基本完成**
5. ✅ 实现清除数据功能（Clear Data）
6. ✅ 实现优化功能（Optimize）
7. ✅ 完善转换功能（Translate）
8. ⏳ 全面测试图标显示（待用户测试）

### 第三阶段：提升用户体验 ⏳ **部分完成**
9. ❌ ~~搜索/过滤功能~~ （已回滚）
10. ✅ 添加键盘快捷键
11. ⏳ 添加重命名功能（TODO）
12. ⏳ 添加属性窗口（待实现）

### 第四阶段：高级特性 📅 **可选，未开始**
13. 📅 实现拖拽操作
14. 📅 实现多选批量操作
15. 📅 添加收藏夹功能
16. 📅 添加历史记录

---

## 📝 备注

1. ✅ 所有高优先级功能已全部完成
2. ✅ 中优先级功能完成 80%（搜索功能因技术限制回滚）
3. ✅ 5个BUG已全部修复
4. ✅ 新建列/修改列功能已区分实现
5. ✅ Object Explorer 菜单已修复
6. ⏳ 图标显示功能已实现，等待用户测试验证
7. ⏳ 低优先级功能可根据需求逐步实现

---

## 🎉 成果总结

### 本次会话完成的工作：

**✅ 功能新增（9项）：**
1. 刷新功能实现
2. 双击操作实现（表、视图、函数、存储过程、触发器）
3. 清除数据功能实现
4. 优化功能实现（MySQL/SQLite）
5. 转换功能完善（动态子菜单生成）
6. 键盘快捷键实现（F5/Delete/Enter/Ctrl+C/Ctrl+F）
7. 新建列功能（右键Columns文件夹）
8. 修改列功能（右键列名，传递列名给设计器）
9. 树节点图标显示（使用资源文件）

**✅ BUG 修复（5项）：**
1. Views -> Object Explorer 菜单无响应
2. 新建/修改列功能重复
3. 类型转换空值检查缺失
4. 双击事件空值检查缺失
5. 重复键盘事件处理方法

**✅ 代码质量改进：**
- 添加防御性编程（空值检查）
- 采用 Early Return 模式
- 移除重复代码
- 统一事件处理器管理

---
*最后更新：2026-04-06*
