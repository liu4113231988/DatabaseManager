using DatabaseInterpreter.Model;
using DatabaseManager.AppCore.Common;
using DatabaseManager.AppCore.Models;

internal static class SqlCompletionChecks
{
    public static void Run()
    {
        var group = new DbObjectTreeNode { NodeType = DbObjectTreeNodeType.Folder };
        var connection = new DbObjectTreeNode
        {
            Name = "server", NodeType = DbObjectTreeNodeType.Connection,
            Connection = new() { Name = "server", DatabaseType = "Postgres", Database = "alpha" },
        };
        group.AddChild(connection);
        var users = Table("users", "public");
        var salesUsers = Table("users", "sales");
        var orders = Table("orders", "public");
        var upper = Table("Users", "public");
        Table("u", "public");
        Table("users", "public", "beta");
        var roots = new[] { group };
        Check(SqlCompletion.Objects(roots, "SERVER", "alpha").Count() == 5, "Grouped connections and current database filter.");
        Check(!SqlCompletion.Objects(roots, "server", "Alpha").Any(), "PostgreSQL database names are case sensitive.");

        var from = Context("SELECT * FROM |");
        Check(from.ExpectsTable && !from.ExpectsColumn, "FROM expects tables, not keywords or columns.");
        var schema = Context("SELECT * FROM sales.us|");
        Check(schema.ExpectsTable && schema.Qualifiers.SequenceEqual(new[] { "sales" }) && schema.Prefix == "us", "Schema-qualified table prefix.");
        Check(Context("SELECT na| FROM public.users u").ExpectsColumn, "SELECT fields resolve sources after the caret.");
        Check(Context("SELECT * FROM public.users WHERE na|").ExpectsColumn, "WHERE expects columns.");
        Check(Context("SELECT * FROM public.users ORDER BY na|").ExpectsColumn, "ORDER BY expects columns.");
        Check(Context("SELECT * FROM public.users u, |").ExpectsTable, "Comma-separated FROM tables.");

        Resolve("SELECT u.na| FROM public.users AS u", users);
        Resolve("SELECT o.| FROM public.users u JOIN public.orders o ON u.id=o.id", orders);
        Resolve("SELECT u.| FROM public.users u; SELECT u.id FROM public.orders u", users);
        Resolve("SELECT u.id FROM public.users u; SELECT u.| FROM public.orders u", orders);
        Resolve("SELECT u.id FROM public.users u UNION SELECT u.| FROM public.orders u", orders);
        Resolve("SELECT COALESCE(u.na|, '') FROM public.users u", users);
        Resolve("SELECT * FROM public.users u WHERE EXISTS (SELECT u.| FROM public.orders u)", orders);
        Resolve("SELECT (SELECT u.id FROM public.orders u), u.| FROM public.users u", users);
        Resolve("SELECT 'FROM public.orders u', u.| FROM public.users u", users);
        Resolve("SELECT ';' AS x; SELECT u.| FROM public.users u; SELECT * FROM public.orders", users);
        Resolve("SELECT 1; /* outer ; /* inner ; */ still ; */ SELECT u.| FROM public.users u", users);
        Resolve("SELECT $tag$ ; FROM fake $tag$; SELECT u.| FROM public.users u", users);
        Resolve("SELECT \"a;\"\"b\", [a;]]b], `a;``b`; SELECT u.| FROM public.users u", users);
        Resolve("SELECT foo$tag$; SELECT u.| FROM public.users u", users);
        Resolve("-- ; SELECT * FROM fake\nSELECT u.| FROM public.users u", users);
        Check(Context("SELECT 1;|").StartOffset == 9, "Completion offsets after a statement terminator must remain absolute.");
        Resolve("SELECT $$ FROM public.orders u $$, u.| FROM public.users u", users);
        Resolve("SELECT u.| FROM public.users u /* JOIN public.orders u */", users);
        Resolve("SELECT u.| FROM sales.users u", salesUsers);
        Resolve("SELECT U.| FROM public.\"Users\" U", upper);
        Resolve("SELECT \"U\".| FROM public.users \"U\"", users);
        Resolve("SELECT u.| FROM public.users \"U\"", null);
        Resolve("SELECT u.| FROM public.users u JOIN public.orders u ON true", null);
        Resolve("SELECT unknown.| FROM public.users u", null);
        Resolve("SELECT users.|", null);
        Resolve("SELECT public.users.|", users);
        Resolve("SELECT beta.public.users.|", null);
        connection.Connection!.DatabaseType = "SqlServer";
        Resolve("SELECT beta.public.users.|", SqlCompletion.Objects(roots, "server", "beta").Single());
        connection.Connection.DatabaseType = "MySql";
        Resolve("SELECT u.| FROM beta.users u", SqlCompletion.Objects(roots, "server", "beta").Single());
        connection.Connection.DatabaseType = "Postgres";
        Resolve("SELECT u.| FROM (SELECT id FROM public.orders) u", null);
        Resolve("WITH users AS (SELECT id FROM public.orders) SELECT u.| FROM users u", null);

        foreach (string text in new[] { "SELECT 'FROM fake f|", "SELECT 'it''s a literal|", "-- SELECT u.|",
                     "SELECT /* outer /* inner */ u.|", "SELECT $tag$ FROM users u; u.|$tag$", "SELECT @va|" })
            Check(Parse(text) is null, "Comments, strings and parameters must not trigger completion: " + text);
        var middle = Context("SELECT na|me FROM public.users");
        Check(middle.StartOffset == 7 && middle.EndOffset == 11 && middle.Prefix == "na", "Replace the complete identifier, including its existing suffix.");
        Check(Context("SELECT [order d|] FROM items").Prefix == "order d", "Quoted identifiers with spaces.");
        Check(SqlCompletion.Unquote("[a]]b]") == "a]b" && SqlCompletion.Unquote("\"a\"\"b\"") == "a\"b", "Escaped identifier delimiters.");
        Check(SqlCompletion.IdentifierEquals("USERS", "users", DatabaseType.Oracle), "Oracle unquoted identifiers fold to uppercase.");
        Check(!SqlCompletion.IdentifierEquals("Users", "users", DatabaseType.Postgres), "PostgreSQL quoted mixed-case objects cannot be resolved from unquoted SQL.");
        Console.WriteLine("SQL completion context checks passed.");

        DbObjectTreeNode Table(string name, string schemaName, string database = "alpha")
        {
            var node = new DbObjectTreeNode { Name = name, NodeType = DbObjectTreeNodeType.DbObject,
                DatabaseObjectType = DatabaseObjectType.Table, Schema = schemaName, DatabaseName = database };
            connection.AddChild(node);
            return node;
        }

        void Resolve(string text, DbObjectTreeNode? expected)
            => Check(ReferenceEquals(SqlCompletion.TableAtDot(roots, "server", "alpha", Context(text)), expected), "Wrong completion table: " + text);
    }

    private static SqlCompletionContext? Parse(string text)
    {
        int caret = text.IndexOf('|');
        return SqlCompletion.Analyze(text.Remove(caret, 1), caret);
    }

    private static SqlCompletionContext Context(string text) => Parse(text) ?? throw new InvalidOperationException("Missing context: " + text);
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
