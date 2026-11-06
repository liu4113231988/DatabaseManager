# 依赖版本与升级

所有项目的直接 NuGet 版本统一维护在根目录 `Directory.Packages.props`。项目文件只声明 `PackageReference Include`，不再各自固定版本。同一个包升级时只修改中央版本，避免 `Geometry -> MySqlConnector 2.6.2` 与 `Core -> MySqlConnector 2.5.0` 形成 NU1605 降级冲突。

2026-11-06 按 NuGet 官方源核对并升级到最新稳定版（不使用预览版）。没有更新版本的包保留当前版本；具体版本以中央文件为准。

| 依赖组 | 主要版本 |
| --- | --- |
| Avalonia / AtomUI | 12.1.5 / 6.2.13；DataGrid 12.1.2、AvaloniaEdit 12.0.0 |
| SQL Server / MySQL / PostgreSQL | SqlClient 7.1.1 / MySqlConnector 2.6.2 / Npgsql 10.0.3 |
| SQLite / 加密原生实现 | Microsoft.Data.Sqlite.Core 10.0.14 / SQLite3MC 2.6.1 |
| Oracle / DuckDB | 23.26.400 / 1.5.6.1 |
| SQL 格式化与分析 | Hogimn 2.0.5、TSQL.Parser 2.6.0、SqlParserSharp 2.2.0、ANTLR 4.13.1 |
| 文件与压缩 | NPOI 2.8.1、CsvHelper 34.0.0、SharpZipLib 1.4.2 |

移除 .NET 10 自带的旧 `System.Net.Http`、正则及 XML 框架包引用；旧 `SharpZipLib.NETStandard` 改为维护中的 `SharpZipLib`。NPOI 2.8 改用 SkiaSharp，项目没有直接调用 ImageSharp，因此删除原先为旧 NPOI 固定的 ImageSharp 引用。XML 和 PKCS 使用 10.0.14 安全补丁，避免数据库驱动或文档库带入旧版。

引用 .NET 10 核心库的示例和认证测试也统一为 .NET 10，避免示例项目停留在 .NET 8 而无法引用新核心库。

FileUtility 同步为 .NET 10。CsvHelper 34 的 .NET Standard 与 .NET 10 资产在 `init` 选项 setter 上存在运行时签名不一致：编译成功仍可能出现 `MissingMethodException`。让文件读写库和客户端使用同一组 .NET 10 资产，避免该问题；不再提供 FileUtility 的 .NET Standard 输出。

CsvHelper 34 重写了公开 API，文件读写入口改用 `CsvReader.Create` / `CsvWriter.Create`、不可变选项及 `EndRecord`。显式保留逗号分隔、当前区域格式、原有编码和表头规则；读取表头不额外消耗第一条数据。畸形引号仍按错误处理，不采用新版默认的宽松恢复。迁移依据见 [官方升级说明](https://joshclose.github.io/CsvHelper/migration/v34.0.0.html)。

## NPOI 许可证变化

[NPOI 2.8.1 官方包说明](https://www.nuget.org/packages/NPOI/2.8.1)引入二进制发行版的 OSMF EULA，并说明面向产生营收的使用者收取维护费。使用、分发或商业部署前需要审阅该版本随包提供的条款；项目自身的 MIT 许可证不能替代依赖许可证。本次升级不包含购买或支付任何许可费用，也没有代替使用者设置 `AcceptNPOIOSMFLicense=true`；构建保留该包的许可证提示。

## 验证

```powershell
dotnet restore DatabaseManager.sln --force-evaluate
dotnet build DatabaseManager.sln -c Release --no-restore
dotnet run --project DatabaseManager.Avalonia/DatabaseManager.AppCore.RegressionTests -c Release
dotnet list DatabaseManager.sln package --outdated --include-transitive
dotnet list DatabaseManager.sln package --vulnerable --include-transitive
```

`PackageCompatibilityChecks` 使用临时文件验证 CSV/Excel 中的中文、引号、逗号和换行可往返读写，Word 文档内容、ZIP 输出、NCalc 表达式与 DuckDB 原生库加载。SQL 格式化、SQLite 加密与事务继续执行现有回归，UI 包升级需执行 P0/P2 隔离冒烟。真实数据库驱动认证和行为仍需专用服务器联调。
