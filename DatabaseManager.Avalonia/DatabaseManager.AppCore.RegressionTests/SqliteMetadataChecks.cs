using DatabaseInterpreter.Core;
using DatabaseInterpreter.Model;
using DatabaseManager.Profile.Manager;
using DatabaseManager.Profile.Model;
using Microsoft.Data.Sqlite;
using System.Reflection;

internal static class SqliteMetadataChecks
{
    public static async Task RunAsync()
    {
        await VerifyMetadataAsync();
        await VerifyProfileStorageAsync();
        Console.WriteLine("SQLite metadata and profile storage checks passed.");
    }

    private static async Task VerifyMetadataAsync()
    {
        string path = Path.Combine(Path.GetTempPath(), $"dbm-metadata-{Guid.NewGuid():N}.db");
        try
        {
            using (var connection = new SqliteConnection(new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder { DataSource = path, Pooling = false }.ConnectionString))
            {
                await connection.OpenAsync();
                using var command = connection.CreateCommand();
                command.CommandText = """
                    CREATE TABLE parent (id INTEGER PRIMARY KEY);
                    CREATE TABLE child (
                        id INTEGER PRIMARY KEY AUTOINCREMENT,
                        parent_id INTEGER NOT NULL REFERENCES parent(id) ON DELETE CASCADE,
                        amount NUMERIC(10, 2) DEFAULT 0,
                        doubled AS (amount * 2)
                    );
                    CREATE UNIQUE INDEX ix_child ON child (parent_id, amount);
                    CREATE VIEW child_view AS SELECT id, amount FROM child;
                    CREATE TRIGGER child_trigger AFTER INSERT ON child BEGIN SELECT NEW.id; END;
                    """;
                await command.ExecuteNonQueryAsync();
            }

            var options = new DbInterpreterOption();
            var interpreter = new SqliteInterpreter(new ConnectionInfo { Database = path, RequireExistingDatabase = true }, options);
            var tableFilter = new SchemaInfoFilter { TableNames = new[] { "child" } };
            Check((await interpreter.GetTablesAsync(tableFilter)).Single().Name == "child", "Table filters must preserve mapped names.");
            var columns = await interpreter.GetTableColumnsAsync(tableFilter);
            Check(columns.Select(c => c.Name).SequenceEqual(new[] { "id", "parent_id", "amount", "doubled" }), "Column order must follow SQLite metadata.");
            Check(columns[0].IsIdentity && !columns[1].IsNullable && columns[1].DefaultValue is null, "Identity and nullable column metadata must survive mapping.");
            Check(columns[2].Precision == 10 && columns[2].Scale == 2 && columns[2].DefaultValue == "0" && columns[3].IsComputed,
                "Decimal metadata, defaults and computed expressions must survive mapping.");
            Check((await interpreter.GetTablePrimaryKeyItemsAsync(tableFilter)).Single().ColumnName == "id", "Primary key column must survive mapping.");
            var foreignKey = (await interpreter.GetTableForeignKeyItemsAsync(tableFilter)).Single();
            Check(foreignKey.ColumnName == "parent_id" && foreignKey.ReferencedTableName == "parent" && foreignKey.ReferencedColumnName == "id"
                  && foreignKey.DeleteCascade && !foreignKey.UpdateCascade, "Foreign key relationships and cascades must survive mapping.");
            var indexes = (await interpreter.GetTableIndexItemsAsync(tableFilter)).Where(i => i.Name == "ix_child").ToList();
            Check(indexes.Select(i => i.ColumnName).SequenceEqual(new[] { "parent_id", "amount" }) && indexes.All(i => i.IsUnique),
                "Unique index columns must preserve their order.");
            Check((await interpreter.GetViewsAsync()).Single().Definition.Contains("SELECT id", StringComparison.Ordinal), "Detailed views must include SQL.");
            Check((await interpreter.GetTableTriggersAsync(tableFilter)).Single().TableName == "child", "Trigger table must survive mapping.");

            options.ObjectFetchMode = DatabaseObjectFetchMode.Simple;
            Check((await interpreter.GetViewsAsync()).Single().Definition is null && (await interpreter.GetTableTriggersAsync(tableFilter)).Single().Definition is null,
                "Simple metadata mode must omit view and trigger definitions.");
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            File.Delete(path);
        }
    }

    private static async Task VerifyProfileStorageAsync()
    {
        string path = Path.Combine(Path.GetTempPath(), $"dbm-profile-mapping-{Guid.NewGuid():N}.db");
        var profileFile = typeof(ProfileBaseManager).GetProperty("ProfileDataFile", BindingFlags.Static | BindingFlags.NonPublic)!;
        object? previousPath = profileFile.GetValue(null);
        try
        {
            File.Copy(Path.Combine(AppContext.BaseDirectory, "Config", "Template", "Data", "profiles.db3"), path);
            profileFile.SetValue(null, path);
            var original = new ConnectionProfileInfo
            {
                Name = "AOT profile ' fixture",
                DatabaseType = "MySql",
                Database = "sample",
                Server = "localhost",
                Port = "3306",
                UseSsl = true,
            };
            string id = await ConnectionProfileManager.Save(original, rememberPassword: false);
            var profile = await ConnectionProfileManager.GetProfileById(id)
                ?? throw new InvalidOperationException("Saved profile could not be read.");
            Check(profile.Name == original.Name && profile.Port == original.Port && profile.Visible && profile.UseSsl,
                "Stored profile must preserve text, numeric port and boolean fields.");
            var account = await AccountProfileManager.GetProfile(profile.AccountId);
            Check(account is not null && account.Server == original.Server && account.Password is null,
                "Stored account must preserve nullable fields.");
            Check(await ConnectionProfileManager.IsNameExisted(true, profile.AccountId, original.Name, null),
                "Profile lookup must bind a name containing a quote.");
        }
        finally
        {
            profileFile.SetValue(null, previousPath);
            SqliteConnection.ClearAllPools();
            File.Delete(path);
        }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
