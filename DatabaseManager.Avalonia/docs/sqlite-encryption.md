# SQLite 加密访问

## 依赖与连接规则

当前源码使用 `Microsoft.Data.Sqlite.Core 10.0.1` 与 `SQLite3MC.PCLRaw.bundle 2.6.1`。移除已弃用的 `SQLitePCLRaw.bundle_e_sqlcipher`，同时移除普通 SQLite 原生包的直接引用，避免同一进程混用多个 bundle。普通文件、配置库与查询历史继续使用无密码连接。

SQLite3MC 的默认加密算法不是 SQLCipher，因此项目为带密码的文件连接显式添加 `cipher=sqlcipher&legacy=4` URI 参数，密码仍通过独立的 `Password` 连接属性传递，不放进 URI。文件路径转为绝对 URI 并转义特殊字符。“测试连接”和实际查询共用连接构建规则；测试使用只读、不池化的连接，不创建或重加密文件。

方案依据：[SQLite3MC 维护者的安装与 SQLCipher 兼容说明](https://github.com/utelle/SQLite3MultipleCiphers-NuGet#examples)。

## 使用

1. 连接类型选择 `Sqlite`，选择已有数据库文件。
2. 普通 SQLite 文件的密码留空；SQLCipher 4 文件填写数据库密码。
3. 按需勾选“记住密码”，测试连接后保存。保存的密码沿用项目已有的凭据保护机制。

SQLite 连接不填写服务器和用户名。连接窗口显示密码输入框并遮蔽内容；切换到 DuckDB 时清空并隐藏密码。已有保存密码的连接重新编辑时保留“记住密码”状态。

对象树连接时同样以只读、不池化方式实际读取 `sqlite_schema`，正确密码验证通过后才标记已连接。对象树始终保留 SQLite 文件路径，不清空数据源。AppCore 的普通查询、后台查询和事务使用 `RequireExistingDatabase=true`（`ReadWrite`），文件被删除后也不会意外创建空库。内部配置库和显式核心创建调用保留 `ReadWriteCreate` 行为。

本次提供 SQLCipher 4 默认参数访问，不自动猜测 SQLCipher 1–3、RC4、ChaCha20 或自定义 KDF/页大小配置，也不提供更改密码、加密或解密现有文件的操作。密码错误不会自动退回无密码连接。

## 验证

`SqliteEncryptionChecks` 生成临时 SQLCipher 4 格式文件，检查：

- 实际加载 SQLite3MC 原生实现，数据库文件头和测试内容不是明文。
- 正确密码读写及无密码/错误密码拒绝。
- 只读测试连接前后文件哈希一致。
- 特殊字符和相对路径、事务回滚、独立查询与元数据读取。
- 显式核心创建的带密码文件能重新打开；配置库仍可初始化。
- 错误密码/缺失文件不能激活连接节点或查询服务，普通查询与事务不创建缺失文件。

P0 隔离 UI 冒烟检查密码遮蔽、用户名隐藏、密码恢复及切换数据库类型时的字段行为；截图输出为 `artifacts/p0-ui/sqlite-encryption.png`。运行命令见 [验证与冒烟说明](../../docs/smoke/verification.md)。

2026-11-06：Release 解决方案与回归项目构建通过，完整回归输出 `All regression checks passed.`；Windows P0 隔离 UI 冒烟通过。Windows x64 框架依赖发布目录仅保留 `sqlite3mc.dll`，直接运行发布版 EXE 的 P0 冒烟退出码为 0。NuGet 依赖图中不存在旧 SQLite bundle。

合成文件检查不等于用户现有加密文件或 Linux/macOS 的实机验收。
