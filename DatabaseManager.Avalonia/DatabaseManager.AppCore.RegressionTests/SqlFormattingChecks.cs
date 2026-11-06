using DatabaseConverter.Core;
using DatabaseInterpreter.Core;
using DatabaseInterpreter.Model;
using TSQL;
using TSQL.Tokens;

internal static class SqlFormattingChecks
{
    public static void Run()
    {
        var samples = new[]
        {
            (DatabaseType.MySql, "select MixedCase, 'from a,b where x and y; end', 'it''s select' from users where id=1"),
            (DatabaseType.MySql, "SELECT `order details`.`select`, `a``b` FROM `order details`"),
            (DatabaseType.MySql, "SELECT /* preserve hint */ id FROM users -- preserve comment\nWHERE id=1"),
            (DatabaseType.MySql, "SELECT a.id FROM a JOIN b ON a.id=b.id"),
            (DatabaseType.MySql, "SELECT a.id FROM a JOIN b WHERE a.id>0"),
            (DatabaseType.MySql, "SELECT a.id FROM a JOIN b USING(id)"),
            (DatabaseType.MySql, "SELECT 1 AS first_value; SELECT 2 AS second_value;"),
            (DatabaseType.Postgres, "SELECT $$from, where;$$, \"MiXeD\", data->>'name' FROM users WHERE id=$1"),
            (DatabaseType.SqlServer, "SELECT TOP 10 [order details].[name] FROM [order details] WITH (NOLOCK) WHERE id=@id"),
            (DatabaseType.Oracle, "SELECT NVL(name, 'fallback') FROM users WHERE ROWNUM<=10"),
            (DatabaseType.KingbaseES, "SELECT \"MiXeD\" FROM users LIMIT 10"),
            (DatabaseType.Sqlite, "SELECT 'join', name FROM users WHERE id=1"),
            (DatabaseType.DuckDB, "SELECT id, name FROM users LIMIT 10"),
            (DatabaseType.DM, "SELECT name FROM users WHERE ROWNUM<=10"),
        };
        foreach (var (type, sql) in samples)
        {
            using var formatter = new DbObjectTokenTranslator(Interpreter(DatabaseType.MySql), Interpreter(type));
            var formatted = formatter.FormatSql(sql, out bool error);
            Check(!error, $"Supported SQL must format successfully for {type}: {sql}");
            if (type is not (DatabaseType.Postgres or DatabaseType.KingbaseES))
                Check(Tokens(sql).SequenceEqual(Tokens(formatted)), "Formatting must preserve significant tokens, comments and case.");
            Check(formatted.Contains('\n'), "Formatting must produce readable SQL rather than an AST or unchanged single line.");
            Check(formatter.FormatSql(formatted, out error) == formatted && !error, "Formatting must be idempotent.");
            Check(DatabaseManager.AppCore.Common.SqlFormatter.Format(sql, type) == formatted,
                "The editor and conversion engine must use the same dialect and fidelity rules.");
            if (type == DatabaseType.Postgres)
                Check(new[] { "$$from, where;$$", "\"MiXeD\"", "'name'", "$1", "->>" }.All(formatted.Contains),
                    "PostgreSQL dollar strings, quoted identifiers, JSON operators and parameters must retain their exact text.");
        }

        using var mysql = new DbObjectTokenTranslator(Interpreter(DatabaseType.MySql), Interpreter(DatabaseType.MySql));
        Check(mysql.FormatSql(null!, out bool emptyError) is null && !emptyError, "Null SQL must remain unchanged.");
        Check(mysql.FormatSql("  ", out emptyError) == "  " && !emptyError, "Whitespace SQL must remain unchanged.");
        const string definer = "CREATE ALGORITHM=UNDEFINED DEFINER=`root`@`localhost` SQL SECURITY DEFINER VIEW `v` AS SELECT id FROM users";
        var formattedDefiner = mysql.FormatSql(definer, out bool definerError);
        Check(!definerError && new[] { "`root`", "`localhost`", "`v`" }.All(formattedDefiner.Contains),
            "MySQL DEFINER accounts must retain their quoted identifiers.");
        const string trailingComment = "SELECT id FROM users -- preserve trailing comment  ";
        Check(mysql.FormatSql(trailingComment, out bool fallback) == trailingComment && fallback,
            "A formatter change to comment content must return the original text and flag the fallback.");
        Check(DatabaseManager.AppCore.Common.SqlFormatter.Format(trailingComment, DatabaseType.MySql) == trailingComment,
            "The editor must also preserve unsupported formatting input exactly.");
        VerifyEditorSemantics();

        foreach (var definition in new[]
        {
            "SELECT a.id FROM a JOIN b ON a.id=b.id",
            "SELECT a.id FROM a LEFT JOIN b ON a.id=b.id",
            "SELECT a.id FROM a JOIN b USING(id)",
            "SELECT 'join' FROM a",
            "SELECT 'join /* on */' FROM a JOIN b ON a.id=b.id",
            "SELECT a.id FROM a CROSS JOIN b",
            "SELECT a.id FROM a JOIN b WHERE a.id>0",
            "SELECT a.id FROM a JOIN (SELECT id FROM b) AS x ON a.id=x.id",
        })
        {
            using var baseline = new DbObjectTokenTranslator(Interpreter(DatabaseType.MySql), Interpreter(DatabaseType.SqlServer));
            using var view = new ViewTranslator(Interpreter(DatabaseType.MySql), Interpreter(DatabaseType.SqlServer), new());
            baseline.LoadMappings();
            view.LoadMappings();
            var translated = baseline.ParseDefinition(definition);
            var formattedView = view.ParseDefinition(definition);
            Check(Tokens(translated).SequenceEqual(Tokens(formattedView)),
                "MySQL view formatting must not invent JOIN conditions or alter existing ON/USING clauses: " + definition);
        }
        foreach (var definition in new[]
        {
            "SELECT 'join' AS marker, a.id FROM a JOIN b ON a.id=b.id",
            "SELECT a.id FROM a LEFT JOIN b ON a.id=b.id WHERE b.id IS NULL",
            "SELECT 'a>b', '`value`', 'it''s <>= from, and end' AS marker FROM a WHERE id>=1 AND id<=2 AND id<>0 AND id!=3",
            "CREATE VIEW `as_view` AS SELECT 'as literal' AS marker FROM a",
            "SELECT `a`.`id` FROM `a` WHERE `id`>=1",
            "/* AS in comment */ CREATE VIEW `has_as` (`value_as`) AS SELECT 'a>b' FROM a",
            "CREATE ALGORITHM=UNDEFINED DEFINER=`as_user`@`localhost` SQL SECURITY DEFINER VIEW `as_view` AS SELECT id FROM a",
        })
        {
            var item = new View { Name = "formatted_view", Definition = definition };
            using var view = new ViewTranslator(Interpreter(DatabaseType.MySql), Interpreter(DatabaseType.SqlServer), new() { item }, "dbo")
            {
                Option = new DatabaseConverter.Model.DbConverterOption { CollectTranslateResultAfterTranslated = true },
            };
            view.Translate();
            Check(!view.HasError && view.TranslateResults.Count == 1 && item.Definition.Contains("CREATE VIEW"),
                "Full view conversion must still collect the generated script.");
            Check(new SqlAnalyser.Core.TSqlAnalyser(item.Definition).Validate() is null,
                "Generated SQL Server view scripts must pass the existing target syntax analyser.");
            if (definition.Contains("'join'")) Check(item.Definition.Contains("'join'"), "View generation must retain the string literal.");
            var sourceLiterals = TSQLTokenizer.ParseTokens(definition, true, true).Where(t => t.Type == TSQLTokenType.StringLiteral).Select(t => t.Text);
            var targetLiterals = TSQLTokenizer.ParseTokens(item.Definition, true, true).Where(t => t.Type == TSQLTokenType.StringLiteral).Select(t => t.Text);
            Check(sourceLiterals.SequenceEqual(targetLiterals), "Full view translation must preserve string contents: " + definition);
            Check(!item.Definition.Contains("> =") && !item.Definition.Contains("< =") && !item.Definition.Contains("`" + "as_view"),
                "View conversion must preserve comparison operators and remove the source view header.");
            if (definition.Contains("(`value_as`)")) Check(item.Definition.Contains("[value_as]"), "Explicit view column names must survive header replacement.");
            if (definition.Contains("`a`.`id`")) Check(item.Definition.Contains("[a].[id]") && !item.Definition.Contains('`'),
                "Quoted source identifiers must be converted without rewriting literal content.");
        }
        Console.WriteLine("SQL formatting checks passed: dialects, literals, identifiers, comments, idempotence and MySQL view JOIN fidelity.");
    }

