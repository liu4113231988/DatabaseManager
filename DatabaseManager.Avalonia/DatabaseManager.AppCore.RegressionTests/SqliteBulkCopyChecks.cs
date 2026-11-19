using DatabaseInterpreter.Core;
using DatabaseInterpreter.Model;
using Microsoft.Data.Sqlite;
using System.Data;
using System.Data.Common;

internal static class SqliteBulkCopyChecks
{
    private const string PlainPassword = "";

    public static async Task RunAsync()
    {
        var folder = Path.Combine(Path.GetTempPath(), $"dbm-sqlite-bulkcopy-{Guid.NewGuid():N}");
        Directory.CreateDirectory(folder);

        try
        {
            await VerifyDataFidelityAsync(Path.Combine(folder, "plain.db"), PlainPassword);
            await VerifyDataFidelityAsync(Path.Combine(folder, "encrypted #%; bulk.db"), "fixture-only;密钥'\"");
            await VerifyTransactionRollbackAsync(Path.Combine(folder, "rollback.db"));
            await VerifyCancellationAsync(Path.Combine(folder, "cancel.db"));
            await VerifyVariableChunkingAsync(Path.Combine(folder, "chunk.db"), columnCount: 10, rowCount: 250);
            await VerifyVariableChunkingAsync(Path.Combine(folder, "wide.db"), columnCount: 1000, rowCount: 2);
            Console.WriteLine("SQLite bulk copy checks passed: plain/encrypted files, quoted identifiers, blobs, nulls, computed columns, rollback, cancellation and variable chunking.");
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            try { Directory.Delete(folder, true); }
            catch (IOException) { Console.WriteLine("Temporary SQLite files are still held by the provider: " + folder); }
        }
    }

    private static async Task VerifyDataFidelityAsync(string path, string password)
    {
        const string createSql = "CREATE TABLE \"bulk target\" (\"id\" INTEGER, \"text value\" TEXT, \"weird\"\"name\" TEXT, \"payload\" BLOB, \"note\" TEXT)";
        await CreateDatabaseAsync(path, password, createSql);

        var interpreter = new SqliteInterpreter(new ConnectionInfo { Database = path, Password = password, RequireExistingDatabase = true }, new DbInterpreterOption());
        var table = BuildFidelityTable();

        using (var connection = new SqliteConnection(BuildConnectionString(path, password, requireExisting: true)))
        {
            await interpreter.BulkCopyAsync(connection, table, new BulkCopyInfo
            {
                DestinationTableName = "bulk target",
                DestinationTableSchema = "ignored-sqlite-schema-is-a-file-path",
                Columns = new[]
                {
                    new TableColumn { Name = "id" },
                    new TableColumn { Name = "computed", ComputeExp = "id + 1" },
                },
                BatchSize = 500,
            });
        }

        Check(!table.Columns.Contains("computed"), "Computed columns must be excluded before generating INSERT statements.");

        using (var readerConnection = new SqliteConnection(BuildConnectionString(path, password, requireExisting: true)))
        {
            await readerConnection.OpenAsync();
            using var command = readerConnection.CreateCommand();
            command.CommandText = "SELECT * FROM \"bulk target\" ORDER BY \"id\"";
            using var reader = await command.ExecuteReaderAsync();

            Check(await reader.ReadAsync(), "The first bulk copy row must be readable.");
            Check(Convert.ToInt32(reader[0]) == 1, "Integer identity-like values must survive bulk copy.");
            Check((string)reader[1] == "中文 ';\n-- not a comment", "Unicode, quotes and newlines must survive bulk copy.");
            Check((string)reader[2] == "quoted", "Column names containing double quotes must be quoted correctly.");
            Check(((byte[])reader[3]).SequenceEqual(new byte[] { 0, 1, 2, 255 }), "Binary values must survive bulk copy.");
            Check(reader[4] is DBNull, "DBNull must be written as SQL NULL.");

            Check(await reader.ReadAsync(), "The second bulk copy row must be readable.");
            Check(Convert.ToInt32(reader[0]) == 2 && (string)reader[1] == "second", "All rows in a multi-value INSERT must be written.");
            Check(await reader.ReadAsync(), "The third bulk copy row must be readable.");
            Check(Convert.ToInt32(reader[0]) == 3 && reader[3] is DBNull, "Null blobs must be written as SQL NULL.");
            Check(!await reader.ReadAsync(), "Bulk copy must not write extra rows.");
        }

        if (!string.IsNullOrEmpty(password))
        {
            var header = await File.ReadAllBytesAsync(path);
            Check(!header.AsSpan(0, Math.Min(16, header.Length)).SequenceEqual("SQLite format 3\0"u8.Slice(0, Math.Min(16, header.Length))),
                "Encrypted bulk copy must not produce a plaintext SQLite header.");
        }
    }

    private static DataTable BuildFidelityTable()
    {
        var table = new DataTable();
        table.Columns.Add("id", typeof(int));
        table.Columns.Add("text value", typeof(string));
        table.Columns.Add("weird\"name", typeof(string));
        table.Columns.Add("payload", typeof(byte[]));
        table.Columns.Add("note", typeof(string));
        table.Columns.Add("computed", typeof(int));

        table.Rows.Add(1, "中文 ';\n-- not a comment", "quoted", new byte[] { 0, 1, 2, 255 }, DBNull.Value, 0);
        table.Rows.Add(2, "second", "quoted", new byte[] { 9 }, "note", 0);
        table.Rows.Add(3, "third", "quoted", DBNull.Value, DBNull.Value, 0);
        return table;
    }

