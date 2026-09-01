# KingbaseES E 阶段：会话与锁测试证据

## 范围

本阶段仅支持 KingbaseES 的 PG 兼容模式，覆盖只读会话/阻塞链监控与用户确认后的会话终止。

## 官方依据

- `sys_stat_activity` 用于读取进程与会话信息；`sys_locks` 可与其按 `pid` 关联。
- `sys_blocking_pids(pid)` 用于识别阻塞会话，优于自行比对锁模式。
- `sys_terminate_backend(pid)` 终止会话；会导致该会话正在执行的事务回滚。调用者须是目标角色成员或拥有 `sys_signal_backend`，且终止超级用户会话仅限超级用户。

## 自动化回归

`DatabaseManager.AppCore.RegressionTests` 覆盖：

- KingbaseES 会话 SQL 使用 `sys_stat_activity`；
- 阻塞链 SQL 使用 `sys_blocking_pids()`；
- 终止会话 SQL 使用 `sys_terminate_backend(pid)`；
- 非数值 PID 被拒绝，避免将用户输入拼接为可执行 SQL。

## 待真实 V8 实例验收

1. 普通业务账号、监控账号与超级用户分别读取会话和锁，确认可见范围与错误提示。
2. 以两个会话构造锁等待，确认阻塞方和被阻塞方 PID 正确显示。
3. 对业务账号会话执行终止，确认函数返回值、客户端断开及未提交事务回滚。
4. 验证终止自身、超级用户会话及无 `sys_signal_backend` 权限时的失败信息。
