using DatabaseManager.AppCore.Models;
using DatabaseManager.AppCore.Services;
using DatabaseManager.AppCore.ViewModels;
using Microsoft.Data.Sqlite;

internal static class WriteSafetyChecks
{
    public static async Task RunAsync()
    {
        var folder = Directory.CreateDirectory(Path.Combine(AppContext.BaseDirectory, $"write-safety-{Guid.NewGuid():N}"));
        try
        {
            await VerifyRestoreAsync(folder.FullName);
            await VerifySyncAsync(folder.FullName);
            await VerifyQueryConfirmationsAsync(folder.FullName);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            foreach (var file in folder.GetFiles()) file.Delete();
            folder.Delete();
        }
        Console.WriteLine("Write safety checks passed: restore validation, partial sync failure and query confirmations.");
    }

    private static async Task VerifyRestoreAsync(string folder)
    {
        var restore = new DefaultBackupService();
        var target = await CreateAsync(folder, "target", "original", "fixture key");
        var invalid = Path.Combine(folder, "invalid.bak");
        await File.WriteAllTextAsync(invalid, "Not a SQLite database");
        var original = await File.ReadAllBytesAsync(target.Database);
        Check(!(await restore.RestoreAsync(target, invalid, null)).IsOK, "An invalid backup must report failure.");
        var tasks = new DefaultTaskCenterService();
        var finished = new TaskCompletionSource<TaskRun>(TaskCreationOptions.RunContinuationsAsynchronously);
        tasks.TaskFinished += run => finished.TrySetResult(run);
        var backupWindow = new BackupViewModel(new QueryExecutionChecks.Connections(target), restore, tasks);
        backupWindow.SetTargetConnection(target);
        Check(await backupWindow.RefreshConnectionsAsync(), "The restore fixture must select its target.");
        backupWindow.RestoreFilePath = invalid;
        await backupWindow.RestoreCommand.ExecuteAsync(null);
        Check((await finished.Task.WaitAsync(TimeSpan.FromSeconds(10))).State == TaskRunState.Failed,
            "An invalid restore must also be marked as failed by the task center.");

        var encrypted = await CreateAsync(folder, "backup #%;", "restored", "fixture key");
        var wrongKey = target.Clone();
        wrongKey.Password = "wrong key";
        Check(!(await restore.RestoreAsync(wrongKey, encrypted.Database, null)).IsOK, "An encrypted backup must require the target key.");
        Check(original.SequenceEqual(File.ReadAllBytes(target.Database)), "Rejected backups must leave the target unchanged.");
        Check(!Directory.EnumerateFiles(folder, "*.restore-*", SearchOption.TopDirectoryOnly).Any(),
            "Rejected backups must remove staged files.");

        using var canceled = new CancellationTokenSource();
        try
        {
            await restore.RestoreAsync(target, encrypted.Database, null, _ => canceled.Cancel(), canceled.Token);
            throw new InvalidOperationException("Canceling a restore must stop before replacing the database.");
        }
        catch (OperationCanceledException) { }
        Check(original.SequenceEqual(File.ReadAllBytes(target.Database)), "A canceled restore must preserve the existing database.");

        var query = new DefaultQueryService(new QueryExecutionChecks.Connections(target));
        Check((await query.ExecuteStandaloneAsync(target, "SELECT value FROM sample")).Rows.Single()[0] == "original",
            "Read through the normal connection before replacing the file.");
        var result = await restore.RestoreAsync(target, encrypted.Database, null);
        Check(result.IsOK, $"A valid SQLCipher backup must restore: {result.Message}");
        var safetyCopy = Directory.GetFiles(folder, "target.db.before-restore-*.bak").Single();
        Check(original.SequenceEqual(File.ReadAllBytes(safetyCopy)), "The safety copy must preserve the previous database.");
        Check((await query.ExecuteStandaloneAsync(target, "SELECT value FROM sample")).Rows.Single()[0] == "restored",
            "The restored file must open with the configured key and contain the backup data.");
    }

    private static async Task VerifySyncAsync(string folder)
    {
        var target = await CreateAsync(folder, "sync", "original");
        var service = new DefaultSyncScriptService();
        var first = new ScriptItem("first", "UPDATE sample SET value='committed' WHERE id=1;", ScriptKind.Data);
        var failed = new ScriptItem("failed", "INSERT INTO sample VALUES (2,'rollback'); INSERT INTO absent_table VALUES (1);", ScriptKind.Data);
        var last = new ScriptItem("last", "INSERT INTO sample VALUES (3,'skip');", ScriptKind.Data);
        var result = await service.ExecuteScriptsAsync(target, [first, failed, last]);
        Check(!result.IsSuccess && result.ExecutedCount == 1 && result.Message.Contains("已确认完成：first")
            && result.Message.Contains("失败：failed") && result.Message.Contains("未执行：last"),
            "Sync failure must distinguish completed, failed and skipped scripts.");

        var query = new DefaultQueryService(new QueryExecutionChecks.Connections(target));
        var rows = await query.ExecuteStandaloneAsync(target, "SELECT value FROM sample ORDER BY id");
        Check(rows.Rows.Single()[0] == "committed", "The failed table must roll back and later scripts must not execute.");

        var tasks = new DefaultTaskCenterService();
        var finished = new TaskCompletionSource<TaskRun>(TaskCreationOptions.RunContinuationsAsynchronously);
        tasks.TaskFinished += run => finished.TrySetResult(run);
        var preview = new ScriptPreviewViewModel(service, tasks) { TargetConnection = target };
        preview.Scripts.Add(failed);
        await preview.ExecuteSelectedCommand.ExecuteAsync(null);
        var task = await finished.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Check(task.State == TaskRunState.Failed && preview.StatusMessage.Contains("执行失败"),
            "Manual sync failure must propagate to the task center.");
    }

