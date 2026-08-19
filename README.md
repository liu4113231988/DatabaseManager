# DatabaseManager

一个基于 **.NET 8 / WinForms** 的多数据库管理与迁移工具，提供对象浏览、数据查询与编辑、结构/数据转换、差异对比、脚本生成、统计诊断等一站式数据库运维能力。同时附带一个 ASP.NET Core Web 壳项目用于发布/展示。

---

## 解决方案结构

`DatabaseManager.sln` 由以下子项目组成：

| 项目 | 说明 |
| --- | --- |
| **DatabaseInterpreter.Core** | 核心数据访问与 Schema 解析引擎，支持多数据库类型 |
| **DatabaseInterpreter.*** | 针对各数据库类型的驱动/方言实现（SQL Server、MySQL、Oracle、PostgreSQL、SQLite 等） |
| **DatabaseConverter.Core** | 数据库结构与数据迁移（转换）核心引擎 |
| **DatabaseConverter.*** | 各数据库类型的转换适配器 |
| **DatabaseManager.Core** | 客户端共享的核心模型、配置、通用逻辑 |
| **DatabaseManager.CoreApp** | 主 WinForms 客户端（UI 与业务入口） |
| **DatabaseManager.Web** | ASP.NET Core MVC 壳项目（基础展示页） |
| **DatabaseManager.FileUtility** | 文件/导出辅助工具库 |
| **DatabaseManager.Profile** | 连接配置（Profile）持久化与管理 |

---

## 支持的数据库

DatabaseInterpreter / DatabaseConverter 通过统一抽象支持以下数据库类型（以 `DatabaseType` 枚举驱动）：

- Microsoft SQL Server
- MySQL
- Oracle
- PostgreSQL
- SQLite
- Microsoft Access（解析/脚本）

> 注：具体每种数据库的可支持操作（Schema 解析、脚本生成、数据读写、转换源/目标）以各 `DatabaseInterpreter.*` / `DatabaseConverter.*` 适配器实现为准。

---

## 核心能力

### 1. 数据库对象浏览（Objects Explorer）
- 左侧树形导航 `UC_DbObjectsExplorer` / `UC_DbObjectsComplexTree`，按 Schema → 表/视图/存储过程/函数/触发器/序列 等层次展示对象。
- 右键菜单快速执行：查询数据、设计表、生成脚本、查看依赖、导出等。
- 表/视图内容查看采用分页 `UC_Pagination` 与通用 `UC_DataViewer`。

### 2. 查询与脚本
- `frmSqlQuery` + `UC_QueryEditor`：SQL 编辑（语法高亮、自动完成）、执行与结果展示（`UC_QueryResultGrid`，可导出）。
- `frmTranslateScript`：跨数据库方言的脚本翻译（将一种数据库的 DDL/DML 翻译为另一种）。
- 对象级脚本生成（`ScriptGenerator`）：建表、索引、约束、外键等脚本生成。

### 3. 数据查看与编辑
- `UC_DataViewer`：表格化查看查询结果/表数据，支持分页、排序、筛选。
- `UC_Edit`：单条/批量数据编辑、新增、删除并提交到数据库。
- 大对象查看：`frmImageViewer`（图片）、`frmJsonViewer`（JSON）、`frmWktViewer`（空间 WKT 文本）。

### 4. 表设计器
`frmTableDesigner` + 子控件：
- `UC_TableColumns`：列定义（类型、默认值、是否为空）。
- `UC_TablePrimaryKey` / `UC_TableIndexes` / `UC_TableForeignKeys` / `UC_TableConstraints`：主键、索引、外键、约束管理。
- `UC_TableDesigner`、`UC_TableComment`：表级设计与注释。

### 5. 数据库转换（结构与数据迁移）
- `frmConvertSetting` + `frmConvertResult`（基于 `DatabaseConverter`）：
  - 源库 → 目标库的 **结构转换**（建表、索引、约束等）。
  - **数据转换**（按表/分页批量迁移）。
  - 支持多种源/目标数据库组合。

### 6. 结构与数据对比
- `frmDbObjectsCompareSetting` / `frmDbObjectsCompareResult`：两个数据库对象结构对比（表、列、索引差异）。
- `frmDataCompareSetting` / `frmDataCompareResult`：数据量/内容对比。

