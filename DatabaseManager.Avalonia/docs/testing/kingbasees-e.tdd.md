# KingbaseES E / F 阶段：运维能力与质量门禁测试证据

## 范围

本阶段仅支持 KingbaseES 的 PG 兼容模式，覆盖只读会话/阻塞链监控、用户/权限、查询剖析、备份恢复、全库搜索与定时任务的代码级门禁；Oracle / SQL Server 兼容模式明确拦截。

## 官方依据

- `sys_stat_activity` 用于读取进程与会话信息；`sys_locks` 可与其按 `pid` 关联。
- `sys_blocking_pids(pid)` 用于识别阻塞会话，优于自行比对锁模式。
- `sys_terminate_backend(pid)` 终止会话；会导致该会话正在执行的事务回滚。调用者须是目标角色成员或拥有 `sys_signal_backend`，且终止超级用户会话仅限超级用户。
- 部分金仓版本（如精简安装或历史版本）仅暴露 `pg_stat_activity` / `pg_terminate_backend`。客户端在探测到 `sys_catalog.stat_activity` 缺失时自动回退到 `pg_*` 路径，保证同一连接配置仍可工作。

## 自动化回归

`DatabaseManager.AppCore.RegressionTests` 覆盖：

- KingbaseES 会话 SQL 使用 `sys_stat_activity`；
- 阻塞链 SQL 使用 `sys_blocking_pids()`；
- 终止会话 SQL 使用 `sys_terminate_backend(pid)`；
- 未暴露 `sys_catalog.stat_activity` 时的回退 SQL（pg_* 系列）；
- 探测 SQL 命中 `sys_catalog` Schema；
- 非数值 PID 被拒绝，避免将用户输入拼接为可执行 SQL。

`KingbaseStageEFChecks` 覆盖：

- 能力矩阵：用户、会话、剖析、备份/恢复、全库搜索方言与发布状态标注；
- 用户/权限 SQL：`sys_catalog.sys_roles` 优先、`pg_catalog.pg_roles` 回退，`information_schema` 授权/角色成员视图，CREATE/DROP USER、GRANT 语法与字面量转义；
- 备份恢复：工厂返回 `KingbaseBackup`，`sys_dump` / `ksql` / `sys_restore` 工具名识别，`pg_dump` 不被静默使用；
- 兼容模式门控：Oracle / SQL Server 模式下用户管理与备份/恢复均拒绝执行；
- 全库搜索：双引号限定标识符、LIKE 反斜杠转义与 `LIMIT` 分页；
- 发布标注：驱动包、默认端口、代码级支持模式与 `IsInstanceVerified = false`；
- 可选真实实例探针：`DBSMOKE_KINGBASE_CONNSTR` 与 `DBSMOKE_KINGBASE_ADMIN_CONNSTR` 未设置时明确跳过；设置时验证连接、Unicode 标量与角色/授权读取。

## 待真实 V8 实例验收

1. 普通业务账号、监控账号与超级用户分别读取会话和锁，确认可见范围与错误提示。
2. 以两个会话构造锁等待，确认阻塞方和被阻塞方 PID 正确显示。
3. 对业务账号会话执行终止，确认函数返回值、客户端断开及未提交事务回滚。
4. 验证终止自身、超级用户会话及无 `sys_signal_backend` 权限时的失败信息。
