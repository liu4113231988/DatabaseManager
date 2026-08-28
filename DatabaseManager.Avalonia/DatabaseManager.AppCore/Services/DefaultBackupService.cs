using DatabaseInterpreter.Core;
using DatabaseInterpreter.Model;
using DatabaseManager.AppCore.Models;
using DatabaseManager.Core;
using DatabaseManager.Core.Model;
using System.Diagnostics;
using System.Text;

namespace DatabaseManager.AppCore.Services;

/// <summary>
/// 数据库备份服务实现（阶段 5）。接入 <c>DatabaseManager.Core.DbBackup</c> 各备份适配器。
/// </summary>
public class DefaultBackupService : IBackupService
{
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
            if (!File.Exists(backupFilePath))
                return new BackupResultItem(false, "找不到备份文件。", string.Empty);

            var dbType = ConnectionHelper.ParseDatabaseType(connection.DatabaseType);
            if (dbType == DatabaseType.Unknown || string.IsNullOrWhiteSpace(connection.Database))
                return new BackupResultItem(false, "连接或目标数据库无效。", string.Empty);
            if (Path.GetExtension(backupFilePath).Equals(".zip", StringComparison.OrdinalIgnoreCase))
                return new BackupResultItem(false, "请先解压 ZIP 备份文件后再恢复。", string.Empty);

            cancellationToken.ThrowIfCancellationRequested();
            onFeedback?.Invoke($"开始恢复 {dbType} 数据库 {connection.Database}...");

            return dbType switch
            {
                DatabaseType.SqlServer => await RestoreSqlServerAsync(connection, backupFilePath, onFeedback, cancellationToken),
                DatabaseType.MySql => await RestoreMySqlAsync(connection, backupFilePath, clientToolFilePath, onFeedback, cancellationToken),
                DatabaseType.Postgres => await RestorePostgresAsync(connection, backupFilePath, clientToolFilePath, onFeedback, cancellationToken),
                DatabaseType.Oracle => await RestoreOracleAsync(connection, backupFilePath, clientToolFilePath, onFeedback, cancellationToken),
                DatabaseType.Sqlite => await RestoreSqliteAsync(connection, backupFilePath, onFeedback, cancellationToken),
                _ => new BackupResultItem(false, $"暂不支持 {connection.DatabaseType} 的恢复。", string.Empty),
            };
        }, cancellationToken);
    }

    private static async Task<BackupResultItem> RestoreSqlServerAsync(
        ConnectionItem connection, string backupFilePath, Action<string>? feedback, CancellationToken ct)
    {
        if (!Path.GetExtension(backupFilePath).Equals(".bak", StringComparison.OrdinalIgnoreCase))
            return new BackupResultItem(false, "SQL Server 仅支持 .bak 备份文件。", string.Empty);

        try
        {
            var info = ConnectionHelper.ToConnectionInfo(connection);
            info.Database = "master";
            var interpreter = DbInterpreterHelper.GetDbInterpreter(DatabaseType.SqlServer, info, new DbInterpreterOption());
            var database = QuoteSqlServerIdentifier(connection.Database);
            var backupPath = backupFilePath.Replace("'", "''", StringComparison.Ordinal);
            var script = $"ALTER DATABASE {database} SET SINGLE_USER WITH ROLLBACK IMMEDIATE; RESTORE DATABASE {database} FROM DISK = N'{backupPath}' WITH REPLACE, RECOVERY; ALTER DATABASE {database} SET MULTI_USER;";
            var result = await interpreter.ExecuteNonQueryAsync(new CommandInfo
            {
                CommandText = script,
                CancellationToken = ct,
                CommandTimeoutSeconds = 1800,
            });
            if (result is null || result.HasError)
                return new BackupResultItem(false, result?.Message ?? "SQL Server 未返回恢复结果。", string.Empty);

            feedback?.Invoke("SQL Server 已完成 RESTORE DATABASE，目标数据库已重新开放连接。");
            return new BackupResultItem(true, string.Empty, backupFilePath);
        }
        catch (Exception ex)
        {
            return new BackupResultItem(false, ex.Message, string.Empty);
        }
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
            args.Add($"--file={backupFilePath}");
        else
        {
            args.Add("--clean");
            args.Add("--if-exists");
            args.Add("--no-owner");
            args.Add(backupFilePath);
        }
        return await RunToolAsync(toolPath!, args, connection.Password, null, "PGPASSWORD", feedback, ct);
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

        var service = string.IsNullOrWhiteSpace(connection.Port) ? connection.Server : $"{connection.Server}:{connection.Port}";
        var args = new List<string>
        {
            $"userid={connection.UserId}/{connection.Password}@{service}/{connection.Database}",
            $"file={backupFilePath}", "full=y", "ignore=y", "commit=y",
        };
        return await RunToolAsync(toolPath!, args, null, null, string.Empty, feedback, ct);
    }

    private static Task<BackupResultItem> RestoreSqliteAsync(
        ConnectionItem connection, string backupFilePath, Action<string>? feedback, CancellationToken ct)
    {
        return Task.Run(() =>
        {
            var target = Path.GetFullPath(connection.Database);
            if (!File.Exists(backupFilePath))
                return new BackupResultItem(false, "找不到 SQLite 备份文件。", string.Empty);
            if (string.Equals(Path.GetFullPath(backupFilePath), target, StringComparison.OrdinalIgnoreCase))
                return new BackupResultItem(false, "备份文件不能与当前 SQLite 数据库为同一文件。", string.Empty);

            ct.ThrowIfCancellationRequested();
            if (File.Exists(target))
            {
                var safetyCopy = $"{target}.before-restore-{DateTime.Now:yyyyMMddHHmmss}.bak";
                File.Copy(target, safetyCopy, overwrite: false);
                feedback?.Invoke($"已创建恢复前安全副本：{safetyCopy}");
            }

            var temporary = $"{target}.restore-{Guid.NewGuid():N}.tmp";
            try
            {
                File.Copy(backupFilePath, temporary, overwrite: false);
                ct.ThrowIfCancellationRequested();
                File.Move(temporary, target, overwrite: true);
                feedback?.Invoke("SQLite 数据库文件已替换，请重新连接。");
                return new BackupResultItem(true, string.Empty, backupFilePath);
            }
            finally
            {
                if (File.Exists(temporary)) File.Delete(temporary);
            }
        }, ct);
    }

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

    private static async Task<BackupResultItem> RunToolAsync(
        string toolPath, IEnumerable<string> arguments, string? password, string? standardInputFile, string passwordVariable, Action<string>? feedback, CancellationToken ct)
    {
        using var process = new Process { StartInfo = new ProcessStartInfo(toolPath) { RedirectStandardOutput = true, RedirectStandardError = true, RedirectStandardInput = standardInputFile is not null, UseShellExecute = false, CreateNoWindow = true } };
        foreach (var argument in arguments) process.StartInfo.ArgumentList.Add(argument);
        if (!string.IsNullOrEmpty(password) && !string.IsNullOrEmpty(passwordVariable)) process.StartInfo.Environment[passwordVariable] = password;
        process.Start();

        var output = new StringBuilder();
        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();
        try
        {
            if (standardInputFile is not null)
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
