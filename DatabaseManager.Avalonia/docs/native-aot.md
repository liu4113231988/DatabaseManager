# Windows Native AOT 发布

Native AOT 的发布目标是 Avalonia 客户端的 Windows x64。正式的 Windows 和 Linux x64 自包含 CoreCLR 包继续使用现有发布流程。Linux AOT 不在本项目的支持范围内。

## 首阶段验收范围

从 Windows x64 的普通 SQLite 和 SQLCipher 文件开始，覆盖连接、对象树、表与列元数据、表设计、查询、配置读取以及导入导出。每项都使用发布后的 EXE 验证。P0、P2 隔离冒烟和控制台回归是这一阶段的基本检查。其他数据库按驱动和认证方式逐项验证后纳入支持范围。

初始试验生成了 Windows x64 原生 EXE。P0 通过，P2 的查询构建器未能取得字段。独立探针确认，同一条 SQLite 查询使用 ADO.NET 可以返回结果，Dapper `QueryAsync<T>` 的对象映射在 AOT 下会因运行时代码生成抛出 `PlatformNotSupportedException`。

## 本地实验发布

在安装了 .NET 10 SDK 和 Visual Studio C++ x64 工具链的 Windows 上，从仓库根目录运行下面的命令。

```powershell
dotnet publish DatabaseManager.Avalonia/DatabaseManager.Avalonia/DatabaseManager.Avalonia.csproj `
  -c Release -r win-x64 -p:DatabaseManagerNativeAot=true -p:UsedAvaloniaProducts= `
  -o .artifacts/aot-win-x64
```

从单独的工作目录对发布目录中的 `DatabaseManager.exe` 分别执行 `--smoke --p0` 和 `--smoke --p2`，检查退出码及各自的 `artifacts/*-ui/passed.txt`。发布目录还包含数据库和渲染组件需要的原生库与配置，应整体使用。

## 改造进度

SQLite 表、视图、列、触发器和键的元数据，以及本地连接和账号配置，已改用显式 ADO.NET 映射。工作台预览改用有类型的单元格模板，工作台和仪表盘的设置逐步改用源生成 JSON。控制台回归覆盖 SQLite 元数据、连接配置往返和旧版仪表盘 JSON。

2026-11-19，Windows x64 Release 构建和控制台回归通过。普通构建和 Native AOT 发布产物均通过 P0、P2 隔离冒烟，覆盖加密 SQLite 连接表单及 SQLite 查询构建、数据生成、质量剖析、数据字典、脱敏和仪表盘。AOT 发布仍有裁剪和运行时代码生成诊断，完整表设计、导入导出、其他数据库及认证方式仍待单独验收。

## 实施顺序

1. 将 AOT 设为 Avalonia 入口的独立构建选项，仅还原和发布 `win-x64`。先改造 SQLite 的元数据及本地连接配置路径。
2. 迁移运行时反射绑定、视图和 SQL 转换器工厂。将持久化的数据结构迁移到源生成 JSON，并验证既有配置和导入文件。
3. 逐项验证数据库驱动、认证方式和导入导出依赖。确认功能覆盖并处理涉及实际调用路径的裁剪和动态代码警告后，再考虑正式发布。

参考 [Avalonia Native AOT 指南](https://docs.avaloniaui.net/docs/deployment/native-aot) 和 [.NET Native AOT 部署](https://learn.microsoft.com/dotnet/core/deploying/native-aot/)。
