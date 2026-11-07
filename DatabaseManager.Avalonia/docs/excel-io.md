# Excel 导入导出

2026-11-07：Excel 数据读写接入 `MiniExcel 1.46.0` 稳定版，版本统一维护在根目录 `Directory.Packages.props`。业务层继续使用 `ExcelReader` / `ExcelWriter`，覆盖 Avalonia 和旧客户端的导入、预览、数据导出及校验/比较结果导出。

| 路径 | 实现与行为 |
| --- | --- |
| `.xlsx` 导入、预览 | MiniExcel 按工作表逐行读取；表头预览在首个有效行后停止 |
| `.xlsx` 多工作表导入 | 按工作表顺序合并数据，避免行号键冲突；有表头时要求各表表头一致 |
| `.xlsx` 数据导出 | MiniExcel，按行枚举；普通表头采用 MiniExcel 默认样式；达到工作表上限时分表，并为表头预留一行 |
| 批注、高亮 | MiniExcel 输出数据后，使用 Open XML SDK补上单元格/行批注、红色背景与粗体高亮 |
| 旧 `.xls` 导入 | 不支持；已从导入筛选、预览和核心导入入口移除 |
| Word 导出 | 使用 Open XML SDK，见 [Word 评估](word-export.md) |

空结果仍生成有效工作簿；有表头、无表头、重复/空白表头和表头空格分别保留。`.xlsx` 有表头时跳过表头前的全空行；数据区全空记录保留，避免丢失全 NULL 记录，工作表中间的空行也会计入数据。`.xlsx` 中不同表头的工作表会报告错误，避免把不同列的数据静默合并。

超过 Excel 15 位有效数字范围的 `long` 按文本导出，避免标识符被 Excel 舍入；以 `=` 开头的字符串保持文本，不作为公式执行。日期沿用原来的 UTC 文本导出行为。

导出先写同目录临时工作簿，成功完成批注处理后替换目标文件。读写失败时清理临时文件，保留原有导出文件。只生成 `.xlsx`，不把新格式内容写入 `.xls` 后缀文件。

## 兼容与性能边界

NPOI 包及源码调用已全部移除，旧 `.xls` 会明确报不支持。批注使用 Open XML 标准 comments 部件和 VML 锚点，高亮使用标准样式表；相关结构通过 SDK schema 校验。MiniExcel 的格式范围依据 [官方文档](https://github.com/mini-software/MiniExcel)。

MiniExcel 读写采用逐行枚举，但当前业务层仍持有 `DataTable`、`GridData` 或完整导入字典；带批注的导出还会进入 Open XML DOM 后处理。因此不能据此承诺端到端恒定内存或发布体积下降。

## 验证

`ExcelMigrationChecks` 覆盖：中文/特殊字符、长整数、公式样式文本、空数据、无表头、重复/空白表头、多表与稀疏列、表头预览、批注/高亮、失败导出保留原文件，以及旧 `.xls` 拒绝。`PackageCompatibilityChecks` 继续验证 CSV/Excel 往返和 Word 导出。

百万行分表检查可显式启用，使用重复引用的合成行控制测试输入内存：

```powershell
$env:DBM_TEST_LARGE_EXCEL = '1'
dotnet run --project DatabaseManager.Avalonia/DatabaseManager.AppCore.RegressionTests -c Release
Remove-Item Env:DBM_TEST_LARGE_EXCEL
```

2026-11-07 验收：主解决方案及旧 WinForms 客户端 Release 构建、完整回归、百万行分表检查、Windows P0/P2 隔离 UI 冒烟均通过；NuGet 已知漏洞审计（含传递依赖）为零。未做真实数据库回放、其他平台或端到端性能基准。
