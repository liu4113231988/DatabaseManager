# SQL 格式化与视图转换

转换引擎将已弃用的 `PoorMansTSqlFormatterRedux 1.0.3` 替换为 `Hogimn.Sql.Formatter 2.0.5`。转换入口 `DbObjectTranslator.FormatSql(sql, out hasError)` 与编辑器入口 `DatabaseManager.AppCore.Common.SqlFormatter` 共用 `SqlFormattingHelper`。编辑器按当前标签的连接类型选择方言，无连接时使用 Standard SQL；支持选区格式化和一次撤销。

## 方言与保真

| 目标数据库 | 使用的格式化方言 |
| --- | --- |
| SQL Server | T-SQL |
| MySQL | MySQL |
| PostgreSQL、KingbaseES（PG 兼容） | PostgreSQL |
| Oracle、DM | PL/SQL 格式化器，用于视图 SQL 与编辑器排版 |
| SQLite、DuckDB 等 | Standard SQL |

每次调用独立创建格式化器，使用四空格缩进并保留原有大小写。不传入参数替换值。格式化前后使用该方言的词法器比较 token 类型及内容，包含字符串、标识符、注释和运算符；异常或不一致时返回原文并设置 `hasError=true`。空输入保持原样。

`hasError` 表示格式化失败，不表示 SQL 语法错误或正确。语法验证与转换继续使用现有分析器。词法保真检查不能替代真实数据库的语义等价验证；检查较保守，例如注释文本被裁剪时也会退回原文。

## MySQL 视图的 JOIN/ON

移除原有按格式化行文本寻找 `JOIN` 并补 `ON 1=1` 的处理。该推断依赖排版，也会误判字符串或注释中的关键字。格式化现在只排版，不增加连接条件，不改变已有 `ON`、`USING` 或 `CROSS JOIN`。

MySQL 允许的无条件 `JOIN` 在某些目标方言中不可直接执行；本次迁移不自动将其转换为 `CROSS JOIN` 或补条件。此类跨库语法需要单独的语法转换与目标数据库验收，不能依赖美化器推断。

## 范围与验证

移除编辑器原先直接对整段文本执行的正则排版，避免更改字面量内容或把下一行 SQL 合并进行注释。Hogimn 不支持存储过程和自定义分隔符，本次未将其接入过程、函数或触发器的转换路径；编辑器保真回退不等于对这些语法提供完整支持。

视图转换不再全局替换比较运算符和引号。引号标识符仍由现有 token 转换处理，字符串保持原样。已有 `CREATE VIEW` 头按 token 边界查找顶层 `AS`，保留显式列名列表，避免误用视图名、DEFINER 或注释里的 `as`。

`SqlFormattingChecks` 在控制台回归中覆盖：各目标方言、大小写、字符串、引号标识符、注释、PostgreSQL dollar 字符串/JSON/参数、MySQL DEFINER、多语句、幂等性及安全回退；通过 SQLite 实际执行比较编辑器排版前后的值和结果集边界。视图检查覆盖 JOIN/LEFT JOIN/USING/CROSS JOIN/子查询、字面量、比较运算符和视图头，检查 token 内容并运行现有目标语法分析器。P0 冒烟覆盖真实编辑器方言、全文、选区和撤销行为。

2026-11-06：Release 解决方案构建通过；完整回归（包含 SQLite 加密检查）输出 `All regression checks passed.`；Windows P0 隔离 UI 冒烟退出码为 0。转换引擎、AppCore、客户端与回归项目的依赖图均只引用 Hogimn 2.0.5，不再包含旧格式化包。

运行命令见 [验证与冒烟说明](../../docs/smoke/verification.md)。本地评估依据为 [Hogimn 2.0.4 官方说明](https://www.nuget.org/packages/Hogimn.Sql.Formatter/2.0.4)；存储过程、真实数据库回放与性能基准未在本轮验收。
