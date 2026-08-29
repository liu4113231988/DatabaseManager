# DatabaseManager 扩展机制评估与指南（P2）

> 对应 todo-202608 P2「扩展机制」：评估数据库方言、导出器、代码模板和对象菜单的插件/模板扩展点，降低新增数据库类型的 UI 改造成本。评估结论 + 已落地的扩展点 + 各场景操作指南。

## 一、总体结论

| 扩展维度 | 现状评估 | 改造成本 | 本期动作 |
|---|---|---|---|
| 数据库方言 | **已高度收敛**。UI/AppCore 层完全由 `DatabaseType` 枚举 + `DbInterpreterHelper` 分发驱动，连接下拉、对象树文件夹（`SupportDbObjectType`）、图标均与方言无关 | 新增方言：UI 层**近零改动**；主要成本在 DatabaseInterpreter.Core（Interpreter/ScriptGenerator/Provider/方言 XML） | 无需改造（附清单） |
| 对象树右键菜单 | 原为 1567 行单体、无注册机制 | **已落地扩展点**：`IObjectTreeMenuContributor` 注册即可追加菜单项 | 菜单贡献者接口 + 注册表 |
| 导出器 | `DatabaseManager.FileUtility` 已是标准 Writer/Reader 继承体系（BaseWriter/BaseReader）；`ExportFileType` 枚举 + `DataExporter.ExportDataTable` 分发 | 新增格式：新 Writer/Reader 各一个类 + `ExportFileType` 加枚举值 + `ExportDataTable`/`Import` 各加一个分支 + `GetExportFormats` 加名称（共 4 处，约 100 行） | 未做注册表重构（格式数量少、分支稳定，抽象收益低；保留扩展指引） |
| 代码模板 | `CodeGenerator` 按 `ProgrammingLanguage` switch（CSharp/Java），模板硬编码在 `GenerateCSharpCode/GenerateJavaCode` | 新增语言：2 处 switch + 1 个模板方法 | 外部化（.tt/模板文件）**未实施**，见差距说明 |

## 二、已落地的扩展点

### 2.1 对象树右键菜单贡献者（`IObjectTreeMenuContributor`）

位置：`DatabaseManager.AppCore/Common/IObjectTreeMenuContributor.cs`。

```csharp
// 1. 实现贡献者
public class MyMenuContributor : IObjectTreeMenuContributor
{
    public int Order => 100;   // 内置菜单之后
    public bool AppliesTo(DbObjectTreeNode node)
        => node.NodeType == DbObjectTreeNodeType.DbObject && node.DbObject is Table;
    public void Contribute(ObjectTreeMenuContext context)
    {
        var item = new MenuItem { Header = "我的自定义操作" };
        item.Click += (_, _) => context.RunAsync(async () =>
        {
            // context.ViewModel 提供连接上下文；context.Node 是命中节点
        });
        context.MenuItems.Add(item);
    }
}

// 2. 启动时注册（如 App.OnFrameworkInitializationCompleted）
ObjectTreeMenuRegistry.Register(new MyMenuContributor());
```

内置菜单保持不变；贡献者菜单追加在内置项之后，按 `Order` 排序。

## 三、各扩展场景操作清单

### 3.1 新增一种数据库方言

1. `DatabaseInterpreter.Model`：`DatabaseType` 枚举加值。
2. `DatabaseInterpreter.Core`：实现 `XxxInterpreter`（继承 `DbInterpreter`）+ `XxxScriptGenerator`（继承 `DbScriptGenerator`）+ ADO Provider 封装；在 `DbInterpreterHelper.GetDbInterpreter` 的分发链中注册。
3. 方言配置：`DatabaseInterpreter.Core\Config\DataTypeSpecification\Xxx.xml`、`FunctionSpecification\Xxx.xml`（ConfigManager 从程序目录 Config\ 加载，天然外部可扩展——放发布目录即可覆盖默认）。
4. **UI 层：通常无需任何改动**（连接类型下拉来自 `GetDisplayDatabaseTypes()`；对象树文件夹由 `SupportDbObjectType` 驱动）。
5. 已知硬编码点（新方言可能需要跟进的 2 处）：`DefaultDbSchemaService.TryGetSchemasAsync`（SqlServer/Postgres/Oracle 字符串分支）、`ObjectTreeContextMenuBuilder` 内个别方言文案。

### 3.2 新增一种导入/导出格式

1. `DatabaseManager.FileUtility`：实现 `XxxWriter : BaseWriter`（`Write(DataTable, tableName)` 返回文件路径）和/或 `XxxReader : BaseReader`（`Read(onlyReadHeader)` 返回 `DataReadResult`）。
2. `DatabaseManager.FileUtility.Model`：`ExportFileType` 枚举加值。
3. `DatabaseManager.Core\Export\DataExporter.cs`：`ExportDataTable` 加分支调用新 Writer（SQL 类需要 interpreter 的格式参考 `WriteToSql`）；`Import\DataImporter.cs`：按扩展名分支调用新 Reader（参考 `ReadFromJson`）。
4. `DefaultExportImportService`：`GetExportFormats()` 加格式名、`ParseExportFileType` 加映射、`PreviewFileAsync` 加扩展名分支；`ExportWindow/ImportWindow` 的文件过滤器跟随。

### 3.3 新增一种代码生成语言

`DatabaseManager.Core\Generator\CodeGenerator.cs`：`ProgrammingLanguage` 枚举加值 → switch 加 case → 实现 `GenerateXxxCode` 模板方法（参考 `GenerateCSharpCode`）；`DefaultCodeGenerateService.ParseLanguage` 加字符串映射。

### 3.4 新增对象树右键菜单项

见 2.1；仅临时/内置菜单才需要改 `ObjectTreeContextMenuBuilder`（新增回调参数 + Build 方法）。

## 四、差距与后续建议（未实施）

1. **插件宿主**：目前无 MEF/AssemblyLoad 插件目录；如需运行时插件，可基于 `ObjectTreeMenuRegistry` / 导出 Writer 注册表的同一模式扩展为「扫描 Plugins\ 目录加载程序集」，风险点在版本兼容与安全审查。
2. **代码模板外部化**：将 `CodeGenerator` 模板迁移为数据文件（T4 或占位符模板）可支持用户自定义，需设计模板变量契约。
3. **导出 Writer 注册表**：格式数超过 ~8 种或出现第三方格式需求时，把 `ExportDataTable` 分支重构为 `IExportWriter` 注册表（`GetExportFormats` 从注册表枚举）。
4. **方言分支收敛**：`DefaultDbSchemaService.TryGetSchemasAsync` 的字符串分支可下沉为 `DbInterpreter` 虚属性（如 `SupportsMultipleSchemas`），进一步消除 UI 层方言判断。