    private static async Task VerifyQueryConfirmationsAsync(string folder)
    {
        var target = await CreateAsync(folder, "query", "original");
        target.Group = " Production ";
        var connections = new QueryExecutionChecks.Connections(target);
        var query = new DefaultQueryService(connections);
        var schema = new DefaultDbSchemaService(connections);
        var editor = new QueryEditorViewModel(query);
        var vm = new MainWindowViewModel(schema, connections, query, new DefaultDataEditService(connections),
            new ObjectsExplorerViewModel(schema), editor);
        await vm.InitializeAsync();
        query.NotifyConnected(target.Name);
        var tab = vm.OpenSqlInNewTab(target.Name, "UPDATE sample SET value='unconfirmed'", target.Database);
        tab.DangerousSqlConfirmationEnabled = false;
        int confirmations = 0;
        tab.RequestDangerousExecution = (name, database, sql) =>
        {
            Check(name == target.Name && database == target.Database && sql == tab.SqlText,
                "The SQL confirmation must receive the actual tab target and SQL.");
            confirmations++;
            return Task.FromResult(false);
        };
        await tab.ExecuteAsync();
        Check(vm.IsSelectedQueryProduction && confirmations == 1 && tab.StatusMessage.Contains("已取消"),
            "Production writes must request confirmation even with the tab preference disabled.");

        tab.SqlText = "SELECT * FROM sample";
        await tab.ExecuteAsync();
        Check(tab.IsResultEditable && confirmations == 1, "A plain SELECT must remain available on a production connection.");

        var values = tab.Columns.Select((_, index) => tab.Rows[0][index]).ToArray();
        values[tab.Columns.IndexOf("value")] = "new";
        tab.ApplyFormValues(tab.Rows[0], values);
        tab.RequestIndependentSave = (_, _) => Task.FromResult(false);
        await tab.SaveEditsAsync();
        Check(tab.HasPendingChanges && tab.StatusMessage.Contains("已取消"), "Canceling grid save must retain pending edits.");
        tab.RevertEdits();

        await editor.SetAutoCommitAsync(false);
        Check((await query.ExecuteAsync(target.Name, "UPDATE sample SET value='pending'")).IsSuccess, "The fixture must start a manual transaction.");
        editor.OnConnectionChanged();
        Check(editor.TransactionDatabase == target.Database, "The editor must expose the active transaction database.");
        editor.RequestAutoCommitConfirmation = (name, database) =>
        {
            Check(name == target.Name && database == target.Database, "Implicit commit confirmation must identify its active transaction.");
            return Task.FromResult(false);
        };
        await editor.SetAutoCommitAsync(true);
        Check(!editor.AutoCommit && query.IsTransactionActive(target.Name), "Declining implicit commit must preserve manual mode and the transaction.");
        Check(!await query.SetAutoCommitAsync(target.Name, true, commitActiveTransaction: false),
            "An active transaction must never be implicitly committed by an unconfirmed queued request.");
        await editor.RollbackTransactionAsync();
        Check((await query.ExecuteStandaloneAsync(target, "SELECT value FROM sample")).Rows.Single()[0] == "original",
            "Declined grid and SQL writes must leave the original row intact.");
        await query.CloseConnectionAsync(target.Name);
    }

    private static async Task<ConnectionItem> CreateAsync(string folder, string name, string value, string? password = null)
    {
        var item = new ConnectionItem { Name = name, DatabaseType = "Sqlite", Database = Path.Combine(folder, name + ".db"), Password = password };
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = password is null ? item.Database : new Uri(item.Database).AbsoluteUri + "?cipher=sqlcipher&legacy=4",
            Password = password,
            Pooling = false,
        };
        using var database = new SqliteConnection(builder.ConnectionString);
        await database.OpenAsync();
        using var command = database.CreateCommand();
        command.CommandText = "CREATE TABLE sample (id INTEGER PRIMARY KEY, value TEXT); INSERT INTO sample VALUES (1, $value);";
        command.Parameters.AddWithValue("$value", value);
        await command.ExecuteNonQueryAsync();
        return item;
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
