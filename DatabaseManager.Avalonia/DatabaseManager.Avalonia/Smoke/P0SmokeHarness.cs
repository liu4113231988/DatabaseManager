using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using DatabaseManager.AppCore.Models;
using DatabaseManager.AppCore.Services;
using DatabaseManager.AppCore.ViewModels;
using DatabaseManager.Avalonia.Views;

namespace DatabaseManager.Avalonia.Smoke;

/// <summary>Isolated UI fixtures: no saved database connections or scheduled jobs are executed.</summary>
public static class P0SmokeHarness
{
    public static async Task RunAsync()
    {
        var output = Path.GetFullPath(Path.Combine("artifacts", "p0-ui"));
        Directory.CreateDirectory(output);
        File.Delete(Path.Combine(output, "passed.txt"));
        var folder = Path.Combine(Path.GetTempPath(), "dbm-ui-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        var item = new ConnectionItem { Name = "测试连接", DatabaseType = "Sqlite", Database = Path.Combine(folder, "test.db") };
        var connections = new FixtureConnections(item);
        var query = new DefaultQueryService(connections);
        query.NotifyConnected(item.Name);
        try
        {
            var loginConnection = new ConnectionItem { Id = "reports-profile", Name = "权限测试", DatabaseType = "SqlServer", Database = "master" };
            var otherLogin = loginConnection.Clone();
            otherLogin.Id = "other-profile";
            var security = new UserManagementWindow(new FixtureUsers(), new FixtureConnections(loginConnection, otherLogin));
            await Capture(security, "user-management", verify: async () =>
            {
                var node = new DbObjectTreeNode
                {
                    Text = "report_reader",
                    PrincipalName = "report_reader",
                    PrincipalKind = DbPrincipalKind.Role,
                    NodeType = DbObjectTreeNodeType.Principal,
                    DatabaseName = "reports",
                };
                await security.OpenPrincipalAsync(loginConnection, node, PrincipalManagementAction.ShowGrants, node.DatabaseName);
                var grid = security.FindControl<DataGrid>("UsersGrid")!;
                var grants = security.GetVisualDescendants().OfType<TextBox>().Single(t => t.Name == "TxtGrants");
                if (grid.SelectedItem is not DbUserInfo { Name: "report_reader" } || grants.Text?.Contains("GRANT SELECT") != true
                    || security.FindControl<TextBlock>("TxtScope")?.Text != "权限测试 / reports")
                    throw new InvalidOperationException("Opening permissions from the tree must preserve the database, select the principal and show its grants.");
            });
            File.WriteAllBytes(item.Database, []);
            await VerifySqlServerLoginMetadataAsync();
            var diagnose = new DiagnoseViewModel(connections, new DefaultDiagnoseService());
            var diagnoseWindow = new DiagnoseWindow(diagnose);
            await Capture(diagnoseWindow, "diagnose-table",
                () => diagnose.TableResults.Add(new TableDiagnoseResultItem("列", "main", "sample", "title", 7, "SELECT title FROM sample")),
                () =>
                {
                    var grid = diagnoseWindow.GetVisualDescendants().OfType<DataGrid>().Single();
                    var cells = grid.GetVisualDescendants().OfType<TextBlock>().Select(text => text.Text).ToArray();
                    if (grid.Columns.Count != 7 || !cells.Contains("main.sample.title") || !cells.Contains("7"))
                        throw new InvalidOperationException("Diagnosis result columns did not render their bound values.");
                    return Task.CompletedTask;
                });
            await SqlCompletionSmokeChecks.RunAsync(output);
            var templateVm = new TemplateManagerViewModel(new DatabaseManager.AppCore.Templating.DefaultTemplateStore(), new DatabaseManager.AppCore.Templating.TemplateEngine());
            await Capture(new TemplateManagerWindow(templateVm), "sql-template-manager", null, async () =>
            {
                var snippet = templateVm.Templates.FirstOrDefault(t => t.Kind == "sql-snippet")
                    ?? throw new InvalidOperationException("Builtin SQL snippets must be listed in the template manager.");
                templateVm.SelectedTemplate = snippet;
                templateVm.PreviewCommand.Execute(null);
                if (templateVm.PreviewText != templateVm.EditingContent)
                    throw new InvalidOperationException("SQL snippet preview must preserve content without variables.");

                var codeTemplate = templateVm.Templates.FirstOrDefault(t => t.Id == "code-csharp-property")
                    ?? throw new InvalidOperationException("Externalized code templates must be listed in the template manager.");
                templateVm.SelectedTemplate = codeTemplate;
                templateVm.PreviewCommand.Execute(null);
                if (!templateVm.PreviewText.Contains("public", StringComparison.Ordinal)
                    || templateVm.PreviewText.Contains("{type}", StringComparison.Ordinal))
                    throw new InvalidOperationException("Sample preview must expand externalized code template variables.");
                await Task.Delay(100);
            });
            await query.ExecuteAsync(item.Name, "CREATE TABLE sample(id INTEGER PRIMARY KEY, title TEXT, payload BLOB); INSERT INTO sample VALUES(1,'测试文本',X'0102');");
            var tab = new QueryTabViewModel(query, new DefaultDataEditService(connections)) { ConnectionName = item.Name, DatabaseName = item.Database, SqlText = "SELECT * FROM sample" };
            await tab.ExecuteAsync(); tab.PinResultCommand.Execute(null);
            var gridProbe = new DataGrid();
            Controls.ResultGridHelper.RebuildColumns(gridProbe, tab);
            if (gridProbe.Columns.Count != tab.Columns.Count
                || gridProbe.Columns.Any(c => c is not DataGridTemplateColumn))
                throw new InvalidOperationException("Result grid must use reflection-free template columns.");
            await Capture(new RecordEditorWindow(tab, tab.Rows[0], item), "record");
            await Capture(new ResultCompareWindow(tab.ResultSnapshots.ToArray()), "compare");
            var server = new ConnectionItem { Id = "postgres-profile", Name = "数据库切换测试", DatabaseType = "Postgres", Database = "app_db" };
            var serverConnections = new FixtureConnections(server);
            var serverQuery = new DefaultQueryService(serverConnections);
            var schema = new DefaultDbSchemaService(serverConnections);
            var explorer = new ObjectsExplorerViewModel(schema);
            var mainVm = new MainWindowViewModel(schema, serverConnections, serverQuery,
                new DefaultDataEditService(serverConnections), explorer, new QueryEditorViewModel(serverQuery));
            mainVm.Initialize();
            var roleFolder = new DbObjectTreeNode { NodeType = DbObjectTreeNodeType.PrincipalFolder, Parent = explorer.FindConnectionNode(server.Name) };
            mainVm.SelectedConnection = server;
            mainVm.CurrentDatabase = "report_db";
            if (mainVm.GetPrincipalManagementDatabase(roleFolder) != "report_db")
                throw new InvalidOperationException("A PostgreSQL role folder must inherit the current database for its connection.");
            var otherServer = server.Clone();
            otherServer.Id = "other-postgres-profile";
            mainVm.SelectedConnection = otherServer;
            mainVm.CurrentDatabase = "other_db";
            if (mainVm.GetPrincipalManagementDatabase(roleFolder) is not null)
                throw new InvalidOperationException("A role folder must not inherit another connection's database.");
            mainVm.SelectedConnection = null;
            await DatabaseDeletionSmokeChecks.RunAsync(output, mainVm, serverConnections);
            await DatabaseManagementSmokeChecks.RunAsync(output, mainVm, serverConnections, item);
            var shell = new MainWindow { DataContext = mainVm };
            var content = shell.Content;
            shell.Content = null;
            var databaseWindow = new Window { DataContext = mainVm, Content = content, Width = 1280, Height = 800 };
            await Capture(databaseWindow, "query-database", () =>
            {
                var node = explorer.FindConnectionNode(server.Name)!;
                serverQuery.NotifyConnected(server.Name);
                foreach (var name in new[] { "app_db", "report_db" })
                    node.Children.Add(new DbObjectTreeNode { Name = name, NodeType = DbObjectTreeNodeType.Database });
                var roles = new DbObjectTreeNode { Name = "不可登录角色", Text = "不可登录角色", NodeType = DbObjectTreeNodeType.PrincipalFolder, PrincipalKind = DbPrincipalKind.Role };
                roles.AddChild(new DbObjectTreeNode { Name = "report_reader", Text = "report_reader", NodeType = DbObjectTreeNodeType.Principal, PrincipalKind = DbPrincipalKind.Role });
                roles.IsLoaded = true;
                roles.IsExpanded = true;
                node.AddChild(roles);
                node.IsConnectionActive = true;
                node.IsExpanded = true;
                var queryTab = mainVm.OpenSqlInNewTab(server.Name, "SELECT current_database();", server.Database);
                if (queryTab.AvailableDatabases.Count != 2) throw new InvalidOperationException("Database selector fixture failed.");
            }, async () =>
            {
                if (!databaseWindow.GetVisualDescendants().OfType<TextBlock>().Any(t => t.IsEffectivelyVisible && t.Text == "report_reader"))
                    throw new InvalidOperationException("The object tree must render a visible role node.");
                var queryTab = mainVm.SelectedQueryTab!;
                var selector = databaseWindow.GetVisualDescendants().OfType<AtomUI.Desktop.Controls.ComboBox>()
                    .Single(c => ReferenceEquals(c.ItemsSource, queryTab.AvailableDatabases));
                if (!selector.IsVisible || !Equals(selector.SelectedItem, "app_db"))
                    throw new InvalidOperationException("Database selector must display its initial context.");
                if (!selector.GetVisualAncestors().OfType<Border>().Any(b => b.Name == "MainToolbarPanel"))
                    throw new InvalidOperationException("Query database selector must be in the main toolbar.");
                selector.SelectedItem = "report_db";
                await Task.Delay(100);
                if (queryTab.DatabaseName != "report_db") throw new InvalidOperationException("Database selection binding failed.");
                queryTab.IsExecuting = true;
                selector.SelectedItem = "app_db";
                await Task.Delay(100);
                if (queryTab.DatabaseName != "report_db" || !Equals(selector.SelectedItem, "report_db"))
                    throw new InvalidOperationException("Rejected database selection must restore the displayed context.");
                queryTab.IsExecuting = false;
                var otherTab = mainVm.OpenSqlInNewTab(server.Name, "SELECT 2;", "app_db");
                await Task.Delay(100);
                if (!databaseWindow.GetVisualDescendants().OfType<Controls.SqlEditor>()
                    .Any(editor => editor.IsEffectivelyVisible && ReferenceEquals(editor.DataContext, otherTab)
                        && editor.SqlText == "SELECT 2;"))
                    throw new InvalidOperationException("The selected query tab must render its SQL editor and text.");
                if (!ReferenceEquals(selector.DataContext, otherTab) || !Equals(selector.SelectedItem, "app_db"))
                    throw new InvalidOperationException("Toolbar must follow the selected query tab.");
                mainVm.SelectedQueryTab = queryTab;
                await Task.Delay(100);
                if (!Equals(selector.SelectedItem, "report_db"))
                    throw new InvalidOperationException("Switching tabs must preserve each database context.");
                databaseWindow.Width = 760;
                await Task.Delay(200);
                var position = selector.TranslatePoint(default, databaseWindow);
                if (position is null || position.Value.X < 0
                    || position.Value.X + selector.Bounds.Width > databaseWindow.ClientSize.Width)
                    throw new InvalidOperationException("Query context must remain visible in a narrow toolbar.");
                using (var narrow = new RenderTargetBitmap(new global::Avalonia.PixelSize(760, 800), new global::Avalonia.Vector(96, 96)))
                {
                    narrow.Render(databaseWindow);
                    narrow.Save(Path.Combine(output, "query-toolbar-narrow.png"), new PngBitmapEncoderOptions());
                }
                databaseWindow.Width = 1280;
                await Task.Delay(200);
                var autoCommit = databaseWindow.GetVisualDescendants().OfType<AtomUI.Desktop.Controls.CheckBox>()
                    .Single(c => Equals(c.Content, "自动提交"));
                autoCommit.IsChecked = false;
                await Task.Delay(100);
                if (serverQuery.IsAutoCommit(server.Name) || mainVm.QueryEditor.AutoCommit)
                    throw new InvalidOperationException("Manual commit checkbox must update the service mode.");
                queryTab.IsExecuting = true;
                await Task.Delay(100);
                if (autoCommit.IsChecked != false) throw new InvalidOperationException("Execution state refresh must preserve manual mode.");
                queryTab.IsExecuting = false;
                autoCommit.IsChecked = true;
                await Task.Delay(100);
                if (!serverQuery.IsAutoCommit(server.Name) || autoCommit.IsChecked != true)
                    throw new InvalidOperationException("Auto commit checkbox must restore automatic mode.");
            });
            var vm = new ExecutionPlanViewModel(new FixturePlan()) { Connection = item, SqlText = "SELECT * FROM sample" };
            await Capture(new ExecutionPlanWindow(vm), "plan", async () =>
            {
                await vm.ExecuteCommand.ExecuteAsync(null);
                if (vm.PlanNodes.Count == 0 || vm.PlanNodes[0].Operation != "Index Scan")
                    throw new InvalidOperationException("Execution plan must parse fixture rows into nodes.");
            });

            var jsonVm = new DatabaseManager.AppCore.ViewModels.JsonViewerViewModel
            {
                JsonText = "{\"name\":\"orders\",\"columns\":[{\"id\":1,\"nullable\":false}]}",
            };
            jsonVm.FormatCommand.Execute(null);
            jsonVm.BuildTreeCommand.Execute(null);
            if (jsonVm.TreeNodes.Count != 1 || jsonVm.TreeNodes[0].Children.Count != 2
                || jsonVm.TreeNodes[0].Children[1].Children.Count != 1
                || jsonVm.TreeNodes[0].Children[1].Children[0].Children.Count != 2
                || jsonVm.StatusMessage != "已加载 JSON 树。")
                throw new InvalidOperationException("JSON viewer must build the tree with source-generated JSON APIs.");
            var connect = new ConnectWindow(new ConnectionManagerViewModel(connections), new ConnectionItem { DatabaseType = "Postgres", Name = "SSH 测试", Server = "db.internal", Port = "5432", Ssh = new() { Enabled = true, Host = "bastion.example", UserName = "developer" } });
            connect.Height = 980;
            await Capture(connect, "ssh", () => { foreach (var expander in connect.GetVisualDescendants().OfType<Expander>()) expander.IsExpanded = true; });
            var encrypted = new ConnectionItem
            {
                Name = "SQLite 加密测试", DatabaseType = "Sqlite", Database = Path.Combine(folder, "encrypted.db"),
                Password = "fixture-only-password", RememberPassword = true,
            };
            var encryptedString = new DatabaseInterpreter.Core.SqliteConnectionStringBuilder().BuildConntionString(
                new DatabaseInterpreter.Model.ConnectionInfo { Database = encrypted.Database, Password = encrypted.Password });
            using (var sqlite = new Microsoft.Data.Sqlite.SqliteConnection(encryptedString))
            {
                await sqlite.OpenAsync();
                using var command = sqlite.CreateCommand();
                command.CommandText = "CREATE TABLE sample(id INTEGER)";
                await command.ExecuteNonQueryAsync();
            }
            var encryptedConnect = new ConnectWindow(new ConnectionManagerViewModel(connections), encrypted);
            await Capture(encryptedConnect, "sqlite-encryption", verify: async () =>
            {
                var password = encryptedConnect.GetVisualDescendants().OfType<AtomUI.Desktop.Controls.TextBox>()
                    .Single(t => t.Name == "TxtPassword");
                var user = encryptedConnect.GetVisualDescendants().OfType<AtomUI.Desktop.Controls.TextBox>()
                    .Single(t => t.Name == "TxtUserId");
                var remember = encryptedConnect.GetVisualDescendants().OfType<AtomUI.Desktop.Controls.CheckBox>()
                    .Single(c => c.Name == "ChkRememberPassword");
                if (!password.IsEffectivelyVisible || user.IsEffectivelyVisible || !remember.IsEffectivelyVisible
                    || password.PasswordChar != '*' || password.Text != encrypted.Password || remember.IsChecked != true)
                    throw new InvalidOperationException("SQLite must show a masked password and remember-password option without a user name.");
                var built = (ConnectionItem)typeof(ConnectWindow).GetMethod("BuildConnection",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.Invoke(encryptedConnect, null)!;
                await new ProfileDbConnectionService(null!).TestConnectionAsync(built);
                var types = encryptedConnect.GetVisualDescendants().OfType<AtomUI.Desktop.Controls.ComboBox>()
                    .Single(c => c.Name == "ComboDatabaseType");
                types.SelectedItem = "DuckDB";
                await Task.Delay(50);
                if (password.IsEffectivelyVisible || !string.IsNullOrEmpty(password.Text))
                    throw new InvalidOperationException("DuckDB must hide and clear the SQLite password.");
                types.SelectedItem = "Sqlite";
                await Task.Delay(50);
                if (!password.IsEffectivelyVisible || !string.IsNullOrEmpty(password.Text))
                    throw new InvalidOperationException("Returning to SQLite must show an empty password field.");
            });
            DefaultScheduleService OpenSchedules() => new(new DefaultTaskCenterService(), connections, query,
                new DefaultBackupService(), new DefaultExportImportService(connections), storageDirectory: folder);
            File.WriteAllText(Path.Combine(folder, "schedules.json"), """
                [{"Id":"p0","Name":"旧计划","ConnectionName":"测试连接","Enabled":false,"LastResult":"完成",
                  "Steps":[{"Name":"步骤","ConnectionName":"测试连接","SqlText":"SELECT 1"}],
                  "Dictionary":{"Objects":["sample"]}}]
                """);
            var schedule = OpenSchedules();
            var window = new ScheduleWindow(schedule, connections);
            void ClickSchedule(string label) => window.GetVisualDescendants().OfType<Button>()
                .Single(b => b.Content?.ToString() == label).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await Capture(window, "schedule", verify: async () =>
            {
                var original = schedule.GetAll().Single();
                window.FindControl<DataGrid>("SchedulesGrid")!.SelectedIndex = 0;
                ClickSchedule("编辑");
                await Task.Delay(200);
                window.FindControl<AtomUI.Desktop.Controls.TextBox>("TxtName")!.Text = "更新计划";
                ClickSchedule("保存");
                var saved = OpenSchedules().GetAll().Single();
                if (original.Name != "旧计划" || saved.Id != "p0" || saved.Name != "更新计划" || saved.Enabled
                    || saved.LastResult != "完成" || saved.Steps.Single().SqlText != "SELECT 1"
                    || !saved.Dictionary.Objects.SequenceEqual(new[] { "sample" }))
                    throw new InvalidOperationException("Editing a legacy schedule must preserve its steps, settings and history.");
                ClickSchedule("新建计划");
            });
            File.WriteAllText(Path.Combine(output, "passed.txt"), "P0 UI fixtures opened and rendered successfully.");
        }
        finally
        {
            await query.CloseConnectionAsync(item.Name);
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        }
        async Task Capture(Window window, string name, Action? arrange = null, Func<Task>? verify = null)
        {
            window.Show(); await Task.Delay(200); arrange?.Invoke(); await Task.Delay(200);
            if (verify is not null) await verify();
            using var bitmap = new RenderTargetBitmap(new global::Avalonia.PixelSize((int)window.Bounds.Width, (int)window.Bounds.Height), new global::Avalonia.Vector(96,96));
            bitmap.Render(window);
            bitmap.Save(Path.Combine(output, name + ".png"), new PngBitmapEncoderOptions());
            window.Close();
        }
    }
    private static async Task VerifySqlServerLoginMetadataAsync()
    {
        // Exercise the SQL Server catalog queries and their row mappers inside the native EXE.
        using var fixture = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:");
        fixture.CreateFunction<int, string>("schema_name", id => id == 1 ? "dbo" : string.Empty);
        await fixture.OpenAsync();
        using var command = fixture.CreateCommand();
        command.CommandText = """
            ATTACH DATABASE ':memory:' AS sys;
            CREATE TABLE sys.databases(name TEXT);
            INSERT INTO sys.databases VALUES ('sales'), ('archive');
            CREATE TABLE sys.schemas(name TEXT);
            INSERT INTO sys.schemas VALUES ('dbo'), ('guest');
            """;
        await command.ExecuteNonQueryAsync();
        var interpreter = new DatabaseInterpreter.Core.SqlServerInterpreter(new(), new());
        var databases = await interpreter.GetDatabasesAsync(fixture);
        var schemas = await interpreter.GetDatabaseSchemasAsync(fixture);
        if (!databases.Select(d => d.Name).SequenceEqual(new[] { "archive", "sales" })
            || schemas.Count != 1 || schemas[0].Name != "dbo" || schemas[0].Schema != "dbo")
            throw new InvalidOperationException("SQL Server database and schema rows must map without dynamic code.");

        command.CommandText = """
            CREATE TABLE sys.tables(object_id INTEGER, schema_id INTEGER, name TEXT);
            INSERT INTO sys.tables VALUES (1, 1, 'orders');
            CREATE TABLE sys.indexes(object_id INTEGER, index_id INTEGER, data_space_id INTEGER, name TEXT);
            INSERT INTO sys.indexes VALUES (1, 1, 7, 'IX_orders');
            CREATE TABLE sys.partition_schemes(data_space_id INTEGER, name TEXT, function_id INTEGER);
            INSERT INTO sys.partition_schemes VALUES (7, 'ps_orders', 3);
            CREATE TABLE sys.index_columns(object_id INTEGER, index_id INTEGER, column_id INTEGER, partition_ordinal INTEGER);
            INSERT INTO sys.index_columns VALUES (1, 1, 2, 1);
            CREATE TABLE sys.columns(object_id INTEGER, column_id INTEGER, name TEXT);
            INSERT INTO sys.columns VALUES (1, 2, 'created_at');
            CREATE TABLE sys.partition_functions(function_id INTEGER, name TEXT, type TEXT, boundary_value_on_right INTEGER);
            INSERT INTO sys.partition_functions VALUES (3, 'pf_orders', 'RANGE', 1);
            CREATE TABLE sys.destination_data_spaces(partition_scheme_id INTEGER, destination_id INTEGER, data_space_id INTEGER);
            INSERT INTO sys.destination_data_spaces VALUES (7, 1, 11), (7, 2, 12);
            CREATE TABLE sys.filegroups(data_space_id INTEGER, name TEXT);
            INSERT INTO sys.filegroups VALUES (11, 'fg_2025'), (12, 'fg_2026');
            CREATE TABLE sys.partition_parameters(function_id INTEGER, system_type_id INTEGER, max_length INTEGER, precision INTEGER, scale INTEGER);
            INSERT INTO sys.partition_parameters VALUES (3, 61, 8, 0, 0);
            CREATE TABLE sys.types(system_type_id INTEGER, name TEXT);
            INSERT INTO sys.types VALUES (61, 'datetime2');
            CREATE TABLE sys.partition_range_values(function_id INTEGER, boundary_id INTEGER, value TEXT);
            INSERT INTO sys.partition_range_values VALUES (3, 1, '2025-01-01'), (3, 2, '2026-01-01');
            """;
        await command.ExecuteNonQueryAsync();

        var table = new DatabaseInterpreter.Model.Table { Schema = "dbo", Name = "orders" };
        var scheme = await interpreter.GetPartitionSchemeByTable(fixture, table, true);
        if (scheme is null || scheme.SchemeName != "ps_orders" || scheme.ColumnName != "created_at"
            || scheme.FunctionName != "pf_orders" || scheme.Filegroups.Count != 2)
            throw new InvalidOperationException("SQL Server partition scheme rows must map without dynamic code.");
        var functions = await interpreter.GetPartitionFunctions(fixture, new[] { "pf_orders" }, true);
        if (functions.Count != 1 || functions[0].Name != "pf_orders" || functions[0].Type != "RANGE"
            || !functions[0].IsOnRight || functions[0].Values.Count != 2
            || functions[0].Values[0] != "2025-01-01")
            throw new InvalidOperationException("SQL Server partition function rows must map without dynamic code.");
    }

    private sealed class FixtureUsers : DefaultDbUserService, IDbUserService
    {
        Task<(List<DbUserInfo>, string?)> IDbUserService.GetUsersAsync(ConnectionItem connection, CancellationToken cancellationToken, string? databaseName, DbPrincipalKind principalKind)
        {
            if (Dispatcher.UIThread.CheckAccess() || connection.Id != "reports-profile" || databaseName != "reports" || principalKind != DbPrincipalKind.Role)
                throw new InvalidOperationException("Principal queries must use the tree scope and run off the UI thread.");
            return Task.FromResult<(List<DbUserInfo>, string?)>(([new() { Name = "report_reader", Kind = "role" }], null));
        }

        Task<(string, string?)> IDbUserService.GetGrantsAsync(ConnectionItem connection, string userName, string? host, CancellationToken cancellationToken, string? databaseName)
        {
            if (Dispatcher.UIThread.CheckAccess() || databaseName != "reports" || userName != "report_reader")
                throw new InvalidOperationException("Grant queries must use the selected principal and run off the UI thread.");
            return Task.FromResult<(string, string?)>(("GRANT SELECT", null));
        }
    }

    private sealed class FixturePlan : IExecutionPlanService
    {
        public Task<QueryResult> ExplainAsync(ConnectionItem connection,string sql,bool analyze=false,CancellationToken cancellationToken=default) => Task.FromResult(new QueryResult
        {
            Columns = new[] { "QUERY PLAN" }, Rows = new[] { new[] { "[{\"Plan\":{\"Node Type\":\"Index Scan\",\"Relation Name\":\"sample\",\"Total Cost\":8.2,\"Actual Total Time\":0.03}}]" } },
        });
    }
    private sealed class FixtureConnections(ConnectionItem item, params ConnectionItem[] otherItems) : IDbConnectionService
    {
        public IReadOnlyList<ConnectionItem> GetConnections() => [.. otherItems, item];
        public IReadOnlyList<ConnectionItem> GetConnections(string type) => GetConnections();
        public ConnectionItem? GetConnectionById(string id) => item;
        public Task<IReadOnlyList<ConnectionItem>> GetConnectionsAsync(CancellationToken cancellationToken = default) => Task.FromResult(GetConnections());
        public Task<ConnectionItem?> GetConnectionByIdAsync(string id, CancellationToken cancellationToken = default) => Task.FromResult(GetConnectionById(id));
        public Task<IReadOnlyList<string>> TestConnectionAsync(ConnectionItem c,CancellationToken ct=default) => Task.FromResult<IReadOnlyList<string>>(new[] { c.Database });
        public Task<string?> SaveAsync(ConnectionItem c,CancellationToken ct=default) => throw new NotSupportedException();
        public Task<bool> DeleteAsync(IEnumerable<string> ids,CancellationToken ct=default) => throw new NotSupportedException();
        public Task<bool> IsNameExistedAsync(bool add,string? account,string name,string? id,CancellationToken ct=default) => Task.FromResult(false);
    }
}
