using DatabaseInterpreter.Core;
using DatabaseInterpreter.Model;
using DatabaseManager.AppCore.Models;
using DatabaseManager.Core;
using DatabaseManager.Core.Model;
using Microsoft.Data.Sqlite;
using System.Data.Common;
using System.Diagnostics;
using System.Text;

namespace DatabaseManager.AppCore.Services;

/// <summary>
/// 数据库备份服务实现（阶段 5）。接入 <c>DatabaseManager.Core.DbBackup</c> 各备份适配器。
/// </summary>
public class DefaultBackupService : IBackupService
{
    public static bool SupportsBackup(string databaseType) => ConnectionHelper.ParseDatabaseType(databaseType)
        is DatabaseType.SqlServer or DatabaseType.MySql or DatabaseType.Postgres or DatabaseType.KingbaseES
        or DatabaseType.Oracle or DatabaseType.Sqlite or DatabaseType.DuckDB or DatabaseType.DM;

    public static bool SupportsRestore(string databaseType) => SupportsBackup(databaseType);

    public Task<BackupResultItem> BackupAsync(
        ConnectionItem connection,
        string saveFolder,
        string? clientToolFilePath,
        bool zipFile,
        Action<string>? onFeedback = null,
        CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            var dbType = ConnectionHelper.ParseDatabaseType(connection.DatabaseType);
            if (dbType == DatabaseType.Unknown || string.IsNullOrEmpty(connection.Database))
            {
                throw new InvalidOperationException("连接或数据库无效。");
            }
            if (dbType == DatabaseType.KingbaseES &&
                KingbaseCompatibilityModes.GetAdminFeatureBlockReason(connection.KingbaseCompatibilityMode) is { } backupModeReason)
            {
                return new BackupResultItem(false, backupModeReason, string.Empty);
            }

            cancellationToken.ThrowIfCancellationRequested();

            // 1. 路径解析 + 目录创建（显式处理相对路径，避免依赖进程当前目录）。
            string fullSaveFolder;
            try
            {
                fullSaveFolder = Path.IsPathRooted(saveFolder)
                    ? saveFolder
                    : Path.Combine(AppContext.BaseDirectory, saveFolder);
                fullSaveFolder = Path.GetFullPath(fullSaveFolder);

                if (!Directory.Exists(fullSaveFolder))
                {
                    Directory.CreateDirectory(fullSaveFolder);
                    onFeedback?.Invoke($"已创建备份目录：{fullSaveFolder}");
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"保存目录无效：{ex.Message}", ex);
            }

            // 2. 客户端工具路径校验（仅当用户指定时）。
            if (!string.IsNullOrWhiteSpace(clientToolFilePath))
            {
                if (!File.Exists(clientToolFilePath))
                {
                    throw new FileNotFoundException($"找不到客户端工具：{clientToolFilePath}");
                }
                onFeedback?.Invoke($"使用客户端工具：{clientToolFilePath}");
            }

            onFeedback?.Invoke("正在初始化备份器...");

            var backup = DbBackup.GetInstance(dbType);

            backup.ConnectionInfo = ConnectionHelper.ToConnectionInfo(connection);
            backup.Setting = new BackupSetting
            {
                DatabaseType = connection.DatabaseType,
                ClientToolFilePath = clientToolFilePath ?? string.Empty,
                SaveFolder = fullSaveFolder,
                ZipFile = zipFile,
            };

            cancellationToken.ThrowIfCancellationRequested();
            onFeedback?.Invoke($"开始备份数据库 {connection.Database} ...");

            try
            {
                var filePath = backup.Backup();
                cancellationToken.ThrowIfCancellationRequested();
                onFeedback?.Invoke($"备份完成：{filePath}");
                return new BackupResultItem(true, string.Empty, filePath);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                var message = ex.Message;
                onFeedback?.Invoke($"备份失败：{message}");
                return new BackupResultItem(false, message, string.Empty);
            }
        }, cancellationToken);
    }

    public Task<BackupResultItem> RestoreAsync(
        ConnectionItem connection,
        string backupFilePath,
        string? clientToolFilePath,
        Action<string>? onFeedback = null,
        CancellationToken cancellationToken = default)
    {
        return Task.Run(async () =>
        {
            var dbType = ConnectionHelper.ParseDatabaseType(connection.DatabaseType);
            // DuckDB 的备份产物是 EXPORT DATABASE 目录，其余类型的备份产物是单个文件。
            bool backupExists = dbType == DatabaseType.DuckDB
                ? Directory.Exists(backupFilePath)
                : File.Exists(backupFilePath);
            if (!backupExists)
                return new BackupResultItem(false, dbType == DatabaseType.DuckDB ? "找不到备份目录。" : "找不到备份文件。", string.Empty);

            if (dbType == DatabaseType.Unknown || string.IsNullOrWhiteSpace(connection.Database))
                return new BackupResultItem(false, "连接或目标数据库无效。", string.Empty);
            if (dbType == DatabaseType.KingbaseES &&
                KingbaseCompatibilityModes.GetAdminFeatureBlockReason(connection.KingbaseCompatibilityMode) is { } restoreModeReason)
                return new BackupResultItem(false, restoreModeReason, string.Empty);
            if (Path.GetExtension(backupFilePath).Equals(".zip", StringComparison.OrdinalIgnoreCase))
                return new BackupResultItem(false, "请先解压 ZIP 备份文件后再恢复。", string.Empty);

            cancellationToken.ThrowIfCancellationRequested();
            onFeedback?.Invoke($"开始恢复 {dbType} 数据库 {connection.Database}...");

            return dbType switch
            {
                DatabaseType.SqlServer => await RestoreSqlServerAsync(connection, backupFilePath, onFeedback, cancellationToken),
                DatabaseType.MySql => await RestoreMySqlAsync(connection, backupFilePath, clientToolFilePath, onFeedback, cancellationToken),
                DatabaseType.Postgres => await RestorePostgresAsync(connection, backupFilePath, clientToolFilePath, onFeedback, cancellationToken),
                DatabaseType.KingbaseES => await RestoreKingbaseAsync(connection, backupFilePath, clientToolFilePath, onFeedback, cancellationToken),
                DatabaseType.Oracle => await RestoreOracleAsync(connection, backupFilePath, clientToolFilePath, onFeedback, cancellationToken),
                DatabaseType.Sqlite => await RestoreSqliteAsync(connection, backupFilePath, onFeedback, cancellationToken),
                DatabaseType.DuckDB => await RestoreDuckDbAsync(connection, backupFilePath, onFeedback, cancellationToken),
                DatabaseType.DM => await RestoreDmAsync(connection, backupFilePath, clientToolFilePath, onFeedback, cancellationToken),
                _ => new BackupResultItem(false, $"暂不支持 {connection.DatabaseType} 的恢复。", string.Empty),
            };
        }, cancellationToken);
    }

    private static async Task<BackupResultItem> RestoreSqlServerAsync(
        ConnectionItem connection, string backupFilePath, Action<string>? feedback, CancellationToken ct)
    {
        if (!Path.GetExtension(backupFilePath).Equals(".bak", StringComparison.OrdinalIgnoreCase))
            return new BackupResultItem(false, "SQL Server 仅支持 .bak 备份文件。", string.Empty);

        var info = ConnectionHelper.ToConnectionInfo(connection);
        info.Database = "master";
        var interpreter = DbInterpreterHelper.GetDbInterpreter(DatabaseType.SqlServer, info, new DbInterpreterOption());
        var database = QuoteSqlServerIdentifier(connection.Database);
        var backupPath = backupFilePath.Replace("'", "''", StringComparison.Ordinal);
        await using var session = interpreter.CreateConnection();

        string? originalAccess = null;
        bool restoreAttempted = false;
        Exception? restoreError = null;
        try
        {
            await session.OpenAsync(ct);
            originalAccess = await ReadSqlServerAccessAsync(session, connection.Database, ct);
            if (originalAccess is not (null or "MULTI_USER" or "RESTRICTED_USER" or "SINGLE_USER"))
                return new BackupResultItem(false, $"无法识别目标库的访问模式：{originalAccess}。", string.Empty);

            ct.ThrowIfCancellationRequested();
            restoreAttempted = true;
            using var command = session.CreateCommand();
            command.CommandTimeout = 1800;
            var restrictAccess = originalAccess is "MULTI_USER" or "RESTRICTED_USER"
                ? $"ALTER DATABASE {database} SET SINGLE_USER WITH ROLLBACK IMMEDIATE; " : string.Empty;
            command.CommandText = $"{restrictAccess}RESTORE DATABASE {database} FROM DISK = N'{backupPath}' WITH REPLACE, RECOVERY;";
            await command.ExecuteNonQueryAsync(ct);
        }
        catch (Exception ex)
        {
            restoreError = ex;
        }

        // 恢复失败或取消后也要还原原有访问模式。清理使用独立的连接和超时。
        var accessToRestore = originalAccess ?? (restoreError is null ? "MULTI_USER" : null);
        if (restoreAttempted && accessToRestore is not null)
        {
            try
            {
                using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                await using var cleanupSession = interpreter.CreateConnection();
                await cleanupSession.OpenAsync(cleanup.Token);
                if (await ReadSqlServerAccessAsync(cleanupSession, connection.Database, cleanup.Token) != accessToRestore)
                {
                    using var command = cleanupSession.CreateCommand();
                    command.CommandText = $"ALTER DATABASE {database} SET {accessToRestore} WITH ROLLBACK IMMEDIATE;";
                    await command.ExecuteNonQueryAsync(cleanup.Token);
                }
            }
            catch (Exception ex)
            {
                var outcome = restoreError is null ? "备份已恢复" : $"恢复未完成：{restoreError.Message}";
                return new BackupResultItem(false, $"{outcome}。未能还原目标库的 {accessToRestore} 访问模式：{ex.Message}", string.Empty);
            }
        }

        if (restoreError is not null)
        {
            ct.ThrowIfCancellationRequested();
            return new BackupResultItem(false, restoreError.Message, string.Empty);
        }

        feedback?.Invoke("SQL Server 已完成 RESTORE DATABASE，目标数据库已恢复原有访问模式。");
        return new BackupResultItem(true, string.Empty, backupFilePath);
    }

    private static async Task<string?> ReadSqlServerAccessAsync(DbConnection session, string database, CancellationToken ct)
    {
        using var command = session.CreateCommand();
        command.CommandText = "SELECT user_access_desc FROM sys.databases WHERE name = @database";
        var parameter = command.CreateParameter();
        parameter.ParameterName = "@database";
        parameter.Value = database;
        command.Parameters.Add(parameter);
        return await command.ExecuteScalarAsync(ct) as string;
    }

    private static async Task<BackupResultItem> RestoreMySqlAsync(
        ConnectionItem connection, string backupFilePath, string? toolPath, Action<string>? feedback, CancellationToken ct)
    {
        if (!EnsureTool(toolPath, "MySQL 恢复需要选择 mysql 客户端程序", out var error))
            return new BackupResultItem(false, error, string.Empty);
        var args = BuildConnectionArgs(connection, "--host", "--port", "--user");
        args.Add($"--database={connection.Database}");
        return await RunToolAsync(toolPath!, args, connection.Password, backupFilePath, "MYSQL_PWD", feedback, ct);
    }

    private static async Task<BackupResultItem> RestorePostgresAsync(
        ConnectionItem connection, string backupFilePath, string? toolPath, Action<string>? feedback, CancellationToken ct)
    {
        if (!EnsureTool(toolPath, "PostgreSQL 恢复需要选择 psql 或 pg_restore 客户端程序", out var error))
            return new BackupResultItem(false, error, string.Empty);

        bool plainSql = Path.GetExtension(backupFilePath).Equals(".sql", StringComparison.OrdinalIgnoreCase);
        var executable = Path.GetFileNameWithoutExtension(toolPath!);
        if (plainSql && !executable.Contains("psql", StringComparison.OrdinalIgnoreCase))
            return new BackupResultItem(false, ".sql 备份请使用 psql；自定义格式请使用 pg_restore。", string.Empty);
        if (!plainSql && !executable.Contains("pg_restore", StringComparison.OrdinalIgnoreCase))
            return new BackupResultItem(false, "非 .sql 备份请使用 pg_restore。", string.Empty);

        var args = BuildConnectionArgs(connection, "--host", "--port", "--username");
        args.Add($"--dbname={connection.Database}");
        if (plainSql)
        {
            args.Add("-X");
            args.Add("--set=ON_ERROR_STOP=on");
            args.Add($"--file={backupFilePath}");
        }
        else
        {
            args.Add("--clean");
            args.Add("--if-exists");
            args.Add("--no-owner");
            args.Add(backupFilePath);
        }
        return await RunToolAsync(toolPath!, args, connection.Password, null, "PGPASSWORD", feedback, ct);
    }

    private static async Task<BackupResultItem> RestoreKingbaseAsync(
        ConnectionItem connection, string backupFilePath, string? toolPath, Action<string>? feedback, CancellationToken ct)
    {
        if (!EnsureTool(toolPath, "KingbaseES 恢复需要选择 ksql 或 sys_restore 客户端程序", out var error))
            return new BackupResultItem(false, error, string.Empty);

        bool plainSql = Path.GetExtension(backupFilePath).Equals(".sql", StringComparison.OrdinalIgnoreCase);
        if (plainSql && !KingbaseBackup.IsTool(toolPath!, KingbaseBackup.SqlRestoreToolName))
            return new BackupResultItem(false, ".sql 备份请使用 ksql；归档格式请使用 sys_restore。", string.Empty);
        if (!plainSql && !KingbaseBackup.IsTool(toolPath!, KingbaseBackup.ArchiveRestoreToolName))
            return new BackupResultItem(false, "归档格式备份请使用 sys_restore；.sql 备份请使用 ksql。", string.Empty);

        var args = BuildConnectionArgs(connection, "--host", "--port", "--username");
        args.Add($"--dbname={connection.Database}");
        args.Add("--no-password");
        if (plainSql)
        {
            args.Add("-X");
            args.Add("--set=ON_ERROR_STOP=on");
            args.Add($"--file={backupFilePath}");
        }
        else
        {
            args.Add("--clean");
            args.Add("--if-exists");
            args.Add("--no-owner");
            args.Add(backupFilePath);
        }

        return await RunToolAsync(
            toolPath!, args, connection.Password, null, "KBPASSWORD", feedback, ct,
            additionalPasswordVariables: new[] { "PGPASSWORD" });
    }

    private static async Task<BackupResultItem> RestoreOracleAsync(
        ConnectionItem connection, string backupFilePath, string? toolPath, Action<string>? feedback, CancellationToken ct)
    {
        if (!EnsureTool(toolPath, "Oracle 恢复需要选择传统 imp 客户端程序", out var error))
            return new BackupResultItem(false, error, string.Empty);
        if (!Path.GetFileNameWithoutExtension(toolPath!).Equals("imp", StringComparison.OrdinalIgnoreCase))
            return new BackupResultItem(false, "当前恢复仅支持 Oracle 传统 imp 工具，请选择 imp.exe。", string.Empty);
        if (string.IsNullOrWhiteSpace(connection.UserId) || string.IsNullOrWhiteSpace(connection.Password))
            return new BackupResultItem(false, "Oracle 恢复需要用户名和密码。", string.Empty);

        var invocation = BuildOracleRestoreInvocation(connection, backupFilePath);
        return await RunToolAsync(
            toolPath!,
            invocation.Arguments,
            null,
            null,
            string.Empty,
            feedback,
            ct,
            invocation.StandardInput);
    }

    private static Task<BackupResultItem> RestoreSqliteAsync(
        ConnectionItem connection, string backupFilePath, Action<string>? feedback, CancellationToken ct)
    {
        return Task.Run(async () =>
        {
            if (connection.Database == ":memory:")
                return new BackupResultItem(false, "SQLite 恢复需要选择目标数据库文件。", string.Empty);
            var target = Path.GetFullPath(connection.Database);
            var source = Path.GetFullPath(backupFilePath);
            var pathComparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            if (string.Equals(source, target, pathComparison))
                return new BackupResultItem(false, "备份文件不能与当前 SQLite 数据库为同一文件。", string.Empty);
            if (HasSqliteSidecar(source))
                return new BackupResultItem(false, "备份文件带有 SQLite 日志，请先关闭备份库的所有连接并准备独立备份文件。", string.Empty);
            if (HasSqliteSidecar(target))
                return new BackupResultItem(false, "目标库仍有 SQLite 日志，请关闭相关连接后再恢复。", string.Empty);

            var temporary = $"{target}.restore-{Guid.NewGuid():N}.tmp";
            try
            {
                await using (var input = File.OpenRead(source))
                await using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    await input.CopyToAsync(output, ct);
                if (HasSqliteSidecar(source) || new FileInfo(temporary).Length == 0)
                    return new BackupResultItem(false, "SQLite 备份为空或仍有日志，无法恢复。", string.Empty);

                var info = ConnectionHelper.ToConnectionInfo(connection);
                var sqliteBuilder = new DatabaseInterpreter.Core.SqliteConnectionStringBuilder();
                var targetConnectionString = sqliteBuilder.BuildConntionString(info);
                info.Database = temporary;
                var builder = new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder(sqliteBuilder.BuildConntionString(info))
                {
                    Mode = SqliteOpenMode.ReadOnly,
                    Pooling = false,
                };
                await using (var database = new SqliteConnection(builder.ConnectionString))
                {
                    await database.OpenAsync(ct);
                    using var command = database.CreateCommand();
                    command.CommandText = "PRAGMA integrity_check;";
                    using var check = await command.ExecuteReaderAsync(ct);
                    if (!await check.ReadAsync(ct) || check.GetString(0) != "ok" || await check.ReadAsync(ct))
                        return new BackupResultItem(false, "SQLite 备份完整性检查失败，目标库未替换。", string.Empty);
                }

                ct.ThrowIfCancellationRequested();
                using var targetConnection = new SqliteConnection(targetConnectionString);
                SqliteConnection.ClearPool(targetConnection);
                if (HasSqliteSidecar(target))
                    return new BackupResultItem(false, "目标库仍有 SQLite 日志，请关闭相关连接后再恢复。", string.Empty);
                if (File.Exists(target))
                {
                    var safetyCopy = $"{target}.before-restore-{DateTime.Now:yyyyMMddHHmmss}-{Guid.NewGuid():N}.bak";
                    File.Copy(target, safetyCopy, overwrite: false);
                    feedback?.Invoke($"已创建恢复前安全副本：{safetyCopy}");
                }

                ct.ThrowIfCancellationRequested();
                File.Move(temporary, target, overwrite: true);
                feedback?.Invoke("SQLite 数据库文件已替换，请重新连接。");
                return new BackupResultItem(true, string.Empty, backupFilePath);
            }
            catch (SqliteException ex)
            {
                ct.ThrowIfCancellationRequested();
                return new BackupResultItem(false, $"无法验证 SQLite 备份，请检查文件和数据库密码：{ex.Message}", string.Empty);
            }
            finally
            {
                if (File.Exists(temporary)) File.Delete(temporary);
                if (File.Exists(temporary + "-shm")) File.Delete(temporary + "-shm");
                if (File.Exists(temporary + "-wal")) File.Delete(temporary + "-wal");
                if (File.Exists(temporary + "-journal")) File.Delete(temporary + "-journal");
            }
        }, ct);
    }

    private static Task<BackupResultItem> RestoreDuckDbAsync(
        ConnectionItem connection, string backupFilePath, Action<string>? feedback, CancellationToken ct)
    {
        return Task.Run(async () =>
        {
            if (!Directory.Exists(backupFilePath))
                return new BackupResultItem(false, "DuckDB 恢复需要 EXPORT DATABASE 生成的目录，ZIP 备份请先解压。", string.Empty);
            if (string.IsNullOrWhiteSpace(connection.Database) || connection.Database == ":memory:")
                return new BackupResultItem(false, "DuckDB 恢复需要选择文件型目标数据库。", string.Empty);

            ct.ThrowIfCancellationRequested();
            var interpreter = DbInterpreterHelper.GetDbInterpreter(
                DatabaseType.DuckDB, ConnectionHelper.ToConnectionInfo(connection), new DbInterpreterOption());

            try
            {
                await using var database = interpreter.CreateConnection();
                await database.OpenAsync(ct);
                await DbRowMapper.ExecuteNonQueryAsync(
                    database, $"IMPORT DATABASE '{backupFilePath.Replace("'", "''", StringComparison.Ordinal)}'");
                feedback?.Invoke("DuckDB 数据已导入目标数据库，请重新连接。");
                return new BackupResultItem(true, string.Empty, backupFilePath);
            }
            catch (Exception ex)
            {
                ct.ThrowIfCancellationRequested();
                return new BackupResultItem(false, $"DuckDB 恢复失败（目标库需为空库或不存在同名表）：{ex.Message}", string.Empty);
            }
        }, ct);
    }

    private static async Task<BackupResultItem> RestoreDmAsync(
        ConnectionItem connection, string backupFilePath, string? toolPath, Action<string>? feedback, CancellationToken ct)
    {
        if (!EnsureTool(toolPath, "达梦恢复需要选择 dimp 客户端程序", out var error))
            return new BackupResultItem(false, error, string.Empty);
        if (!Path.GetFileNameWithoutExtension(toolPath!).Equals("dimp", StringComparison.OrdinalIgnoreCase))
            return new BackupResultItem(false, "当前恢复仅支持达梦 dimp 工具，请选择 dimp.exe。", string.Empty);
        if (string.IsNullOrWhiteSpace(connection.UserId) || string.IsNullOrWhiteSpace(connection.Password))
            return new BackupResultItem(false, "达梦恢复需要用户名和密码。", string.Empty);
        if (!Path.GetExtension(backupFilePath).Equals(".dmp", StringComparison.OrdinalIgnoreCase))
            return new BackupResultItem(false, "达梦仅支持 .dmp 备份文件。", string.Empty);

        var port = string.IsNullOrWhiteSpace(connection.Port) ? "5236" : connection.Port;
        var directory = Path.GetDirectoryName(Path.GetFullPath(backupFilePath)) ?? ".";
        var arguments = new List<string>
        {
            $"USERID={connection.UserId}/{connection.Password}@{connection.Server}:{port}",
            $"FILE={Path.GetFileName(backupFilePath)}",
            $"DIRECTORY={directory}",
            "FULL=Y",
            "IGNORE=Y",
        };

        return await RunToolAsync(toolPath!, arguments, null, null, string.Empty, feedback, ct);
    }

    private static bool HasSqliteSidecar(string path) => File.Exists(path + "-wal")
        || File.Exists(path + "-shm") || File.Exists(path + "-journal");

    private static bool EnsureTool(string? toolPath, string message, out string error)
    {
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(toolPath) || !File.Exists(toolPath))
        {
            error = message;
            return false;
        }
        return true;
    }

    private static List<string> BuildConnectionArgs(ConnectionItem connection, string hostName, string portName, string userName)
    {
        var args = new List<string> { $"{hostName}={connection.Server}" };
        if (!string.IsNullOrWhiteSpace(connection.Port)) args.Add($"{portName}={connection.Port}");
        if (!string.IsNullOrWhiteSpace(connection.UserId)) args.Add($"{userName}={connection.UserId}");
        return args;
    }

    internal static OracleRestoreInvocation BuildOracleRestoreInvocation(ConnectionItem connection, string backupFilePath)
    {
        var service = string.IsNullOrWhiteSpace(connection.Port) ? connection.Server : $"{connection.Server}:{connection.Port}";
        var connectName = $"{connection.UserId}@{service}/{connection.Database}";
        var standardInput = string.Join(Environment.NewLine, connectName, connection.Password, string.Empty);
        var arguments = new[] { $"file={backupFilePath}", "full=y", "ignore=y", "commit=y" };
        return new OracleRestoreInvocation(arguments, standardInput);
    }

    private static async Task<BackupResultItem> RunToolAsync(
        string toolPath,
        IEnumerable<string> arguments,
        string? password,
        string? standardInputFile,
        string passwordVariable,
        Action<string>? feedback,
        CancellationToken ct,
        string? standardInputText = null,
        IEnumerable<string>? additionalPasswordVariables = null)
    {
        using var process = new Process { StartInfo = new ProcessStartInfo(toolPath) { RedirectStandardOutput = true, RedirectStandardError = true, RedirectStandardInput = standardInputFile is not null || standardInputText is not null, UseShellExecute = false, CreateNoWindow = true } };
        foreach (var argument in arguments) process.StartInfo.ArgumentList.Add(argument);
        if (!string.IsNullOrEmpty(password))
        {
            if (!string.IsNullOrEmpty(passwordVariable)) process.StartInfo.Environment[passwordVariable] = password;
            if (additionalPasswordVariables is not null)
            {
                foreach (string variable in additionalPasswordVariables)
                {
                    if (!string.IsNullOrWhiteSpace(variable)) process.StartInfo.Environment[variable] = password;
                }
            }
        }
        process.Start();

        var output = new StringBuilder();
        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();
        try
        {
            if (standardInputText is not null)
            {
                await process.StandardInput.WriteAsync(standardInputText.AsMemory(), ct);
                process.StandardInput.Close();
            }
            else if (standardInputFile is not null)
            {
                await using var source = File.OpenRead(standardInputFile);
                await source.CopyToAsync(process.StandardInput.BaseStream, ct);
                process.StandardInput.Close();
            }
            await process.WaitForExitAsync(ct);
        }
        catch (OperationCanceledException)
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
            throw;
        }

        output.Append(await outputTask);
        var error = await errorTask;
        if (process.ExitCode != 0)
            return new BackupResultItem(false, string.IsNullOrWhiteSpace(error) ? output.ToString() : error, string.Empty);
        if (!string.IsNullOrWhiteSpace(output.ToString())) feedback?.Invoke(output.ToString().Trim());
        if (!string.IsNullOrWhiteSpace(error)) feedback?.Invoke(error.Trim());
        return new BackupResultItem(true, string.Empty, string.Empty);
    }

    private static string QuoteSqlServerIdentifier(string value) => $"[{value.Replace("]", "]]", StringComparison.Ordinal)}]";
}

internal sealed record OracleRestoreInvocation(IReadOnlyList<string> Arguments, string StandardInput);
