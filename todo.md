# DatabaseManager 待完善与缺失功能清单

> 基于对项目代码（`DatabaseInterpreter`、`DatabaseConverter`、`DatabaseManager.CoreApp`、`.Web`、`.FileUtility`、`.Profile`）的检查整理。
> 对象树相关的重构待办另见 `DatabaseManager/todo.md`。

## 一、功能缺失（尚未实现或仅为空壳）

- [ ] **Web 项目功能缺失**：`DatabaseManager.Web` 仅有 `HomeController` 一个基础页面，无任何数据库管理 API/界面。需明确其定位：
  - 若作为 Web 版管理端：需实现连接管理、对象浏览、查询、数据查看等 API 与前端（目前 `wwwroot` 仅有占位资源）。
  - 若仅作发布/展示页：应在 README 中明确说明，避免产生误导。
- [ ] **单元测试缺失**：整个解决方案未发现任何 `*.Tests` 测试项目。建议为核心引擎（DatabaseInterpreter 解析、DatabaseConverter 转换、ScriptGenerator 脚本生成）补充单元测试。
- [ ] **Microsoft Access 支持验证**：`DatabaseType` 含 Access，但对应的 `DatabaseInterpreter.Access` / `DatabaseConverter.Access` 适配器需确认是否完整实现（读写、转换、脚本生成）。
- [ ] **导入/导出格式覆盖**：`Import`/`Export` 模块需确认对 Excel / CSV / XML / JSON 各格式的完整读写能力，缺失格式应补全。
- [ ] **CI/CD 配置缺失**：仓库无 GitHub Actions / 流水线配置，建议补充自动构建与测试。

## 二、需完善的功能（已实现但深度/质量待提升）

- [ ] **数据查看与编辑**：`UC_DataViewer` / `UC_Edit` 需完善大结果集虚拟滚动、批量编辑事务回滚、二进制（BLOB）字段可视化编辑。
- [ ] **查询编辑器**：`UC_QueryEditor` 需增强语法高亮、跨数据库方言自动完成、执行计划展示、多结果集标签化。
- [ ] **表设计器**：`frmTableDesigner` 各子控件需验证对全部数据库类型的 DDL 差异（如 Oracle 自增、PostgreSQL 序列、SQLite 类型限制）的正确生成。
- [ ] **数据库转换**：`DatabaseConverter` 需完善：
  - 自增/序列、默认值、注释的跨库映射；
  - 大表分页迁移的断点续传与失败重试；
  - 类型不兼容时的映射规则与告警。
- [ ] **结构/数据对比**：`Compare` 模块需支持更多对象类型（函数、触发器、视图定义）的逐字对比，并输出可执行的差异同步脚本。
- [ ] **诊断与优化**：`Diagnose` / `Opitimize` 当前较基础，需扩充规则库（缺失索引、统计信息过期、碎片整理等）并区分各数据库能力。
- [ ] **统计功能**：`Statistic` 模块仅含记录数、列最大长度，建议补充空值率、基数（distinct）、数据类型分布等。
- [ ] **依赖分析**：`DbObjectDependency` / `TableDependency` 需支持跨 Schema 依赖图与循环依赖检测。
- [ ] **代码生成**：`CodeGenerator` 需支持更多语言/ORM 模板（如 Dapper、EF Core、Java POJO），并提供模板自定义。
- [ ] **文档生成**：`DatabaseDocumentation` 需支持更多输出格式（Markdown、Word、PDF）与字段级中文注释。
- [ ] **连接管理**：`frmDbConnectionManage` 近期有改动，需完善：
  - SSH 隧道连接的稳定性与多跳；
  - 连接字符串解析与校验；
  - Profile 的加密存储与导入导出。
- [ ] **数据库图表**：`frmDatabaseDiagram` 需支持拖拽布局、缩放、导出图片、自引用关系展示。

## 三、工程化与质量

- [ ] **README**：已根据当前代码重新梳理（见根 `README.md`），后续保持与实现同步。
- [ ] **国际化**：界面文案（中文为主）需梳理是否支持多语言切换。
- [ ] **异常与日志**：统一异常处理与日志（错误边界、操作审计），提升可观测性。
- [ ] **依赖与版本**：确认各 NuGet 包版本统一、无已知安全漏洞。

## 四、参考资料

- 对象树/迁移窗体专项待办：`DatabaseManager/todo.md`
- UI 组件说明：`antdui.md`
- 历史迁移表单待办：`migration-form-todo.md`