### 7. 诊断与优化
- `frmDiagnose`（基于 `DatabaseDiagnose`）：连接、对象、权限等诊断。
- `frmOpitimizeResult`（基于 `DatabaseOpitimize`）：索引/统计信息优化建议与结果。

### 8. 统计分析
- `frmStatistic`：表/字段级统计。
- `frmTableRecordCount`：各表记录数统计。
- `frmTableColumnContentMaxLength`：列内容最大长度分析。

### 9. 备份
- `frmBackupSetting`：备份配置与执行（依赖 `Backup` 模块与各数据库备份适配器）。

### 10. 依赖关系分析
- `frmDbObjectDependency` / `frmTableDependency`：分析表/对象之间的外键与引用依赖。

### 11. 数据库图表
- `frmDatabaseDiagram`：以图形方式展示表及表间关系。

### 12. 代码生成
- `frmCodeGenerator`：根据表结构生成实体类 / 数据访问代码（基于 `CodeGenerator`）。

### 13. 文档生成
- `frmDatabaseDocumentation`（`Documentation` 模块）：生成数据库结构文档（HTML/文本等）。

### 14. 导入 / 导出
- `frmImportForm` / `frmExportForm`：数据与结构在文件与数据库之间导入导出。
- 多种格式支持（Excel / CSV / XML / JSON 等，以 `FileUtility` 与导入导出适配器实现为准）。

### 15. 连接与配置管理
- `frmDbConnectionProfiles` / `frmDbConnectionManage`：管理数据库连接信息（Profile）。
  - 支持 **SSH 隧道** 连接（`UC_SSHTunnelProfile`）。
  - 支持 **连接字符串** 方式（`UC_ConnectionStringProfile`）。
  - 支持 **账号信息** 方式（`UC_DbAccountInfo` / `UC_DbConnectionProfile`）。
- `frmMain`：主界面，集成对象浏览器、查询、结果区、消息区（Dock 布局 `frmDockWindowBase`）。

---

## 自定义控件一览（部分）

| 控件 | 用途 |
| --- | --- |
| `UC_DbObjectsExplorer` / `UC_DbObjectsComplexTree` | 数据库对象树 |
| `UC_QueryEditor` / `UC_QueryResultGrid` | SQL 编辑与结果网格 |
| `UC_DataViewer` / `UC_Pagination` / `UC_Edit` | 数据查看、分页、编辑 |
| `UC_TableDesigner` 及列/索引/外键/约束子控件 | 表设计器 |
| `UC_DbObjectContent` | 对象内容展示 |
| `UC_DbConnectionProfile` / `UC_DbAccountInfo` / `UC_SSHTunnelProfile` / `UC_ConnectionStringProfile` | 连接配置 |
| `UC_Script` | 脚本展示 |
| `UC_TableColumnSelector`、`UC_TableForeignkeysSelector`、`UC_TableSelector` 等 | 选择辅助 |

---

## 构建与运行

### 环境要求
- .NET 8 SDK
- Windows（WinForms 客户端依赖 Windows 窗体）
- 对应数据库的客户端驱动（随 NuGet 包引入）

### 构建
```powershell
# 还原并构建整个解决方案
dotnet build DatabaseManager.sln

# 运行 WinForms 客户端
dotnet run --project DatabaseManager/DatabaseManager.CoreApp/DatabaseManager.CoreApp.csproj
```

### 运行 Web 壳项目（基础展示页）
```powershell
dotnet run --project DatabaseManager/DatabaseManager.Web/DatabaseManager.Web.csproj
```

---

## 目录说明

- `DatabaseInterpreter/`：核心数据访问与 Schema 解析引擎及其各数据库适配器。
- `DatabaseConverter/`：结构与数据迁移引擎及其各数据库适配器。
- `DatabaseManager/`：客户端（`CoreApp`）、核心（`Core`）、Web（`Web`）、工具（`FileUtility`、`Profile`）。
- `antdui.md`：UI 组件/样式相关说明。
- `migration-form-todo.md`：对象树/迁移窗体的待办记录。

---

## 待完善事项

参见 [todo.md](./todo.md)。
