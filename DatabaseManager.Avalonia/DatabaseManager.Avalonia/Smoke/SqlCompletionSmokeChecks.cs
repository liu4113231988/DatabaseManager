using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using AvaloniaEdit;
using AvaloniaEdit.CodeCompletion;
using DatabaseInterpreter.Model;
using DatabaseManager.AppCore.Models;
using DatabaseManager.AppCore.Services;
using DatabaseManager.Avalonia.Controls;
using Microsoft.Extensions.DependencyInjection;

namespace DatabaseManager.Avalonia.Smoke;

internal static class SqlCompletionSmokeChecks
{
    public static async Task RunAsync(string output)
    {
        var root = new DbObjectTreeNode { NodeType = DbObjectTreeNodeType.Folder };
        var connection = new DbObjectTreeNode { Name = "completion", NodeType = DbObjectTreeNodeType.Connection,
            Connection = new() { Name = "completion", DatabaseType = "Postgres", Database = "alpha" } };
        root.AddChild(connection);
        AddTable("users", "alpha", true);
        AddTable("users", "alpha", true, "sales");
        AddTable("beta_only", "beta", true);
        AddTable("lazy", "alpha", false);
        AddTable("late", "alpha", false);
        for (int i = 0; i < 100; i++) AddTable($"table_{i:000}", "alpha", true);
        var editor = new SqlEditor { ObjectTreeRoots = new[] { root }, ConnectionName = connection.Name, DatabaseName = "alpha" };
        var window = new Window { Content = editor, Width = 900, Height = 500 };
        var app = (App)Application.Current!;
        var servicesField = typeof(App).GetField("_services", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var originalServices = app.Services;
        var schema = new CompletionSchema();
        using var services = new ServiceCollection().AddSingleton<IDbSchemaService>(schema).BuildServiceProvider();
        servicesField.SetValue(app, services);
        try
        {
            window.Show();
            await Task.Delay(100);
            var textEditor = editor.FindControl<TextEditor>("PART_TextEditor")!;
            await Show("SELECT * FROM |");
            Check(Items().Any(c => c.Text == "table_099") && Items().All(c => c.Text != "SELECT" && c.Text != "beta_only"),
                "FROM candidates must include all loaded tables from the current database.");
            await Show("SELECT * FROM table_0|");
            textEditor.TextArea.RaiseEvent(new TextInputEventArgs { RoutedEvent = InputElement.TextInputEvent, Text = "9" });
            await Task.Delay(50);
            Check(textEditor.Text == "SELECT * FROM table_09" && Popup()!.CompletionList.CurrentList.Any(c => c.Text == "table_099"),
                "Typing must filter all candidates without the old 80-item cutoff.");
            await Show("SELECT u| FROM public.users u");
            textEditor.TextArea.RaiseEvent(new TextInputEventArgs { RoutedEvent = InputElement.TextInputEvent, Text = "." });
            await Task.Delay(50);
            Check(textEditor.Text == "SELECT u. FROM public.users u" && Items().Any(c => c.Text == "name") && Items().All(c => c.Text != "UNION"),
                "Dot typing must switch to alias fields without accepting a keyword.");
            await Show("SELECT u.na| FROM public.users u");
            Check(Items().Select(c => c.Text).Order().SequenceEqual(new[] { "id", "name", "order details" }), "Alias fields must exclude keywords.");
            Check(Items().All(c => c.Description?.ToString()?.StartsWith("字段") == true), "Column descriptions.");
            Accept("name");
            Check(textEditor.Text == "SELECT u.name FROM public.users u", "Accept alias column without duplicating its prefix.");
            await Show("SELECT u.na|me FROM public.users u");
            Accept("name");
            Check(textEditor.Text == "SELECT u.name FROM public.users u", "Replace the existing identifier suffix.");
            await Show("SELECT u.\"order d|\" FROM public.users u");
            Accept("order details");
            Check(textEditor.Text == "SELECT u.\"order details\" FROM public.users u", "Keep quoted identifiers intact.");
            await Show("SELECT * FROM public.us|");
            Accept("users");
            Check(textEditor.Text == "SELECT * FROM public.users", "Schema prefix must not be duplicated.");
            await Show("SELECT na| FROM public.users");
            Check(Items().Any(c => c.Text == "name"), "Unqualified SELECT column completion.");
            await Show("SELECT na| FROM public.users u JOIN public.table_001 t ON true");
            Accept("name", "t.name");
            Check(textEditor.Text == "SELECT t.name FROM public.users u JOIN public.table_001 t ON true", "JOIN fields must insert the chosen alias.");
            await Show("SELECT i| FROM public.users JOIN sales.users ON true");
            Accept("id", "sales.users.id");
            Check(textEditor.Text == "SELECT sales.users.id FROM public.users JOIN sales.users ON true", "Same-name tables must retain their schema qualification.");
            await Show("-- SELECT u.|");
            Check(Popup() is null, "No completion in comments.");
            await Show("SELECT 'u.na|");
            Check(Popup() is null, "No completion in string literals.");

            schema.Reset();
            await Show("SELECT x.| FROM public.lazy AS x");
            Check(schema.Calls == 1 && Popup() is null, "Column loading must be asynchronous and single-flight.");
            schema.Pending.SetResult(new[] { Column("lazy_id") });
            await WaitFor(() => Items().Any(c => c.Text == "lazy_id"));
            var list = Popup()!.CompletionList;
            using (var bitmap = new RenderTargetBitmap(new PixelSize(Math.Max(1, (int)list.Bounds.Width), Math.Max(1, (int)list.Bounds.Height))))
            {
                bitmap.Render(list);
                bitmap.Save(Path.Combine(output, "sql-completion.png"));
            }
            await Show("SELECT x.la| FROM public.lazy x");
            Check(schema.Calls == 1 && Items().Any(c => c.Text == "lazy_id"), "Reuse loaded alias metadata.");

            schema.Reset();
            await Show("SELECT x.| FROM public.late x");
            editor.DatabaseName = "beta";
            schema.Pending.SetResult(new[] { Column("stale_alpha_column") });
            await Task.Delay(100);
            Check(Popup() is null, "Database changes must suppress stale metadata results.");
            await Show("SELECT * FROM |");
            Check(Items().Select(c => c.Text).SequenceEqual(new[] { "beta_only" }), "Changed database binding must refresh candidates.");

            editor.DatabaseName = "alpha";
            schema.Reset();
            await Show("SELECT x.| FROM public.late x");
            textEditor.TextArea.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Escape });
            schema.Pending.SetResult(new[] { Column("late_id") });
            await Task.Delay(100);
            Check(Popup() is null, "Escape must suppress a pending completion popup.");
            await Show("SELECT x.| FROM public.late x");
            Check(Items().Any(c => c.Text == "late_id"), "Manual completion after Escape can reuse metadata.");

            CompletionWindow? Popup() => (CompletionWindow?)typeof(SqlEditor).GetField("_completionWindow", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(editor);
            ICompletionData[] Items() => Popup()?.CompletionList.CompletionData.ToArray() ?? Array.Empty<ICompletionData>();
            async Task Show(string sql)
            {
                Popup()?.Close();
                int caret = sql.IndexOf('|');
                editor.SqlText = sql.Remove(caret, 1);
                textEditor.CaretOffset = caret;
                textEditor.Focus();
                textEditor.TextArea.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Space, KeyModifiers = KeyModifiers.Control });
                await Task.Delay(50);
            }
            void Accept(string name, string? content = null)
            {
                var popup = Popup() ?? throw new InvalidOperationException("Completion popup missing.");
                popup.CompletionList.SelectedItem = Items().Single(c => c.Text == name && (content is null || Equals(c.Content, content)));
                popup.CompletionList.RequestInsertion(new KeyEventArgs { Key = Key.Tab });
            }
        }
        finally
        {
            window.Close();
            servicesField.SetValue(app, originalServices);
        }

        void AddTable(string name, string database, bool loaded, string schemaName = "public")
        {
            var table = new DbObjectTreeNode { Name = name, Schema = schemaName, DatabaseName = database,
                NodeType = DbObjectTreeNodeType.DbObject, DatabaseObjectType = DatabaseObjectType.Table,
                DbObject = new Table { Name = name, Schema = schemaName } };
            var columns = new DbObjectTreeNode { Name = "Columns", NodeType = DbObjectTreeNodeType.ChildFolder, IsLoaded = loaded };
            if (loaded) foreach (var column in new[] { "id", "name", "order details" }) columns.AddChild(Column(column));
            table.AddChild(columns);
            connection.AddChild(table);
        }
    }

    private static DbObjectTreeNode Column(string name) => new() { Name = name, NodeType = DbObjectTreeNodeType.ChildObject, DatabaseObjectType = DatabaseObjectType.Column };
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private static async Task WaitFor(Func<bool> predicate)
    {
        for (int i = 0; i < 100 && !predicate(); i++) await Task.Delay(25);
        Check(predicate(), "Timed out waiting for column completion.");
    }

    private sealed class CompletionSchema : IDbSchemaService
    {
        public TaskCompletionSource<IReadOnlyList<DbObjectTreeNode>> Pending { get; private set; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int Calls { get; private set; }
        public void Reset() { Pending = new(TaskCreationOptions.RunContinuationsAsynchronously); Calls = 0; }
        public Task<IReadOnlyList<DbObjectTreeNode>> GetTableChildNodesAsync(string c, string db, DbObjectChildType kind, DatabaseObject table, bool view = false, CancellationToken ct = default)
        { Calls++; return Pending.Task; }
        public IReadOnlyList<string> GetSupportedDatabaseTypes() => new[] { "Postgres" };
        public Task<IReadOnlyList<DbObjectTreeNode>> GetObjectTreeAsync(string c, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<DbObjectTreeNode>> GetDatabaseChildrenAsync(string c, string db, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<DbObjectTreeNode>> GetDbObjectNodesAsync(string c, string db, DatabaseObjectType type, string? schema = null, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool> HasMultipleSchemasAsync(string c, string db, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<DbObjectTreeNode>> GetSchemasAsync(string c, string db, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<SearchResultItem>> SearchMetadataAsync(string c, string keyword, int limit = 100, string? db = null, string? schema = null, CancellationToken ct = default) => throw new NotSupportedException();
    }
}
