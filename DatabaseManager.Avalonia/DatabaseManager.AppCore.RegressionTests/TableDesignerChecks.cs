using DatabaseManager.AppCore.Models;
using DatabaseManager.AppCore.Services;
using DatabaseManager.AppCore.ViewModels;

internal static class TableDesignerChecks
{
    public static async Task RunAsync()
    {
        VerifyColumnParser();
        VerifyInlineEditingModels();
        await VerifyViewModelNotificationsAsync();
        await VerifyPartitionCapabilityAsync();
        Console.WriteLine("Table designer checks passed: column parsing, inline editing, change tracking and partition capability.");
    }

    private static void VerifyColumnParser()
    {
        var columns = TableDesignColumnParser.ParseColumns(" id, name ,id,，created_at ");
        Check(columns.SequenceEqual(new[] { "id", "name", "created_at" }),
            "Column parser must trim, deduplicate and accept Chinese commas.");
        var mappings = TableDesignColumnParser.ParseMappings("id=ref_id, name, id=other, =bad");
        Check(mappings.Count == 2
              && mappings[0].ColumnName == "id" && mappings[0].ReferencedColumnName == "ref_id"
              && mappings[1].ColumnName == "name" && mappings[1].ReferencedColumnName == "name",
            "Foreign key mapping parser must support omitted references and reject invalid/duplicate entries.");
        Check(TableDesignColumnParser.ToDisplay(mappings) == "id=ref_id, name=name",
            "Foreign key mapping display must be parseable again.");
    }

    private static void VerifyInlineEditingModels()
    {
        var key = new TableDesignKey();
        var keyEvents = new List<string>();
        key.PropertyChanged += (_, e) => keyEvents.Add(e.PropertyName ?? string.Empty);
        key.ColumnsDisplay = "id, name, id";
        Check(key.Columns.SequenceEqual(new[] { "id", "name" }), "Primary key columns must be editable inline.");
        Check(keyEvents.Contains("Columns") && keyEvents.Contains("ColumnsDisplay"),
            "Primary key inline edit must notify both Columns and ColumnsDisplay.");

        var index = new TableDesignIndex();
        index.ColumnsDisplay = "created_at, id";
        Check(index.ColumnsDisplay == "created_at, id" && index.Columns.Count == 2,
            "Index columns must be editable inline and preserve order.");

        var fk = new TableDesignForeignKey();
        fk.ColumnsDisplay = "user_id=id, tenant_id=tenant_id";
        Check(fk.Columns.Count == 2 && fk.Columns[0].ReferencedColumnName == "id",
            "Foreign key mappings must be editable inline.");
        fk.SelectLocalColumns(new[] { "region,code", "user_id" });
        Check(fk.Columns.Select(c => c.ColumnName).SequenceEqual(new[] { "region,code", "user_id" })
              && fk.Columns.Select(c => c.ReferencedColumnName).SequenceEqual(new[] { "region,code", "id" }),
            "Picking local columns must preserve existing references and keep selected identifiers intact.");
        fk.ColumnsDisplay = "user_id";
        Check(fk.ColumnsDisplay == "user_id=user_id", "Omitted reference column must default to the local column.");
    }

    private static async Task VerifyViewModelNotificationsAsync()
    {
        var stub = new DesignServiceStub();
        var vm = new TableDesignerViewModel(stub);
        bool loaded = await vm.LoadAsync("conn", "db", "orders", "public", isNew: false);
        Check(loaded && vm.Columns.Count == 1 && !vm.HasChanges,
            "Loading a table must succeed and reset the dirty flag.");
        Check(vm.PartitionSummary.Contains("分区", StringComparison.Ordinal),
            "Partition summary must be populated during load.");

        var index = new TableDesignIndex { Name = "IX_orders" };
        vm.Indexes.Add(index);
        vm.HasChanges = false;
        index.ColumnsDisplay = "id, created_at";
        Check(vm.HasChanges, "Inline index column editing must mark the designer dirty.");

        var fk = new TableDesignForeignKey { Name = "FK_orders" };
        vm.ForeignKeys.Add(fk);
        vm.HasChanges = false;
        fk.ColumnsDisplay = "user_id=id";
        Check(vm.HasChanges, "Inline foreign key mapping editing must mark the designer dirty.");

        vm.PrimaryKey = new TableDesignKey { Name = "PK_orders" };
        vm.HasChanges = false;
        vm.PrimaryKey.ColumnsDisplay = "id";
        Check(vm.HasChanges, "Inline primary key editing must mark the designer dirty.");
    }

    private static async Task VerifyPartitionCapabilityAsync()
    {
        var connectionService = new QueryExecutionChecks.Connections(new ConnectionItem
        {
            Name = "sqlite", DatabaseType = "Sqlite", Database = ":memory:",
        });
        var service = new DefaultTableDesignService(connectionService);
        var result = await service.LoadPartitionAsync("sqlite", "main", "orders", null);
        Check(result.IsSuccess && result.DatabaseType == "Sqlite"
              && result.Summary.Contains("暂不提供分区元数据读取", StringComparison.Ordinal),
            "Unsupported dialects must return a clear partition capability message without opening a connection.");
        Check(result.Items.Count == 0 && result.Definition.Length == 0,
            "Unsupported dialects must not fabricate partition definitions.");

        var missing = await service.LoadPartitionAsync("missing", "main", "orders", null);
        Check(!missing.IsSuccess && missing.ErrorMessage.Contains("未找到连接", StringComparison.Ordinal),
            "Missing connections must be reported before partition loading.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private sealed class DesignServiceStub : ITableDesignService
    {
        public Task<TableDesignLoadResult> LoadTableAsync(
            string connectionName, string databaseName, string tableName, string? schema, bool isNew,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new TableDesignLoadResult
            {
                IsSuccess = true,
                Design = new TableDesignInfo
                {
                    DatabaseName = databaseName, Schema = schema, Name = tableName, IsNew = isNew,
                    Columns = new List<TableDesignColumn> { new() { Name = "id", DataType = "int" } },
                },
            });

        public Task<TablePartitionLoadResult> LoadPartitionAsync(
            string connectionName, string databaseName, string tableName, string? schema,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new TablePartitionLoadResult
            {
                IsSuccess = true, DatabaseType = "Stub", Summary = "分区信息：测试桩", Definition = string.Empty,
            });

        public Task<TableDesignScriptResult> GenerateScriptsAsync(
            string connectionName, string databaseName, TableDesignInfo design,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new TableDesignScriptResult { IsSuccess = true, Script = string.Empty });

        public Task<TableDesignSaveResult> SaveAsync(
            string connectionName, string databaseName, TableDesignInfo design,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new TableDesignSaveResult { IsSuccess = true });
    }
}