    private static async Task VerifyTransactionRollbackAsync(string path)
    {
        await CreateDatabaseAsync(path, PlainPassword, "CREATE TABLE \"bulk rollback\" (\"id\" INTEGER, \"value\" TEXT)");
        var interpreter = CreateInterpreter(path, PlainPassword);
        var table = new DataTable();
        table.Columns.Add("id", typeof(int));
        table.Columns.Add("value", typeof(string));
        table.Rows.Add(1, "rollback");
        table.Rows.Add(2, "rollback");

        using var connection = new SqliteConnection(BuildConnectionString(path, PlainPassword, requireExisting: true));
        await connection.OpenAsync();
        using DbTransaction transaction = await connection.BeginTransactionAsync();
        await interpreter.BulkCopyAsync(connection, table, new BulkCopyInfo
        {
            DestinationTableName = "bulk rollback",
            Transaction = transaction,
        });
        await transaction.RollbackAsync();

        Check(await CountRowsAsync(connection, "bulk rollback") == 0, "Rolling back the caller transaction must discard all bulk copy rows.");
    }

    private static async Task VerifyCancellationAsync(string path)
    {
        await CreateDatabaseAsync(path, PlainPassword, "CREATE TABLE \"bulk cancel\" (\"id\" INTEGER)");
        var interpreter = CreateInterpreter(path, PlainPassword);
        var table = new DataTable();
        table.Columns.Add("id", typeof(int));
        table.Rows.Add(1);

        using var connection = new SqliteConnection(BuildConnectionString(path, PlainPassword, requireExisting: true));
        await connection.OpenAsync();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        bool cancelled = false;
        try
        {
            await interpreter.BulkCopyAsync(connection, table, new BulkCopyInfo
            {
                DestinationTableName = "bulk cancel",
                CancellationToken = cancellation.Token,
            });
        }
        catch (OperationCanceledException) { cancelled = true; }

        Check(cancelled, "A cancelled token must abort bulk copy before executing any INSERT.");
        Check(await CountRowsAsync(connection, "bulk cancel") == 0, "Cancelled bulk copy must not insert rows.");
    }

    private static async Task VerifyVariableChunkingAsync(string path, int columnCount, int rowCount)
    {
        var columnNames = Enumerable.Range(0, columnCount).Select(index => "c" + index).ToArray();
        var createSql = "CREATE TABLE \"bulk chunk\" (" + string.Join(", ", columnNames.Select(name => "\"" + name + "\" TEXT")) + ")";
        await CreateDatabaseAsync(path, PlainPassword, createSql);

        var interpreter = CreateInterpreter(path, PlainPassword);
        var table = new DataTable();
        foreach (string name in columnNames) table.Columns.Add(name, typeof(string));
        for (int row = 0; row < rowCount; row++)
        {
            table.Rows.Add(columnNames.Select((_, column) => $"r{row}-c{column}").Cast<object>().ToArray());
        }

        using (var connection = new SqliteConnection(BuildConnectionString(path, PlainPassword, requireExisting: true)))
        {
            await interpreter.BulkCopyAsync(connection, table, new BulkCopyInfo
            {
                DestinationTableName = "bulk chunk",
                BatchSize = 500,
            });
        }

        using var readerConnection = new SqliteConnection(BuildConnectionString(path, PlainPassword, requireExisting: true));
        await readerConnection.OpenAsync();
        Check(await CountRowsAsync(readerConnection, "bulk chunk") == rowCount,
            "Rows requiring more than 999 bound variables must be split into multiple INSERT statements.");
        using var command = readerConnection.CreateCommand();
        command.CommandText = "SELECT c0, c9 FROM \"bulk chunk\" ORDER BY c0 LIMIT 1";
        using var reader = await command.ExecuteReaderAsync();
        Check(await reader.ReadAsync() && (string)reader[0] == "r0-c0" && (string)reader[1] == "r0-c9",
            "Chunk boundaries must preserve row and column order.");
    }

    private static SqliteInterpreter CreateInterpreter(string path, string password) =>
        new(new ConnectionInfo { Database = path, Password = password, RequireExistingDatabase = true }, new DbInterpreterOption());

    private static string BuildConnectionString(string path, string password, bool requireExisting)
    {
        var info = new ConnectionInfo { Database = path, Password = password, RequireExistingDatabase = requireExisting };
        string raw = new DatabaseInterpreter.Core.SqliteConnectionStringBuilder().BuildConntionString(info);
        return new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder(raw) { Pooling = false }.ConnectionString;
    }

    private static async Task CreateDatabaseAsync(string path, string password, string createSql)
    {
        using var connection = new SqliteConnection(BuildConnectionString(path, password, requireExisting: false));
        await connection.OpenAsync();
        using var command = connection.CreateCommand();
        command.CommandText = createSql;
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<long> CountRowsAsync(DbConnection connection, string tableName)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM \"" + tableName.Replace("\"", "\"\"") + "\"";
        return Convert.ToInt64(await command.ExecuteScalarAsync());
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
