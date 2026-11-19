using DatabaseInterpreter.Model;

namespace DatabaseManager.AppCore.Templating;

/// <summary>
/// T3 内置模板清单：DDL（按方言）、数据字典、HTML 导出、代码生成片段。
/// 用户可在 Profiles/Templates/&lt;kind&gt;/&lt;id&gt;.tpl 覆盖同名模板。
/// </summary>
public static class TemplateCatalog
{
    private static readonly Dictionary<string, string[]> DialectGroups = new(StringComparer.Ordinal)
    {
        ["sqlserver"] = [nameof(DatabaseType.SqlServer)],
        ["mysql"] = [nameof(DatabaseType.MySql)],
        ["oracle"] = [nameof(DatabaseType.Oracle), nameof(DatabaseType.DM)],
        ["postgres"] = [nameof(DatabaseType.Postgres), nameof(DatabaseType.KingbaseES), nameof(DatabaseType.DuckDB)],
        ["sqlite"] = [nameof(DatabaseType.Sqlite)],
    };

    private static readonly Dictionary<string, string> Samples = new(StringComparer.Ordinal)
    {
        ["schema"] = "\"public\".", ["object"] = "ViewName",
        ["name"] = "ColumnName", ["comment"] = "示例说明", ["type"] = "int", ["nullable"] = "",
        ["default"] = "", ["title"] = "数据字典", ["body"] = "<p>示例内容</p>",
        ["indent"] = "\t", ["field"] = "columnName", ["property"] = "ColumnName",
        ["class"] = "Sample", ["namespace"] = "Demo", ["import"] = "System",
    };

    private static readonly IReadOnlyList<TemplateManifest> BuiltIn = Build();

    public static IReadOnlyList<TemplateManifest> GetBuiltin() => BuiltIn;

    public static string GetBuiltinContent(string kind, string id)
        => BuiltIn.First(t => t.Kind == kind && t.Id == id).Content;

    public static string GetDialectGroup(DatabaseType databaseType) => databaseType switch
    {
        DatabaseType.SqlServer => "sqlserver",
        DatabaseType.MySql => "mysql",
        DatabaseType.Oracle or DatabaseType.DM => "oracle",
        DatabaseType.Postgres or DatabaseType.KingbaseES or DatabaseType.DuckDB => "postgres",
        DatabaseType.Sqlite => "sqlite",
        _ => throw new NotSupportedException($"未知数据库方言：{databaseType}。"),
    };

    public static string GetDdlTemplateId(DatabaseType databaseType, DatabaseObjectType objectType)
    {
        string objectName = objectType switch
        {
            DatabaseObjectType.View => "view",
            DatabaseObjectType.Procedure => "procedure",
            DatabaseObjectType.Function => "function",
            _ => throw new NotSupportedException($"暂不支持生成 {objectType} 对象模板。"),
        };
        return $"ddl-{objectName}-{GetDialectGroup(databaseType)}";
    }

    public static bool SupportsRoutineTemplates(DatabaseType databaseType)
        => databaseType is not (DatabaseType.Sqlite or DatabaseType.DuckDB);

    private static IReadOnlyList<TemplateManifest> Build()
    {
        var templates = new List<TemplateManifest>();

        foreach (var (group, dialects) in DialectGroups)
        {
            var definitions = DdlTemplates(group);
            templates.Add(Ddl($"ddl-view-{group}", $"{group} 新建视图", dialects, definitions.View));
            if (definitions.Procedure is not null)
                templates.Add(Ddl($"ddl-procedure-{group}", $"{group} 新建存储过程", dialects, definitions.Procedure));
            if (definitions.Function is not null)
                templates.Add(Ddl($"ddl-function-{group}", $"{group} 新建函数", dialects, definitions.Function));
        }

        templates.Add(Manifest("dictionary", "dictionary-object", "数据字典对象模板", "text",
            "{schema}.{name} — {comment}", [], "schema", "name", "comment"));
        templates.Add(Manifest("dictionary", "dictionary-column", "数据字典列模板", "text",
            "{name} | {type} | 可空:{nullable} | 默认:{default} | {comment}", [], "name", "type", "nullable", "default", "comment"));
        templates.Add(Manifest("export", "dictionary-html", "数据字典 HTML 骨架", "html",
            "<!doctype html><html lang=\"zh\"><meta charset=\"utf-8\"><title>{title}</title><style>body{font-family:sans-serif;max-width:1100px;margin:40px auto}p{white-space:pre-wrap;overflow-wrap:anywhere;border-bottom:1px solid #ddd;padding:8px}</style><body>{body}</body></html>",
            [], "title", "body"));

        AddCodeTemplates(templates);
        return templates;
    }

    private static TemplateManifest Ddl(string id, string name, string[] dialects, string content)
        => Manifest("ddl", id, name, "sql", content, dialects, "schema", "object");