    private static void VerifyEditorSemantics()
    {
        using var sqlite = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:");
        sqlite.Open();
        foreach (var sql in new[]
        {
            "SELECT 'from a,b and end' AS marker;",
            "SELECT 1 -- keep comment\n+2 AS total;",
            "SELECT 'a>b', '`value`', 'it''s select; from', 'a  b', 2>=1, 2<=3, 2<>1;",
            "SELECT /* from, where */ (1+2)*3, '中文 换行\nfrom, and';",
            "SELECT CASE WHEN 2>1 THEN 'from, and' ELSE 'end' END;",
            "SELECT 1 AS first_value; SELECT 'where, and' AS second_value;",
        })
        {
            string formatted = DatabaseManager.AppCore.Common.SqlFormatter.Format(sql, DatabaseType.Sqlite);
            Check(Read(sql).SequenceEqual(Read(formatted)), "Editor formatting must preserve executed values and statement boundaries: " + sql);
            Check(Tokens(sql).SequenceEqual(Tokens(formatted)), "Editor formatting must preserve strings, comments and operators.");
            Check(DatabaseManager.AppCore.Common.SqlFormatter.Format(formatted, DatabaseType.Sqlite) == formatted,
                "Editor formatting must be idempotent.");
        }

        List<string> Read(string sql)
        {
            using var command = sqlite.CreateCommand();
            command.CommandText = sql;
            using var reader = command.ExecuteReader();
            var values = new List<string>();
            do
            {
                values.Add("result:" + reader.FieldCount);
                while (reader.Read())
                    for (int i = 0; i < reader.FieldCount; i++) values.Add(Convert.ToString(reader.GetValue(i))!);
            } while (reader.NextResult());
            return values;
        }
    }

    private static DbInterpreter Interpreter(DatabaseType type)
        => DbInterpreterHelper.GetDbInterpreter(type, new ConnectionInfo { Database = "formatting_fixture" }, new DbInterpreterOption());

    private static IEnumerable<string> Tokens(string sql)
        => TSQLTokenizer.ParseTokens(sql, true, true).Where(t => t.Type != TSQLTokenType.Whitespace).Select(t => t.Text);

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
