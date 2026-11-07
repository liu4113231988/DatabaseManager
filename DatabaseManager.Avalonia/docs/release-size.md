# v1.1.3 发布体积与裁剪评估

2026-11-07。正式 Windows/Linux x64 包采用自包含部署，关闭 ReadyToRun 和 PublishTrimmed。解压后无需安装 .NET；Linux 仍依赖系统桌面环境及原生图形库。

```powershell
dotnet publish DatabaseManager.Avalonia/DatabaseManager.Avalonia/DatabaseManager.Avalonia.csproj -c Release -r win-x64 --self-contained true -p:PublishTrimmed=false -p:PublishReadyToRun=false -p:UsedAvaloniaProducts= -o .artifacts/release-win-x64
```

Linux 将 RID 改为 `linux-x64`。发布工作流检查随包 CoreCLR、hostfxr、System.Private.CoreLib，不包含 NPOI，且不分发本地 Profiles；生成 ZIP/TAR.GZ 和 SHA-256 校验文件。

## 本地 Windows x64 对照

同一 SDK、Release、关闭 ReadyToRun；大小为目录文件长度总和，使用十进制 MB，非下载压缩包大小。

| 方案 | 字节数 | MB | 结论 |
| --- | ---: | ---: | --- |
| 迁移 NPOI 后的框架依赖包 | 147,565,791 | 147.6 | 需安装 .NET，不满足本次要求 |
| 自包含基线 | 227,914,367 | 227.9 | 包含 .NET 运行时 |
| 移除无用包后的正式自包含包 | 225,414,286 | 225.4 | 本次发布方案 |
| 同一版本部分裁剪实验 | 150,200,229 | 150.2 | 仅实验，不发布 |

自包含增加的是运行时文件，并非应用代码同幅增长。未发现 Inter 字体注册/资源引用和 Dock 控件/模型使用；现用字体由 AtomUI 提供，DockPanel 为 Avalonia 自带布局。删除 `Avalonia.Fonts.Inter`、`Dock.Avalonia` 的直接引用及中央版本后减少约 2.5 MB。保留实际使用的 FluentTheme、MessageBox、数据库驱动、SQL 解析器和文档处理库。

正式包与部分裁剪包在空 DOTNET_ROOT 下直接启动 EXE，P0/P2 均退出 0；正式包宿主日志确认加载的是发布目录内的 `coreclr.dll`。此检查覆盖 UI 启动，不能替代数据库和全部导入导出路径测试，也不等同于全新操作系统验证。

## 裁剪判断

全局命令行 `PublishTrimmed=true` 会传播到 netstandard 项目，触发 NETSDK1124。实验改为仅在 GUI 项目临时启用 `PublishTrimmed=true`、`TrimMode=partial`，自包含发布成功，日志记录 712 次 IL 警告（包含构建和链接阶段，非去重计数；未抑制）。实验后恢复 `PublishTrimmed=false`。

部分裁剪减少约 75.2 MB（33.4%），但存在以下未解决项：

- 反射创建视图、Avalonia 反射绑定，以及 JSON 序列化需要明确保留规则或迁移到可静态分析的方式。
- Newtonsoft.Json、System.Configuration 与数据库驱动内部反射产生警告；DataSet/ODBC 元数据处理也有警告。
- 实验 runtimeconfig 默认关闭 `System.Text.Json.JsonSerializer.IsReflectionEnabledByDefault`、`System.Data.DataSet.XmlSerializationIsSupported` 等能力，UI 冒烟通过不代表数据操作兼容。

因此本次不发布裁剪包，不通过忽略警告或直接删除 DLL 缩小包。后续应先处理这些实际调用路径，再以真实数据库、加密 SQLite、格式化、会话恢复和文件导入导出矩阵验证。完整裁剪/NativeAOT 需要更广的兼容性工作，本次未推进。

依据：[Microsoft 裁剪选项](https://learn.microsoft.com/en-us/dotnet/core/deploying/trimming/trimming-options)、[已知不兼容模式](https://learn.microsoft.com/en-us/dotnet/core/deploying/trimming/incompatibilities)。微软明确列出反射序列化、动态程序集加载等限制，不能将构建成功视作兼容证明。

## 继续减小体积的可选方向

优先评估重复的 SQL 解析链路及大型数据库驱动的实际用途。DuckDB、SqlParserSharp、Open XML、Oracle 等占用明显，但当前均有功能调用，删除会减少功能；按数据库拆分下载组件需要额外的分发、加载和兼容性设计，不在本次范围。ZIP/TAR.GZ 已降低下载体积，单文件打包主要改善分发形式，不能当作代码裁剪收益。