    private static (string View, string? Procedure, string? Function) DdlTemplates(string group) => group switch
    {
        "sqlserver" => (
            "CREATE VIEW {schema}{object} AS\nSELECT 1 AS Col1;",
            "CREATE PROCEDURE {schema}{object}\n    @Param1 INT = 0,\n    @Param2 NVARCHAR(50) = NULL\nAS\nBEGIN\n    SET NOCOUNT ON;\n    SELECT @Param1 AS Value;\nEND;",
            "CREATE FUNCTION {schema}{object}(@Param1 INT)\nRETURNS INT\nAS\nBEGIN\n    RETURN @Param1;\nEND;"),
        "mysql" => (
            "CREATE VIEW {schema}{object} AS\nSELECT 1 AS Col1;",
            "-- MySQL 客户端创建例程时需设置 DELIMITER，以下主体保持可编辑的语句块。\nCREATE PROCEDURE {schema}{object}(IN p1 INT)\nBEGIN\n    SELECT p1 AS Value;\nEND;",
            "-- MySQL 函数需 DETERMINISTIC 声明；客户端创建时设置 DELIMITER。\nCREATE FUNCTION {schema}{object}(p1 INT)\nRETURNS INT DETERMINISTIC\nBEGIN\n    RETURN p1;\nEND;"),
        "oracle" => (
            "CREATE OR REPLACE VIEW {schema}{object} AS\nSELECT 1 AS Col1 FROM DUAL;",
            "CREATE OR REPLACE PROCEDURE {schema}{object}(p1 IN NUMBER DEFAULT 0) AS\nBEGIN\n    NULL;\nEND;\n/",
            "CREATE OR REPLACE FUNCTION {schema}{object}(p1 IN NUMBER DEFAULT 0)\nRETURN NUMBER\nIS\nBEGIN\n    RETURN p1;\nEND;\n/"),
        "postgres" => (
            "CREATE OR REPLACE VIEW {schema}{object} AS\nSELECT 1 AS Col1;",
            "CREATE OR REPLACE PROCEDURE {schema}{object}(p1 integer DEFAULT 0)\nLANGUAGE plpgsql AS $$\nBEGIN\n    RAISE NOTICE 'p1=%', p1;\nEND;\n$$;",
            "CREATE OR REPLACE FUNCTION {schema}{object}(p1 integer DEFAULT 0)\nRETURNS integer\nLANGUAGE plpgsql AS $$\nBEGIN\n    RETURN p1;\nEND;\n$$;"),
        "sqlite" => (
            "CREATE VIEW {schema}{object} AS\nSELECT 1 AS Col1;",
            null,
            null),
        _ => throw new NotSupportedException($"未知 DDL 方言组：{group}。"),
    };

    private static void AddCodeTemplates(List<TemplateManifest> templates)
    {
        templates.Add(Manifest("code", "code-csharp-comment", "C# 注释片段", "csharp",
            "{indent}/// <summary>\n{indent}/// {comment}\n{indent}/// </summary>\n", [], "indent", "comment"));
        templates.Add(Manifest("code", "code-java-class-comment", "Java 类注释片段", "java",
            "{indent}/*\n{indent} *{comment}\n{indent} */\n", [], "indent", "comment"));
        templates.Add(Manifest("code", "code-java-field-comment", "Java 字段注释片段", "java",
            "{indent}//{comment}\n", [], "indent", "comment"));
        templates.Add(Manifest("code", "code-csharp-property", "C# 属性片段", "csharp",
            "{indent}public {type}{nullable} {name} {get; set;}\n", [], "indent", "type", "nullable", "name"));
        templates.Add(Manifest("code", "code-java-field", "Java 字段片段", "java",
            "{indent}private {type} {field};\n", [], "indent", "type", "field"));
        templates.Add(Manifest("code", "code-java-method", "Java getter/setter 片段", "java",
            "{indent}public {type} get{property}() {\n{indent}\treturn this.{field};\n{indent}}\n\n{indent}public void set{property}({type} {field}) {\n{indent}\tthis.{field} = {field};\n{indent}}\n",
            [], "indent", "type", "property", "field"));
        templates.Add(Manifest("code", "code-csharp-namespace", "C# 命名空间头", "csharp",
            "namespace {namespace}\n{\n", [], "namespace"));
        templates.Add(Manifest("code", "code-csharp-namespace-end", "C# 命名空间尾", "csharp", "}\n", []));
        templates.Add(Manifest("code", "code-csharp-class", "C# 类骨架头", "csharp",
            "{indent}public class {class}\n{indent}{\n", [], "indent", "class"));
        templates.Add(Manifest("code", "code-csharp-class-end", "C# 类骨架尾", "csharp",
            "{indent}}\n", [], "indent"));
        templates.Add(Manifest("code", "code-csharp-using", "C# using 片段", "csharp",
            "using {import};\n", [], "import"));
        templates.Add(Manifest("code", "code-java-package", "Java package 头", "java",
            "package {namespace};\n\n", [], "namespace"));
        templates.Add(Manifest("code", "code-java-import", "Java import 片段", "java",
            "import {import};\n", [], "import"));
        templates.Add(Manifest("code", "code-java-class", "Java 类骨架头", "java",
            "public class {class} {\n", [], "class"));
        templates.Add(Manifest("code", "code-java-class-end", "Java 类骨架尾", "java", "}\n", []));
    }

    private static TemplateManifest Manifest(string kind, string id, string name, string language,
        string content, string[] dialects, params string[] variables)
    {
        return new TemplateManifest
        {
            Id = id,
            Name = name,
            Kind = kind,
            Version = TemplateContract.CurrentVersion,
            Engine = "simple",
            Language = language,
            AppliesToDialects = dialects.ToList(),
            Variables = variables.Select(v => new TemplateVariable
            {
                Name = v,
                Description = v,
                Required = true,
                Sample = Samples.TryGetValue(v, out var sample) ? sample : string.Empty,
            }).ToList(),
            Content = content,
            Source = "builtin",
        };
    }
}
