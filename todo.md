# 对象浏览器右键菜单完善计划（Avalonia 版，按优先级）

> 参考 `resources/dbeaver-screenshot/` 下各节点右键菜单截图（connection / tables / table / columns / column / constraints / index / trigger）。
> 目标：不追求与 DBeaver 1:1 对齐，优先补齐**常用高频功能**。
> 当前右键菜单在 `DatabaseManager.Avalonia/DatabaseManager.Avalonia/Views/MainWindow.axaml.cs` 的 `ObjectsTree_ContextRequested` 中硬编码，能力有限。

## P0（核心常用，优先实现）

- [ ] **连接节点右键菜单完善**
  - 保留：连接 / 重连 / 断开 / 新建查询
  - 新增：刷新连接（重新加载对象树）、编辑连接（打开连接管理并定位）、删除连接、重命名连接、SQL Editor（新建查询并聚焦）
- [ ] **表 / 视图节点右键菜单完善**
  - 已有：查看数据（SELECT）、编辑数据、设计表
  - 新增：Generate SQL 子菜单（SELECT / INSERT / UPDATE / DELETE / CREATE）
  - 新增：复制对象名、复制完整路径（schema.table）
  - 新增：删除对象、重命名对象
  - 新增：刷新
- [ ] **类型文件夹（Tables / Views / Procedures / Functions / Triggers 等）右键菜单**
  - 新建对象（如 Create New Table / View）
  - 刷新
- [ ] **通用菜单项（所有节点）**
  - 复制名称 / 复制完整路径
  - 刷新（F5）

## P1（高频补齐）

- [ ] **Database / Schema 节点右键菜单**
  - 设为当前数据库 / Schema
  - 新建查询
  - 刷新
- [ ] **视图节点右键**
  - 查看数据、Generate SQL、刷新、删除、重命名
- [ ] **列节点 / Columns 文件夹右键**
  - 查看列 / 查看所有列
  - 新建列
  - 删除列、重命名列
- [ ] **索引 / 约束 / 触发器子对象右键**
  - 查看、删除、重命名、刷新父节点
- [ ] **表节点右键 导出 / 导入数据**
  - 打开已有 ExportWindow / ImportWindow，预填连接名与表名

## P2（体验增强）

- [ ] **菜单分组与图标**
  - 用 Separator 分隔：Open/View、DDL、数据、管理、剪贴板、刷新
  - 菜单项加小图标与快捷键提示（F4 查看、F5 刷新、Delete 删除、F2 重命名）
- [ ] **Generate SQL 子菜单扩展**
  - 表：SELECT *、SELECT TOP N、INSERT 模板、UPDATE 模板、DELETE、CREATE TABLE
  - 视图：CREATE VIEW
  - 列：ALTER COLUMN / DROP COLUMN 模板
- [ ] **Filter / Browse from here**
  - 表/视图节点右键 Filter：弹出过滤输入或打开 SQL Editor 预填 `SELECT * FROM table WHERE`
- [ ] **Compare / Migrate**
  - 连接/数据库/表节点挂接 SchemaCompareWindow / DataCompareWindow / ConvertWindow
- [ ] **Copy Advanced Info**
  - 连接：复制连接字符串
  - 表：复制 schema.table
  - 列：复制 `name type nullable default`

## 依赖与阻塞（实现前需确认/补齐）

- [ ] 统一对话框工具：在 `MainWindow.axaml.cs` 增加 `ShowInfoAsync/ShowErrorAsync`（或抽出 `DialogHelper`，引用 `MsBox.Avalonia`）
- [ ] DDL 脚本生成：为 `IDbSchemaService` 补充 `GenerateInsertScript` / `GenerateUpdateScript` / `GenerateDeleteScript` / `GenerateCreateScript` / `GenerateDropScript` 等方法（`IDbSchemaService.cs` 当前为空接口）
- [ ] 对象删除 / 重命名后端接口：`IDbSchemaService` 需提供对应方法（表/列/索引/约束/触发器）
- [ ] 连接重命名 / 编辑：`IDbConnectionService` 仅有 `DeleteAsync`，需补充单条更新 / 重命名能力

## 建议落地顺序

1. 先重构 `ObjectsTree_ContextRequested` 为按节点类型分发（抽 `ObjectTreeContextMenuBuilder`）。
2. 补齐 P0：连接节点、表节点、类型文件夹、通用 Copy/Refresh。
3. 补齐 P1：Database/Schema、列/索引/约束/触发器、导出导入入口。
4. 最后做 P2：图标、快捷键、Generate SQL 子菜单、Filter 等体验项。
