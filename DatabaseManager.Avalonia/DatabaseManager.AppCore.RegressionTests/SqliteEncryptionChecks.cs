using System.Security.Cryptography;
using System.Text;
using DatabaseInterpreter.Model;
using DatabaseManager.AppCore.Models;
using DatabaseManager.AppCore.Services;
using DatabaseManager.AppCore.ViewModels;
using Microsoft.Data.Sqlite;

internal static class SqliteEncryptionChecks
{
    public static async Task RunAsync()
    {
        var folder = Path.Combine(Path.GetTempPath(), $"dbm-encryption-{Guid.NewGuid():N}");
        Directory.CreateDirectory(folder);
        var item = new ConnectionItem
        {
            Name = "Encrypted regression", DatabaseType = "Sqlite",
            Database = Path.Combine(folder, "加密 #%; sample.db"), Password = "fixture-only;密钥'\"",
        };
        var query = new DefaultQueryService(new QueryExecutionChecks.Connections(item));
        var connections = new ProfileDbConnectionService(null!);
        try
        {
            // 独立于项目连接构建器，按 SQLite3MC 的 SQLCipher 4 URI 配置生成测试文件。
            var seed = new SqliteConnectionStringBuilder
            {
                DataSource = new Uri(item.Database).AbsoluteUri + "?cipher=sqlcipher&legacy=4",
                Password = item.Password, Pooling = false,
            };
            using (var sqlite = new SqliteConnection(seed.ConnectionString))
            {
                await sqlite.OpenAsync();
                using var command = sqlite.CreateCommand();
                command.CommandText = "SELECT sqlite3mc_version()";
                Check(!string.IsNullOrWhiteSpace(Convert.ToString(await command.ExecuteScalarAsync())),
                    "The selected native provider must expose SQLite3MC encryption support.");
                command.CommandText = "PRAGMA cipher";
                Check(Convert.ToString(await command.ExecuteScalarAsync()) == "sqlcipher", "Encrypted files must use SQLCipher.");
                command.CommandText = "CREATE TABLE sample(id INTEGER PRIMARY KEY, value TEXT); INSERT INTO sample VALUES(1,'encrypted-marker');";
                await command.ExecuteNonQueryAsync();
            }
            var content = await File.ReadAllBytesAsync(item.Database);
            Check(!content.AsSpan(0, 16).SequenceEqual("SQLite format 3\0"u8)
                && !Encoding.UTF8.GetString(content).Contains("encrypted-marker", StringComparison.Ordinal),
                "The database file must be encrypted, not merely accept a password option.");
            var originalHash = SHA256.HashData(content);

            Check((await connections.TestConnectionAsync(item)).Single() == item.Database,
                "Read-only connection testing must use the same cipher configuration as normal execution.");
            foreach (var password in new[] { "", "wrong-password" })
            {
                var rejected = item.Clone();
                rejected.Password = password;
                try
                {
                    await connections.TestConnectionAsync(rejected);
                    throw new InvalidOperationException("Encrypted files must reject missing or incorrect passwords.");
                }
                catch (SqliteException) { }
                await VerifyRejectedConnection(rejected);
            }
            var afterTestHash = SHA256.HashData(await File.ReadAllBytesAsync(item.Database));
            Check(originalHash.SequenceEqual(afterTestHash),
                "Connection tests, including failed password attempts, must not modify or rekey the file.");

            query.NotifyConnected(item.Name);
            var result = await query.ExecuteAsync(item.Name, "SELECT value FROM sample WHERE id=1");
            Check(result.IsSuccess && result.Rows.Single()[0] == "encrypted-marker", result.ErrorMessage ?? "Encrypted query failed.");
            Check(await query.BeginTransactionAsync(item.Name), "Encrypted transactions must begin.");
            Check((await query.ExecuteAsync(item.Name, "UPDATE sample SET value='pending' WHERE id=1")).IsSuccess,
                "Encrypted transaction writes must succeed.");
            Check(await query.RollbackAsync(item.Name), "Encrypted transactions must roll back.");
            var independent = await query.ExecuteStandaloneAsync(item, "SELECT value FROM sample WHERE id=1");
            Check(independent.IsSuccess && independent.Rows.Single()[0] == "encrypted-marker",
                "Standalone queries must reopen encrypted files and observe rollback.");
            var metadata = await new DefaultDataEditService(new QueryExecutionChecks.Connections(item))
                .GetTableMetadataAsync(item.Name, item.Database, "sample", null);
            Check(metadata.IsSuccess, metadata.ErrorMessage ?? "Encrypted metadata access failed.");

            var builder = new DatabaseInterpreter.Core.SqliteConnectionStringBuilder();
            var previousDirectory = Directory.GetCurrentDirectory();
            try
            {
                Directory.SetCurrentDirectory(folder);
                var relative = new SqliteConnectionStringBuilder(builder.BuildConntionString(
                    new ConnectionInfo { Database = Path.GetFileName(item.Database), Password = item.Password })) { Pooling = false };
                using var sqlite = new SqliteConnection(relative.ConnectionString);
                await sqlite.OpenAsync();
                using var command = sqlite.CreateCommand();
                command.CommandText = "SELECT COUNT(*) FROM sample";
                Check(Convert.ToInt64(await command.ExecuteScalarAsync()) == 1, "Relative encrypted paths must resolve to the same file.");
            }
            finally { Directory.SetCurrentDirectory(previousDirectory); }

            var created = item.Clone();
            created.Database = Path.Combine(folder, "created-by-query.db");
            var emptyPath = item.Clone();
            emptyPath.Database = string.Empty;
            await VerifyRejectedConnection(emptyPath);
            await VerifyRejectedConnection(created);
            Check(!(await query.ExecuteStandaloneAsync(created, "SELECT 1")).IsSuccess && !File.Exists(created.Database),
                "Standalone queries must not create a missing database file.");
            var missingProfiles = new QueryExecutionChecks.Connections(created);
            var missingQuery = new DefaultQueryService(missingProfiles);
            missingQuery.NotifyConnected(created.Name);
            Check(!(await missingQuery.ExecuteAsync(created.Name, "SELECT 1")).IsSuccess
                && !await missingQuery.BeginTransactionAsync(created.Name) && !File.Exists(created.Database),
                "Queries and transactions must not create a missing database even after notification.");
            await missingQuery.CloseConnectionAsync(created.Name);
            var createString = new SqliteConnectionStringBuilder(builder.BuildConntionString(
                new ConnectionInfo { Database = created.Database, Password = created.Password })) { Pooling = false };
            using (var sqlite = new SqliteConnection(createString.ConnectionString))
            {
                await sqlite.OpenAsync();
                using var command = sqlite.CreateCommand();
                command.CommandText = "CREATE TABLE created(id INTEGER)";
                await command.ExecuteNonQueryAsync();
            }
            Check((await connections.TestConnectionAsync(created)).Single() == created.Database,
                "New encrypted files must reopen through read-only connection testing.");
            SqliteConnection.ClearAllPools();
            Check(!(await File.ReadAllBytesAsync(created.Database)).AsSpan(0, 16).SequenceEqual("SQLite format 3\0"u8),
                "Explicitly created core database files must also be encrypted.");
        }
        finally
        {
            await query.CloseConnectionAsync(item.Name);
            SqliteConnection.ClearAllPools();
            try { Directory.Delete(folder, true); }
            catch (IOException) { Console.WriteLine("Temporary SQLite files are still held by the provider: " + folder); }
        }
        Console.WriteLine("SQLite encryption checks passed: SQLCipher 4, passwords, read-only testing, paths, transactions and metadata.");
    }

    private static async Task VerifyRejectedConnection(ConnectionItem connection)
    {
        var profiles = new QueryExecutionChecks.Connections(connection);
        var query = new DefaultQueryService(profiles);
        var schema = new DefaultDbSchemaService(profiles);
        var explorer = new ObjectsExplorerViewModel(schema);
        var vm = new MainWindowViewModel(schema, profiles, query, new DefaultDataEditService(profiles),
            explorer, new QueryEditorViewModel(query));
        vm.Initialize();
        var node = explorer.FindConnectionNode(connection.Name)!;
        bool rejected = false;
        try { await vm.ConnectConnectionNodeAsync(node); }
        catch (SqliteException) { rejected = true; }
        catch (ArgumentException) { rejected = true; }
        Check(rejected && !node.IsConnectionActive && !query.IsConnected(connection.Name) && !vm.IsConnected,
            "Wrong passwords and missing files must fail before the tree or query service reports connected.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
