using DatabaseInterpreter.Model;
using DatabaseManager.AppCore.Services;
using DatabaseManager.AppCore.Templating;
using DatabaseManager.Core;

internal static class TemplateExternalizationChecks
{
    public static void Run()
    {
        VerifyContext();
        VerifyBuiltinCatalog();
        VerifyTransfer();
        VerifyDialectTemplates();
        VerifyCodeTemplates();
        Console.WriteLine("Template externalization checks passed: context, builtin catalog, transfer envelope, dialect DDL and code snippets.");
    }

    private static void VerifyContext()
    {
        var context = new TemplateContext { ConnectionName = "prod", DatabaseName = "sales", Dialect = DatabaseType.Postgres };
        var values = context.ToVariables();
        Check(values["connection"] == "prod" && values["database"] == "sales", "Template context must expose connection and database.");
        Check(values["dialect"] == "Postgres" && values.ContainsKey("date") && values.ContainsKey("datetime"),
            "Template context must expose dialect and time variables.");

        var engine = new TemplateEngine();
        var manifest = new TemplateManifest
        {
            Content = "-- {dialect}: {connection}.{database} @ {date}",
            Variables = new List<TemplateVariable> { new() { Name = "dialect", Sample = "Unknown" } },
        };
        Check(engine.Expand(manifest, context).StartsWith("-- Postgres: prod.sales @ ", StringComparison.Ordinal),
            "Context expansion must use context values when no explicit override is supplied.");
    }

    private static void VerifyBuiltinCatalog()
    {
        var builtins = TemplateCatalog.GetBuiltin();
        Check(builtins.Any(t => t.Kind == "ddl" && t.Id == "ddl-view-sqlserver"), "Dialect DDL templates must be built in.");
        Check(builtins.Any(t => t.Kind == "dictionary" && t.Id == "dictionary-object"), "Dictionary templates must be built in.");
        Check(builtins.Any(t => t.Kind == "export" && t.Id == "dictionary-html"), "HTML export template must be built in.");
        Check(builtins.Count(t => t.Kind == "code") >= 10, "Code generation snippets must be built in.");

        var engine = new TemplateEngine();
        foreach (var manifest in builtins)
        {
            var samples = manifest.Variables.Where(v => v.Sample is not null)
                .ToDictionary(v => v.Name, v => v.Sample!, StringComparer.Ordinal);
            string rendered = engine.Expand(manifest, TemplateContext.CreateDefault(), samples);
            Check(!rendered.Contains("{schema}", StringComparison.Ordinal) || !manifest.Variables.Any(v => v.Name == "schema"),
                $"Builtin template {manifest.Id} must render its declared variables.");
        }

        var store = new DefaultTemplateStore();
        Check(store.Get("ddl", "ddl-view-sqlserver") is not null, "Template store must expose builtin DDL templates.");
        Check(store.GetAll("code").Count >= 10, "Template store must list builtin code templates by kind.");
    }

    private static void VerifyTransfer()
    {
        var templates = new DefaultTemplateStore().GetBuiltin("ddl").Take(3).ToList();
        string json = TemplateTransferService.Export(templates);
        Check(json.Contains("\"Version\": 1", StringComparison.Ordinal) && json.Contains("\"Payload\"", StringComparison.Ordinal),
            "Template export must use the versioned envelope.");
        var parsed = TemplateTransferService.Preview(json);
        Check(parsed.Count == 3 && parsed[0].Source == "user", "Template preview must restore manifests as user overrides.");

        var store = new MemoryStore();
        Check(TemplateTransferService.Import(store, parsed) == 3 && store.Saved.Count == 3,
            "Template import must save each validated manifest.");

        bool rejected = false;
        try { TemplateTransferService.Preview(json.Replace("\"Version\": 1", "\"Version\": 2", StringComparison.Ordinal)); }
        catch (ArgumentException) { rejected = true; }
        Check(rejected, "Template import must reject unsupported envelope versions.");
    }

