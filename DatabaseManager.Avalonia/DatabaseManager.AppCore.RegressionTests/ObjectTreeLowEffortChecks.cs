using DatabaseInterpreter.Model;
using DatabaseManager.AppCore.Models;
using DatabaseManager.AppCore.Services;
using Microsoft.Data.Sqlite;

/// <summary>对象树低难度增强的回归检查：全局触发器目录与节点。</summary>
internal static class ObjectTreeLowEffortChecks
{
    public static async Task RunAsync()
    {
        string path = Path.Combine(Path.GetTempPath(), $"dbm-tree-triggers-{Guid.NewGuid():N}.db");
        var connection = new ConnectionItem
        {
            Name = "tree-trigger-check",
            DatabaseType = "Sqlite",
            Database = path,
        };

        try
        {
            using (var sqlite = new SqliteConnection($"Data Source={path};Pooling=False"))
            {
                await sqlite.OpenAsync();
                using var command = sqlite.CreateCommand();
                command.CommandText = "CREATE TABLE sample (id INTEGER); "
                                      + "CREATE TRIGGER trg_sample AFTER INSERT ON sample BEGIN SELECT 1; END;";
                await command.ExecuteNonQueryAsync();
            }

            var connections = new QueryExecutionChecks.Connections(connection);
            var schema = new DefaultDbSchemaService(connections);

            var folders = await schema.GetDatabaseChildrenAsync(connection.Name, path);
            var triggerFolder = folders.SingleOrDefault(n =>
                n.NodeType == DbObjectTreeNodeType.Folder
                && n.Name == "Triggers"
                && n.DatabaseObjectType == DatabaseObjectType.Trigger);
            Check(triggerFolder is not null, "SQLite databases must expose a global Triggers folder.");

            var triggers = await schema.GetDbObjectNodesAsync(connection.Name, path, DatabaseObjectType.Trigger);
            Check(triggers.Count == 1, "The global Triggers folder must load table triggers.");
            Check(triggers[0].Name == "trg_sample"
                  && triggers[0].DatabaseObjectType == DatabaseObjectType.Trigger
                  && triggers[0].DbObject is TableTrigger,
                "Trigger nodes must carry the trigger object type for icons and menus.");
            Check(triggers[0].DatabaseName == path && triggers[0].DbObject is not null,
                "Trigger nodes must keep database context for definition lookup.");
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            File.Delete(path);
        }

        Console.WriteLine("Object tree low-effort checks passed: global trigger folder and nodes.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
