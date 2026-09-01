# KingbaseES D 阶段测试证据

## 来源与用户旅程

来源为 `docs/kingbasees-support-plan.md` 的阶段 D。

- 作为用户，我可以对 KingbaseES PG 兼容库生成可回放的 SQL/DDL/同步脚本。
- 作为用户，我可以导入数据；在供应商 COPY 接口未验证前，工具会使用安全的参数化批量插入，而不是尝试 Npgsql 专用二进制 COPY。

## RED / GREEN 记录

| 行为 | RED 证据 | GREEN 证据 | 保证 |
|---|---|---|---|
| 脚本生成器注册 | `dotnet run --project DatabaseManager.AppCore.RegressionTests... --no-restore`：`KingbaseES PG 兼容路径应使用 PostgreSQL 脚本生成器` 失败 | 同一命令输出 `All regression checks passed.` | KingbaseES 选择 `PostgresScriptGenerator`，SQL 导入导出和 DDL 脚本不再取得空生成器。 |
| 批量导入保护 | 同一 RED 运行先因空脚本生成器失败 | 同一 GREEN 运行通过 `!kingbaseInterpreter.SupportBulkCopy` 断言 | 不会把 `KdbndpConnection` 传给 Npgsql 的二进制 COPY；保留参数化插入回退。 |
| 转换能力门控 | `DefaultConvertService.GetConversionBlockReason(KingbaseES)` 返回 null，导致静默套用 PG 翻译规则 | 新增断言：KingbaseES 返回含“未验证”的提示、Postgres 返回 null、KingbaseES 在 `UnverifiedConversionTypes` 集合中 | `ConvertAsync`/`PreviewAsync`/`LoadSchemaMappingsAsync` 三个入口都拦截未验证类型，避免静默执行跨库转换。 |
| 金仓入口核查 | 对全部 `DatabaseType.Postgres` 分支与金仓入口逐项核对；`Optimizer.cs` 注释声明“Postgres / Kingbase 执行 VACUUM”但分支只判 Postgres | 修复为 `databaseType is Postgres or KingbaseES` 并更新“暂不支持”提示；`dotnet build` 0 errors、`All regression checks passed.` | 消除 MVP 范围内唯一遗漏的分支；会话/锁、用户/权限、备份、诊断/碎片分析等阶段 E 功能按规划刻意不补，后续按实例验收后接入。 |

## 已知缺口

尚未用真实 V8 实例验证 `RETURNING`、事务、锁、类型 round-trip，以及 CSV/Excel/JSON/XML/SQL 的实际导入导出。它们已具备 PG 方言代码路径，需后续实例测试后再决定是否调整。跨库转换能力门控已就位，待结构翻译/类型映射/数据回放验证通过后，将 KingbaseES 从 `UnverifiedConversionTypes` 移除即可开放转换。

会话/锁现已依据金仓官方 `sys_stat_activity`、`sys_blocking_pids()` 和 `sys_terminate_backend(pid)` 完成代码级接入；仍须使用真实 V8 PG 兼容实例验证普通账号与监控账号的可见范围、权限不足提示及终止会话行为。用户/权限、备份恢复、诊断/碎片分析仍待接入，未用真实实例验证前不得静默套用 PG 规则。