    private static void VerifyDialectTemplates()
    {
        var ddl = new DefaultDdlService(new QueryExecutionChecks.Connections());

        var viewExpectations = new (DatabaseType Type, string Fragment)[]
        {
            (DatabaseType.SqlServer, "CREATE VIEW [dbo].[ViewName] AS"),
            (DatabaseType.MySql, "CREATE VIEW `dbo`.`ViewName` AS"),
            (DatabaseType.Oracle, "CREATE OR REPLACE VIEW \"dbo\".\"ViewName\" AS"),
            (DatabaseType.DM, "CREATE OR REPLACE VIEW \"dbo\".\"ViewName\" AS"),
            (DatabaseType.Postgres, "CREATE OR REPLACE VIEW \"dbo\".\"ViewName\" AS"),
            (DatabaseType.KingbaseES, "CREATE OR REPLACE VIEW \"dbo\".\"ViewName\" AS"),
            (DatabaseType.DuckDB, "CREATE OR REPLACE VIEW \"dbo\".\"ViewName\" AS"),
            (DatabaseType.Sqlite, "CREATE VIEW \"ViewName\" AS"),
        };
        foreach (var (type, fragment) in viewExpectations)
        {
            var result = ddl.GetCreateTemplate(DatabaseObjectType.View, "dbo", type);
            Check(result.IsSuccess && result.Script!.Contains(fragment, StringComparison.Ordinal),
                $"{type} view template must be dialect specific: {fragment}");
            Check(!result.Script!.Contains("TODO", StringComparison.OrdinalIgnoreCase)
                && !result.Script.Contains("通用 CREATE 模板", StringComparison.Ordinal),
                $"{type} view template must not contain generic placeholders.");
        }

        Check(ddl.GetCreateTemplate(DatabaseObjectType.Procedure, null, DatabaseType.SqlServer).Script!.Contains("SET NOCOUNT ON", StringComparison.Ordinal),
            "SQL Server procedure template must use T-SQL syntax.");
        Check(ddl.GetCreateTemplate(DatabaseObjectType.Function, null, DatabaseType.MySql).Script!.Contains("DETERMINISTIC", StringComparison.Ordinal),
            "MySQL function template must use MySQL syntax.");
        Check(ddl.GetCreateTemplate(DatabaseObjectType.Procedure, null, DatabaseType.Postgres).Script!.Contains("LANGUAGE plpgsql", StringComparison.Ordinal),
            "PostgreSQL procedure template must use plpgsql.");
        Check(ddl.GetCreateTemplate(DatabaseObjectType.Function, null, DatabaseType.KingbaseES).Script!.Contains("RETURNS integer", StringComparison.Ordinal),
            "KingbaseES function template must reuse PostgreSQL syntax.");
        Check(ddl.GetCreateTemplate(DatabaseObjectType.Procedure, null, DatabaseType.Oracle).Script!.Contains("CREATE OR REPLACE PROCEDURE", StringComparison.Ordinal),
            "Oracle procedure template must use PL/SQL syntax.");

        foreach (var type in new[] { DatabaseType.Sqlite, DatabaseType.DuckDB })
        {
            foreach (var objectType in new[] { DatabaseObjectType.Procedure, DatabaseObjectType.Function })
            {
                var result = ddl.GetCreateTemplate(objectType, null, type);
                Check(!result.IsSuccess && (result.ErrorMessage ?? "").Contains("不支持", StringComparison.Ordinal),
                    $"{type} must explicitly reject {objectType} templates.");
            }
        }
        Check(!ddl.GetCreateTemplate(DatabaseObjectType.View, null, DatabaseType.Unknown).IsSuccess,
            "Unknown dialect must not fall back to a generic template.");

        var overrideStore = new OverrideStore(new TemplateManifest
        {
            Kind = "ddl", Id = "ddl-view-sqlserver",
            Content = "-- user override\nCREATE VIEW {schema}{object} AS SELECT 1;",
            Variables = new List<TemplateVariable>
            {
                new() { Name = "schema", Sample = "" }, new() { Name = "object", Sample = "ViewName" },
            },
        });
        var overridden = new DefaultDdlService(new QueryExecutionChecks.Connections(), overrideStore, new TemplateEngine())
            .GetCreateTemplate(DatabaseObjectType.View, "dbo", DatabaseType.SqlServer);
        Check(overridden.IsSuccess && overridden.Script!.Contains("-- user override", StringComparison.Ordinal)
            && overridden.Script.Contains("[dbo].[ViewName]", StringComparison.Ordinal),
            "User template overrides must be honored by DDL generation.");
    }

    private static void VerifyCodeTemplates()
    {
        var contents = TemplateCatalog.GetBuiltin().Where(t => t.Kind == "code")
            .ToDictionary(t => t.Id, t => t.Content, StringComparer.OrdinalIgnoreCase);
        var set = CodeTemplateSet.FromContents(contents);

        string property = CodeTemplateSet.Expand(set.CSharpProperty, new Dictionary<string, string>
        {
            ["indent"] = "\t", ["type"] = "int", ["nullable"] = "?", ["name"] = "Id",
        });
        Check(property.Contains("public int? Id {get; set;}", StringComparison.Ordinal),
            "Externalized C# property template must preserve generated code.");

        string method = CodeTemplateSet.Expand(set.JavaMethod, new Dictionary<string, string>
        {
            ["indent"] = "\t", ["type"] = "String", ["property"] = "Name", ["field"] = "name",
        });
        Check(method.Contains("getName()", StringComparison.Ordinal) && method.Contains("setName(String name)", StringComparison.Ordinal),
            "Externalized Java getter and setter must have valid method signatures.");
        Check(CodeTemplateSet.Expand("a\r\nb\nc", new Dictionary<string, string>())
                == string.Join(Environment.NewLine, "a", "b", "c"),
            "Code templates must normalize existing CRLF and LF line endings exactly once.");

        string comment = CodeTemplateSet.Expand(set.CSharpComment, new Dictionary<string, string>
        {
            ["indent"] = "", ["comment"] = "说明",
        });
        Check(comment.Contains("/// 说明", StringComparison.Ordinal), "Externalized comment template must render.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private sealed class MemoryStore : ITemplateStore
    {
        public List<TemplateManifest> Saved { get; } = new();
        public IReadOnlyList<TemplateManifest> GetAll(string? kind = null)
            => Saved.Where(t => kind is null || t.Kind == kind).ToList();
        public TemplateManifest? Get(string kind, string id) => Saved.FirstOrDefault(t => t.Kind == kind && t.Id == id);
        public void Save(TemplateManifest manifest) => Saved.Add(manifest);
        public bool Delete(string kind, string id) => false;
        public IReadOnlyList<TemplateManifest> GetBuiltin(string? kind = null) => Array.Empty<TemplateManifest>();
    }

    private sealed class OverrideStore(TemplateManifest manifest) : ITemplateStore
    {
        public IReadOnlyList<TemplateManifest> GetAll(string? kind = null) => [manifest];
        public TemplateManifest? Get(string kind, string id) => manifest;
        public void Save(TemplateManifest template) { }
        public bool Delete(string kind, string id) => false;
        public IReadOnlyList<TemplateManifest> GetBuiltin(string? kind = null) => Array.Empty<TemplateManifest>();
    }
}
