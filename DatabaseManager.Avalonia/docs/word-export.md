# Word 导出与开源库评估

2026-11-07，当前 Word 导出内容为动态生成的标题、说明和表格；支持多节内容、表头粗体、前景/背景颜色、居中及单元格垂直对齐。现有模型不需要 Word/PDF 排版引擎或 Office 安装。

## 候选比较

| 候选 | 许可与模式 | 当前项目的适配判断 |
| --- | --- | --- |
| [Open XML SDK](https://github.com/dotnet/Open-XML-SDK) / `DocumentFormat.OpenXml` | MIT；底层 DOCX 结构操作 | **本轮采用 3.5.1**。能覆盖现有标题和动态表格；Excel 批注/高亮也需要同一 SDK，无需再增加 Word 专用依赖。代价是需要自行构造段落、样式和表格。 |
| [OfficeIMO.Word](https://github.com/EvotecIT/OfficeIMO/blob/master/OfficeIMO.Word/README.md) | MIT；基于 Open XML SDK 的高层文档 API | 后续需要页眉页脚、目录、复杂段落或更多文档编辑能力时优先评估。现有需求较简单，额外引入其高层库及配套依赖的收益有限。 |
| [MiniWord](https://github.com/mini-software/MiniWord) | Apache-2.0；DOCX 模板填充，支持表格及循环 | 适合固定版式、由用户维护模板的报告。当前列由用户选项动态决定，迁移还需要模板资产、动态列设计和验证，成本高于直接构造表格。 |

OfficeIMO.Word 与 MiniWord 本轮仅核对官方文档及许可，未安装或运行原型。高层库维护成本与迁移成本属于结合现有代码作出的判断。

[DocX 当前项目说明](https://github.com/xceedsoftware/DocX)注明新版社区许可面向非商业使用，未列入本次宽松许可证方案。

## 当前实现

`WordWriter` 使用 Open XML SDK 保留原业务接口，NPOI 已从全部项目引用中移除。颜色接受六位 RGB，可带 `#`，写入前统一校验；文本由 SDK 转义，保留中文和空格，换行写为 Word 显式换行元素。

生成文件先写同目录临时 DOCX，成功后替换目标；数据行列数不一致、颜色非法或生成失败时保留原文件并清理临时文件。保留表头颜色、粗体、水平/垂直居中及标题说明；不提供 `.doc`、Word 渲染或 PDF 转换。

`WordExportChecks` 校验：Open XML schema、多段与空表格、标题说明和 XML 特殊字符、中文/空格/换行、颜色与对齐、失败时原文件完整性。结构校验不等于 Word/LibreOffice 的实际版式验收。
